using System.Net.Http.Headers;
using System.Text.Json;
using AppServicios.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace AppServicios.Api.Services;

public sealed class PaymentCheckException(string message, int status = 409) : Exception(message)
{
    public int Status { get; } = status;
}
public sealed record CheckedPayment(string Id, string Status, bool Approved);

// Only the authenticated provider API may establish that money was collected.
public sealed class PaymentSecurity(IHttpClientFactory factory, IConfiguration config, IHostEnvironment environment,
    AppServiciosDbContext db)
{
    public string BaseUrl
    {
        get
        {
            var value = config["MercadoPago:BaseUrl"] ?? "https://api.mercadopago.com";
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
                || !(uri.Scheme == "https" && uri.Host == "api.mercadopago.com" && uri.IsDefaultPort
                     || environment.IsDevelopment() && uri.IsLoopback))
                throw new PaymentCheckException("La conexión con Mercado Pago no está configurada correctamente.", 503);
            return value.TrimEnd('/');
        }
    }
    public HttpClient Client()
    {
        var token = config["MercadoPago:AccessToken"];
        if (string.IsNullOrWhiteSpace(token)) throw new PaymentCheckException("El cobro no está disponible. Reintentá más tarde.", 400);
        _ = BaseUrl;
        var client = factory.CreateClient("MercadoPago");
        client.Timeout = TimeSpan.FromSeconds(15);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
    static string Str(JsonElement value, string name) => value.TryGetProperty(name, out var v) ? v.ToString() : "";
    static bool Money(JsonElement value, string name, decimal expected)
        => value.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var amount) && amount == expected;
    async Task<JsonElement> Get(HttpClient client, string path)
    {
        try
        {
            using var response = await client.GetAsync(BaseUrl + path);
            if (!response.IsSuccessStatusCode) throw new PaymentCheckException("No se pudo comprobar el cobro con Mercado Pago. Reintentá más tarde.", 502);
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return doc.RootElement.Clone();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        { throw new PaymentCheckException("Mercado Pago no respondió correctamente. No se modificó el saldo.", 502); }
    }
    async Task<CheckedPayment> Check(HttpClient client, string id, string reference, decimal amount, string currency, bool refund = false)
    {
        if (string.IsNullOrEmpty(id) || id.Length > 80 || !id.All(char.IsAsciiDigit))
            throw new PaymentCheckException("El identificador del cobro no es válido.");
        var account = await Get(client, "/users/me");
        var payment = await Get(client, "/v1/payments/" + id);
        var status = Str(payment, "status");
        var live = payment.TryGetProperty("live_mode", out var mode) && mode.ValueKind is JsonValueKind.True or JsonValueKind.False;
        if (Str(payment, "id") != id || string.IsNullOrEmpty(Str(account, "id"))
            || Str(payment, "collector_id") != Str(account, "id")
            || Str(payment, "external_reference") != reference || Str(payment, "currency_id") != currency
            || !Money(payment, "transaction_amount", amount) || amount <= 0
            || !live || mode.GetBoolean() == config.GetValue("MercadoPago:UseSandbox", false)
            || !Money(payment, "transaction_amount_refunded", refund && status == "refunded" ? amount : 0))
            throw new PaymentCheckException("El cobro no coincide con la orden o tiene una devolución. Se requiere revisión.");
        if (refund && status == "refunded") return new(id, status, false);
        return new(id, status, status == "approved");
    }
    public async Task<CheckedPayment> Verify(string reference, decimal amount, string currency, string? paymentId = null)
    {
        using var client = Client();
        if (!string.IsNullOrWhiteSpace(paymentId)) return await Check(client, paymentId, reference, amount, currency);
        var search = await Get(client, "/v1/payments/search?sort=date_created&criteria=desc&limit=100&external_reference=" + Uri.EscapeDataString(reference));
        if (!search.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
            throw new PaymentCheckException("La consulta del cobro no devolvió un resultado válido.", 502);
        var approved = results.EnumerateArray().Where(x => Str(x, "status") == "approved").ToList();
        if (approved.Count > 1) throw new PaymentCheckException("Hay más de un cobro aprobado para esta orden. Se requiere revisión.");
        var candidate = approved.Count == 1 ? approved[0] : results.EnumerateArray().FirstOrDefault();
        if (candidate.ValueKind == JsonValueKind.Undefined) return new("", "pending", false);
        return await Check(client, Str(candidate, "id"), reference, amount, currency);
    }
    public async Task Claim(string paymentId, string order)
    {
        var existing = await db.CobrosVerificados.SingleOrDefaultAsync(x => x.ProveedorId == paymentId || x.Orden == order);
        if (existing is not null)
        {
            if (existing.ProveedorId != paymentId || existing.Orden != order)
                throw new PaymentCheckException("Este cobro ya está asociado a otra operación.");
            return;
        }
        db.CobrosVerificados.Add(new Domain.CobroVerificado { ProveedorId = paymentId, Orden = order });
        await db.SaveChangesAsync();
    }
    public async Task Refund(string paymentId, string reference, decimal amount, string currency, string order)
    {
        using var client = Client();
        var payment = await Check(client, paymentId, reference, amount, currency, true);
        if (!payment.Approved && payment.Status != "refunded") throw new PaymentCheckException("El proveedor no permite reintegrar este cobro.");
        await Claim(paymentId, order);
        if (payment.Status == "refunded") return; // Recovery after provider success and local rollback.
        using var request = new HttpRequestMessage(HttpMethod.Post, BaseUrl + "/v1/payments/" + paymentId + "/refunds");
        request.Headers.Add("X-Idempotency-Key", "servilabs-refund-" + order);
        request.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        try
        {
            using var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode) throw new PaymentCheckException("Mercado Pago no confirmó la devolución. El pago sigue en disputa.", 502);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        { throw new PaymentCheckException("La devolución está pendiente de comprobación. Reintentá la misma operación.", 502); }
        var confirmed = await Check(client, paymentId, reference, amount, currency, true);
        if (confirmed.Status != "refunded") throw new PaymentCheckException("La devolución aún no está confirmada. Reintentá la misma operación.", 409);
    }
}

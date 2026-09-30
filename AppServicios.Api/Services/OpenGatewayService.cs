using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AppServicios.Api.Domain;
using AppServicios.Api.Helpers;
using Microsoft.AspNetCore.WebUtilities;
namespace AppServicios.Api.Services;
public sealed class OpenGatewayService(IConfiguration config, IHttpClientFactory clients, IWebHostEnvironment environment)
{
    public static readonly string[] Types = ["numero", "titular", "sim", "ubicacion"];
    public string? Value(string name) => config["OPEN_GATEWAY_" + name];
    public string? Endpoint(string type) => Value(type switch { "numero" => "NUMBER_VERIFICATION_URL", "titular" => "KYC_MATCH_URL", "sim" => "SIM_SWAP_URL", "ubicacion" => "DEVICE_LOCATION_URL", _ => "UNKNOWN" });
    public string? AuthorizeUrl
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Value("AUTHORIZE_URL"))) return Value("AUTHORIZE_URL");
            var auth = Value("AUTH_URL");
            if (Uri.TryCreate(auth, UriKind.Absolute, out var uri) && uri.Host == "sandbox.opengateway.telefonica.com" && uri.AbsolutePath.EndsWith("/bc-authorize"))
                return auth![..^"bc-authorize".Length] + "authorize";
            return auth?.EndsWith("/authorize") == true ? auth : null;
        }
    }
    public string? CallbackUrl => Value("REDIRECT_URI") ?? ((config["APP_PUBLIC_URL"] ?? config["App:PublicUrl"] ?? "https://appservicios-mn6i.onrender.com").TrimEnd('/') + "/api/Verificaciones/callback");
    public bool ValidUrl(string? url) => Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == "https" || (environment.IsDevelopment() && uri.IsLoopback && uri.Scheme == "http"));
    public bool IsTest(string type) => !string.Equals(Value("MODE"), "production", StringComparison.OrdinalIgnoreCase)
        || new[] { Endpoint(type), Value("TOKEN_URL"), AuthorizeUrl }.Any(x => x?.Contains("sandbox", StringComparison.OrdinalIgnoreCase) == true);
    public string[] Missing(string type)
    {
        var values = new Dictionary<string,string?> { ["CLIENT_ID"] = Value("CLIENT_ID"), ["CLIENT_SECRET"] = Value("CLIENT_SECRET"), ["TOKEN_URL"] = Value("TOKEN_URL"), ["AUTHORIZE_URL"] = AuthorizeUrl, ["REDIRECT_URI"] = CallbackUrl, [type switch { "numero" => "NUMBER_VERIFICATION_URL", "titular" => "KYC_MATCH_URL", "sim" => "SIM_SWAP_URL", _ => "DEVICE_LOCATION_URL" }] = Endpoint(type) };
        return values.Where(x => string.IsNullOrWhiteSpace(x.Value) || (x.Key.EndsWith("URL") || x.Key.EndsWith("URI")) && !ValidUrl(x.Value)).Select(x => "OPEN_GATEWAY_" + x.Key).ToArray();
    }
    public static string Random() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static string UserHash(Usuario user) => Hash($"{user.Telefono}|{user.DNI}|{user.FechaNacimiento:yyyy-MM-dd}");
    private string Scope(string type) => Value(type switch { "numero" => "NUMBER_VERIFICATION_SCOPE", "titular" => "KYC_MATCH_SCOPE", "sim" => "SIM_SWAP_SCOPE", _ => "DEVICE_LOCATION_SCOPE" }) ?? "dpv:FraudPreventionAndDetection#" + (type switch { "numero" => "number-verification-verify-read", "titular" => "kyc-match:match", "sim" => "sim-swap", _ => "device-location-read" });
    public string Authorization(VerificacionLinea flow, string state)
    {
        var args = new Dictionary<string,string?> { ["client_id"] = Value("CLIENT_ID"), ["response_type"] = "code", ["scope"] = Scope(flow.Tipo), ["redirect_uri"] = CallbackUrl, ["state"] = state,
            ["code_challenge"] = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(flow.Verifier))), ["code_challenge_method"] = "S256" };
        // Number Verification must identify the connecting SIM; never trust a phone supplied as state.
        if (flow.Tipo != "numero") args["login_hint"] = "tel:" + flow.Telefono;
        return QueryHelpers.AddQueryString(AuthorizeUrl!, args);
    }
    public async Task<string> Complete(VerificacionLinea flow, Usuario user, string code, CancellationToken ct)
    {
        using var client = clients.CreateClient("OpenGateway");
        client.Timeout = TimeSpan.FromSeconds(20);
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes(Value("CLIENT_ID") + ":" + Value("CLIENT_SECRET")));
        using var request = new HttpRequestMessage(HttpMethod.Post, Value("TOKEN_URL"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        request.Content = new FormUrlEncodedContent(new Dictionary<string,string> { ["grant_type"] = "authorization_code", ["code"] = code, ["redirect_uri"] = CallbackUrl!, ["code_verifier"] = flow.Verifier });
        using var tokenResponse = await client.SendAsync(request, ct);
        if (!tokenResponse.IsSuccessStatusCode) return "error_autorizacion";
        using var tokenJson = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync(ct));
        if (!tokenJson.RootElement.TryGetProperty("access_token", out var token) || token.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(token.GetString())) return "error_autorizacion";
        object payload = flow.Tipo switch {
            "numero" => new { phoneNumber = flow.Telefono },
            "titular" => new { phoneNumber = flow.Telefono, idDocument = user.DNI, birthdate = user.FechaNacimiento.ToString("yyyy-MM-dd") },
            "sim" => new { phoneNumber = flow.Telefono, maxAge = 24 },
            "ubicacion" => new { device = new { phoneNumber = flow.Telefono }, area = new { areaType = "CIRCLE", center = new { latitude = flow.Latitud, longitude = flow.Longitud }, radius = 1000 } },
            _ => throw new InvalidOperationException("Tipo no soportado") };
        using var api = new HttpRequestMessage(HttpMethod.Post, Endpoint(flow.Tipo));
        api.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.GetString());
        api.Content = JsonContent.Create(payload);
        using var response = await client.SendAsync(api, ct);
        if (!response.IsSuccessStatusCode) return (int)response.StatusCode is 401 or 403 ? "sin_autorizacion" : "no_disponible";
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var root = json.RootElement;
        bool? Boolean(string key) => root.TryGetProperty(key, out var val) && val.ValueKind is JsonValueKind.True or JsonValueKind.False ? val.GetBoolean() : null;
        string? Text(string key) => root.TryGetProperty(key, out var val) && val.ValueKind == JsonValueKind.String ? val.GetString() : null;
        return flow.Tipo switch {
            "numero" => Boolean("devicePhoneNumberVerified") switch { true => "coincide", false => "no_coincide", _ => "sin_datos" },
            "titular" => Text("idDocumentMatch") == "true" && Text("birthdateMatch") == "true" ? "coincide" : Text("idDocumentMatch") == "false" || Text("birthdateMatch") == "false" ? "no_coincide" : "sin_datos",
            "sim" => Boolean("swapped") switch { true => "cambio_reciente", false => "sin_cambios", _ => "sin_datos" },
            "ubicacion" => Text("verificationResult") switch { "TRUE" => "coincide", "FALSE" => "no_coincide", _ => "sin_datos" },
            _ => "sin_datos" };
    }
}

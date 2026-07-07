using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AppServicios.Api.Data;
using AppServicios.Api.Domain;
using AppServicios.Api.DTOs;
using AppServicios.Api.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AppServicios.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public sealed class PagosProfesionalesController : ControllerBase
    {
        private readonly AppServiciosDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public PagosProfesionalesController(
            AppServiciosDbContext context,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        [HttpGet("planes")]
        public async Task<ActionResult<IEnumerable<PlanComercialDto>>> GetPlanes()
        {
            var founder = await BuildFounderPlanAsync();
            return Ok(new[] { founder });
        }

        [Authorize(Roles = "Administrador")]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<PagoProfesionalDto>>> GetAll()
        {
            var items = await _context.PagosProfesionales
                .AsNoTracking()
                .Include(p => p.Usuario)
                .OrderByDescending(p => p.FechaCreacion)
                .ToListAsync();

            return Ok(items.Select(ToDto));
        }

        [Authorize]
        [HttpGet("{id:int}")]
        public async Task<ActionResult<PagoProfesionalDto>> GetById(int id)
        {
            var pago = await _context.PagosProfesionales
                .AsNoTracking()
                .Include(p => p.Usuario)
                .FirstOrDefaultAsync(p => p.Id == id);

            return pago is null ? NotFound() : Ok(ToDto(pago));
        }

        [HttpPost]
        public async Task<ActionResult<PagoProfesionalDto>> Create([FromBody] PagoProfesionalCreateDto request)
        {
            await ValidateCreateAsync(request);
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var plan = await BuildFounderPlanAsync();
            if (!plan.Disponible)
            {
                return Conflict("El Plan Fundadores ya agotó sus cupos promocionales.");
            }

            var currency = string.IsNullOrWhiteSpace(request.Moneda)
                ? plan.Moneda
                : request.Moneda.Trim().ToUpperInvariant();
            if (!string.Equals(currency, "ARS", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(nameof(request.Moneda), "Por ahora el alta profesional se cobra en ARS. Los equivalentes internacionales quedan como referencia hasta integrar Stripe.");
                return ValidationProblem(ModelState);
            }

            var pago = new PagoProfesional
            {
                UsuarioId = request.UsuarioId,
                Monto = plan.AltaMonto,
                Moneda = plan.Moneda,
                Concepto = plan.Nombre,
                Estado = "Pendiente",
                Proveedor = string.IsNullOrWhiteSpace(request.Proveedor) ? "Mercado Pago" : request.Proveedor.Trim(),
                ReferenciaExterna = $"APP-PRO-{DateTime.UtcNow:yyyyMMddHHmmssfff}",
                Detalle = BuildPaymentDetail(request, plan),
                FechaCreacion = DateTime.UtcNow
            };

            _context.PagosProfesionales.Add(pago);
            await _context.SaveChangesAsync();
            await _context.Entry(pago).Reference(p => p.Usuario).LoadAsync();
            await AuditoriaHelper.RegistrarAsync(
                _context,
                pago.UsuarioId,
                "Pago",
                "Orden generada",
                $"Se generó la orden de pago #{pago.Id} por {pago.Monto:N0} {pago.Moneda}.",
                "PagoProfesional",
                pago.Id,
                pago.Estado);

            return CreatedAtAction(nameof(GetById), new { id = pago.Id }, ToDto(pago));
        }

        [Authorize]
        [HttpPost("{id:int}/mercadopago/preference")]
        public async Task<ActionResult<MercadoPagoPreferenceDto>> CreateMercadoPagoPreference(int id)
        {
            var pago = await _context.PagosProfesionales
                .Include(p => p.Usuario)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (pago is null)
            {
                return NotFound();
            }

            var accessToken = GetMercadoPagoAccessToken();
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                return BadRequest("Configura `MercadoPago:AccessToken` y `MercadoPago:PublicKey` en `appsettings.Development.json` para usar Checkout Pro.");
            }

            var client = CreateMercadoPagoClient(accessToken);
            var body = new
            {
                items = new[]
                {
                    new
                    {
                        title = pago.Concepto,
                        description = pago.Detalle,
                        quantity = 1,
                        currency_id = pago.Moneda,
                        unit_price = pago.Monto
                    }
                },
                payer = new
                {
                    email = pago.Usuario?.Email,
                    name = pago.Usuario?.Nombre
                },
                external_reference = pago.ReferenciaExterna,
                back_urls = new
                {
                    success = _configuration["MercadoPago:SuccessUrl"] ?? "http://localhost:5256/pago-resultado.html?status=success",
                    failure = _configuration["MercadoPago:FailureUrl"] ?? "http://localhost:5256/pago-resultado.html?status=failure",
                    pending = _configuration["MercadoPago:PendingUrl"] ?? "http://localhost:5256/pago-resultado.html?status=pending"
                },
                metadata = new
                {
                    pagoId = pago.Id,
                    usuarioId = pago.UsuarioId
                }
            };

            var response = await client.PostAsync(
                $"{GetMercadoPagoBaseUrl().TrimEnd('/')}/checkout/preferences",
                new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"));

            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, $"Mercado Pago no pudo crear la preferencia: {content}");
            }

            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;
            var preferenceId = GetStringProperty(root, "id");
            var initPoint = GetStringProperty(root, "init_point");
            var sandboxInitPoint = GetStringProperty(root, "sandbox_init_point");
            var sandboxEnabled = _configuration.GetValue("MercadoPago:UseSandbox", true);

            pago.Proveedor = "Mercado Pago";
            pago.Detalle = $"{pago.Detalle} | PreferenceId={preferenceId}".Trim();
            await _context.SaveChangesAsync();

            return Ok(new MercadoPagoPreferenceDto(
                pago.Id,
                pago.Estado,
                preferenceId,
                initPoint,
                sandboxInitPoint,
                _configuration["MercadoPago:PublicKey"],
                sandboxEnabled,
                "Orden generada. Se habilitó Mercado Pago para completar el cobro en una nueva pestaña."));
        }

        [Authorize]
        [HttpPost("{id:int}/mercadopago/verificar")]
        public async Task<ActionResult<MercadoPagoVerificationDto>> VerifyMercadoPagoPayment(int id)
        {
            var pago = await _context.PagosProfesionales
                .Include(p => p.Usuario)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (pago is null)
            {
                return NotFound();
            }

            var accessToken = GetMercadoPagoAccessToken();
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                return BadRequest("Configura `MercadoPago:AccessToken` en `appsettings.Development.json` para verificar el pago contra Mercado Pago.");
            }

            var client = CreateMercadoPagoClient(accessToken);
            var url = $"{GetMercadoPagoBaseUrl().TrimEnd('/')}/v1/payments/search?external_reference={Uri.EscapeDataString(pago.ReferenciaExterna)}";
            var response = await client.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, $"Mercado Pago no pudo verificar el pago: {content}");
            }

            using var document = JsonDocument.Parse(content);
            string? providerStatus = null;
            string? providerId = null;

            if (document.RootElement.TryGetProperty("results", out var results)
                && results.ValueKind == JsonValueKind.Array
                && results.GetArrayLength() > 0)
            {
                var firstResult = results[0];
                providerStatus = GetStringProperty(firstResult, "status");
                providerId = GetStringProperty(firstResult, "id");
            }

            if (string.Equals(providerStatus, "approved", StringComparison.OrdinalIgnoreCase))
            {
                pago.Estado = "Aprobado";
                pago.FechaAprobacion = DateTime.UtcNow;
            }
            else if (string.Equals(providerStatus, "rejected", StringComparison.OrdinalIgnoreCase)
                || string.Equals(providerStatus, "cancelled", StringComparison.OrdinalIgnoreCase))
            {
                pago.Estado = "Rechazado";
                pago.FechaAprobacion = null;
            }
            else
            {
                pago.Estado = "Pendiente";
            }

            if (!string.IsNullOrWhiteSpace(providerStatus) || !string.IsNullOrWhiteSpace(providerId))
            {
                pago.Detalle = $"{pago.Detalle} | MPStatus={providerStatus ?? "N/D"} | MPPaymentId={providerId ?? "N/D"}".Trim();
            }

            await _context.SaveChangesAsync();

            var approved = string.Equals(pago.Estado, "Aprobado", StringComparison.OrdinalIgnoreCase);
            var message = approved
                ? "Mercado Pago confirmó el cobro como aprobado."
                : string.IsNullOrWhiteSpace(providerStatus)
                    ? "Todavía no hay un pago acreditado en Mercado Pago para esta orden."
                    : $"Mercado Pago informó el estado `{providerStatus}` para esta operación.";

            return Ok(new MercadoPagoVerificationDto(
                pago.Id,
                pago.Estado,
                approved,
                providerStatus,
                providerId,
                message));
        }

        [Authorize(Roles = "Administrador")]
        [HttpPost("{id:int}/confirmar")]
        public async Task<ActionResult<PagoProfesionalDto>> Confirmar(int id)
        {
            var pago = await _context.PagosProfesionales
                .Include(p => p.Usuario)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (pago is null)
            {
                return NotFound();
            }

            if (string.Equals(pago.Estado, "Aprobado", StringComparison.OrdinalIgnoreCase))
            {
                return Ok(ToDto(pago));
            }

            pago.Estado = "Aprobado";
            pago.FechaAprobacion = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            await AuditoriaHelper.RegistrarAsync(
                _context,
                pago.UsuarioId,
                "Pago",
                "Confirmación",
                $"El pago #{pago.Id} fue confirmado como aprobado.",
                "PagoProfesional",
                pago.Id,
                pago.ReferenciaExterna);

            return Ok(ToDto(pago));
        }

        [Authorize(Roles = "Administrador")]
        [HttpPost("{id:int}/rechazar")]
        public async Task<ActionResult<PagoProfesionalDto>> Rechazar(int id)
        {
            var pago = await _context.PagosProfesionales
                .Include(p => p.Usuario)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (pago is null)
            {
                return NotFound();
            }

            pago.Estado = "Rechazado";
            pago.FechaAprobacion = null;
            await _context.SaveChangesAsync();
            await AuditoriaHelper.RegistrarAsync(
                _context,
                pago.UsuarioId,
                "Pago",
                "Rechazo",
                $"El pago #{pago.Id} fue marcado como rechazado.",
                "PagoProfesional",
                pago.Id,
                pago.ReferenciaExterna);

            return Ok(ToDto(pago));
        }

        private async Task ValidateCreateAsync(PagoProfesionalCreateDto request)
        {
            var usuario = await _context.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == request.UsuarioId);
            if (usuario is null)
            {
                ModelState.AddModelError(nameof(request.UsuarioId), "El usuario indicado no existe.");
                return;
            }

            if (!string.Equals(usuario.Rol, "Profesional", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(nameof(request.UsuarioId), "El usuario debe tener rol Profesional para generar este pago.");
            }
        }

        private async Task<PlanComercialDto> BuildFounderPlanAsync()
        {
            var cupos = Math.Max(1, _configuration.GetValue<int?>("Planes:Fundadores:MaxCupos") ?? 100);
            var usados = await _context.PagosProfesionales
                .AsNoTracking()
                .CountAsync(p => p.Concepto == "Plan Fundadores Pro" && p.Estado == "Aprobado");
            var alta = _configuration.GetValue<decimal?>("Planes:Fundadores:AltaArs") ?? 2500m;
            var mensualidad = _configuration.GetValue<decimal?>("Planes:Fundadores:MensualidadArs") ?? 2500m;
            var meses = Math.Max(1, _configuration.GetValue<int?>("Planes:Fundadores:MesesPromocion") ?? 3);
            var comision = _configuration.GetValue<decimal?>("Planes:PagoProtegido:ComisionPorcentaje") ?? 2m;

            return new PlanComercialDto(
                "FUNDADORES_PRO",
                "Plan Fundadores Pro",
                $"Alta promocional para los primeros {cupos} profesionales. Incluye mensualidad de {mensualidad:N0} ARS por {meses} meses y comisión de pago protegido del {comision:N1}%.",
                alta,
                mensualidad,
                meses,
                "ARS",
                cupos,
                usados,
                comision,
                usados < cupos);
        }

        private static string BuildPaymentDetail(PagoProfesionalCreateDto request, PlanComercialDto plan)
        {
            var userDetail = string.IsNullOrWhiteSpace(request.Detalle) ? string.Empty : request.Detalle.Trim();
            var parts = new[]
            {
                $"Plan={plan.Codigo}",
                $"Alta={plan.AltaMonto:N0} {plan.Moneda}",
                $"Mensualidad={plan.MensualidadMonto:N0} {plan.Moneda} por {plan.MesesPromocion} meses",
                $"ComisionPagoProtegido={plan.ComisionPagoProtegidoPorcentaje:N1}%",
                userDetail
            };

            return string.Join(" | ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
        }

        private HttpClient CreateMercadoPagoClient(string accessToken)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            return client;
        }

        private string GetMercadoPagoAccessToken() => _configuration["MercadoPago:AccessToken"] ?? string.Empty;

        private string GetMercadoPagoBaseUrl() => _configuration["MercadoPago:BaseUrl"] ?? "https://api.mercadopago.com";

        private static string GetStringProperty(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out var value)
                ? value.ToString() ?? string.Empty
                : string.Empty;
        }

        private static PagoProfesionalDto ToDto(PagoProfesional pago) => new(
            pago.Id,
            pago.UsuarioId,
            pago.Usuario?.Nombre ?? string.Empty,
            pago.Usuario?.Email ?? string.Empty,
            pago.Monto,
            pago.Moneda,
            pago.Concepto,
            pago.Estado,
            pago.Proveedor,
            pago.ReferenciaExterna,
            pago.Detalle,
            pago.FechaCreacion,
            pago.FechaAprobacion);
    }
}

using AppServicios.Api.Services;
using System.Security.Claims;
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
    [ServiceFilter(typeof(FinancialTransactionFilter))]
    [Authorize]
    [Route("api/[controller]")]
    public sealed class BilleteraController : ControllerBase
    {
        private readonly AppServiciosDbContext _context;
        private readonly PaymentSecurity _payments;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;

        public BilleteraController(
            AppServiciosDbContext context,
            PaymentSecurity payments,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _payments = payments;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet("usuario/{usuarioId:int}")]
        public async Task<ActionResult<BilleteraDto>> GetBilletera(int usuarioId)
        {
            if (!CanOperateAs(usuarioId))
            {
                return Forbid();
            }

            var wallet = await GetOrCreateWalletAsync(usuarioId);
            await _context.Entry(wallet).Reference(b => b.Usuario).LoadAsync();
            await _context.Entry(wallet).Collection(b => b.Movimientos).Query()
                .OrderByDescending(m => m.FechaCreacion)
                .Take(20)
                .LoadAsync();

            return Ok(ToDto(wallet));
        }

        [HttpGet("pagos-protegidos")]
        public async Task<ActionResult<IEnumerable<PagoServicioProtegidoDto>>> GetPagosProtegidos([FromQuery] int? userId = null)
        {
            var query = BasePagosQuery();
            if (userId.HasValue && userId.Value > 0)
            {
                if (!CanOperateAs(userId.Value))
                {
                    return Forbid();
                }

                var usuario = await _context.Usuarios
                    .AsNoTracking()
                    .Include(u => u.Cliente)
                    .Include(u => u.Profesional)
                    .FirstOrDefaultAsync(u => u.Id == userId.Value);

                if (usuario is null)
                {
                    return NotFound();
                }

                var clienteId = usuario.Cliente?.Id;
                var profesionalId = usuario.Profesional?.Id;
                query = query.Where(p =>
                    (clienteId.HasValue && p.ClienteId == clienteId.Value)
                    || (profesionalId.HasValue && p.ProfesionalId == profesionalId.Value));
            }
            else if (!User.IsInRole("Administrador"))
            {
                return Forbid();
            }

            var pagos = await query
                .OrderByDescending(p => p.FechaCreacion)
                .Take(80)
                .ToListAsync();

            return Ok(pagos.Select(ToDto));
        }

        [HttpGet("pagos-protegidos/solicitud/{solicitudId:int}")]
        public async Task<ActionResult<PagoServicioProtegidoDto>> GetPagoPorSolicitud(int solicitudId)
        {
            var pago = await BasePagosQuery()
                .FirstOrDefaultAsync(p => p.SolicitudTrabajoId == solicitudId);

            if (pago is null)
            {
                return NotFound();
            }

            if (!await CanAccessPagoAsync(pago))
            {
                return Forbid();
            }

            return Ok(ToDto(pago));
        }

        [HttpPost("pagos-protegidos")]
        public async Task<ActionResult<PagoServicioProtegidoDto>> CrearPagoProtegido([FromBody] PagoServicioProtegidoCreateDto request)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            if (!CanOperateAs(request.UsuarioOperadorId))
            {
                return Forbid();
            }

            var solicitud = await _context.SolicitudesTrabajo
                .Include(s => s.Cliente).ThenInclude(c => c.Usuario)
                .Include(s => s.Profesional).ThenInclude(p => p!.Usuario)
                .Include(s => s.Servicio)
                .FirstOrDefaultAsync(s => s.Id == request.SolicitudTrabajoId);

            if (solicitud is null)
            {
                return NotFound("La solicitud indicada no existe.");
            }

            var actor = await _context.Usuarios
                .AsNoTracking()
                .Include(u => u.Cliente)
                .FirstOrDefaultAsync(u => u.Id == request.UsuarioOperadorId);

            if (actor is null || !actor.Activo)
            {
                return Unauthorized("Debes operar con una cuenta activa.");
            }

            if (!User.IsInRole("Administrador") && actor.Cliente?.Id != solicitud.ClienteId)
            {
                return Forbid();
            }

            if (!solicitud.ProfesionalId.HasValue || solicitud.Profesional is null)
            {
                return Conflict("La solicitud debe estar aceptada por un profesional antes de crear el pago protegido.");
            }

            if (!string.Equals(solicitud.Estado, "Aceptado", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(solicitud.Estado, "Completado", StringComparison.OrdinalIgnoreCase))
            {
                return Conflict("El pago protegido se crea cuando la solicitud ya está aceptada.");
            }

            var existing = await _context.PagosServicioProtegidos
                .FirstOrDefaultAsync(p => p.SolicitudTrabajoId == solicitud.Id);

            if (existing is not null)
            {
                await LoadPagoRelationsAsync(existing);
                return Ok(ToDto(existing));
            }

            var grossAmount = Math.Round(ResolveSolicitudAmount(solicitud), 2, MidpointRounding.AwayFromZero);
            if ((request.Monto.HasValue && request.Monto.Value != grossAmount)
                || (!string.IsNullOrWhiteSpace(request.Moneda) && request.Moneda.Trim().ToUpperInvariant() != "ARS"))
                return BadRequest("El importe debe coincidir con la solicitud y la moneda debe ser ARS.");
            if (grossAmount <= 0)
            {
                return BadRequest("El monto del pago protegido debe ser mayor a cero.");
            }

            var commissionPercent = Math.Clamp(_configuration.GetValue<decimal?>("Planes:PagoProtegido:ComisionPorcentaje") ?? 0m, 0m, 100m);
            var commissionAmount = Math.Round(grossAmount * commissionPercent / 100m, 2, MidpointRounding.AwayFromZero);
            var professionalAmount = grossAmount - commissionAmount;

            var pago = new PagoServicioProtegido
            {
                SolicitudTrabajoId = solicitud.Id,
                ClienteId = solicitud.ClienteId,
                ProfesionalId = solicitud.ProfesionalId.Value,
                MontoBruto = grossAmount,
                ComisionPorcentaje = commissionPercent,
                ComisionMonto = commissionAmount,
                MontoProfesional = professionalAmount,
                Moneda = string.IsNullOrWhiteSpace(request.Moneda) ? "ARS" : request.Moneda.Trim().ToUpperInvariant(),
                Estado = "PendienteDePago",
                Proveedor = "Mercado Pago",
                ReferenciaExterna = $"APP-SERV-{Guid.NewGuid():N}",
                Detalle = $"Solicitud #{solicitud.Id} - {solicitud.Servicio?.Nombre ?? "Servicio"}",
                FechaVencimientoLiberacion = null
            };

            _context.PagosServicioProtegidos.Add(pago);
            await _context.SaveChangesAsync();
            await LoadPagoRelationsAsync(pago);
            await AuditoriaHelper.RegistrarAsync(
                _context,
                request.UsuarioOperadorId,
                "Billetera",
                "Pago protegido creado",
                $"Se creó pago protegido #{pago.Id} para solicitud #{solicitud.Id} por {pago.MontoBruto:N2} {pago.Moneda}.",
                "PagoServicioProtegido",
                pago.Id,
                pago.ReferenciaExterna);

            return CreatedAtAction(nameof(GetPagoPorSolicitud), new { solicitudId = solicitud.Id }, ToDto(pago));
        }

        // Kept as an explicit retired endpoint so old app versions cannot fabricate credit.
        [HttpPost("pagos-protegidos/{id:int}/confirmar-pago-demo")]
        public ActionResult<PagoServicioProtegidoDto> ConfirmarPagoDemo(int id, [FromBody] PagoProtegidoAccionDto request)
            => StatusCode(StatusCodes.Status410Gone, "La confirmación demo fue retirada. Verificá el pago con Mercado Pago.");

        [HttpPost("pagos-protegidos/{id:int}/mercadopago/preference")]
        public async Task<ActionResult<MercadoPagoPreferenceDto>> CrearPreferenciaMercadoPago(int id, [FromBody] PagoProtegidoAccionDto request)
        {
            var pago = await BasePagosQuery().FirstOrDefaultAsync(p => p.Id == id);
            if (pago is null)
            {
                return NotFound();
            }

            if (!CanOperateAs(request.UsuarioOperadorId)
                || !(User.IsInRole("Administrador") || pago.Cliente.UsuarioId == request.UsuarioOperadorId))
            {
                return Forbid();
            }

            if (!string.Equals(pago.Estado, "PendienteDePago", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(pago.Estado, "PagoRechazado", StringComparison.OrdinalIgnoreCase))
            {
                return Conflict("Mercado Pago se genera solo para pagos protegidos pendientes.");
            }

            var accessToken = GetMercadoPagoAccessToken();
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                return BadRequest("Configura `MercadoPago:AccessToken` y `MercadoPago:PublicKey` para usar Checkout Pro.");
            }

            var client = CreateMercadoPagoClient(accessToken);
            var body = new
            {
                items = new[]
                {
                    new
                    {
                        title = $"Pago protegido SERVILABS #{pago.SolicitudTrabajoId}",
                        description = pago.Detalle,
                        quantity = 1,
                        currency_id = pago.Moneda,
                        unit_price = pago.MontoBruto
                    }
                },
                payer = new
                {
                    email = pago.Cliente.Usuario?.Email,
                    name = pago.Cliente.Usuario?.Nombre
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
                    pagoProtegidoId = pago.Id,
                    solicitudTrabajoId = pago.SolicitudTrabajoId,
                    clienteId = pago.ClienteId,
                    profesionalId = pago.ProfesionalId,
                    tipo = "pago-protegido"
                }
            };

            var response = await client.PostAsync(
                $"{GetMercadoPagoBaseUrl().TrimEnd('/')}/checkout/preferences",
                new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"));

            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                return StatusCode(502, "Mercado Pago no pudo iniciar el cobro. Reintentá más tarde.");
            }

            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;
            var preferenceId = GetStringProperty(root, "id");
            var initPoint = GetStringProperty(root, "init_point");
            var sandboxInitPoint = GetStringProperty(root, "sandbox_init_point");
            var sandboxEnabled = _configuration.GetValue("MercadoPago:UseSandbox", false);

            pago.Proveedor = "Mercado Pago";
            pago.Estado = "PendienteDePago";
            pago.Detalle = $"{pago.Detalle} | PreferenceId={preferenceId}".Trim();
            await _context.SaveChangesAsync();
            await RegistrarAuditoriaPagoAsync(request.UsuarioOperadorId, "Preferencia Mercado Pago creada", pago);

            return Ok(new MercadoPagoPreferenceDto(
                pago.Id,
                pago.Estado,
                preferenceId,
                initPoint,
                sandboxInitPoint,
                _configuration["MercadoPago:PublicKey"],
                sandboxEnabled,
                "Checkout Pro generado para pagar la solicitud con Mercado Pago."));
        }

        [HttpPost("pagos-protegidos/{id:int}/mercadopago/verificar")]
        public async Task<ActionResult<MercadoPagoVerificationDto>> VerificarMercadoPago(int id, [FromBody] PagoProtegidoAccionDto request)
        {
            var pago = await BasePagosQuery().FirstOrDefaultAsync(p => p.Id == id);
            if (pago is null)
            {
                return NotFound();
            }

            if (!CanOperateAs(request.UsuarioOperadorId) || !await CanAccessPagoAsync(pago))
            {
                return Forbid();
            }

            if (pago.Estado is "Reintegrado" or "EnDisputa") return Conflict("Este pago requiere revisión administrativa.");
            var verified = await _payments.Verify(pago.ReferenciaExterna, pago.MontoBruto, pago.Moneda,
                pago.Estado is "PagadoRetenido" or "TrabajoCompletado" or "Liberado" ? pago.ReferenciaProveedor : null);
            var providerStatus = verified.Status;
            var providerId = verified.Id;
            if (verified.Approved)
            {
                await _payments.Claim(providerId, $"serv-{pago.Id}");
                if (pago.Estado is "PendienteDePago" or "PagoRechazado")
                {
                    await RetainPaidProtectedPaymentAsync(pago, providerId);
                    await RegistrarAuditoriaPagoAsync(request.UsuarioOperadorId, "Cobro Mercado Pago verificado", pago);
                }
            }
            var approved = verified.Approved;
            var message = approved ? "Mercado Pago confirmó el cobro. Se registró en la cuenta de la solicitud."
                : "Mercado Pago todavía no confirmó un cobro aprobado para esta orden.";

            return Ok(new MercadoPagoVerificationDto(
                pago.Id,
                pago.Estado,
                approved,
                providerStatus,
                providerId,
                message));
        }

        [HttpPost("pagos-protegidos/{id:int}/disputas")]
        public async Task<ActionResult<PagoServicioProtegidoDto>> AbrirDisputa(int id, [FromBody] DisputaPagoCreateDto request)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var pago = await BasePagosQuery().FirstOrDefaultAsync(p => p.Id == id);
            if (pago is null)
            {
                return NotFound();
            }

            if (!CanOperateAs(request.UsuarioOperadorId)
                || !await CanAccessPagoAsync(pago))
            {
                return Forbid();
            }

            if (!string.Equals(pago.Estado, "PagadoRetenido", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(pago.Estado, "TrabajoCompletado", StringComparison.OrdinalIgnoreCase))
            {
                return Conflict("Solo se puede abrir una disputa sobre un pago retenido.");
            }

            if (pago.Disputas.Any(d => string.Equals(d.Estado, "Abierta", StringComparison.OrdinalIgnoreCase)))
            {
                return Conflict("Este pago ya tiene una disputa abierta.");
            }

            pago.Estado = "EnDisputa";
            pago.Disputas.Add(new DisputaPago
            {
                UsuarioId = request.UsuarioOperadorId,
                Motivo = request.Motivo.Trim(),
                Estado = "Abierta",
                FechaCreacion = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            await RegistrarAuditoriaPagoAsync(request.UsuarioOperadorId, "Disputa abierta", pago);

            return Ok(ToDto(pago));
        }

        [HttpPost("pagos-protegidos/{id:int}/marcar-trabajo-completado")]
        public async Task<ActionResult<PagoServicioProtegidoDto>> MarcarTrabajoCompletado(int id, [FromBody] PagoProtegidoAccionDto request)
        {
            var pago = await BasePagosQuery().FirstOrDefaultAsync(p => p.Id == id);
            if (pago is null)
            {
                return NotFound();
            }

            if (!CanOperateAs(request.UsuarioOperadorId) || !await CanAccessPagoAsync(pago))
            {
                return Forbid();
            }

            if (!string.Equals(pago.Estado, "PagadoRetenido", StringComparison.OrdinalIgnoreCase))
            {
                return Conflict("El trabajo se marca completado cuando el pago ya está retenido.");
            }

            pago.Estado = "TrabajoCompletado";
            pago.FechaTrabajoCompletado = DateTime.UtcNow;
            pago.FechaVencimientoLiberacion = DateTime.UtcNow.AddHours(Math.Max(1,
                _configuration.GetValue<int?>("Billetera:PagoProtegido:LiberacionAutomaticaHoras") ?? 72));
            await _context.SaveChangesAsync();
            await RegistrarAuditoriaPagoAsync(request.UsuarioOperadorId, "Trabajo completado en pago protegido", pago);

            return Ok(ToDto(pago));
        }

        [HttpPost("pagos-protegidos/{id:int}/liberar")]
        public async Task<ActionResult<PagoServicioProtegidoDto>> LiberarPago(int id, [FromBody] PagoProtegidoAccionDto request)
        {
            var pago = await BasePagosQuery().FirstOrDefaultAsync(p => p.Id == id);
            if (pago is null)
            {
                return NotFound();
            }

            if (!CanOperateAs(request.UsuarioOperadorId))
            {
                return Forbid();
            }

            var canRelease = User.IsInRole("Administrador")
                || pago.Cliente.UsuarioId == request.UsuarioOperadorId
                || (pago.Profesional.UsuarioId == request.UsuarioOperadorId && pago.Estado == "TrabajoCompletado"
                    && pago.FechaVencimientoLiberacion.HasValue && pago.FechaVencimientoLiberacion.Value <= DateTime.UtcNow);

            if (!canRelease)
            {
                return Forbid();
            }

            var result = await ReleasePaymentAsync(pago, request.UsuarioOperadorId, "Liberado", request.Detalle);
            if (result is not null)
            {
                return result;
            }

            return Ok(ToDto(pago));
        }

        [Authorize(Roles = "Administrador")]
        [HttpPost("pagos-protegidos/{id:int}/resolver-disputa")]
        public async Task<ActionResult<PagoServicioProtegidoDto>> ResolverDisputa(int id, [FromBody] DisputaPagoResolucionDto request)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            if (!CanOperateAs(request.AdminUserId))
            {
                return Forbid();
            }

            var pago = await BasePagosQuery().FirstOrDefaultAsync(p => p.Id == id);
            if (pago is null)
            {
                return NotFound();
            }

            var abierta = pago.Disputas.FirstOrDefault(d => string.Equals(d.Estado, "Abierta", StringComparison.OrdinalIgnoreCase));
            if (abierta is null)
            {
                return Conflict("Este pago no tiene una disputa abierta.");
            }

            abierta.Estado = request.LiberarAlProfesional ? "ResueltaALiberar" : "ResueltaAReintegrar";
            abierta.Resolucion = request.Resolucion.Trim();
            abierta.FechaResolucion = DateTime.UtcNow;

            if (request.LiberarAlProfesional)
            {
                pago.Estado = "TrabajoCompletado";
                var result = await ReleasePaymentAsync(pago, request.AdminUserId, "LiberadoPorDisputa", request.Resolucion);
                if (result is not null)
                {
                    return result;
                }
            }
            else
            {
                await RefundPaymentAsync(pago, request.AdminUserId, request.Resolucion);
            }

            await _context.SaveChangesAsync();
            return Ok(ToDto(pago));
        }

        [Authorize(Roles = "Administrador")]
        [HttpPost("pagos-protegidos/liberar-vencidos")]
        public async Task<ActionResult<IEnumerable<PagoServicioProtegidoDto>>> LiberarVencidos([FromBody] PagoProtegidoAccionDto request)
        {
            if (!CanOperateAs(request.UsuarioOperadorId))
            {
                return Forbid();
            }

            var now = DateTime.UtcNow;
            var pagos = await BasePagosQuery()
                .Where(p => p.FechaVencimientoLiberacion.HasValue
                    && p.FechaVencimientoLiberacion.Value <= now
                    && p.Estado == "TrabajoCompletado")
                .ToListAsync();

            foreach (var pago in pagos)
            {
                var error = await ReleasePaymentAsync(pago, request.UsuarioOperadorId, "LiberadoAutomatico", request.Detalle);
                if (error is not null) return Conflict("No se pudieron conciliar los pagos. Revisá las operaciones antes de reintentar.");
            }

            await _context.SaveChangesAsync();
            return Ok(pagos.Select(ToDto));
        }

        private IQueryable<PagoServicioProtegido> BasePagosQuery() => _context.PagosServicioProtegidos
            .Include(p => p.Cliente).ThenInclude(c => c.Usuario)
            .Include(p => p.Profesional).ThenInclude(p => p.Usuario)
            .Include(p => p.SolicitudTrabajo).ThenInclude(s => s.Servicio)
            .Include(p => p.Disputas).ThenInclude(d => d.Usuario)
            .Include(p => p.Movimientos);

        private async Task RetainPaidProtectedPaymentAsync(PagoServicioProtegido pago, string providerReference)
        {
            if (string.Equals(pago.Estado, "PagadoRetenido", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var clienteWallet = await GetOrCreateWalletAsync(pago.Cliente.UsuarioId);
            var profesionalWallet = await GetOrCreateWalletAsync(pago.Profesional.UsuarioId);
            var now = DateTime.UtcNow;

            pago.Estado = "PagadoRetenido";
            pago.FechaPago = now;
            pago.ReferenciaProveedor = providerReference;
            pago.Detalle = $"{pago.Detalle} | MPStatus=approved | MPPaymentId={providerReference}".Trim();

            clienteWallet.SaldoRetenido += pago.MontoBruto;
            profesionalWallet.SaldoRetenido += pago.MontoProfesional;
            Touch(clienteWallet, profesionalWallet);

            AddMovimiento(clienteWallet, pago, "PagoProtegido", "Retenido", pago.MontoBruto, "Pago protegido retenido hasta confirmación o disputa.");
            AddMovimiento(profesionalWallet, pago, "CobroProtegido", "Retenido", pago.MontoProfesional, "Cobro protegido pendiente de liberación.");

            await _context.SaveChangesAsync();
        }

        private async Task<ActionResult<PagoServicioProtegidoDto>?> ReleasePaymentAsync(
            PagoServicioProtegido pago,
            int actorUserId,
            string action,
            string detail)
        {
            if (string.Equals(pago.Estado, "EnDisputa", StringComparison.OrdinalIgnoreCase))
            {
                return Conflict("No se puede liberar un pago con disputa abierta sin resolución administrativa.");
            }

            if (string.Equals(pago.Estado, "Liberado", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (!string.Equals(pago.Estado, "PagadoRetenido", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(pago.Estado, "TrabajoCompletado", StringComparison.OrdinalIgnoreCase))
            {
                return Conflict("Solo se pueden liberar pagos retenidos o trabajos completados.");
            }

            var verified = await _payments.Verify(pago.ReferenciaExterna, pago.MontoBruto, pago.Moneda, pago.ReferenciaProveedor);
            if (!verified.Approved) throw new PaymentCheckException("El cobro ya no está aprobado en Mercado Pago.");
            await _payments.Claim(verified.Id, $"serv-{pago.Id}");
            var clienteWallet = await GetOrCreateWalletAsync(pago.Cliente.UsuarioId);
            var profesionalWallet = await GetOrCreateWalletAsync(pago.Profesional.UsuarioId);

            if (clienteWallet.SaldoRetenido < pago.MontoBruto || profesionalWallet.SaldoRetenido < pago.MontoProfesional)
                throw new PaymentCheckException("Los saldos requieren conciliación. No se modificó la operación.");
            clienteWallet.SaldoRetenido -= pago.MontoBruto;
            profesionalWallet.SaldoRetenido -= pago.MontoProfesional;
            profesionalWallet.SaldoDisponible += pago.MontoProfesional;
            Touch(clienteWallet, profesionalWallet);

            pago.Estado = "Liberado";
            pago.FechaLiberacion = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(detail))
            {
                pago.Detalle = $"{pago.Detalle} | Liberacion={detail.Trim()}";
            }

            AddMovimiento(clienteWallet, pago, "PagoProtegido", "Liberado", -pago.MontoBruto, "Pago registrado para liquidación al profesional.");
            AddMovimiento(profesionalWallet, pago, "CobroProtegido", "Disponible", pago.MontoProfesional, "Cobro registrado para liquidación. No representa una transferencia bancaria.");

            var professional = await _context.Profesionales.FirstOrDefaultAsync(p => p.Id == pago.ProfesionalId);
            if (professional is not null)
            {
                professional.GananciaMensualActual += pago.MontoProfesional;
            }

            await _context.SaveChangesAsync();
            await RegistrarAuditoriaPagoAsync(actorUserId, action, pago);
            return null;
        }

        private async Task RefundPaymentAsync(PagoServicioProtegido pago, int actorUserId, string detail)
        {
            var clienteWallet = await GetOrCreateWalletAsync(pago.Cliente.UsuarioId);
            var profesionalWallet = await GetOrCreateWalletAsync(pago.Profesional.UsuarioId);

            if (clienteWallet.SaldoRetenido < pago.MontoBruto || profesionalWallet.SaldoRetenido < pago.MontoProfesional)
                throw new PaymentCheckException("Los saldos requieren conciliación. No se modificó la operación.");
            clienteWallet.SaldoRetenido -= pago.MontoBruto;
            profesionalWallet.SaldoRetenido -= pago.MontoProfesional;
            await _payments.Refund(pago.ReferenciaProveedor, pago.ReferenciaExterna, pago.MontoBruto, pago.Moneda, $"serv-{pago.Id}");
            Touch(clienteWallet, profesionalWallet);

            pago.Estado = "Reintegrado";
            pago.FechaLiberacion = DateTime.UtcNow;
            pago.Detalle = $"{pago.Detalle} | Reintegro={detail.Trim()}";

            AddMovimiento(clienteWallet, pago, "ReintegroProtegido", "Reintegrado", -pago.MontoBruto, "Devolución confirmada por Mercado Pago al medio de pago original.");
            AddMovimiento(profesionalWallet, pago, "CobroProtegido", "Reintegrado", -pago.MontoProfesional, "Cobro protegido reintegrado al cliente por resolución.");

            await RegistrarAuditoriaPagoAsync(actorUserId, "Pago protegido reintegrado", pago);
        }

        private async Task<Billetera> GetOrCreateWalletAsync(int usuarioId)
        {
            var wallet = await _context.Billeteras
                .Include(b => b.Usuario)
                .FirstOrDefaultAsync(b => b.UsuarioId == usuarioId);

            if (wallet is not null)
            {
                return wallet;
            }

            wallet = new Billetera
            {
                UsuarioId = usuarioId,
                Moneda = "ARS",
                FechaCreacion = DateTime.UtcNow,
                FechaActualizacion = DateTime.UtcNow
            };

            _context.Billeteras.Add(wallet);
            await _context.SaveChangesAsync();
            return wallet;
        }

        private async Task LoadPagoRelationsAsync(PagoServicioProtegido pago)
        {
            await _context.Entry(pago).Reference(p => p.Cliente).LoadAsync();
            await _context.Entry(pago.Cliente).Reference(c => c.Usuario).LoadAsync();
            await _context.Entry(pago).Reference(p => p.Profesional).LoadAsync();
            await _context.Entry(pago.Profesional).Reference(p => p.Usuario).LoadAsync();
            await _context.Entry(pago).Reference(p => p.SolicitudTrabajo).LoadAsync();
            await _context.Entry(pago.SolicitudTrabajo).Reference(s => s.Servicio).LoadAsync();
            await _context.Entry(pago).Collection(p => p.Disputas).LoadAsync();
        }

        private async Task<bool> CanAccessPagoAsync(PagoServicioProtegido pago)
        {
            if (User.IsInRole("Administrador"))
            {
                return true;
            }

            var userId = GetAuthenticatedUserId();
            if (!userId.HasValue)
            {
                return false;
            }

            var usuario = await _context.Usuarios
                .AsNoTracking()
                .Include(u => u.Cliente)
                .Include(u => u.Profesional)
                .FirstOrDefaultAsync(u => u.Id == userId.Value);

            return usuario is not null
                && ((usuario.Cliente?.Id == pago.ClienteId) || (usuario.Profesional?.Id == pago.ProfesionalId));
        }

        private bool CanOperateAs(int usuarioId)
        {
            var authenticatedId = GetAuthenticatedUserId();
            return authenticatedId.HasValue
                && authenticatedId.Value == usuarioId;
        }

        private int? GetAuthenticatedUserId()
        {
            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(raw, out var userId) ? userId : null;
        }

        private static decimal ResolveSolicitudAmount(SolicitudTrabajo solicitud)
            => solicitud.PresupuestoFinal
                ?? (solicitud.PresupuestoEstimado + (solicitud.CostoTraslado ?? 0m) + (solicitud.Incentivo ?? 0m));

        private static void AddMovimiento(Billetera wallet, PagoServicioProtegido pago, string type, string state, decimal amount, string description)
        {
            wallet.Movimientos.Add(new MovimientoBilletera
            {
                PagoServicioProtegidoId = pago.Id,
                Tipo = type,
                Estado = state,
                Monto = amount,
                Moneda = pago.Moneda,
                Descripcion = description,
                Referencia = pago.ReferenciaExterna,
                FechaCreacion = DateTime.UtcNow
            });
        }

        private static void Touch(params Billetera[] wallets)
        {
            var now = DateTime.UtcNow;
            foreach (var wallet in wallets)
            {
                wallet.FechaActualizacion = now;
            }
        }

        private async Task RegistrarAuditoriaPagoAsync(int usuarioId, string action, PagoServicioProtegido pago)
        {
            await AuditoriaHelper.RegistrarAsync(
                _context,
                usuarioId,
                "Billetera",
                action,
                $"Pago protegido #{pago.Id} ({pago.Estado}) por solicitud #{pago.SolicitudTrabajoId}.",
                "PagoServicioProtegido",
                pago.Id,
                pago.ReferenciaExterna);
        }

        private HttpClient CreateMercadoPagoClient(string accessToken) => _payments.Client();
        private string GetMercadoPagoAccessToken() => _configuration["MercadoPago:AccessToken"] ?? string.Empty;
        private string GetMercadoPagoBaseUrl() => _payments.BaseUrl;

        private static string GetStringProperty(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out var value)
                ? value.ToString() ?? string.Empty
                : string.Empty;
        }

        private static BilleteraDto ToDto(Billetera wallet) => new(
            wallet.Id,
            wallet.UsuarioId,
            wallet.Usuario?.Nombre ?? string.Empty,
            wallet.SaldoDisponible,
            wallet.SaldoRetenido,
            wallet.Moneda,
            wallet.FechaActualizacion,
            wallet.Movimientos
                .OrderByDescending(m => m.FechaCreacion)
                .Select(ToDto)
                .ToList());

        private static MovimientoBilleteraDto ToDto(MovimientoBilletera movement) => new(
            movement.Id,
            movement.PagoServicioProtegidoId,
            movement.Tipo,
            movement.Estado,
            movement.Monto,
            movement.Moneda,
            movement.Descripcion,
            movement.Referencia,
            movement.FechaCreacion);

        private static PagoServicioProtegidoDto ToDto(PagoServicioProtegido pago) => new(
            pago.Id,
            pago.SolicitudTrabajoId,
            pago.ClienteId,
            pago.Cliente?.Usuario?.Nombre ?? $"Cliente #{pago.ClienteId}",
            pago.ProfesionalId,
            pago.Profesional?.Usuario?.Nombre ?? $"Profesional #{pago.ProfesionalId}",
            pago.MontoBruto,
            pago.ComisionPorcentaje,
            pago.ComisionMonto,
            pago.MontoProfesional,
            pago.Moneda,
            pago.Estado,
            pago.Proveedor,
            pago.ReferenciaExterna,
            pago.ReferenciaProveedor,
            pago.Detalle,
            pago.FechaCreacion,
            pago.FechaPago,
            pago.FechaTrabajoCompletado,
            pago.FechaVencimientoLiberacion,
            pago.FechaLiberacion,
            pago.Disputas.OrderByDescending(d => d.FechaCreacion).Select(ToDto).ToList());

        private static DisputaPagoDto ToDto(DisputaPago dispute) => new(
            dispute.Id,
            dispute.UsuarioId,
            dispute.Usuario?.Nombre ?? $"Usuario #{dispute.UsuarioId}",
            dispute.Motivo,
            dispute.Estado,
            dispute.Resolucion,
            dispute.FechaCreacion,
            dispute.FechaResolucion);
    }
}

using System;

namespace AppServicios.Api.Domain
{
    public class DisputaPago
    {
        public int Id { get; set; }
        public int PagoServicioProtegidoId { get; set; }
        public PagoServicioProtegido PagoServicioProtegido { get; set; } = null!;

        public int UsuarioId { get; set; }
        public Usuario Usuario { get; set; } = null!;

        public string Motivo { get; set; } = string.Empty;
        public string Estado { get; set; } = "Abierta";
        public string Resolucion { get; set; } = string.Empty;
        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
        public DateTime? FechaResolucion { get; set; }
    }
}

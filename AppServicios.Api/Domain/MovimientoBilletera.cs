using System;

namespace AppServicios.Api.Domain
{
    public class MovimientoBilletera
    {
        public int Id { get; set; }
        public int BilleteraId { get; set; }
        public Billetera Billetera { get; set; } = null!;

        public int? PagoServicioProtegidoId { get; set; }
        public PagoServicioProtegido? PagoServicioProtegido { get; set; }

        public string Tipo { get; set; } = string.Empty;
        public string Estado { get; set; } = "Registrado";
        public decimal Monto { get; set; }
        public string Moneda { get; set; } = "ARS";
        public string Descripcion { get; set; } = string.Empty;
        public string Referencia { get; set; } = string.Empty;
        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    }
}

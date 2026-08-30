using System;
using System.Collections.Generic;

namespace AppServicios.Api.Domain
{
    public class PagoServicioProtegido
    {
        public int Id { get; set; }

        public int SolicitudTrabajoId { get; set; }
        public SolicitudTrabajo SolicitudTrabajo { get; set; } = null!;

        public int ClienteId { get; set; }
        public Cliente Cliente { get; set; } = null!;

        public int ProfesionalId { get; set; }
        public Profesional Profesional { get; set; } = null!;

        public decimal MontoBruto { get; set; }
        public decimal ComisionPorcentaje { get; set; }
        public decimal ComisionMonto { get; set; }
        public decimal MontoProfesional { get; set; }
        public string Moneda { get; set; } = "ARS";
        public string Estado { get; set; } = "PendienteDePago";
        public string Proveedor { get; set; } = "Mercado Pago";
        public string ReferenciaExterna { get; set; } = string.Empty;
        public string ReferenciaProveedor { get; set; } = string.Empty;
        public string Detalle { get; set; } = string.Empty;

        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
        public DateTime? FechaPago { get; set; }
        public DateTime? FechaTrabajoCompletado { get; set; }
        public DateTime? FechaVencimientoLiberacion { get; set; }
        public DateTime? FechaLiberacion { get; set; }

        public ICollection<DisputaPago> Disputas { get; set; } = new List<DisputaPago>();
        public ICollection<MovimientoBilletera> Movimientos { get; set; } = new List<MovimientoBilletera>();
    }
}

using System;
using System.Collections.Generic;

namespace AppServicios.Api.Domain
{
    public class Billetera
    {
        public int Id { get; set; }
        public int UsuarioId { get; set; }
        public Usuario Usuario { get; set; } = null!;

        public decimal SaldoDisponible { get; set; }
        public decimal SaldoRetenido { get; set; }
        public string Moneda { get; set; } = "ARS";
        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
        public DateTime FechaActualizacion { get; set; } = DateTime.UtcNow;

        public ICollection<MovimientoBilletera> Movimientos { get; set; } = new List<MovimientoBilletera>();
    }
}

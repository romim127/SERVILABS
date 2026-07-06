namespace AppServicios.Api.Domain
{
    public class IpBloqueada
    {
        public int Id { get; set; }
        public string Ip { get; set; } = string.Empty;
        public string Motivo { get; set; } = string.Empty;
        public int IntentosFallidos { get; set; }
        public bool Activa { get; set; } = true;
        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
        public DateTime? FechaUltimoIntento { get; set; }
        public DateTime? FechaDesbloqueo { get; set; }
        public string DesbloqueadoPor { get; set; } = string.Empty;
    }
}

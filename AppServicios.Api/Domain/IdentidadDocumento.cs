using System.ComponentModel.DataAnnotations;
namespace AppServicios.Api.Domain;
public sealed class IdentidadDocumento
{
    [Key] public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;
    public byte[] Foto { get; set; } = [];
    public string FotoTipo { get; set; } = string.Empty;
    public byte[] Dni { get; set; } = [];
    public string DniTipo { get; set; } = string.Empty;
    public DateTime FechaEnvio { get; set; } = DateTime.UtcNow;
}

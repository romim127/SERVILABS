using System.ComponentModel.DataAnnotations;
namespace AppServicios.Api.Domain;
public sealed class CobroVerificado
{
    [Key, MaxLength(80)] public string ProveedorId { get; set; } = "";
    [MaxLength(80)] public string Orden { get; set; } = "";
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
}

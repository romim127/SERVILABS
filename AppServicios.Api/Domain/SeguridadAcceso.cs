using System.ComponentModel.DataAnnotations;
namespace AppServicios.Api.Domain;
public sealed class TokenRevocado
{
    [Key, MaxLength(80)] public string Id { get; set; } = "";
    public DateTime Expira { get; set; }
}
public sealed class LimiteAcceso
{
    [Key, MaxLength(64)] public string Clave { get; set; } = "";
    public DateTime Ventana { get; set; }
    public int Intentos { get; set; }
}

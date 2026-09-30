using System.ComponentModel.DataAnnotations;
namespace AppServicios.Api.Domain;
public sealed class PerfilPublico
{
    [Key] public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;
    public byte[] Foto { get; set; } = [];
    public string Tipo { get; set; } = "image/jpeg";
    public DateTime Actualizado { get; set; } = DateTime.UtcNow;
}
public sealed class VerificacionLinea
{
    [Key, MaxLength(64)] public string Id { get; set; } = "";
    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;
    public string Tipo { get; set; } = "";
    public string Telefono { get; set; } = "";
    public string DatosHash { get; set; } = "";
    public string NavegadorHash { get; set; } = "";
    public string Verifier { get; set; } = "";
    public string Estado { get; set; } = "pendiente";
    public bool Prueba { get; set; } = true;
    public bool Nativa { get; set; }
    public DateTime Creado { get; set; } = DateTime.UtcNow;
    public DateTime? Completado { get; set; }
    public double? Latitud { get; set; }
    public double? Longitud { get; set; }
}

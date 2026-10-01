using System;

namespace AppServicios.Api.Domain
{
    public class Direccion
    {
        public int Id { get; set; }
        public int ClienteId { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        [Microsoft.AspNetCore.Mvc.ModelBinding.Validation.ValidateNever]
        public Cliente Cliente { get; set; } = null!;

        public string Calle { get; set; } = string.Empty;
        public string Numero { get; set; } = string.Empty;
        public string Piso { get; set; } = string.Empty;
        public string Departamento { get; set; } = string.Empty;
        public string Localidad { get; set; } = string.Empty;
        public string CodigoPostal { get; set; } = string.Empty;
        public double Latitud { get; set; }
        public double Longitud { get; set; }

        public bool EsPrincipal { get; set; } = false;
        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    }
}

using System.Text.RegularExpressions;
namespace AppServicios.Api.Helpers;
public static class Telefono
{
    // Existing ten-digit Argentine numbers are migrated on use, never by deleting interior digits.
    public static string? Normalizar(string? raw, string pais = "54")
    {
        var value = Regex.Replace(raw ?? "", @"[\s()\-]", "");
        if (value.StartsWith("00")) value = "+" + value[2..];
        if (value.StartsWith("+"))
        {
            if (!Regex.IsMatch(value, @"^\+[1-9]\d{7,14}$")) return null;
            if (!value.StartsWith("+54")) return value;
            value = value[3..]; pais = "54";
        }
        if (!Regex.IsMatch(value, @"^\d+$") || !Regex.IsMatch(pais, @"^[1-9]\d{0,2}$")) return null;
        if (pais == "54")
        {
            if (value.Length == 11 && value.StartsWith('9')) value = value[1..];
            if (value.Length != 10 || value.StartsWith('0')) return null;
            return "+549" + value;
        }
        var full = "+" + pais + value;
        return Regex.IsMatch(full, @"^\+[1-9]\d{7,14}$") ? full : null;
    }
}

public sealed class TelefonoValidoAttribute : System.ComponentModel.DataAnnotations.ValidationAttribute
{
    public override bool IsValid(object? value) => value is string text && Telefono.Normalizar(text) is not null;
    public TelefonoValidoAttribute() { ErrorMessage = "Ingresá un celular válido con código de país; para Argentina, código de área y número sin 0 ni 15."; }
}

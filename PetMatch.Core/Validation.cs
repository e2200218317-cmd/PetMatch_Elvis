using System.Net.Mail;
using System.Text.RegularExpressions;

namespace PetMatch.Core;

public static class Validation
{
    public static int Age(DateTime birth, DateTime today)
    {
        var years = today.Year - birth.Year;
        return birth.Date > today.Date.AddYears(-years) ? years - 1 : years;
    }

    public static string Email(string? value)
    {
        var email = (value ?? "").Trim();
        if (email.Length > 254 || !MailAddress.TryCreate(email, out var parsed) ||
            parsed.Address != email || !parsed.Host.Contains('.'))
            throw new ArgumentException("Escribe un correo electrónico válido.");
        return email;
    }

    public static void Password(string? password, string? confirmation)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 8 || password.Length > 72 ||
            !password.Any(char.IsUpper) || !password.Any(char.IsLower) || !password.Any(char.IsDigit))
            throw new ArgumentException("La contraseña debe tener entre 8 y 72 caracteres, mayúscula, minúscula y número.");
        if (password != confirmation) throw new ArgumentException("Las contraseñas no coinciden.");
    }

    public static string Phone(string? value)
    {
        var digits = Regex.Replace(value ?? "", @"[\s()\-]", "");
        if (Regex.IsMatch(digits, @"^09[0-9]{8}$")) digits = "+593" + digits[1..];
        if (!Regex.IsMatch(digits, @"^\+5939[0-9]{8}$"))
            throw new ArgumentException("Escribe un celular de Ecuador: 09XXXXXXXX o +5939XXXXXXXX.");
        return digits;
    }

    public static void Profile(UserProfile p, bool hasPhoto, DateTime? today = null)
    {
        p.nombres = p.nombres.Trim(); p.apellidos = p.apellidos.Trim();
        p.ciudad = p.ciudad.Trim(); p.direccion = p.direccion.Trim(); p.cedula = p.cedula.Trim();
        if (p.nombres.Length is < 2 or > 80 || p.apellidos.Length is < 2 or > 80)
            throw new ArgumentException("Completa nombres y apellidos (de 2 a 80 caracteres cada uno).");
        if (Age(p.fecha_nacimiento, today ?? DateTime.Today) < 18 || p.fecha_nacimiento.Year < 1900)
            throw new ArgumentException("Debes tener al menos 18 años. Revisa tu fecha de nacimiento.");
        if (!Regex.IsMatch(p.cedula, @"^[0-9]{10}$"))
            throw new ArgumentException("La cédula es obligatoria y debe tener 10 dígitos.");
        if (p.ciudad.Length is < 2 or > 100 || p.direccion.Length is < 5 or > 250)
            throw new ArgumentException("Completa la ciudad y una dirección de 5 a 250 caracteres.");
        p.celular = Phone(p.celular);
        if (!hasPhoto) throw new ArgumentException("Selecciona una foto de perfil.");
    }

    public static string ImageType(byte[] bytes)
    {
        if (bytes.Length is 0 or > 5242880) throw new ArgumentException("La foto debe pesar como máximo 5 MB.");
        if (bytes.Length >= 8 && bytes.Take(8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10})) return "png";
        if (bytes.Length >= 3 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255) return "jpg";
        throw new ArgumentException("Elige una imagen JPG o PNG.");
    }
}

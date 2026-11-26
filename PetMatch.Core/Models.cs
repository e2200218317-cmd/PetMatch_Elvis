using System.Text.Json.Serialization;

namespace PetMatch.Core;

public sealed class AuthUser
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("email")] public string Email { get; set; } = "";
}

public sealed class AuthSession
{
    [JsonPropertyName("access_token")] public string AccessToken { get; set; } = "";
    [JsonPropertyName("refresh_token")] public string RefreshToken { get; set; } = "";
    [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
    [JsonPropertyName("expires_at")] public long ExpiresAt { get; set; }
    [JsonPropertyName("user")] public AuthUser User { get; set; } = new();
}

public sealed class UserProfile
{
    public string id { get; set; } = "";
    public string email { get; set; } = "";
    public string nombres { get; set; } = "";
    public string apellidos { get; set; } = "";
    public string ciudad { get; set; } = "";
    public string direccion { get; set; } = "";
    public string celular { get; set; } = "";
    public string cedula { get; set; } = "";
    public DateTime fecha_nacimiento { get; set; } = DateTime.Today.AddYears(-18);
    public string? avatar_path { get; set; }
    public bool eliminando { get; set; }
}

public interface ISessionStore
{
    Task<string?> ReadAsync();
    Task WriteAsync(string value);
    Task ClearAsync();
}

public sealed class ApiException(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}

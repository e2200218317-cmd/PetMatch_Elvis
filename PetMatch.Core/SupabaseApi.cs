using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace PetMatch.Core;

// REST oficial de Supabase Auth / PostgREST / Storage. Sin contraseña de PostgreSQL.
public sealed partial class SupabaseApi(HttpClient http, ISessionStore store, string url, string publicKey)
{
    private readonly SemaphoreSlim refreshLock = new(1, 1);
    private readonly string root = url.TrimEnd('/');
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    public AuthSession? Session { get; private set; }
    public string? UserId => Session?.User.Id;
    public string? Email => Session?.User.Email;
    public bool IsConfigured => Uri.TryCreate(root, UriKind.Absolute, out var u) && u.Scheme == "https" &&
        !root.Contains("TU_PROYECTO") && publicKey.Length > 20 && !publicKey.Contains("TU_CLAVE");

    private async Task<string> Request(HttpMethod method, string path, object? body = null,
        string? token = null, byte[]? bytes = null, string? contentType = null)
    {
        if (!IsConfigured) throw new InvalidOperationException("Configura la URL y la clave pública en Config/SupabaseConfig.cs.");
        using var request = new HttpRequestMessage(method, root + path);
        request.Headers.Add("apikey", publicKey);
        if (!string.IsNullOrEmpty(token)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (bytes != null)
        {
            request.Content = new ByteArrayContent(bytes);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType!);
        }
        else if (body != null) request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        HttpResponseMessage response;
        try { response = await http.SendAsync(request); }
        catch (HttpRequestException) { throw new IOException("No se pudo conectar. Revisa tu conexión a Internet."); }
        catch (TaskCanceledException) { throw new IOException("La conexión tardó demasiado. Intenta nuevamente."); }
        using (response)
        {
            string text = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode) return text;
            string code = "", detail = "";
            try
            {
                using var doc = JsonDocument.Parse(text);
                foreach (var field in new[] { "error_code", "code", "error" })
                    if (doc.RootElement.TryGetProperty(field, out var v)) { code = v.ToString(); break; }
                foreach (var field in new[] { "msg", "message", "error_description" })
                    if (doc.RootElement.TryGetProperty(field, out var v)) { detail = v.ToString(); break; }
            }
            catch (JsonException) { }
            string message = code switch
            {
                "invalid_credentials" => "Correo o contraseña incorrectos.",
                "email_not_confirmed" => "Confirma tu correo antes de iniciar sesión.",
                "user_already_exists" => "Esta cuenta ya existe. Inicia sesión o recupera tu contraseña.",
                "otp_expired" => "El código es incorrecto o venció. Solicita uno nuevo.",
                "weak_password" => "La contraseña no cumple las reglas de seguridad del proyecto.",
                "email_address_not_authorized" => "El envío de correo necesita configurar SMTP en Supabase.",
                "23505" => "La cédula ya está registrada en otra cuenta.",
                "P0001" when detail.StartsWith("PETMATCH:") => detail[9..].Trim(),
                "same_password" => "Usa una contraseña diferente a la anterior.",
                "delete_pending" => "La eliminación quedó pendiente. Vuelve a intentarlo desde tu perfil para terminarla.",
                _ when (int)response.StatusCode == 429 => "Se alcanzó el límite de intentos o correos. Espera antes de volver a intentar.",
                _ when (int)response.StatusCode == 401 => "Tu sesión venció. Inicia sesión nuevamente.",
                _ when (int)response.StatusCode == 403 => "No tienes permiso para realizar esta acción.",
                _ => $"No se pudo completar la operación (HTTP {(int)response.StatusCode}, código {code}). Revisa la configuración y vuelve a intentar."
            };
            throw new ApiException((int)response.StatusCode, code, message);
        }
    }

    public async Task<IReadOnlyList<CatalogPet>> CatalogAsync(int offset, int limit)
    {
        var path = "/rest/v1/mascotas?select=id,nombre,especie,raza,edad,sexo,ubicacion,descripcion,foto_url"
            + FormattableString.Invariant($"&order=creado_en.desc,id.desc&offset={offset}&limit={limit}");
        try
        {
            var json = await Request(HttpMethod.Get, path);
            return JsonSerializer.Deserialize<List<CatalogPet>>(json, Json) ?? [];
        }
        catch (ApiException e) when (e.Status == 404)
        {
            throw new InvalidOperationException("El catálogo todavía no está configurado en Supabase.");
        }
    }

    private static AuthSession ParseSession(string json)
    {
        var session = JsonSerializer.Deserialize<AuthSession>(json, Json) ?? throw new InvalidOperationException("Sesión no válida.");
        if (string.IsNullOrEmpty(session.AccessToken) || string.IsNullOrEmpty(session.RefreshToken))
            throw new InvalidOperationException("Supabase no devolvió una sesión válida.");
        if (session.ExpiresAt == 0) session.ExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + session.ExpiresIn;
        return session;
    }

    private async Task Accept(AuthSession session)
    {
        await store.WriteAsync(JsonSerializer.Serialize(session));
        Session = session;
    }

    public async Task LoginAsync(string email, string password) =>
        await Accept(ParseSession(await Request(HttpMethod.Post, "/auth/v1/token?grant_type=password", new { email, password })));

    public async Task<bool> SignupAsync(string email, string password)
    {
        var json = await Request(HttpMethod.Post, "/auth/v1/signup", new { email, password });
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("access_token", out var token) || string.IsNullOrEmpty(token.GetString())) return false;
        await Accept(ParseSession(json)); return true;
    }

    public Task SendRecoveryAsync(string email) => Request(HttpMethod.Post, "/auth/v1/recover", new { email });
    public Task ResendSignupAsync(string email) => Request(HttpMethod.Post, "/auth/v1/resend", new { email, type = "signup" });
    public async Task ConfirmSignupAsync(string email, string code) =>
        await Accept(ParseSession(await Request(HttpMethod.Post, "/auth/v1/verify", new { email, token = code.Trim(), type = "signup" })));

    // La sesión de recuperación vive solamente en memoria y no abre el panel de usuario.
    public async Task<AuthSession> VerifyRecoveryAsync(string email, string code) =>
        ParseSession(await Request(HttpMethod.Post, "/auth/v1/verify", new { email, token = code.Trim(), type = "recovery" }));

    public async Task ResetPasswordAsync(AuthSession recovery, string password)
    {
        await Request(HttpMethod.Put, "/auth/v1/user", new { password }, recovery.AccessToken);
        try { await Request(HttpMethod.Post, "/auth/v1/logout?scope=global", token: recovery.AccessToken); }
        catch (ApiException) { /* La contraseña ya cambió; el login siguiente obtiene una sesión nueva. */ }
        catch (IOException) { }
        await ClearLocalAsync();
    }

    public async Task<bool> RestoreAsync()
    {
        var json = await store.ReadAsync();
        if (string.IsNullOrEmpty(json)) return false;
        try { Session = ParseSession(json); }
        catch (Exception e) when (e is JsonException or InvalidOperationException) { await ClearLocalAsync(); return false; }
        try { await ValidateSessionAsync(); return true; }
        catch (ApiException e) when (e.Status is 400 or 401 or 403) { await ClearLocalAsync(); return false; }
    }

    public async Task<string> AccessTokenAsync(bool forceRefresh = false)
    {
        await refreshLock.WaitAsync();
        try
        {
            if (Session == null) throw new ApiException(401, "no_session", "Inicia sesión para continuar.");
            if (forceRefresh || Session.ExpiresAt <= DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 60)
            {
                try
                {
                    var refreshed = ParseSession(await Request(HttpMethod.Post, "/auth/v1/token?grant_type=refresh_token",
                        new { refresh_token = Session.RefreshToken }));
                    await Accept(refreshed);
                }
                catch (ApiException e) when (e.Status is 400 or 401 or 403) { await ClearLocalAsync(); throw; }
            }
            return Session.AccessToken;
        }
        finally { refreshLock.Release(); }
    }

    public async Task<string> AuthorizedAsync(HttpMethod method, string path, object? body = null,
        byte[]? bytes = null, string? contentType = null)
    {
        var token = await AccessTokenAsync();
        try { return await Request(method, path, body, token, bytes, contentType); }
        catch (ApiException e) when (e.Status == 401)
        { return await Request(method, path, body, await AccessTokenAsync(true), bytes, contentType); }
    }

    public async Task ValidateSessionAsync()
    {
        var json = await AuthorizedAsync(HttpMethod.Get, "/auth/v1/user");
        Session!.User = JsonSerializer.Deserialize<AuthUser>(json, Json)!;
    }

    public async Task<bool> LogoutAsync()
    {
        bool remote = true;
        try { if (Session != null) await AuthorizedAsync(HttpMethod.Post, "/auth/v1/logout?scope=local"); }
        catch (Exception e) when (e is ApiException or IOException) { remote = false; }
        finally { await ClearLocalAsync(); }
        return remote;
    }

    public async Task ClearLocalAsync() { Session = null; await store.ClearAsync(); }

    public async Task<UserProfile?> MyProfileAsync()
    {
        var json = await AuthorizedAsync(HttpMethod.Post, "/rest/v1/rpc/mi_perfil", new { });
        return JsonSerializer.Deserialize<List<UserProfile>>(json, Json)?.SingleOrDefault();
    }

    public async Task SaveProfileAsync(UserProfile p, byte[]? newPhoto)
    {
        Validation.Profile(p, newPhoto != null || !string.IsNullOrEmpty(p.avatar_path));
        string? uploaded = null;
        if (newPhoto != null)
        {
            string type = Validation.ImageType(newPhoto);
            uploaded = $"{UserId}/{Guid.NewGuid():N}.{type}";
            await AuthorizedAsync(HttpMethod.Post, "/storage/v1/object/avatars/" + uploaded,
                bytes: newPhoto, contentType: type == "jpg" ? "image/jpeg" : "image/png");
        }
        string previous = p.avatar_path;
        try
        {
            await AuthorizedAsync(HttpMethod.Post, "/rest/v1/rpc/guardar_perfil", new
            {
                p_nombres = p.nombres, p_apellidos = p.apellidos, p_ciudad = p.ciudad,
                p_direccion = p.direccion, p_celular = p.celular, p_cedula = p.cedula,
                p_fecha_nacimiento = p.fecha_nacimiento.ToString("yyyy-MM-dd"), p_avatar_path = uploaded ?? previous
            });
            p.avatar_path = uploaded ?? previous;
        }
        catch
        {
            // Una carga cuyo guardado falla puede permanecer huérfana. No borrarla aquí:
            // una respuesta perdida no demuestra que la transacción del perfil falló.
            // La eliminación de cuenta limpia todos los objetos de la carpeta del usuario.
            throw;
        }
        if (uploaded != null && !string.IsNullOrEmpty(previous))
        {
            try { await RemoveAvatarAsync(previous); }
            catch (Exception e) when (e is ApiException or IOException) { /* Perfil guardado; limpieza recuperable. */ }
        }
    }

    public Task RemoveAvatarAsync(string path) => AuthorizedAsync(HttpMethod.Delete, "/storage/v1/object/avatars", new { prefixes = new[] { path } });

    public async Task<string?> AvatarUrlAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var json = await AuthorizedAsync(HttpMethod.Post, "/storage/v1/object/sign/avatars/" + path, new { expiresIn = 300 });
        using var doc = JsonDocument.Parse(json);
        var signed = doc.RootElement.GetProperty("signedURL").GetString()!;
        return signed.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? signed : root + "/storage/v1" + signed;
    }

    public async Task DeleteAccountAsync(string password)
    {
        await AuthorizedAsync(HttpMethod.Post, "/functions/v1/delete-account", new { password });
        await ClearLocalAsync();
    }
}

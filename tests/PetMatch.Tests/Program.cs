using PetMatch.Core;
using System.Net;
using System.Text.Json;

int passed = 0;
void Check(bool value, string label) { if (!value) throw new Exception("FAIL: " + label); passed++; Console.WriteLine("OK " + label); }
void Reject(Action action, string label) { try { action(); } catch (ArgumentException) { Check(true, label); return; } throw new Exception("FAIL: " + label); }
var today = new DateTime(2026, 11, 7);
Check(Validation.Age(new(2008, 11, 7), today) == 18, "cumple 18 hoy");
Check(Validation.Age(new(2008, 11, 8), today) == 17, "menor por un día");
Check(Validation.Age(new(2008, 2, 29), new(2026, 2, 28)) == 17, "límite nacimiento bisiesto conservador");
Check(Validation.Phone("099 123 4567") == "+593991234567", "normalización celular");
Reject(() => Validation.Phone("12345"), "celular inválido");
Reject(() => Validation.Email("correo"), "correo inválido");
Reject(() => Validation.Password("Abc12345", "otra"), "confirmación diferente");
Reject(() => Validation.Password("abcdefgh", "abcdefgh"), "contraseña débil");
Reject(() => Validation.ImageType([1, 2, 3]), "archivo que no es imagen");
Reject(() => Validation.ImageType(new byte[5242881]), "foto mayor de 5 MB");
var profile = new UserProfile { nombres = "Ana", apellidos = "Prueba", cedula = "0123456789", ciudad = "El Coca", direccion = "Calle de pruebas", celular = "0991234567", fecha_nacimiento = new(2008,11,8) };
Reject(() => Validation.Profile(profile, true, today), "registro de menor rechazado");
profile.fecha_nacimiento = new(2008,11,7);
Validation.Profile(profile, true, today); Check(true, "registro de mayor aceptado");
Reject(() => Validation.Profile(profile, false, today), "foto obligatoria");

string Session(long expires, string access = "access") => JsonSerializer.Serialize(new AuthSession
{ AccessToken = access, RefreshToken = "refresh", ExpiresAt = expires, User = new AuthUser { Id = "11111111-1111-1111-1111-111111111111", Email = "test@example.com" } });
var saved = new MemoryStore(); var handler = new FakeHttp();
var api = new SupabaseApi(new HttpClient(handler), saved, "https://test.supabase.co", "sb_publishable_example_for_tests");
handler.Queue.Enqueue(_ => new(HttpStatusCode.OK) { Content = new StringContent("{\"id\":\"user\"}") });
Check(!await api.SignupAsync("test@example.com", "Password1"), "registro pendiente no crea sesión falsa");
Check(api.Session == null && saved.Value == null, "confirmación requerida no persiste tokens");
handler.Queue.Enqueue(_ => new(HttpStatusCode.OK) { Content = new StringContent(Session(1)) });
await api.LoginAsync("test@example.com", "Password1");
handler.Queue.Enqueue(_ => new(HttpStatusCode.OK) { Content = new StringContent(Session(DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(), "fresh")) });
var tokens = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => api.AccessTokenAsync()));
Check(tokens.All(x => x == "fresh") && handler.Queue.Count == 0, "renovación serializada con 12 llamadas simultáneas");
Check(handler.Requests.Count(x => x.Contains("refresh_token")) == 1, "solo una renovación de token");
handler.Queue.Enqueue(_ => throw new HttpRequestException());
Check(!await api.LogoutAsync() && api.Session == null && saved.Value == null, "cierre sin Internet limpia sesión local");
handler.Queue.Enqueue(_ => new(HttpStatusCode.OK) { Content = new StringContent(Session(DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds())) });
var recovery = await api.VerifyRecoveryAsync("test@example.com", "123456");
Check(api.Session == null && saved.Value == null, "recuperación no abre sesión persistente");
handler.Queue.Enqueue(req => { Check(req.Method == HttpMethod.Put, "cambio de contraseña usa PUT /user"); return new(HttpStatusCode.OK) { Content = new StringContent("{}") }; });
handler.Queue.Enqueue(_ => new(HttpStatusCode.NoContent));
await api.ResetPasswordAsync(recovery, "AnotherPass1");
Check(api.Session == null, "restablecimiento termina fuera de la cuenta");
saved.Value = Session(1);
handler.Queue.Enqueue(_ => new(HttpStatusCode.BadRequest) { Content = new StringContent("{\"error_code\":\"refresh_token_not_found\"}") });
Check(!await api.RestoreAsync() && saved.Value == null, "refresh revocado elimina sesión caducada");
handler.Queue.Enqueue(req =>
{
    Check(req.Method == HttpMethod.Get && req.Headers.Authorization == null &&
        req.RequestUri!.AbsolutePath == "/rest/v1/mascotas" &&
        req.RequestUri.Query.Contains("offset=12&limit=13"), "catálogo público consulta la página solicitada");
    return new(HttpStatusCode.OK)
    {
        Content = new StringContent("""
            [{"id":"11111111-1111-1111-1111-111111111111","especie":"Perro","ubicacion":"El Coca","foto_url":"https://example.com/luna.jpg"}]
            """)
    };
});
var pets = await api.CatalogAsync(12, 13);
Check(pets.Single().FotoUrl == "https://example.com/luna.jpg", "catálogo lee la URL de la foto");
Check(handler.Keys.All(x => x == "sb_publishable_example_for_tests"), "cada solicitud incluye clave pública");
Console.WriteLine($"\n{passed} comprobaciones correctas.");

sealed class MemoryStore : ISessionStore
{
    public string? Value;
    public Task<string?> ReadAsync() => Task.FromResult(Value);
    public Task WriteAsync(string value) { Value = value; return Task.CompletedTask; }
    public Task ClearAsync() { Value = null; return Task.CompletedTask; }
}
sealed class FakeHttp : HttpMessageHandler
{
    public Queue<Func<HttpRequestMessage, HttpResponseMessage>> Queue { get; } = new();
    public List<string> Requests { get; } = [];
    public List<string> Keys { get; } = [];
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request.RequestUri!.ToString()); Keys.Add(request.Headers.GetValues("apikey").Single());
        if (Queue.Count == 0) throw new Exception("Solicitud HTTP inesperada: " + request.RequestUri);
        return Task.FromResult(Queue.Dequeue()(request));
    }
}

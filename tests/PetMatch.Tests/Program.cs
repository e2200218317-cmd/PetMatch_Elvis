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

PetPost Pet() => new() { nombre = " Luna ", especie = "Perro", raza = "Mestizo", edad = "2 años", sexo = "Hembra",
    tamano = "Mediano", salud = "Sana y desparasitada", ubicacion = "El Coca" };
var pet = Pet(); Validation.Pet(pet); Check(pet.nombre == "Luna", "mascota válida y nombre recortado");
Reject(() => Validation.Pet(new PetPost { especie = "Dragón" }), "especie fuera de la lista");
Reject(() => { var p = Pet(); p.salud = ""; Validation.Pet(p); }, "salud obligatoria");
Reject(() => { var p = Pet(); p.descripcion = new string('x', 501); Validation.Pet(p); }, "comentario de más de 500 caracteres");
byte[] png = [137, 80, 78, 71, 13, 10, 26, 10, 0];
Reject(() => Validation.PetPhotos([png, png]), "menos de 3 fotos");
Reject(() => Validation.PetPhotos([png, png, png, png, png, png]), "más de 5 fotos");
Reject(() => Validation.PetPhotos([png, png, [1, 2, 3]]), "foto de mascota que no es imagen");
var now = DateTimeOffset.UtcNow;
Check(new PetPost { disponible = true, vence_en = now.AddDays(1) }.Estado(now) == "Disponible", "estado disponible");
Check(new PetPost { disponible = true, vence_en = now.AddMinutes(-1) }.Estado(now) == "Vencida", "estado vencida antes de la tarea programada");
Check(new PetPost { disponible = false, vence_en = now.AddDays(1) }.Estado(now) == "Adoptada", "estado adoptada");

handler.Queue.Enqueue(_ => new(HttpStatusCode.OK) { Content = new StringContent(Session(DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds())) });
await api.LoginAsync("test@example.com", "Password1");
string? rpcBody = null;
for (int i = 0; i < 3; i++)
    handler.Queue.Enqueue(req =>
    {
        Check(req.RequestUri!.AbsolutePath.StartsWith("/storage/v1/object/pet-photos/11111111-1111-1111-1111-111111111111/"), "foto subida a la carpeta del usuario");
        return new(HttpStatusCode.OK) { Content = new StringContent("{}") };
    });
handler.Queue.Enqueue(req =>
{
    rpcBody = req.Content!.ReadAsStringAsync().Result;
    return new(HttpStatusCode.OK) { Content = new StringContent("\"22222222-2222-2222-2222-222222222222\"") };
});
var created = await api.PublishPetAsync(Pet(), [png, png, png]);
using (var body = JsonDocument.Parse(rpcBody!))
{
    var fotos = body.RootElement.GetProperty("p_fotos").EnumerateArray().Select(x => x.GetString()!).ToList();
    Check(created == Guid.Parse("22222222-2222-2222-2222-222222222222") && fotos.Count == 3, "publicación envía las 3 rutas y devuelve el id");
    Check(body.RootElement.GetProperty("p_portada_url").GetString() == "https://test.supabase.co/storage/v1/object/public/pet-photos/" + fotos[0], "portada es la URL pública de la primera foto");
}
for (int i = 0; i < 3; i++) handler.Queue.Enqueue(_ => new(HttpStatusCode.OK) { Content = new StringContent("{}") });
handler.Queue.Enqueue(_ => new(HttpStatusCode.BadRequest) { Content = new StringContent("{\"code\":\"P0001\",\"message\":\"PETMATCH: Ya tienes 5 publicaciones activas.\"}") });
string? cleanup = null;
handler.Queue.Enqueue(req => { cleanup = req.Method + " " + req.RequestUri!.AbsolutePath; return new(HttpStatusCode.OK) { Content = new StringContent("[]") }; });
try { await api.PublishPetAsync(Pet(), [png, png, png]); Check(false, "límite rechazado por el servidor"); }
catch (ApiException e) { Check(e.Message == "Ya tienes 5 publicaciones activas.", "mensaje del límite de 5 visible"); }
Check(cleanup == "DELETE /storage/v1/object/pet-photos", "fotos subidas se limpian si el servidor rechaza");
handler.Queue.Enqueue(_ => new(HttpStatusCode.OK) { Content = new StringContent("[\"11111111-1111-1111-1111-111111111111/a.png\"]") });
handler.Queue.Enqueue(req => { cleanup = req.Content!.ReadAsStringAsync().Result; return new(HttpStatusCode.OK) { Content = new StringContent("[]") }; });
await api.DeletePetAsync(created);
Check(cleanup!.Contains("11111111-1111-1111-1111-111111111111/a.png"), "eliminar publicación borra sus fotos de Storage");
handler.Queue.Enqueue(_ => new(HttpStatusCode.OK) { Content = new StringContent("""
    [{"id":"33333333-3333-3333-3333-333333333333","mascota_id":"22222222-2222-2222-2222-222222222222","mascota":"Luna","estado":"Pendiente","mensaje":null,"creado_en":"2026-11-18T10:00:00+00:00","solicitante_id":"44444444-4444-4444-4444-444444444444","nombres":"Beto","apellidos":"Prueba","ciudad":"Quito","celular":"+593991234567"}]
    """) });
var received = await api.ReceivedRequestsAsync();
Check(received.Single().estado == "Pendiente" && received[0].celular == "+593991234567", "solicitudes recibidas se leen");
handler.Queue.Enqueue(_ => new(HttpStatusCode.OK) { Content = new StringContent("""
    [{"id":"55555555-5555-5555-5555-555555555555","mensaje":"Tu solicitud para Luna fue aceptada.","leida":false,"creado_en":"2026-11-18T10:00:00+00:00"}]
    """) });
Check(!(await api.NotificationsAsync()).Single().leida, "notificaciones se leen");
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

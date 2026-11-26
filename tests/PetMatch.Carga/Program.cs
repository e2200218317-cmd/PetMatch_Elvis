using PetMatch.Core;
using System.Diagnostics;

// Prueba de carga del catálogo público: usuarios simultáneos consultando la primera página.
// Uso: dotnet run --project tests/PetMatch.Carga -- <url_supabase> <clave_publica> [usuarios] [rondas]
if (args.Length < 2) { Console.WriteLine("Uso: <url_supabase> <clave_publica> [usuarios=50] [rondas=3]"); return; }
int users = args.Length > 2 ? int.Parse(args[2]) : 50, rounds = args.Length > 3 ? int.Parse(args[3]) : 3;
using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
var api = new SupabaseApi(http, new NoSession(), args[0], args[1]);
await api.CatalogAsync(0, 13);

Console.WriteLine($"Catálogo · {users} usuarios simultáneos · {rounds} rondas\n");
Console.WriteLine("Ronda | Correctas | Errores | Promedio ms | p95 ms | Máximo ms | Duración ms");
Console.WriteLine("------|-----------|---------|-------------|--------|-----------|------------");
var all = new List<double>(); int failures = 0;
for (int round = 1; round <= rounds; round++)
{
    var clock = Stopwatch.StartNew();
    var results = await Task.WhenAll(Enumerable.Range(0, users).Select(async _ =>
    {
        var watch = Stopwatch.StartNew();
        try { await api.CatalogAsync(0, 13); return (ok: true, ms: watch.Elapsed.TotalMilliseconds); }
        catch (Exception e) when (e is ApiException or IOException or HttpRequestException or TaskCanceledException)
        { return (ok: false, ms: watch.Elapsed.TotalMilliseconds); }
    }));
    clock.Stop();
    var times = results.Where(r => r.ok).Select(r => r.ms).Order().ToList();
    int errors = results.Count(r => !r.ok);
    all.AddRange(times); failures += errors;
    Console.WriteLine($"{round,5} | {times.Count,9} | {errors,7} | {Avg(times),11:F0} | {P95(times),6:F0} | {Max(times),9:F0} | {clock.Elapsed.TotalMilliseconds,11:F0}");
}
all.Sort();
Console.WriteLine($"\nTotal: {all.Count + failures} solicitudes · {failures} errores · promedio {Avg(all):F0} ms · p95 {P95(all):F0} ms");

static double Avg(List<double> t) => t.Count == 0 ? 0 : t.Average();
static double Max(List<double> t) => t.Count == 0 ? 0 : t[^1];
static double P95(List<double> t) => t.Count == 0 ? 0 : t[Math.Min(t.Count - 1, (int)Math.Ceiling(t.Count * 0.95) - 1)];

sealed class NoSession : ISessionStore
{
    public Task<string?> ReadAsync() => Task.FromResult<string?>(null);
    public Task WriteAsync(string value) => Task.CompletedTask;
    public Task ClearAsync() => Task.CompletedTask;
}

using PetMatch.Core;
using PetMatch.Config;

namespace PetMatch.Services;

public static class AppServices
{
    public static SupabaseApi Api { get; } = new(new HttpClient { Timeout = TimeSpan.FromSeconds(30) },
        new MauiSessionStore(), SupabaseConfig.Url, SupabaseConfig.PublicKey);
    public static PlaceLookup Places { get; } = new(new HttpClient { Timeout = TimeSpan.FromSeconds(15) });
}

public sealed class MauiSessionStore : ISessionStore
{
    private const string Key = "petmatch.session.v1";
    public async Task<string?> ReadAsync()
    {
        try { return await SecureStorage.Default.GetAsync(Key); }
        catch { SecureStorage.Default.Remove(Key); return null; }
    }
    public Task WriteAsync(string value) => SecureStorage.Default.SetAsync(Key, value);
    public Task ClearAsync() { SecureStorage.Default.Remove(Key); return Task.CompletedTask; }
}

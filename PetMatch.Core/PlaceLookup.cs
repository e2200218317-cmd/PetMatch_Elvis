using System.Globalization;
using System.Text.Json;

namespace PetMatch.Core;

// Convierte coordenadas GPS en "Ciudad, sector" con el servicio público de OpenStreetMap (Nominatim).
public sealed class PlaceLookup(HttpClient http)
{
    private const string Endpoint = "https://nominatim.openstreetmap.org/reverse";

    public async Task<string> DescribeAsync(double latitude, double longitude)
    {
        var url = string.Create(CultureInfo.InvariantCulture,
            $"{Endpoint}?format=jsonv2&zoom=16&accept-language=es&lat={latitude:F6}&lon={longitude:F6}");
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("PetMatch/1.0 (aplicacion academica de adopcion)");
        using var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            throw new ApiException((int)response.StatusCode, "nominatim", "No se pudo obtener la dirección. Escribe la ubicación a mano.");
        return Format(await response.Content.ReadAsStringAsync());
    }

    public static string Format(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("address", out var address))
            throw new InvalidOperationException("No se encontró una dirección para tu ubicación. Escríbela a mano.");
        string? First(string? skip, params string[] keys) => keys
            .Select(k => address.TryGetProperty(k, out var v) ? v.GetString() : null)
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v) && !string.Equals(v, skip, StringComparison.OrdinalIgnoreCase));
        // En Ecuador la parroquia suele venir como town/village y el cantón como county.
        var city = First(null, "city") ?? Canton(First(null, "county")) ?? First(null, "town", "village", "municipality", "state");
        var sector = First(city, "neighbourhood", "suburb", "quarter", "hamlet", "village", "town", "city_district", "road");
        var text = string.Join(", ", new[] { city, sector }.Where(v => v != null));
        if (text.Length < 3)
            throw new InvalidOperationException("No se encontró una dirección para tu ubicación. Escríbela a mano.");
        return text.Length > 120 ? text[..120] : text;
    }

    private static string? Canton(string? county) => county == null ? null : county
        .Replace("Distrito Metropolitano de ", "").Replace("Cantón ", "").Replace("Canton ", "").Trim();
}

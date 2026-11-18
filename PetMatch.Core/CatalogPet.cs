using System.Text.Json.Serialization;

namespace PetMatch.Core;

public sealed record CatalogPet(
    Guid Id, string? Nombre, string Especie, string? Raza, string? Edad,
    string? Sexo, string Ubicacion, string? Descripcion,
    [property: JsonPropertyName("foto_url")] string? FotoUrl);

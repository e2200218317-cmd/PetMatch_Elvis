using System.Text.Json;

namespace PetMatch.Core;

// Publicaciones, solicitudes recibidas y notificaciones.
public sealed partial class SupabaseApi
{
    private const string PetBucket = "pet-photos";

    public string PetPhotoUrl(string path) => $"{root}/storage/v1/object/public/{PetBucket}/{path}";

    private static Dictionary<string, object?> PetArgs(PetPost p) => new()
    {
        ["p_nombre"] = p.nombre, ["p_especie"] = p.especie, ["p_raza"] = p.raza, ["p_edad"] = p.edad,
        ["p_sexo"] = p.sexo, ["p_tamano"] = p.tamano, ["p_color"] = p.color, ["p_salud"] = p.salud,
        ["p_vacunas"] = p.vacunas, ["p_ubicacion"] = p.ubicacion, ["p_descripcion"] = p.descripcion
    };

    public async Task<Guid> PublishPetAsync(PetPost p, IReadOnlyList<byte[]> photos)
    {
        Validation.Pet(p); Validation.PetPhotos(photos);
        var paths = new List<string>();
        try
        {
            foreach (var photo in photos)
            {
                string type = Validation.ImageType(photo);
                var path = $"{UserId}/{Guid.NewGuid():N}.{type}";
                await AuthorizedAsync(HttpMethod.Post, $"/storage/v1/object/{PetBucket}/{path}",
                    bytes: photo, contentType: type == "jpg" ? "image/jpeg" : "image/png");
                paths.Add(path);
            }
            var args = PetArgs(p);
            args["p_fotos"] = paths; args["p_portada_url"] = PetPhotoUrl(paths[0]);
            var json = await AuthorizedAsync(HttpMethod.Post, "/rest/v1/rpc/publicar_mascota", args);
            return JsonSerializer.Deserialize<Guid>(json, Json);
        }
        catch (ApiException) when (paths.Count > 0)
        {
            // El servidor respondió con un rechazo: las fotos subidas no quedaron en ninguna publicación.
            await RemovePetPhotosAsync(paths);
            throw;
        }
    }

    public async Task<IReadOnlyList<PetPost>> MyPetsAsync()
    {
        var json = await AuthorizedAsync(HttpMethod.Post, "/rest/v1/rpc/mis_mascotas", new { });
        return JsonSerializer.Deserialize<List<PetPost>>(json, Json) ?? [];
    }

    public async Task UpdatePetAsync(PetPost p)
    {
        Validation.Pet(p);
        var args = PetArgs(p);
        args["p_mascota"] = p.id;
        await AuthorizedAsync(HttpMethod.Post, "/rest/v1/rpc/editar_mascota", args);
    }

    public Task RenewPetAsync(Guid id) =>
        AuthorizedAsync(HttpMethod.Post, "/rest/v1/rpc/renovar_mascota", new { p_mascota = id });

    public async Task DeletePetAsync(Guid id)
    {
        var json = await AuthorizedAsync(HttpMethod.Post, "/rest/v1/rpc/eliminar_mascota", new { p_mascota = id });
        await RemovePetPhotosAsync(JsonSerializer.Deserialize<List<string>>(json, Json) ?? []);
    }

    private async Task RemovePetPhotosAsync(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0) return;
        try { await AuthorizedAsync(HttpMethod.Delete, $"/storage/v1/object/{PetBucket}", new { prefixes = paths }); }
        catch (Exception e) when (e is ApiException or IOException) { /* La eliminación de cuenta limpia la carpeta completa. */ }
    }

    public async Task<IReadOnlyList<ReceivedRequest>> ReceivedRequestsAsync()
    {
        var json = await AuthorizedAsync(HttpMethod.Post, "/rest/v1/rpc/solicitudes_recibidas", new { });
        return JsonSerializer.Deserialize<List<ReceivedRequest>>(json, Json) ?? [];
    }

    public Task RespondRequestAsync(Guid id, bool accept) =>
        AuthorizedAsync(HttpMethod.Post, "/rest/v1/rpc/responder_solicitud", new { p_solicitud = id, p_aceptar = accept });

    public async Task<IReadOnlyList<AppNotification>> NotificationsAsync()
    {
        var json = await AuthorizedAsync(HttpMethod.Get,
            "/rest/v1/notificaciones?select=id,mensaje,leida,creado_en&order=creado_en.desc&limit=50");
        return JsonSerializer.Deserialize<List<AppNotification>>(json, Json) ?? [];
    }

    public Task MarkNotificationsReadAsync() =>
        AuthorizedAsync(HttpMethod.Post, "/rest/v1/rpc/marcar_notificaciones_leidas", new { });
}

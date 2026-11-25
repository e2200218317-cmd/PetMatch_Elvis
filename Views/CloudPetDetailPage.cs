using Microsoft.Maui.ApplicationModel;
using PetMatch.Core;
using PetMatch.Services;

namespace PetMatch.Views;

/// <summary>Detalle público de una publicación compartida y acciones de contacto e interés.</summary>
public sealed class CloudPetDetailPage : FormPage
{
    private readonly Guid petId;
    private readonly HorizontalStackLayout photos = new() { Spacing = 10 };
    private readonly VerticalStackLayout details = new() { Spacing = 8 };
    private readonly Editor message = new()
    {
        Placeholder = "Mensaje para la persona que publicó (opcional)",
        MaxLength = 500,
        AutoSize = EditorAutoSizeOption.TextChanges,
        HeightRequest = 100,
        TextColor = Ui.Ink,
        PlaceholderColor = Ui.Muted
    };
    private PetDetails? pet;
    private bool loaded;

    public CloudPetDetailPage(Guid id)
        : base("Detalle", "Conoce a esta mascota", "Información compartida por su responsable.")
    {
        petId = id;
        Body.Add(new ScrollView { Orientation = ScrollOrientation.Horizontal, Content = photos });
        Body.Add(details);
        Body.Add(Ui.Field("Tu mensaje", message));
        Body.Add(ActionButton("Enviar solicitud de adopción", SendRequestAsync));
        Body.Add(ActionButton("Ver contacto del responsable", ContactOwnerAsync, true));
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!loaded) await Run(LoadAsync);
    }

    private async Task LoadAsync()
    {
        pet = await AppServices.Api.PetDetailsAsync(petId)
            ?? throw new InvalidOperationException("La publicación ya no existe o no está disponible.");

        Title = pet.Titulo;
        details.Clear();
        details.Add(Ui.Text(pet.Titulo, 25, true));
        details.Add(Ui.Text(string.Join(" · ", new[] { pet.especie, pet.raza, pet.edad, pet.sexo, pet.tamano }
            .Where(value => !string.IsNullOrWhiteSpace(value))), color: Ui.Muted));
        details.Add(Ui.Text("Ubicación", 17, true));
        details.Add(Ui.Text(pet.ubicacion));
        details.Add(Ui.Text("Salud", 17, true));
        details.Add(Ui.Text(pet.salud));
        if (!string.IsNullOrWhiteSpace(pet.color)) details.Add(Ui.Text("Color: " + pet.color));
        if (!string.IsNullOrWhiteSpace(pet.vacunas)) details.Add(Ui.Text("Vacunas: " + pet.vacunas));
        if (!string.IsNullOrWhiteSpace(pet.descripcion))
        {
            details.Add(Ui.Text("Descripción", 17, true));
            details.Add(Ui.Text(pet.descripcion));
        }

        photos.Clear();
        IEnumerable<string> paths = pet.fotos.Length > 0 ? pet.fotos :
            string.IsNullOrWhiteSpace(pet.foto_url) ? Array.Empty<string>() : [pet.foto_url!];
        foreach (var path in paths)
        {
            var url = path.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                ? path
                : AppServices.Api.PetPhotoUrl(path);
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
                photos.Add(new Image { Source = ImageSource.FromUri(uri), HeightRequest = 240, WidthRequest = 300, Aspect = Aspect.AspectFill });
        }
        loaded = true;
    }

    private async Task EnsureAccountAsync()
    {
        if (AppServices.Api.Session == null)
        {
            await AppFlow.RequireAsync(this, () => new CloudPetDetailPage(petId));
            throw new OperationCanceledException();
        }

        if (await AppServices.Api.MyProfileAsync() == null)
        {
            await AppFlow.RequireAsync(this, () => new CloudPetDetailPage(petId));
            throw new OperationCanceledException();
        }
    }

    private async Task SendRequestAsync()
    {
        if (pet == null) return;
        if (AppServices.Api.UserId == pet.propietario_id)
            throw new InvalidOperationException("No puedes enviar una solicitud para tu propia publicación.");

        try
        {
            await EnsureAccountAsync();
            await AppServices.Api.SendAdoptionRequestAsync(petId, message.Text);
            message.Text = "";
            await DisplayAlertAsync("Solicitud enviada", "La persona responsable recibirá una notificación.", "Aceptar");
        }
        catch (OperationCanceledException) { }
    }

    private async Task ContactOwnerAsync()
    {
        if (pet?.propietario_id == null || !Guid.TryParse(pet.propietario_id, out var ownerId))
            throw new InvalidOperationException("No se pudo identificar al responsable de la publicación.");

        try
        {
            await EnsureAccountAsync();
            var owner = await AppServices.Api.PetOwnerAsync(ownerId);
            if (owner == null) throw new InvalidOperationException("El perfil del responsable ya no está disponible.");
            var phone = owner.celular.TrimStart('+');
            await Launcher.Default.OpenAsync(new Uri($"https://wa.me/{phone}"));
        }
        catch (OperationCanceledException) { }
    }
}

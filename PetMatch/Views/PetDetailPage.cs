using PetMatch.Core;
using PetMatch.Services;

namespace PetMatch.Views;

public sealed class PetDetailPage : FormPage
{
    private readonly Guid petId;
    private readonly HorizontalStackLayout photos = new() { Spacing = 10 };
    private readonly VerticalStackLayout details = new() { Spacing = 8 };
    private readonly VerticalStackLayout actions = new() { Spacing = 14, IsVisible = false };
    private readonly Editor message = new()
    {
        Placeholder = "Cuéntale al responsable por qué quieres adoptar (opcional)", MaxLength = 500,
        AutoSize = EditorAutoSizeOption.TextChanges, HeightRequest = 100, TextColor = Ui.Ink, PlaceholderColor = Ui.Muted
    };
    private PetDetails? pet;
    private bool loaded;

    public PetDetailPage(Guid id) : base("Detalle", "Conoce a esta mascota", "Información compartida por su responsable.")
    {
        petId = id;
        Body.Add(new ScrollView { Orientation = ScrollOrientation.Horizontal, Content = photos });
        Body.Add(details);
        actions.Add(Ui.Field("Tu mensaje", message));
        actions.Add(ActionButton("Enviar solicitud de adopción", SendRequestAsync));
        actions.Add(ActionButton("Escribir por WhatsApp", ContactOwnerAsync, true));
        Body.Add(actions);
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
        IEnumerable<string> urls = pet.fotos.Length > 0 ? pet.fotos.Select(AppServices.Api.PetPhotoUrl)
            : string.IsNullOrWhiteSpace(pet.foto_url) ? [] : [pet.foto_url];
        foreach (var url in urls)
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
                photos.Add(new Image { Source = ImageSource.FromUri(uri), HeightRequest = 240, WidthRequest = 300, Aspect = Aspect.AspectFill });

        actions.IsVisible = pet.propietario_id != AppServices.Api.UserId;
        loaded = true;
    }

    // Sin sesión o sin perfil, lleva al usuario a completarlo y luego vuelve a este detalle.
    private async Task<bool> AccountReadyAsync()
    {
        if (AppServices.Api.Session != null && await AppServices.Api.MyProfileAsync() != null) return true;
        await AppFlow.RequireAsync(this, () => new PetDetailPage(petId));
        return false;
    }

    private async Task SendRequestAsync()
    {
        if (pet == null || !await AccountReadyAsync()) return;
        await AppServices.Api.SendAdoptionRequestAsync(petId, message.Text);
        message.Text = "";
        await DisplayAlertAsync("Solicitud enviada", "La persona responsable recibirá una notificación.", "Aceptar");
    }

    private async Task ContactOwnerAsync()
    {
        if (!Guid.TryParse(pet?.propietario_id, out var ownerId) || !await AccountReadyAsync()) return;
        var owner = await AppServices.Api.PetOwnerAsync(ownerId)
            ?? throw new InvalidOperationException("El perfil del responsable ya no está disponible.");
        await Launcher.Default.OpenAsync(new Uri("https://wa.me/" + owner.celular.TrimStart('+')));
    }
}

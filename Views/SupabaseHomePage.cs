using PetMatch.Core;
using PetMatch.Services;

namespace PetMatch.Views;

/// <summary>Vista previa pública del catálogo compartido.</summary>
public sealed class SupabaseHomePage : FormPage
{
    private const int PageSize = 12;
    private readonly VerticalStackLayout catalog = new() { Spacing = 14 };
    private readonly Button loadMore;
    private int offset;
    private bool loaded;

    public SupabaseHomePage() : base("Catálogo", "Mascotas en adopción", "Explora las publicaciones. Para solicitar una adopción o contactar al responsable, inicia sesión o crea una cuenta.")
    {
        Body.Add(catalog);
        loadMore = ActionButton("Cargar catálogo", LoadPageAsync, true);
        Body.Add(loadMore);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!loaded && AppServices.Api.IsConfigured)
            await Run(LoadPageAsync);
    }

    private async Task LoadPageAsync()
    {
        var pets = await AppServices.Api.CatalogAsync(offset, PageSize + 1);
        foreach (var pet in pets.Take(PageSize))
        {
            var card = new VerticalStackLayout { Spacing = 8 };
            if (Uri.TryCreate(pet.FotoUrl, UriKind.Absolute, out var photo) && photo.Scheme == Uri.UriSchemeHttps)
                card.Add(new Image { Source = ImageSource.FromUri(photo), HeightRequest = 210, Aspect = Aspect.AspectFit });
            card.Add(Ui.Text(string.IsNullOrWhiteSpace(pet.Nombre) ? pet.Especie : pet.Nombre, 21, true));
            card.Add(Ui.Text(pet.Ubicacion, color: Ui.Muted));
            card.Add(Ui.Text(string.Join(" · ", new[] { pet.Especie, pet.Raza, pet.Edad, pet.Sexo }.Where(value => !string.IsNullOrWhiteSpace(value)))));
            if (!string.IsNullOrWhiteSpace(pet.Descripcion)) card.Add(Ui.Text(pet.Descripcion));
            var details = Ui.Button("Ver detalles", secondary: true);
            details.Clicked += async (_, _) => await Navigation.PushAsync(new CloudPetDetailPage(pet.Id));
            card.Add(details);
            catalog.Add(Ui.Card(card));
        }
        if (offset == 0 && pets.Count == 0)
            catalog.Add(Ui.Card(Ui.Text("No hay mascotas disponibles.", color: Ui.Muted), true));
        offset += Math.Min(pets.Count, PageSize);
        loaded = true;
        loadMore.Text = "Ver más mascotas";
        loadMore.IsVisible = pets.Count > PageSize;
    }
}

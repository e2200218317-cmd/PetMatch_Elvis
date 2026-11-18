using PetMatch.Core;
using PetMatch.Services;

namespace PetMatch.Views;

public sealed class HomePage : FormPage
{
    private const int PageSize = 12;
    private readonly VerticalStackLayout catalog = new() { Spacing = 14 };
    private readonly Button loadMore;
    private int offset;
    private bool loaded;

    public HomePage() : base("Inicio", "Encuentra un nuevo compañero", "Historias que empiezan con una adopción. Nunca con una venta.")
    {
        Body.Add(ActionButton(AppServices.Api.Session == null ? "Iniciar sesión" : "Ir a mi perfil",
            () => AppFlow.RequireAsync(this, () => new ProfilePage())));
        if (AppServices.Api.Session == null)
            Body.Add(ActionButton("Crear una cuenta", () => Navigation.PushAsync(new RegisterPage()), true));

        Body.Add(Ui.Text("Mascotas en adopción", 22, true));
        Body.Add(catalog);
        loadMore = ActionButton("Reintentar", LoadPageAsync, true);
        Body.Add(loadMore);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!loaded)
            await Run(LoadPageAsync);
    }

    private async Task LoadPageAsync()
    {
        var pets = await AppServices.Api.CatalogAsync(offset, PageSize + 1);
        foreach (var pet in pets.Take(PageSize))
            catalog.Add(PetCard(pet));

        if (offset == 0 && pets.Count == 0)
            catalog.Add(Ui.Card(Ui.Text("No hay mascotas disponibles para adopción por el momento.", color: Ui.Muted), true));

        offset += Math.Min(pets.Count, PageSize);
        loaded = true;
        loadMore.Text = "Ver más mascotas";
        loadMore.IsVisible = pets.Count > PageSize;
    }

    private View PetCard(CatalogPet pet)
    {
        var name = string.IsNullOrWhiteSpace(pet.Nombre) ? pet.Especie : pet.Nombre;
        var card = new VerticalStackLayout { Spacing = 8 };
        if (Uri.TryCreate(pet.FotoUrl, UriKind.Absolute, out var photo) && photo.Scheme == Uri.UriSchemeHttps)
            card.Add(new Image { Source = ImageSource.FromUri(photo), HeightRequest = 210, Aspect = Aspect.AspectFit });

        card.Add(Ui.Text(name, 21, true));
        card.Add(Ui.Text(pet.Ubicacion, color: Ui.Muted));
        var facts = string.Join(" · ", new[] { pet.Especie, pet.Raza, pet.Edad, pet.Sexo }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
        card.Add(Ui.Text(facts));
        if (!string.IsNullOrWhiteSpace(pet.Descripcion))
            card.Add(Ui.Text(pet.Descripcion));

        return Ui.Card(card);
    }
}

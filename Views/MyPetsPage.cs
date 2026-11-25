using PetMatch.Core;
using PetMatch.Services;

namespace PetMatch.Views;

public sealed class MyPetsPage : FormPage
{
    private readonly VerticalStackLayout list = new() { Spacing = 14 };

    public MyPetsPage() : base("Mis publicaciones", "Mis mascotas publicadas", "Hasta 5 publicaciones activas. Cada una dura 10 días.")
    {
        Body.Add(ActionButton("Publicar una mascota", () => Navigation.PushAsync(new PetFormPage())));
        Body.Add(ActionButton("Solicitudes recibidas", () => Navigation.PushAsync(new ReceivedRequestsPage()), true));
        Body.Add(list);
    }

    protected override async void OnAppearing() { base.OnAppearing(); await Run(Load); }

    private async Task Load()
    {
        var pets = await AppServices.Api.MyPetsAsync();
        list.Clear();
        if (pets.Count == 0) list.Add(Ui.Card(Ui.Text("Todavía no has publicado mascotas.", color: Ui.Muted), true));
        foreach (var pet in pets) list.Add(PetCard(pet));
    }

    private View PetCard(PetPost pet)
    {
        var state = pet.Estado(DateTimeOffset.UtcNow);
        var card = new VerticalStackLayout { Spacing = 8 };
        if (Uri.TryCreate(pet.foto_url, UriKind.Absolute, out var photo) && photo.Scheme == Uri.UriSchemeHttps)
            card.Add(new Image { Source = ImageSource.FromUri(photo), HeightRequest = 180, Aspect = Aspect.AspectFit });
        card.Add(Ui.Text(pet.Titulo, 21, true));
        card.Add(Ui.Text(state == "Disponible" ? $"Disponible · vence el {pet.vence_en.ToLocalTime():dd/MM/yyyy}" : state,
            bold: true, color: state == "Disponible" ? Color.FromArgb("#0284C7") : Ui.Muted));
        card.Add(Ui.Text(string.Join(" · ", new[] { pet.especie, pet.raza, pet.edad, pet.sexo, pet.tamano })));
        card.Add(Ui.Text(pet.ubicacion, color: Ui.Muted));
        card.Add(ActionButton("Editar", () => Navigation.PushAsync(new PetFormPage(pet)), true));
        if (state == "Vencida")
            card.Add(ActionButton("Renovar por 10 días", async () => { await AppServices.Api.RenewPetAsync(pet.id); await Load(); }));
        card.Add(ActionButton("Eliminar publicación", async () =>
        {
            if (!await DisplayAlertAsync("Eliminar publicación", $"Se borrarán {pet.Titulo}, sus fotos y sus solicitudes.", "Eliminar", "Cancelar")) return;
            await AppServices.Api.DeletePetAsync(pet.id);
            await Load();
        }, danger: true));
        return Ui.Card(card);
    }
}

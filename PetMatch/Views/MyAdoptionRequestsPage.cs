using PetMatch.Services;

namespace PetMatch.Views;

public sealed class MyAdoptionRequestsPage : FormPage
{
    private readonly VerticalStackLayout list = new() { Spacing = 12 };

    public MyAdoptionRequestsPage() : base("Mis solicitudes", "Mascotas de interés", "Revisa las solicitudes de adopción que has enviado.")
    {
        Body.Add(list);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await Run(LoadAsync);
    }

    private async Task LoadAsync()
    {
        var requests = await AppServices.Api.MyAdoptionRequestsAsync();
        list.Clear();
        if (requests.Count == 0)
        {
            list.Add(Ui.Card(Ui.Text("Todavía no has enviado solicitudes.", color: Ui.Muted), true));
            return;
        }
        foreach (var r in requests)
        {
            var card = new VerticalStackLayout { Spacing = 8 };
            card.Add(Ui.Text(r.mascota, 20, true));
            card.Add(Ui.Text(r.estado, bold: true, color: r.estado switch
            {
                "Aceptada" => Color.FromArgb("#0284C7"),
                "Rechazada" => Color.FromArgb("#B91C1C"),
                _ => Ui.Muted
            }));
            card.Add(Ui.Text("Enviada el " + r.creado_en.ToLocalTime().ToString("dd/MM/yyyy HH:mm"), 13, color: Ui.Muted));
            if (!string.IsNullOrWhiteSpace(r.mensaje)) card.Add(Ui.Text(r.mensaje));
            if (r.estado == "Pendiente")
                card.Add(ActionButton("Ver mascota", () => Navigation.PushAsync(new PetDetailPage(r.mascota_id)), true));
            list.Add(Ui.Card(card, r.estado == "Pendiente"));
        }
    }
}

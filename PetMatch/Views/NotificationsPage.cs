using PetMatch.Core;
using PetMatch.Services;

namespace PetMatch.Views;

public sealed class NotificationsPage : FormPage
{
    private readonly VerticalStackLayout list = new() { Spacing = 12 };

    public NotificationsPage() : base("Notificaciones", "Notificaciones", "Avisos sobre tus publicaciones y solicitudes.")
    {
        Body.Add(list);
    }

    protected override async void OnAppearing() { base.OnAppearing(); await Run(Load); }

    private async Task Load()
    {
        var items = await AppServices.Api.NotificationsAsync();
        list.Clear();
        if (items.Count == 0) list.Add(Ui.Card(Ui.Text("No tienes notificaciones.", color: Ui.Muted), true));
        foreach (var n in items)
            list.Add(Ui.Card(new VerticalStackLayout { Spacing = 6, Children =
            {
                Ui.Text(n.mensaje, bold: !n.leida),
                Ui.Text(n.creado_en.ToLocalTime().ToString("dd/MM/yyyy HH:mm"), 13, color: Ui.Muted)
            } }, !n.leida));
        if (items.Any(n => !n.leida)) await AppServices.Api.MarkNotificationsReadAsync();
    }
}

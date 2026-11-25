using Microsoft.Maui.ApplicationModel;
using PetMatch.Core;
using PetMatch.Services;

namespace PetMatch.Views;

public sealed class ReceivedRequestsPage : FormPage
{
    private readonly VerticalStackLayout list = new() { Spacing = 14 };

    public ReceivedRequestsPage() : base("Solicitudes", "Solicitudes recibidas", "Personas interesadas en adoptar tus mascotas.")
    {
        Body.Add(list);
    }

    protected override async void OnAppearing() { base.OnAppearing(); await Run(Load); }

    private async Task Load()
    {
        var requests = await AppServices.Api.ReceivedRequestsAsync();
        list.Clear();
        if (requests.Count == 0) list.Add(Ui.Card(Ui.Text("Aún no recibes solicitudes.", color: Ui.Muted), true));
        foreach (var request in requests) list.Add(RequestCard(request));
    }

    private View RequestCard(ReceivedRequest r)
    {
        var card = new VerticalStackLayout { Spacing = 8 };
        card.Add(Ui.Text(r.mascota, 21, true));
        card.Add(Ui.Text(r.estado, bold: true, color: r.estado switch
        {
            "Aceptada" => Color.FromArgb("#0284C7"), "Rechazada" => Color.FromArgb("#B91C1C"), _ => Ui.Muted
        }));
        card.Add(Ui.Text($"{r.nombres} {r.apellidos} · {r.ciudad}"));
        card.Add(Ui.Text("Celular: " + r.celular));
        card.Add(Ui.Text("Enviada el " + r.creado_en.ToLocalTime().ToString("dd/MM/yyyy HH:mm"), 13, color: Ui.Muted));
        if (!string.IsNullOrWhiteSpace(r.mensaje)) card.Add(Ui.Text("“" + r.mensaje + "”"));
        card.Add(ActionButton("Escribir por WhatsApp", () => Launcher.Default.OpenAsync(new Uri("https://wa.me/" + r.celular.TrimStart('+'))), true));
        if (r.estado == "Pendiente")
        {
            card.Add(ActionButton("Aceptar", async () =>
            {
                if (!await DisplayAlertAsync("Aceptar solicitud", $"{r.mascota} saldrá del catálogo y las demás solicitudes pendientes se rechazarán.", "Aceptar", "Cancelar")) return;
                await AppServices.Api.RespondRequestAsync(r.id, true);
                await Load();
            }));
            card.Add(ActionButton("Rechazar", async () =>
            {
                if (!await DisplayAlertAsync("Rechazar solicitud", $"¿Rechazar la solicitud de {r.nombres}?", "Rechazar", "Cancelar")) return;
                await AppServices.Api.RespondRequestAsync(r.id, false);
                await Load();
            }, danger: true));
        }
        return Ui.Card(card, r.estado == "Pendiente");
    }
}

using PetMatch.Core;
using PetMatch.Services;

namespace PetMatch.Views;

public sealed class ProfilePage : FormPage
{
    private readonly VerticalStackLayout details = new() { Spacing = 14 };
    public ProfilePage() : base("Mi perfil", "Mi espacio", "Tu cuenta y tus próximos encuentros.") { Body.Add(details); }
    protected override async void OnAppearing() { base.OnAppearing(); await Run(Load); }
    private async Task Load()
    {
        await AppServices.Api.ValidateSessionAsync();
        var p = await AppServices.Api.MyProfileAsync();
        if (p == null) { AppFlow.Reset(new ProfileFormPage(null)); return; }
        details.Clear();
        if (p.eliminando)
        {
            details.Add(Ui.Text("La eliminación de tu cuenta está pendiente. Vuelve a intentarlo para terminar la limpieza de tus datos.", color: Ui.Muted));
            details.Add(ActionButton("Completar eliminación", () => Navigation.PushAsync(new DeleteAccountPage()), danger: true));
        }
        else
        {
            var avatar = new Image { Source = "paw.png", HeightRequest = 130, Aspect = Aspect.AspectFit };
            details.Add(Ui.Card(new VerticalStackLayout { Spacing = 12, Children =
            {
                avatar, Ui.Text($"{p.nombres} {p.apellidos}", 23, true), Ui.Text(p.email, color: Ui.Muted),
                Ui.Text($"{Validation.Age(p.fecha_nacimiento, DateTime.Today)} años · {p.ciudad}"),
                Ui.Text("Dirección: " + p.direccion), Ui.Text("Celular: " + p.celular),
                Ui.Text("Cédula: " + p.cedula), Ui.Text("Fecha de nacimiento: " + p.fecha_nacimiento.ToString("dd/MM/yyyy"))
            } }, true));
            details.Add(ActionButton("Editar mi perfil", () => Navigation.PushAsync(new ProfileFormPage(p)), true));
            details.Add(ActionButton("Publicar una mascota", () => AppFlow.RequireAsync(this, () => new PetFormPage())));
            details.Add(ActionButton("Mis mascotas publicadas", () => AppFlow.RequireAsync(this, () => new MyPetsPage()), true));
            details.Add(ActionButton("Solicitudes recibidas", () => AppFlow.RequireAsync(this, () => new ReceivedRequestsPage()), true));
            details.Add(ActionButton("Mascotas de interés", () => AppFlow.RequireAsync(this, () => new IntegrationPage("Mascotas de interés", "David")), true));
            details.Add(ActionButton("Notificaciones", () => AppFlow.RequireAsync(this, () => new NotificationsPage()), true));
            details.Add(ActionButton("Eliminar mi cuenta", () => Navigation.PushAsync(new DeleteAccountPage()), danger: true));
            try
            {
                var signed = await AppServices.Api.AvatarUrlAsync(p.avatar_path);
                if (signed != null) avatar.Source = new UriImageSource { Uri = new Uri(signed), CachingEnabled = false };
            }
            catch (Exception e) when (e is ApiException or IOException) { Status.Text = "No se pudo cargar tu foto. Puedes volver a abrir el perfil para reintentar."; }
        }
        details.Add(ActionButton("Inicio", () => { AppFlow.Home(); return Task.CompletedTask; }, true));
        details.Add(ActionButton("Cerrar sesión", async () =>
        {
            bool remote = await AppServices.Api.LogoutAsync();
            if (!remote) await DisplayAlertAsync("Sesión cerrada en este dispositivo", "No se pudo confirmar el cierre con el servidor por un problema de conexión.", "Aceptar");
            AppFlow.Home();
        }, true));
    }
}

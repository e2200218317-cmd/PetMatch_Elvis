using PetMatch.Services;

namespace PetMatch.Views;

public sealed class HomePage : FormPage
{
    private static bool Visitor => AppServices.Api.Session == null;

    public HomePage() : base("PetMatch", Visitor ? "Un hogar puede cambiar una vida" : "¡Qué bueno verte!",
        Visitor ? "Encuentra mascotas que buscan una familia o publica para ayudarles a encontrarla."
                : "Explora el catálogo o administra tu cuenta y tus publicaciones.")
    {
        BackgroundColor = Color.FromArgb("#F4F8F8");
        Body.Padding = new Thickness(26, 48, 26, 32);
        foreach (var label in Body.Children.OfType<Label>()) label.HorizontalTextAlignment = TextAlignment.Center;
        if (Visitor) AddWelcome(); else AddMemberHome();
    }

    private void AddWelcome()
    {
        Body.Add(Ui.Card(new VerticalStackLayout { Spacing = 10, Children =
        {
            ActionButton("Crear cuenta", () => Navigation.PushAsync(new RegisterPage(() => new HomePage()))),
            ActionButton("Iniciar sesión", () => Navigation.PushAsync(new LoginPage(() => new HomePage())), true)
        } }, true));
        Body.Add(ActionButton("Explorar catálogo como visitante", () => Navigation.PushAsync(new CatalogPage()), true));
        var note = Ui.Text("Puedes ver las publicaciones sin cuenta. Para enviar una solicitud o contactar a alguien, inicia sesión o regístrate.", 13, color: Ui.Muted);
        note.HorizontalTextAlignment = TextAlignment.Center;
        Body.Add(note);
    }

    private void AddMemberHome()
    {
        Body.Add(ActionButton("Explorar catálogo", () => Navigation.PushAsync(new CatalogPage())));
        Body.Add(ActionButton("Mi perfil y publicaciones", () => AppFlow.RequireAsync(this, () => new ProfilePage()), true));
        Body.Add(ActionButton("Mis solicitudes", () => AppFlow.RequireAsync(this, () => new MyAdoptionRequestsPage()), true));
        Body.Add(ActionButton("Notificaciones", () => AppFlow.RequireAsync(this, () => new NotificationsPage()), true));
        Body.Add(ActionButton("Cerrar sesión", async () =>
        {
            if (!await AppServices.Api.LogoutAsync())
                await DisplayAlertAsync("Sesión cerrada en este dispositivo", "No se pudo confirmar el cierre con el servidor por un problema de conexión.", "Aceptar");
            AppFlow.Home();
        }, true));
    }
}

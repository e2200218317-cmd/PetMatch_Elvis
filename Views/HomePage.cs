using PetMatch.Services;

namespace PetMatch.Views;

/// <summary>Bienvenida pública y panel principal para una sesión activa.</summary>
public sealed class HomePage : ContentPage
{
    private static readonly Color Ink = Color.FromArgb("#172B3A");
    private static readonly Color Muted = Color.FromArgb("#617386");
    private static readonly Color Accent = Color.FromArgb("#138A8A");

    public HomePage()
    {
        Title = "PetMatch";
        BackgroundColor = Color.FromArgb("#F4F8F8");
        Content = AppServices.Api.Session == null ? CreateWelcome() : CreateMemberHome();
    }

    private View CreateWelcome()
    {
        var content = new VerticalStackLayout
        {
            Spacing = 18,
            Padding = new Thickness(26, 36),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            MaximumWidthRequest = 520
        };

        content.Add(new Label
        {
            Text = "🐾  PETMATCH",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Accent,
            HorizontalTextAlignment = TextAlignment.Center
        });
        content.Add(new Label
        {
            Text = "Un hogar puede\ncambiar una vida",
            FontSize = 34,
            FontAttributes = FontAttributes.Bold,
            TextColor = Ink,
            HorizontalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.WordWrap
        });
        content.Add(new Label
        {
            Text = "Encuentra mascotas que buscan una familia o publica para ayudarles a encontrarla.",
            FontSize = 16,
            TextColor = Muted,
            HorizontalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.WordWrap
        });

        if (AppServices.Api.IsConfigured)
        {
            var actions = new VerticalStackLayout { Spacing = 10, Margin = new Thickness(0, 8, 0, 0) };
            var createAccount = Ui.Button("Crear cuenta");
            createAccount.Clicked += async (_, _) => await Navigation.PushAsync(new RegisterPage());
            var signIn = Ui.Button("Iniciar sesión", secondary: true);
            signIn.Clicked += async (_, _) => await Navigation.PushAsync(new LoginPage());
            actions.Add(createAccount);
            actions.Add(signIn);
            content.Add(Ui.Card(actions, true));

            var preview = Ui.Button("Explorar catálogo como visitante", secondary: true);
            preview.Clicked += async (_, _) => await Navigation.PushAsync(new SupabaseHomePage());
            content.Add(preview);
            content.Add(new Label
            {
                Text = "Puedes ver las publicaciones sin cuenta. Para enviar una solicitud o contactar a alguien, inicia sesión o regístrate.",
                FontSize = 13,
                TextColor = Muted,
                HorizontalTextAlignment = TextAlignment.Center,
                LineBreakMode = LineBreakMode.WordWrap
            });
        }
        else
        {
            content.Add(Ui.Card(new Label
            {
                Text = "El acceso en línea aparecerá cuando se configure la conexión compartida con Supabase.",
                FontSize = 14,
                TextColor = Muted,
                HorizontalTextAlignment = TextAlignment.Center
            }, true));
        }

        return new ScrollView
        {
            Content = new Grid { Children = { content } }
        };
    }

    private View CreateMemberHome()
    {
        var content = new VerticalStackLayout
        {
            Spacing = 14,
            Padding = new Thickness(26, 36),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            MaximumWidthRequest = 520
        };
        content.Add(new Label
        {
            Text = "🐾  PETMATCH",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Accent,
            HorizontalTextAlignment = TextAlignment.Center
        });
        content.Add(new Label
        {
            Text = "Inicio",
            FontSize = 30,
            FontAttributes = FontAttributes.Bold,
            TextColor = Ink,
            HorizontalTextAlignment = TextAlignment.Center
        });
        content.Add(new Label
        {
            Text = "Explora mascotas en adopción y administra tu cuenta desde el menú.",
            FontSize = 15,
            TextColor = Muted,
            HorizontalTextAlignment = TextAlignment.Center
        });

        var catalog = Ui.Button("Explorar catálogo");
        catalog.Clicked += async (_, _) => await Navigation.PushAsync(new SupabaseHomePage());
        content.Add(catalog);

        var menuItems = new VerticalStackLayout { Spacing = 9, IsVisible = false };
        var menuToggle = Ui.Button("☰  Mi cuenta  ▾", secondary: true);
        menuToggle.Clicked += (_, _) =>
        {
            menuItems.IsVisible = !menuItems.IsVisible;
            menuToggle.Text = menuItems.IsVisible ? "☰  Mi cuenta  ▴" : "☰  Mi cuenta  ▾";
        };
        var profile = Ui.Button("Mi perfil y publicaciones", secondary: true);
        profile.Clicked += async (_, _) => await AppFlow.RequireAsync(this, () => new ProfilePage());
        menuItems.Add(profile);
        var requests = Ui.Button("Mis solicitudes", secondary: true);
        requests.Clicked += async (_, _) => await AppFlow.RequireAsync(this, () => new MyAdoptionRequestsPage());
        menuItems.Add(requests);
        var notifications = Ui.Button("Notificaciones", secondary: true);
        notifications.Clicked += async (_, _) => await AppFlow.RequireAsync(this, () => new NotificationsPage());
        menuItems.Add(notifications);

        var signOut = Ui.Button("Cerrar sesión", secondary: true);
        signOut.Clicked += async (_, _) =>
        {
            await AppServices.Api.LogoutAsync();
            AppFlow.Home();
        };
        menuItems.Add(signOut);
        content.Add(menuToggle);
        content.Add(Ui.Card(menuItems, true));

        return new ScrollView
        {
            Content = new Grid { Children = { content } }
        };
    }
}

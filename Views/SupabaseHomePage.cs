using PetMatch.Core;
using PetMatch.Services;
using Microsoft.Maui.Dispatching;

namespace PetMatch.Views;

/// <summary>Vista previa pública del catálogo compartido.</summary>
public sealed class SupabaseHomePage : FormPage
{
    private const int PageSize = 12;
    private readonly VerticalStackLayout catalog = new() { Spacing = 14 };
    private readonly Button loadMore;
    private readonly Button filterToggleButton = Ui.Button("🔎 Buscar y filtrar", secondary: true);
    private readonly VerticalStackLayout filterPanel = new() { Spacing = 12 };
    private readonly Picker speciesFilter = new() { ItemsSource = new[] { "Todas", "Perro", "Gato", "Otro" }, SelectedIndex = 0 };
    private readonly Picker sexFilter = new() { ItemsSource = new[] { "Todos", "Macho", "Hembra" }, SelectedIndex = 0 };
    private readonly Entry breedFilter = Ui.Input("Ej. Mestizo");
    private readonly Entry locationFilter = Ui.Input("Ciudad o sector");
    private readonly Label filterSummary = Ui.Text("", 13, true, Ui.Muted);
    private Border? filterCard;
    private IReadOnlyList<CatalogPet>? filteredResults;
    private int filteredOffset;
    private readonly Button notificationButton = new()
    {
        Text = "🔔", FontSize = 19, WidthRequest = 48, HeightRequest = 48,
        CornerRadius = 24, Padding = 0, BackgroundColor = Color.FromArgb("#EFF4F8"), TextColor = Ui.Ink
    };
    private readonly Border notificationBadge = new()
    {
        IsVisible = false, WidthRequest = 21, HeightRequest = 21,
        HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Start,
        BackgroundColor = Color.FromArgb("#DC2626"), Stroke = Colors.White, StrokeThickness = 1.5,
        StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 11 }
    };
    private readonly Label notificationBadgeText = new()
    {
        TextColor = Colors.White, FontSize = 10, FontAttributes = FontAttributes.Bold,
        HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center
    };
    private readonly ImageButton accountAvatar = new()
    {
        Source = "paw.png", WidthRequest = 48, HeightRequest = 48, CornerRadius = 24,
        Padding = 2, BackgroundColor = Color.FromArgb("#E0F2FE"), BorderColor = Color.FromArgb("#7DD3FC"),
        BorderWidth = 1, Aspect = Aspect.AspectFill
    };
    private readonly VerticalStackLayout accountItems = new() { Spacing = 8 };
    private readonly VerticalStackLayout notificationItems = new() { Spacing = 8 };
    private readonly Label accountMenuLabel = Ui.Text("Mi cuenta  ▾", 15, true, Color.FromArgb("#075985"));
    private Border? accountMenuCard;
    private Border? notificationCard;
    private IReadOnlyList<AppNotification> currentNotifications = [];
    private IDispatcherTimer? notificationTimer;
    private bool refreshingNotifications;
    private int offset;
    private bool loaded;

    public SupabaseHomePage() : base("Catálogo", "Mascotas en adopción", "Explora las publicaciones. Para solicitar una adopción o contactar al responsable, inicia sesión o crea una cuenta.")
    {
        if (AppServices.Api.Session != null)
        {
            accountAvatar.Clicked += async (_, _) => await ToggleAccountMenuAsync();
            accountItems.Add(AccountAction("Ver perfil", () => AppFlow.RequireAsync(this, () => new ProfilePage())));
            accountItems.Add(AccountAction("Publicar una mascota", () => AppFlow.RequireAsync(this, () => new PetFormPage())));
            accountItems.Add(AccountAction("Mis mascotas", () => AppFlow.RequireAsync(this, () => new MyPetsPage())));
            accountItems.Add(AccountAction("Solicitudes recibidas", () => AppFlow.RequireAsync(this, () => new ReceivedRequestsPage())));
            accountItems.Add(AccountAction("Mascotas de interés", () => AppFlow.RequireAsync(this, () => new MyAdoptionRequestsPage())));
            accountItems.Add(AccountAction("Cerrar sesión", async () =>
            {
                bool remote = await AppServices.Api.LogoutAsync();
                if (!remote) await DisplayAlertAsync("Sesión", "No se pudo confirmar el cierre con el servidor. Se cerró la sesión en este dispositivo.", "Aceptar");
                AppFlow.Home();
            }));

            var accountHeader = new Grid
            {
                ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
                ColumnSpacing = 12
            };
            accountHeader.Add(Ui.Text("PETMATCH", 18, true, Color.FromArgb("#0284C7")));
            notificationBadge.Content = notificationBadgeText;
            var bellControl = new Grid { WidthRequest = 50, HeightRequest = 50 };
            bellControl.Add(notificationButton);
            bellControl.Add(notificationBadge);
            notificationButton.Clicked += async (_, _) => await ToggleNotificationsAsync();
            var accountControl = new HorizontalStackLayout
            {
                Spacing = 8, VerticalOptions = LayoutOptions.Center,
                Children = { accountMenuLabel, bellControl, accountAvatar }
            };
            accountMenuLabel.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(async () => await ToggleAccountMenuAsync()) });
            accountHeader.Add(accountControl, 1, 0);
            accountMenuCard = Ui.Card(accountItems, true);
            accountMenuCard.IsVisible = false;
            accountMenuCard.WidthRequest = 285;
            notificationItems.Add(Ui.Text("Notificaciones", 16, true));
            notificationItems.Add(AccountAction("Ver todas", () => AppFlow.RequireAsync(this, () => new NotificationsPage())));
            notificationCard = Ui.Card(notificationItems, true);
            notificationCard.IsVisible = false;
            notificationCard.WidthRequest = 320;
            var menuAnchor = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };
            menuAnchor.Add(accountMenuCard, 1, 0);
            menuAnchor.Add(notificationCard, 1, 0);
            Body.Children.RemoveAt(0); // El encabezado de cuenta reemplaza la marca superior del formulario.
            Body.Children.Insert(0, accountHeader);
            Body.Children.Insert(1, menuAnchor);
        }
        filterToggleButton.Clicked += (_, _) =>
        {
            if (filterCard != null) filterCard.IsVisible = !filterCard.IsVisible;
        };
        filterPanel.Add(Ui.Field("Especie", speciesFilter));
        filterPanel.Add(Ui.Field("Raza", breedFilter));
        filterPanel.Add(Ui.Field("Sexo", sexFilter));
        filterPanel.Add(Ui.Field("Ciudad o lugar", locationFilter));
        var filterActions = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
            ColumnSpacing = 10
        };
        var applyFilters = Ui.Button("Aplicar filtros");
        applyFilters.Clicked += async (_, _) => await Run(ApplyFiltersAsync);
        var clearFilters = Ui.Button("Limpiar", secondary: true);
        clearFilters.Clicked += async (_, _) => await Run(ClearFiltersAsync);
        filterActions.Add(applyFilters, 0, 0); filterActions.Add(clearFilters, 1, 0);
        filterPanel.Add(filterActions);
        filterCard = Ui.Card(filterPanel, true);
        filterCard.IsVisible = false;
        filterSummary.IsVisible = false;
        Body.Add(filterToggleButton);
        Body.Add(filterCard);
        Body.Add(filterSummary);
        Body.Add(catalog);
        loadMore = ActionButton("Cargar catálogo", LoadPageAsync, true);
        Body.Add(loadMore);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (AppServices.Api.Session != null)
        {
            await LoadAccountAvatarAsync();
            await RefreshNotificationsAsync();
            StartNotificationTimer();
        }
        if (!loaded && AppServices.Api.IsConfigured)
            await Run(LoadPageAsync);
    }

    protected override void OnDisappearing()
    {
        notificationTimer?.Stop();
        base.OnDisappearing();
    }

    private async Task LoadAccountAvatarAsync()
    {
        try
        {
            var profile = await AppServices.Api.MyProfileAsync();
            var url = profile == null ? null : await AppServices.Api.AvatarUrlAsync(profile.avatar_path);
            if (url != null) accountAvatar.Source = new UriImageSource { Uri = new Uri(url), CachingEnabled = false };
        }
        catch (Exception e) when (e is ApiException or IOException) { accountAvatar.Source = "paw.png"; }
    }

    private Task ToggleAccountMenuAsync()
    {
        if (accountMenuCard == null) return Task.CompletedTask;
        accountMenuCard.IsVisible = !accountMenuCard.IsVisible;
        if (notificationCard != null) notificationCard.IsVisible = false;
        accountMenuLabel.Text = accountMenuCard.IsVisible ? "Mi cuenta  ▴" : "Mi cuenta  ▾";
        return Task.CompletedTask;
    }

    private async Task ToggleNotificationsAsync()
    {
        if (notificationCard == null) return;
        await RefreshNotificationsAsync();
        if (accountMenuCard != null) accountMenuCard.IsVisible = false;
        accountMenuLabel.Text = "Mi cuenta  ▾";
        notificationCard.IsVisible = !notificationCard.IsVisible;
    }

    private void StartNotificationTimer()
    {
        if (notificationTimer == null)
        {
            notificationTimer = Dispatcher.CreateTimer();
            notificationTimer.Interval = TimeSpan.FromSeconds(30);
            notificationTimer.IsRepeating = true;
            notificationTimer.Tick += async (_, _) => await RefreshNotificationsAsync();
        }
        if (!notificationTimer.IsRunning) notificationTimer.Start();
    }

    private async Task RefreshNotificationsAsync()
    {
        if (refreshingNotifications || AppServices.Api.Session == null) return;
        refreshingNotifications = true;
        try
        {
            currentNotifications = await AppServices.Api.NotificationsAsync();
            int unread = currentNotifications.Count(item => !item.leida);
            notificationBadgeText.Text = unread > 99 ? "99+" : unread.ToString();
            notificationBadge.IsVisible = unread > 0;
            RenderNotifications();
        }
        catch (Exception e) when (e is ApiException or IOException or InvalidOperationException) { }
        finally { refreshingNotifications = false; }
    }

    private void RenderNotifications()
    {
        while (notificationItems.Children.Count > 2) notificationItems.Children.RemoveAt(2);
        if (currentNotifications.Count == 0)
        {
            notificationItems.Add(Ui.Text("Todavía no tienes notificaciones.", 13, color: Ui.Muted));
            return;
        }
        foreach (var item in currentNotifications.Take(5))
        {
            var content = new VerticalStackLayout { Spacing = 4, Children =
            {
                Ui.Text(item.mensaje, 13, bold: !item.leida),
                Ui.Text(item.creado_en.ToLocalTime().ToString("dd/MM HH:mm"), 11, color: Ui.Muted)
            } };
            var card = Ui.Card(content, !item.leida);
            card.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(async () => await OpenNotificationAsync(item))
            });
            notificationItems.Add(card);
        }
    }

    private async Task OpenNotificationAsync(AppNotification item)
    {
        try { await AppServices.Api.MarkNotificationsReadAsync(); }
        catch (Exception e) when (e is ApiException or IOException)
        {
            await DisplayAlertAsync("Notificaciones", "No se pudo actualizar el estado de lectura. Revisa tu conexión e inténtalo otra vez.", "Aceptar");
            return;
        }
        if (notificationCard != null) notificationCard.IsVisible = false;
        await RefreshNotificationsAsync();
        if (item.mensaje.StartsWith("Recibiste una nueva solicitud", StringComparison.OrdinalIgnoreCase))
            await AppFlow.RequireAsync(this, () => new ReceivedRequestsPage());
        else if (item.mensaje.StartsWith("La publicación", StringComparison.OrdinalIgnoreCase))
            await AppFlow.RequireAsync(this, () => new MyPetsPage());
        else
            await AppFlow.RequireAsync(this, () => new MyAdoptionRequestsPage());
    }

    private Button AccountAction(string text, Func<Task> action)
    {
        var button = Ui.Button(text, secondary: true);
        button.Clicked += async (_, _) =>
        {
            if (accountMenuCard != null) accountMenuCard.IsVisible = false;
            accountMenuLabel.Text = "Mi cuenta  ▾";
            if (notificationCard != null) notificationCard.IsVisible = false;
            await action();
        };
        return button;
    }

    private async Task LoadPageAsync()
    {
        if (filteredResults != null)
        {
            RenderFilteredPage();
            return;
        }
        var pets = await AppServices.Api.CatalogAsync(offset, PageSize + 1);
        foreach (var pet in pets.Take(PageSize))
            AddPetCard(pet);
        if (offset == 0 && pets.Count == 0)
            catalog.Add(Ui.Card(Ui.Text("No hay mascotas disponibles.", color: Ui.Muted), true));
        offset += Math.Min(pets.Count, PageSize);
        loaded = true;
        loadMore.Text = "Ver más mascotas";
        loadMore.IsVisible = pets.Count > PageSize;
    }

    private async Task ApplyFiltersAsync()
    {
        var filters = new CatalogFilters
        {
            Especie = speciesFilter.SelectedIndex > 0 ? speciesFilter.SelectedItem as string : null,
            Raza = string.IsNullOrWhiteSpace(breedFilter.Text) ? null : breedFilter.Text.Trim(),
            Sexo = sexFilter.SelectedIndex > 0 ? sexFilter.SelectedItem as string : null,
            Lugar = string.IsNullOrWhiteSpace(locationFilter.Text) ? null : locationFilter.Text.Trim()
        };
        int activeCount = new[] { filters.Especie, filters.Raza, filters.Sexo, filters.Lugar }.Count(value => !string.IsNullOrWhiteSpace(value));
        if (activeCount == 0) { await ClearFiltersAsync(); return; }

        filterCard!.IsVisible = false;
        filterToggleButton.Text = $"🔎 Filtros activos ({activeCount})";
        catalog.Clear(); offset = filteredOffset = 0; loaded = false; loadMore.IsVisible = false;
        filteredResults = null;
        filterSummary.Text = "Buscando mascotas…"; filterSummary.IsVisible = true;
        IReadOnlyList<CatalogPet> matches;
        try { matches = await AppServices.Api.SearchCatalogAsync(filters); }
        catch
        {
            filterSummary.Text = "No se pudieron cargar los resultados.";
            throw;
        }
        filteredResults = matches;
        filterSummary.Text = $"{matches.Count} resultado(s)";
        await LoadPageAsync();
    }

    private async Task ClearFiltersAsync()
    {
        speciesFilter.SelectedIndex = sexFilter.SelectedIndex = 0;
        breedFilter.Text = locationFilter.Text = "";
        filterCard!.IsVisible = false;
        filterToggleButton.Text = "🔎 Buscar y filtrar";
        filterSummary.Text = ""; filterSummary.IsVisible = false;
        filteredResults = null; offset = filteredOffset = 0; loaded = false;
        catalog.Clear(); loadMore.IsVisible = false;
        await LoadPageAsync();
    }

    private void RenderFilteredPage()
    {
        var results = filteredResults ?? [];
        var page = results.Skip(filteredOffset).Take(PageSize).ToArray();
        foreach (var pet in page) AddPetCard(pet);
        filteredOffset += page.Length;
        if (results.Count == 0 && filteredOffset == 0)
        {
            catalog.Add(Ui.Card(Ui.Text("No hay mascotas que coincidan con esos filtros.", color: Ui.Muted), true));
        }
        loaded = true;
        loadMore.Text = "Ver más mascotas";
        loadMore.IsVisible = filteredOffset < results.Count;
    }

    private void AddPetCard(CatalogPet pet)
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

}

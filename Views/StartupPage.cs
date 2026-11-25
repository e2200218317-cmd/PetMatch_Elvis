using PetMatch.Core;
using PetMatch.Services;

namespace PetMatch.Views;

/// <summary>Restaura una sesión si el equipo ya configuró el backend compartido.</summary>
public sealed class StartupPage : ContentPage
{
    private bool started;

    public StartupPage()
    {
        Title = "PetMatch";
        Content = new VerticalStackLayout
        {
            Padding = 24,
            Spacing = 14,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new ActivityIndicator { IsRunning = true, Color = Color.FromArgb("#38BDF8") },
                new Label { Text = "Abriendo PetMatch…", HorizontalTextAlignment = TextAlignment.Center }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (started) return;
        started = true;
        if (AppServices.Api.IsConfigured)
        {
            try { await AppServices.Api.RestoreAsync(); }
            catch (Exception ex) when (ex is ApiException or IOException or InvalidOperationException) { }
        }
        AppFlow.Home();
    }
}

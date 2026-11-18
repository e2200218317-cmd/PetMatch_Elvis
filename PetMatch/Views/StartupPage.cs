using PetMatch.Services;

namespace PetMatch.Views;

public sealed class StartupPage : FormPage
{
    private bool started;
    public StartupPage() : base("PETMATCH", "Un hogar cambia una vida", "Adopción responsable de mascotas.")
    {
        Body.Add(ActionButton("Reintentar conexión", Start));
        Body.Add(ActionButton("Entrar como visitante", () => { AppFlow.Home(); return Task.CompletedTask; }, true));
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing(); if (started) return; started = true; await Run(Start);
    }
    private async Task Start()
    {
        if (AppServices.Api.IsConfigured) await AppServices.Api.RestoreAsync();
        AppFlow.Home();
    }
}

using PetMatch.Core;
using PetMatch.Services;

namespace PetMatch.Views;

public sealed class ProfileFormPage : FormPage
{
    private readonly ProfileFields fields;
    private string? initialError;
    public ProfileFormPage(UserProfile? profile, Func<Page>? destination = null, UserProfile? draft = null, byte[]? photo = null, string? error = null)
        : base(profile == null ? "Completar perfil" : "Editar perfil", profile == null ? "Un último paso" : "Tu información",
            profile == null ? "Completa tus datos para usar tu cuenta. La foto de perfil es opcional." : "Actualiza tus datos de contacto. La foto es opcional.")
    {
        fields = new ProfileFields(profile ?? draft, profile != null, photo);
        fields.PickPhotoButton.Clicked += async (_, _) => await Run(fields.PickPhotoAsync);
        Body.Add(fields); initialError = error;
        Body.Add(ActionButton("Guardar perfil", async () =>
        {
            await AppServices.Api.SaveProfileAsync(fields.Read(), fields.NewPhoto);
            AppFlow.Reset(destination?.Invoke() ?? (profile == null ? new SupabaseHomePage() : new ProfilePage()));
        }));
        Body.Add(ActionButton("Cerrar sesión", async () => { await AppServices.Api.LogoutAsync(); AppFlow.Home(); }, true));
        if (profile == null) Body.Add(ActionButton("Eliminar esta cuenta", () => Navigation.PushAsync(new DeleteAccountPage()), danger: true));
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing(); await Run(fields.LoadPhotoAsync);
        if (initialError != null) { Status.Text = initialError; initialError = null; }
    }
}

using PetMatch.Core;
using PetMatch.Views;

namespace PetMatch.Services;

public static class AppFlow
{
    public static NavigationPage Wrap(Page page) => new(page)
    {
        BarBackgroundColor = Colors.White, BarTextColor = Color.FromArgb("#111827")
    };
    public static void Reset(Page page) => Application.Current!.Windows[0].Page = Wrap(page);
    public static void Home() => Reset(new HomePage());

    // David y Dylan deben llamar a este método antes de entrar en una pantalla protegida.
    public static async Task RequireAsync(Page origin, Func<Page> destination)
    {
        if (AppServices.Api.Session == null)
        { await origin.Navigation.PushAsync(new LoginPage(destination)); return; }
        await AppServices.Api.ValidateSessionAsync();
        var profile = await AppServices.Api.MyProfileAsync();
        if (profile == null) await origin.Navigation.PushAsync(new ProfileFormPage(null, destination));
        else if (profile.eliminando) Reset(new ProfilePage());
        else await origin.Navigation.PushAsync(destination());
    }

    public static async Task AfterLoginAsync(Func<Page>? destination = null, UserProfile? draft = null, byte[]? photo = null)
    {
        var profile = await AppServices.Api.MyProfileAsync();
        if (profile == null)
        {
            if (draft != null && photo != null)
            {
                try { await AppServices.Api.SaveProfileAsync(draft, photo); }
                catch (Exception e) when (e is ArgumentException or ApiException or IOException)
                {
                    Reset(new ProfileFormPage(null, destination, draft, photo, e.Message)); return;
                }
            }
            else { Reset(new ProfileFormPage(null, destination)); return; }
        }
        Reset(profile?.eliminando == true ? new ProfilePage() : destination?.Invoke() ?? new ProfilePage());
    }
}

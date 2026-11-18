using PetMatch.Services;
using PetMatch.Views;

namespace PetMatch;

public sealed class App : Application
{
    public App() { UserAppTheme = AppTheme.Light; }
    protected override Window CreateWindow(IActivationState? activationState) =>
        new(AppFlow.Wrap(new StartupPage()));
}

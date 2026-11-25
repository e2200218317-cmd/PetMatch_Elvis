using Microsoft.Extensions.DependencyInjection;

namespace PetMatch;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new NavigationPage(new Views.StartupPage()));
    }
}

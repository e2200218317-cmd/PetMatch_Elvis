namespace PetMatch;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute(nameof(Views.vDetalleMascotaPage), typeof(Views.vDetalleMascotaPage));
    }
}

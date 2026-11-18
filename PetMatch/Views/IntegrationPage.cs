namespace PetMatch.Views;

// Sustituir por las vistas reales al integrar las ramas del equipo.
public sealed class IntegrationPage : FormPage
{
    public IntegrationPage(string title, string owner) : base(title, title, "Este apartado aún no está disponible.")
    {
        Body.Add(Ui.Card(Ui.Text($"Pantalla reservada para integrar el módulo de {owner}. Esta entrega incluye usuarios, autenticación y perfiles."), true));
    }
}

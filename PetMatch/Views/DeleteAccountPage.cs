using PetMatch.Services;

namespace PetMatch.Views;

public sealed class DeleteAccountPage : FormPage
{
    public DeleteAccountPage() : base("Eliminar cuenta", "¿Quieres darte de baja?", "Esta acción eliminará tu acceso, tu perfil y tus fotos. No se puede deshacer.")
    {
        var password = Ui.Input("Tu contraseña actual", password: true);
        Body.Add(Ui.Field("Confirma tu contraseña", password));
        Body.Add(ActionButton("Eliminar mi cuenta definitivamente", async () =>
        {
            if (string.IsNullOrEmpty(password.Text)) throw new ArgumentException("Escribe tu contraseña actual.");
            if (!await DisplayAlertAsync("Eliminar cuenta", "¿Confirmas que deseas eliminar tu cuenta y tus datos?", "Sí, eliminar", "Cancelar")) return;
            await AppServices.Api.DeleteAccountAsync(password.Text); password.Text = "";
            await DisplayAlertAsync("Cuenta eliminada", "Tu cuenta se eliminó correctamente.", "Aceptar");
            AppFlow.Home();
        }, danger: true));
    }
}

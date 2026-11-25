using PetMatch.Core;
using PetMatch.Services;

namespace PetMatch.Views;

public sealed class RecoveryPage : FormPage
{
    private AuthSession? recovery;
    public RecoveryPage() : base("Recuperar contraseña", "Recupera tu acceso", "Solicita un código a tu correo y elige una nueva contraseña.")
    {
        var email = Ui.Input("tu@correo.com", Keyboard.Email);
        var code = Ui.Input("Código del correo", Keyboard.Numeric);
        var password = Ui.Input("Nueva contraseña", password: true);
        var confirm = Ui.Input("Repite la nueva contraseña", password: true);
        Body.Add(Ui.Field("Correo electrónico", email));
        Body.Add(ActionButton("Enviar código al correo", async () =>
        {
            await AppServices.Api.SendRecoveryAsync(Validation.Email(email.Text)); recovery = null;
            email.IsReadOnly = true;
            await DisplayAlertAsync("Revisa tu correo", "Si existe una cuenta con ese correo, recibirás un código para restablecer la contraseña.", "Aceptar");
        }));
        Body.Add(Ui.Field("Código de recuperación", code)); Body.Add(Ui.Field("Nueva contraseña", password));
        Body.Add(Ui.Text("De 8 a 72 caracteres, con mayúscula, minúscula y número.", 12, color: Ui.Muted));
        Body.Add(Ui.Field("Confirmar contraseña", confirm));
        Body.Add(ActionButton("Guardar nueva contraseña", async () =>
        {
            Validation.Password(password.Text, confirm.Text);
            if (string.IsNullOrWhiteSpace(code.Text)) throw new ArgumentException("Escribe el código del correo.");
            recovery ??= await AppServices.Api.VerifyRecoveryAsync(Validation.Email(email.Text), code.Text);
            await AppServices.Api.ResetPasswordAsync(recovery, password.Text!);
            password.Text = confirm.Text = code.Text = ""; recovery = null;
            await DisplayAlertAsync("Contraseña actualizada", "Ya puedes iniciar sesión con tu nueva contraseña.", "Continuar");
            AppFlow.Reset(new LoginPage());
        }));
    }
}

using PetMatch.Core;
using PetMatch.Services;

namespace PetMatch.Views;

public sealed class ConfirmEmailPage : FormPage
{
    public ConfirmEmailPage(string mail, Func<Page>? destination = null, UserProfile? draft = null, byte[]? photo = null)
        : base("Confirmar correo", "Revisa tu correo", "Escribe el código recibido para activar tu cuenta. Revisa también la carpeta de correo no deseado.")
    {
        var email = Ui.Input("tu@correo.com", Keyboard.Email); email.Text = mail;
        email.IsReadOnly = draft != null;
        var code = Ui.Input("Código del correo", Keyboard.Numeric); code.MaxLength = 10;
        Body.Add(Ui.Field("Correo electrónico", email)); Body.Add(Ui.Field("Código de confirmación", code));
        Body.Add(ActionButton("Confirmar y continuar", async () =>
        {
            if (string.IsNullOrWhiteSpace(code.Text)) throw new ArgumentException("Escribe el código que recibiste.");
            await AppServices.Api.ConfirmSignupAsync(Validation.Email(email.Text), code.Text);
            code.Text = ""; await AppFlow.AfterLoginAsync(destination, draft, photo);
        }));
        Body.Add(ActionButton("Reenviar código", async () =>
        {
            await AppServices.Api.ResendSignupAsync(Validation.Email(email.Text));
            await DisplayAlertAsync("Correo", "Si la cuenta requiere confirmación, recibirás un código. Respeta el tiempo de espera entre envíos.", "Aceptar");
        }, true));
        Body.Add(ActionButton("Ir a iniciar sesión", () => { AppFlow.Reset(new LoginPage(destination)); return Task.CompletedTask; }, true));
        Body.Add(Ui.Text("Si cierras la app antes de terminar, podrás confirmar el correo e ingresar; después completarás tu perfil.", 12, color: Ui.Muted));
    }
}

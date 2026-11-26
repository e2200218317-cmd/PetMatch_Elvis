using PetMatch.Core;
using PetMatch.Services;

namespace PetMatch.Views;

public sealed class RegisterPage : FormPage
{
    public RegisterPage(Func<Page>? destination = null, string? initialEmail = null,
        UserProfile? initialProfile = null, byte[]? pendingPhoto = null)
        : base("Registro", "Tu historia empieza aquí", "Crea una cuenta para adoptar o encontrar un hogar para una mascota.")
    {
        var email = Ui.Input("tu@correo.com", Keyboard.Email); email.Text = initialEmail;
        var password = Ui.Input("Mínimo 8 caracteres", password: true);
        var confirm = Ui.Input("Repite tu contraseña", password: true);
        var fields = new ProfileFields(initialProfile, pendingPhoto: pendingPhoto);
        fields.PickPhotoButton.Clicked += async (_, _) => await Run(fields.PickPhotoAsync);
        Body.Add(Ui.Field("Correo electrónico *", email)); Body.Add(Ui.Field("Contraseña *", password));
        Body.Add(Ui.Text("Usa mayúscula, minúscula y número; de 8 a 72 caracteres.", 12, color: Ui.Muted));
        Body.Add(Ui.Field("Confirmar contraseña *", confirm)); Body.Add(fields);
        Body.Add(ActionButton("Crear mi cuenta", async () =>
        {
            var mail = Validation.Email(email.Text); Validation.Password(password.Text, confirm.Text);
            var profile = fields.Read();
            var loggedIn = await AppServices.Api.SignupAsync(mail, password.Text!);
            password.Text = confirm.Text = "";
            if (loggedIn) await AppFlow.AfterLoginAsync(destination, profile, fields.NewPhoto);
            else AppFlow.Reset(new ConfirmEmailPage(mail, destination, profile, fields.NewPhoto));
        }));
        Body.Add(ActionButton("Ya tengo una cuenta", () => Navigation.PushAsync(new LoginPage(destination)), true));
    }
}

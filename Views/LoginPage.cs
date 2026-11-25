using PetMatch.Core;
using PetMatch.Services;

namespace PetMatch.Views;

public sealed class LoginPage : FormPage
{
    private readonly Entry email = Ui.Input("tu@correo.com", Keyboard.Email);
    private readonly Entry password = Ui.Input("Tu contraseña", password: true);
    public LoginPage(Func<Page>? destination = null) : base("Iniciar sesión", "Qué bueno verte", "Ingresa para conocer más de las mascotas y administrar tu perfil.")
    {
        Body.Add(Ui.Field("Correo electrónico", email)); Body.Add(Ui.Field("Contraseña", password));
        Body.Add(ActionButton("Iniciar sesión", async () =>
        {
            var mail = Validation.Email(email.Text);
            if (string.IsNullOrEmpty(password.Text)) throw new ArgumentException("Escribe tu contraseña.");
            await AppServices.Api.LoginAsync(mail, password.Text); password.Text = "";
            await AppFlow.AfterLoginAsync(destination);
        }));
        Body.Add(ActionButton("Recuperar contraseña", () => Navigation.PushAsync(new RecoveryPage()), true));
        Body.Add(Ui.Text("¿Todavía no tienes una cuenta?", color: Ui.Muted));
        Body.Add(ActionButton("Registrarme como usuario nuevo", () => Navigation.PushAsync(new RegisterPage(destination)), true));
        Body.Add(ActionButton("Confirmar mi correo", () => Navigation.PushAsync(new ConfirmEmailPage(email.Text ?? "", destination)), true));
        Body.Add(ActionButton("Volver al inicio", () => { AppFlow.Home(); return Task.CompletedTask; }, true));
    }
}

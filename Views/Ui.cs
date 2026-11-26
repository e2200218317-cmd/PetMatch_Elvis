using Microsoft.Maui.Controls.Shapes;
using PetMatch.Core;
using PetMatch.Services;

namespace PetMatch.Views;

public static class Ui
{
    public static Color Ink => Color.FromArgb("#111827");
    public static Color Blue => Color.FromArgb("#38BDF8");
    public static Color Muted => Color.FromArgb("#64748B");
    public static Label Text(string text, double size = 15, bool bold = false, Color? color = null) => new()
    {
        Text = text, FontSize = size, TextColor = color ?? Ink,
        FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None, LineBreakMode = LineBreakMode.WordWrap
    };
    public static Entry Input(string placeholder, Keyboard? keyboard = null, bool password = false) => new()
    {
        Placeholder = placeholder, Keyboard = keyboard ?? Keyboard.Default, IsPassword = password,
        TextColor = Ink, PlaceholderColor = Muted, BackgroundColor = Colors.Transparent,
        FontSize = 16, HeightRequest = 50, ClearButtonVisibility = password ? ClearButtonVisibility.Never : ClearButtonVisibility.WhileEditing
    };
    public static Border Card(View content, bool tinted = false) => new()
    {
        Content = content, Padding = 16, StrokeThickness = 1, Stroke = Color.FromArgb("#DCE5EB"),
        StrokeShape = new RoundRectangle { CornerRadius = 18 },
        BackgroundColor = tinted ? Color.FromArgb("#EEF9FF") : Colors.White
    };
    public static View Field(string label, View input)
    {
        bool selectable = input is Picker or DatePicker;
        if (selectable) input.HeightRequest = 54;
        return new VerticalStackLayout
        {
            Spacing = 6,
            Children =
            {
                Text(selectable ? $"{label} · Toca para elegir" : label, 13, true,
                    selectable ? Color.FromArgb("#075985") : null),
                new Border
                {
                    Content = input,
                    Padding = new Thickness(12, 0),
                    BackgroundColor = selectable ? Color.FromArgb("#E0F2FE") : Color.FromArgb("#F8FAFC"),
                    Stroke = selectable ? Color.FromArgb("#38BDF8") : Color.FromArgb("#DCE5EB"),
                    StrokeThickness = selectable ? 1.5 : 1,
                    StrokeShape = new RoundRectangle { CornerRadius = 12 }
                }
            }
        };
    }
    public static Button Button(string text, bool secondary = false, bool danger = false) => new()
    {
        Text = text, FontSize = 15, FontAttributes = FontAttributes.Bold, CornerRadius = 14,
        MinimumHeightRequest = 50, Padding = new Thickness(14, 10),
        TextColor = danger ? Color.FromArgb("#B91C1C") : Ink,
        BackgroundColor = danger ? Color.FromArgb("#FEF2F2") : secondary ? Color.FromArgb("#EFF4F8") : Blue
    };
}

public abstract class FormPage : ContentPage
{
    protected VerticalStackLayout Body { get; } = new() { Spacing = 18, Padding = new Thickness(22, 20, 22, 32) };
    protected Label Status { get; } = Ui.Text("", color: Color.FromArgb("#B91C1C"));
    private readonly ActivityIndicator spinner = new() { Color = Ui.Blue, IsVisible = false };
    private bool busy;
    protected FormPage(string title, string heading, string subtitle)
    {
        Title = title; BackgroundColor = Colors.White;
        Body.HorizontalOptions = LayoutOptions.Center;
        Body.MaximumWidthRequest = 620;
        Body.Add(Ui.Text("PETMATCH", 18, true, Color.FromArgb("#0284C7")));
        Body.Add(Ui.Text(heading, 29, true));
        if (!string.IsNullOrEmpty(subtitle)) Body.Add(Ui.Text(subtitle, 15, color: Ui.Muted));
        Body.Add(spinner); Body.Add(Status);
        Content = new ScrollView { Content = Body };
    }
    protected Button ActionButton(string text, Func<Task> action, bool secondary = false, bool danger = false)
    {
        var button = Ui.Button(text, secondary, danger);
        button.Clicked += async (_, _) => await Run(action); return button;
    }
    protected async Task Run(Func<Task> action)
    {
        if (busy) return;
        busy = true; Body.IsEnabled = false; spinner.IsVisible = spinner.IsRunning = true; Status.Text = "";
        try { await action(); }
        catch (Exception e) when (e is ArgumentException or ApiException or IOException or InvalidOperationException)
        {
            Status.Text = e.Message;
            if (e is ApiException api && api.Status == 401)
            {
                await AppServices.Api.ClearLocalAsync();
                await DisplayAlertAsync("Sesión", "Inicia sesión nuevamente para continuar.", "Aceptar");
                AppFlow.Reset(new LoginPage());
            }
        }
        catch (Exception)
        { Status.Text = "No se pudo completar la operación. Revisa la conexión y vuelve a intentar."; }
        finally { busy = false; Body.IsEnabled = true; spinner.IsVisible = spinner.IsRunning = false; }
    }
    protected override bool OnBackButtonPressed() => busy || base.OnBackButtonPressed();
}

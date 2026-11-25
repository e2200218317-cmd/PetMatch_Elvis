using PetMatch.Models;
using PetMatch.Services;

namespace PetMatch.Views;

/// <summary>Formulario local de publicación y edición de mascotas.</summary>
public sealed class LocalPetFormPage : ContentPage
{
    private readonly MascotaService service = new();
    private readonly Mascota? original;
    private readonly Entry nombre = new() { Placeholder = "Opcional" };
    private readonly Picker especie = new() { Title = "Selecciona una especie", ItemsSource = new[] { "Perro", "Gato", "Otro" } };
    private readonly Entry raza = new() { Placeholder = "Raza" };
    private readonly Entry edadAnios = new() { Placeholder = "Años", Keyboard = Keyboard.Numeric };
    private readonly Entry edadMeses = new() { Placeholder = "Meses", Keyboard = Keyboard.Numeric };
    private readonly Picker sexo = new() { Title = "Selecciona el sexo", ItemsSource = new[] { "Macho", "Hembra", "No especificado" } };
    private readonly Picker tamano = new() { Title = "Selecciona el tamaño", ItemsSource = new[] { "Pequeño", "Mediano", "Grande" } };
    private readonly Entry color = new() { Placeholder = "Color (opcional)" };
    private readonly Editor descripcion = new() { Placeholder = "Cuéntanos sobre la mascota", AutoSize = EditorAutoSizeOption.TextChanges, HeightRequest = 100 };
    private readonly Image foto = new() { HeightRequest = 190, Aspect = Aspect.AspectFit };
    private readonly Label estado = new() { TextColor = Colors.Firebrick };
    private string imagenUrl = string.Empty;

    public LocalPetFormPage(Mascota? mascota = null)
    {
        original = mascota;
        Title = mascota == null ? "Publicar mascota" : "Editar mascota";
        BackgroundColor = Colors.White;
        if (mascota != null)
        {
            nombre.Text = mascota.Nombre;
            especie.SelectedItem = mascota.Especie;
            raza.Text = mascota.Raza;
            edadAnios.Text = mascota.EdadAnios.ToString();
            edadMeses.Text = mascota.EdadMeses.ToString();
            sexo.SelectedItem = mascota.Sexo;
            tamano.SelectedItem = mascota.Tamano;
            color.Text = mascota.Color;
            descripcion.Text = mascota.Descripcion;
            imagenUrl = mascota.ImagenUrl;
            if (!string.IsNullOrWhiteSpace(imagenUrl)) foto.Source = ImageSource.FromFile(imagenUrl);
        }

        var seleccionarFoto = new Button { Text = "Elegir foto" };
        seleccionarFoto.Clicked += async (_, _) => await ElegirFotoAsync();
        var guardar = new Button { Text = mascota == null ? "Guardar publicación" : "Guardar cambios", MinimumHeightRequest = 50 };
        guardar.Clicked += async (_, _) => await GuardarAsync(guardar);
        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 22,
                Spacing = 14,
                Children =
                {
                    new Label { Text = mascota == null ? "Nueva mascota" : "Editar publicación", FontSize = 28, FontAttributes = FontAttributes.Bold },
                    foto, seleccionarFoto,
                    new Label { Text = "Nombre" }, nombre,
                    new Label { Text = "Especie *" }, especie,
                    new Label { Text = "Raza" }, raza,
                    new Label { Text = "Edad" }, new HorizontalStackLayout { Spacing = 10, Children = { edadAnios, edadMeses } },
                    new Label { Text = "Sexo" }, sexo,
                    new Label { Text = "Tamaño" }, tamano,
                    new Label { Text = "Color" }, color,
                    new Label { Text = "Descripción" }, descripcion,
                    estado, guardar
                }
            }
        };
    }

    private async Task ElegirFotoAsync()
    {
        try
        {
            var archivo = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Selecciona una foto", FileTypes = FilePickerFileType.Images });
            if (archivo == null) return;
            var extension = Path.GetExtension(archivo.FileName);
            var destino = Path.Combine(FileSystem.AppDataDirectory, $"mascota-{Guid.NewGuid():N}{extension}");
            await using var entrada = await archivo.OpenReadAsync();
            await using var salida = File.Create(destino);
            await entrada.CopyToAsync(salida);
            imagenUrl = destino;
            foto.Source = ImageSource.FromFile(destino);
        }
        catch (Exception ex)
        {
            estado.Text = $"No se pudo cargar la foto: {ex.Message}";
        }
    }

    private async Task GuardarAsync(Button boton)
    {
        if (!int.TryParse(edadAnios.Text ?? "0", out var anios) || !int.TryParse(edadMeses.Text ?? "0", out var meses) || anios < 0 || anios > 40 || meses < 0 || meses > 11)
        {
            estado.Text = "Escribe una edad válida: años entre 0 y 40 y meses entre 0 y 11.";
            return;
        }
        if (especie.SelectedItem is not string especieElegida)
        {
            estado.Text = "Selecciona la especie.";
            return;
        }

        boton.IsEnabled = false;
        estado.Text = "";
        try
        {
            var mascota = original ?? new Mascota { Id = Guid.NewGuid() };
            mascota.Nombre = nombre.Text?.Trim() ?? "";
            mascota.Especie = especieElegida;
            mascota.Raza = raza.Text?.Trim() ?? "";
            mascota.EdadAnios = anios;
            mascota.EdadMeses = meses;
            mascota.Sexo = sexo.SelectedItem as string ?? "No especificado";
            mascota.Tamano = tamano.SelectedItem as string ?? "";
            mascota.Color = color.Text?.Trim() ?? "";
            mascota.Descripcion = descripcion.Text?.Trim() ?? "";
            mascota.ImagenUrl = imagenUrl;
            mascota.Estado = "Disponible";
            await service.GuardarMascotaAsync(mascota);
            await DisplayAlertAsync("PetMatch", "La mascota se guardó en este dispositivo.", "Aceptar");
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            estado.Text = $"No se pudo guardar: {ex.Message}";
        }
        finally
        {
            boton.IsEnabled = true;
        }
    }
}

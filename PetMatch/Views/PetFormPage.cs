using PetMatch.Core;
using PetMatch.Services;

namespace PetMatch.Views;

// Formulario para publicar una mascota o editar una publicación propia.
public sealed class PetFormPage : FormPage
{
    private readonly Entry name = Ui.Input("Opcional");
    private readonly Picker species = Choice("Elige la especie", Validation.Species);
    private readonly Entry breed = Ui.Input("Ej. Mestizo");
    private readonly Entry age = Ui.Input("Ej. 2 años o 6 meses");
    private readonly Picker sex = Choice("Elige el sexo", Validation.Sexes);
    private readonly Picker size = Choice("Elige el tamaño", Validation.Sizes);
    private readonly Entry color = Ui.Input("Opcional");
    private readonly Entry health = Ui.Input("Ej. Sana, desparasitada, esterilizada");
    private readonly Entry vaccines = Ui.Input("Opcional");
    private readonly Entry location = Ui.Input("Ciudad y sector");
    private readonly Editor comment = new() { Placeholder = "Opcional · carácter, cuidados, con quién convive", MaxLength = 500,
        HeightRequest = 110, TextColor = Ui.Ink, PlaceholderColor = Ui.Muted, FontSize = 16 };
    private readonly HorizontalStackLayout previews = new() { Spacing = 8 };
    private readonly List<byte[]> photos = [];

    private static Picker Choice(string title, string[] options) =>
        new() { Title = title, ItemsSource = options, TextColor = Ui.Ink, TitleColor = Ui.Muted, FontSize = 16 };

    public PetFormPage(PetPost? pet = null)
        : base(pet == null ? "Publicar" : "Editar publicación", pet == null ? "Busca un hogar" : "Tu publicación",
            pet == null ? "Completa los datos y agrega de 3 a 5 fotos." : "Actualiza los datos de la mascota. Las fotos no cambian.")
    {
        name.MaxLength = breed.MaxLength = color.MaxLength = 60; age.MaxLength = 30;
        health.MaxLength = vaccines.MaxLength = 200; location.MaxLength = 120;
        if (pet != null)
        {
            name.Text = pet.nombre; species.SelectedItem = pet.especie; breed.Text = pet.raza; age.Text = pet.edad;
            sex.SelectedItem = pet.sexo; size.SelectedItem = pet.tamano; color.Text = pet.color; health.Text = pet.salud;
            vaccines.Text = pet.vacunas; location.Text = pet.ubicacion; comment.Text = pet.descripcion;
        }
        else
        {
            var pick = Ui.Button("Agregar fotos", true);
            pick.Clicked += async (_, _) => await Run(PickPhotosAsync);
            var clear = Ui.Button("Quitar fotos", true);
            clear.Clicked += (_, _) => { photos.Clear(); previews.Clear(); };
            Body.Add(Ui.Card(new VerticalStackLayout { Spacing = 10, Children = {
                new ScrollView { Orientation = ScrollOrientation.Horizontal, Content = previews }, pick, clear,
                Ui.Text("De 3 a 5 fotos · JPG o PNG · máximo 5 MB cada una. La primera será la portada.", 12, color: Ui.Muted) } }, true));
        }
        Body.Add(Ui.Field("Nombre", name)); Body.Add(Ui.Field("Especie *", species));
        Body.Add(Ui.Field("Raza *", breed)); Body.Add(Ui.Field("Edad *", age));
        Body.Add(Ui.Field("Sexo *", sex)); Body.Add(Ui.Field("Tamaño *", size));
        Body.Add(Ui.Field("Color", color)); Body.Add(Ui.Field("Estado de salud *", health));
        Body.Add(Ui.Field("Vacunas", vaccines)); Body.Add(Ui.Field("Ubicación *", location));
        Body.Add(Ui.Field("Comentario", comment));
        Body.Add(Ui.Text("La publicación dura 10 días. Puedes tener hasta 5 publicaciones activas.", 12, color: Ui.Muted));
        Body.Add(ActionButton(pet == null ? "Publicar mascota" : "Guardar cambios", async () =>
        {
            var value = Read(pet?.id ?? Guid.Empty);
            if (pet == null) await AppServices.Api.PublishPetAsync(value, photos);
            else await AppServices.Api.UpdatePetAsync(value);
            await DisplayAlertAsync("PetMatch", pet == null ? "Tu mascota ya aparece en el catálogo." : "Cambios guardados.", "Aceptar");
            await Navigation.PopAsync();
        }));
    }

    private PetPost Read(Guid id) => new()
    {
        id = id, nombre = name.Text, especie = species.SelectedItem as string ?? "", raza = breed.Text ?? "",
        edad = age.Text ?? "", sexo = sex.SelectedItem as string ?? "", tamano = size.SelectedItem as string ?? "",
        color = color.Text, salud = health.Text ?? "", vacunas = vaccines.Text, ubicacion = location.Text ?? "",
        descripcion = comment.Text
    };

    private async Task PickPhotosAsync()
    {
        var results = await FilePicker.Default.PickMultipleAsync(new PickOptions { PickerTitle = "Fotos de la mascota", FileTypes = FilePickerFileType.Images });
        foreach (var result in results ?? [])
        {
            if (result == null) continue;
            if (photos.Count == 5) throw new ArgumentException("Puedes agregar máximo 5 fotos.");
            var data = await ReadPhotoAsync(result);
            photos.Add(data);
            previews.Add(new Image { Source = ImageSource.FromStream(() => new MemoryStream(data)), HeightRequest = 96, WidthRequest = 96, Aspect = Aspect.AspectFill });
        }
    }

    private static async Task<byte[]> ReadPhotoAsync(FileResult file)
    {
        await using var stream = await file.OpenReadAsync();
        using var memory = new MemoryStream();
        byte[] buffer = new byte[81920]; int read;
        while ((read = await stream.ReadAsync(buffer)) != 0)
        {
            if (memory.Length + read > 5242880) throw new ArgumentException("Cada foto debe pesar como máximo 5 MB.");
            memory.Write(buffer, 0, read);
        }
        var data = memory.ToArray(); Validation.ImageType(data); return data;
    }
}

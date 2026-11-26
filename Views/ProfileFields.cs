using PetMatch.Core;
using PetMatch.Services;

namespace PetMatch.Views;

// Formulario reutilizado por registro, completar perfil y edición.
public sealed class ProfileFields : VerticalStackLayout
{
    private readonly Entry names = Ui.Input("Tus nombres");
    private readonly Entry lastNames = Ui.Input("Tus apellidos");
    private readonly DatePicker birth = new()
    {
        Date = DateTime.Today.AddYears(-18), MinimumDate = new DateTime(1900, 1, 1),
        MaximumDate = DateTime.Today.AddYears(-18), Format = "dd/MM/yyyy", TextColor = Ui.Ink
    };
    private readonly Entry identity = Ui.Input("10 dígitos", Keyboard.Numeric);
    private readonly Entry city = Ui.Input("Ej. El Coca");
    private readonly Entry address = Ui.Input("Barrio, calle y referencia");
    private readonly Entry phone = Ui.Input("09XXXXXXXX", Keyboard.Telephone);
    private readonly Image preview = new() { Source = "paw.png", HeightRequest = 120, WidthRequest = 120, Aspect = Aspect.AspectFill };
    private readonly UserProfile initial;
    public byte[]? NewPhoto { get; private set; }
    public Button PickPhotoButton { get; } = Ui.Button("Elegir foto de perfil (opcional)", true);

    public ProfileFields(UserProfile? value = null, bool edit = false, byte[]? pendingPhoto = null)
    {
        initial = value ?? new UserProfile(); Spacing = 16; NewPhoto = pendingPhoto;
        names.MaxLength = lastNames.MaxLength = 80; identity.MaxLength = 10; city.MaxLength = 100; address.MaxLength = 250;
        names.Text = initial.nombres; lastNames.Text = initial.apellidos; birth.Date = initial.fecha_nacimiento;
        identity.Text = initial.cedula; city.Text = initial.ciudad; address.Text = initial.direccion; phone.Text = initial.celular;
        identity.IsReadOnly = edit; birth.IsEnabled = !edit;
        Add(Ui.Card(new VerticalStackLayout { Spacing = 10, Children = { preview, PickPhotoButton,
            Ui.Text("Foto opcional · JPG o PNG · máximo 5 MB. Si no agregas una, usaremos el ícono de PetMatch.", 12, color: Ui.Muted) } }, true));
        Add(Ui.Field("Nombres *", names)); Add(Ui.Field("Apellidos *", lastNames));
        Add(Ui.Field("Fecha de nacimiento * · mínimo 18 años", birth)); Add(Ui.Field("Cédula * · dato privado", identity));
        Add(Ui.Field("Ciudad *", city)); Add(Ui.Field("Dirección *", address)); Add(Ui.Field("Celular *", phone));
        Add(Ui.Text("Tu nombre y ciudad pueden aparecer en publicaciones. Tu dirección y celular serán visibles para usuarios que hayan iniciado sesión. Tu cédula y fecha de nacimiento son privadas.", 12, color: Ui.Muted));
        if (pendingPhoto != null) preview.Source = ImageSource.FromStream(() => new MemoryStream(pendingPhoto));
    }

    public async Task LoadPhotoAsync()
    {
        if (NewPhoto != null || string.IsNullOrEmpty(initial.avatar_path)) return;
        var uri = await AppServices.Api.AvatarUrlAsync(initial.avatar_path);
        if (uri != null) preview.Source = new UriImageSource { Uri = new Uri(uri), CachingEnabled = false };
    }

    public async Task PickPhotoAsync()
    {
        var result = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Foto de perfil", FileTypes = FilePickerFileType.Images });
        if (result == null) return;
        await using var stream = await result.OpenReadAsync();
        using var memory = new MemoryStream();
        byte[] buffer = new byte[81920]; int read;
        while ((read = await stream.ReadAsync(buffer)) != 0)
        {
            if (memory.Length + read > 5242880) throw new ArgumentException("La foto debe pesar como máximo 5 MB.");
            memory.Write(buffer, 0, read);
        }
        var data = memory.ToArray(); Validation.ImageType(data); NewPhoto = data;
        preview.Source = ImageSource.FromStream(() => new MemoryStream(data));
    }

    public UserProfile Read()
    {
        var value = new UserProfile
        {
            id = initial.id, avatar_path = initial.avatar_path,
            nombres = names.Text ?? "", apellidos = lastNames.Text ?? "", fecha_nacimiento = birth.Date ?? DateTime.Today,
            cedula = identity.Text ?? "", ciudad = city.Text ?? "", direccion = address.Text ?? "", celular = phone.Text ?? ""
        };
        Validation.Profile(value, hasPhoto: true);
        return value;
    }
}

using PetMatch.ViewModels;

namespace PetMatch.Views;

[QueryProperty(nameof(MascotaId), "id")]
public partial class vDetalleMascotaPage : ContentPage
{
    private readonly DetalleMascotaViewModel _viewModel;
    private string? _mascotaId;

    public string? MascotaId
    {
        get => _mascotaId;
        set
        {
            _mascotaId = value;
            if (Guid.TryParse(value, out var id))
                _ = CargarDetalleAsync(id);
        }
    }

    public vDetalleMascotaPage() : this(new DetalleMascotaViewModel()) { }

    public vDetalleMascotaPage(DetalleMascotaViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    private async Task CargarDetalleAsync(Guid id)
    {
        try
        {
            await _viewModel.CargarMascotaAsync(id);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("No se pudo cargar la mascota", ex.Message, "Aceptar");
        }
    }
}

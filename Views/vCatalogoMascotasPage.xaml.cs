using PetMatch.ViewModels;

namespace PetMatch.Views;

public partial class vCatalogoMascotasPage : ContentPage
{
    private readonly CatalogoMascotasViewModel _viewModel;

    public vCatalogoMascotasPage() : this(new CatalogoMascotasViewModel()) { }

    public vCatalogoMascotasPage(CatalogoMascotasViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            await _viewModel.CargarMascotasAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("No se pudo cargar el catálogo", ex.Message, "Aceptar");
        }
    }
}

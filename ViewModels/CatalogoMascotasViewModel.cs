using System.Collections.ObjectModel;
using PetMatch.Models;
using PetMatch.Services;

namespace PetMatch.ViewModels;

public class CatalogoMascotasViewModel
{
    private readonly MascotaService _mascotaService;

    public ObservableCollection<Mascota> Mascotas { get; } = new();
    public string Titulo => "Mascotas disponibles";

    public CatalogoMascotasViewModel() : this(new MascotaService()) { }

    public CatalogoMascotasViewModel(MascotaService mascotaService)
    {
        _mascotaService = mascotaService;
    }

    public async Task CargarMascotasAsync(CancellationToken cancellationToken = default)
    {
        var mascotas = await _mascotaService.ObtenerMascotasAsync(cancellationToken);
        Mascotas.Clear();
        foreach (var mascota in mascotas)
            Mascotas.Add(mascota);
    }
}

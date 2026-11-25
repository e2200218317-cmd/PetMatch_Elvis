using PetMatch.Models;
using PetMatch.Services;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PetMatch.ViewModels;

public partial class DetalleMascotaViewModel(MascotaService mascotaService) : INotifyPropertyChanged
{
    private readonly MascotaService _mascotaService = mascotaService;
    private Mascota? _mascota;
    public Mascota? Mascota
    {
        get => _mascota;
        private set
        {
            if (_mascota == value) return;
            _mascota = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Nombre));
            OnPropertyChanged(nameof(Especie));
            OnPropertyChanged(nameof(Raza));
            OnPropertyChanged(nameof(Edad));
            OnPropertyChanged(nameof(Sexo));
            OnPropertyChanged(nameof(Tamaño));
            OnPropertyChanged(nameof(Descripcion));
            OnPropertyChanged(nameof(ImagenUrl));
        }
    }
    public string Nombre => Mascota?.Nombre ?? string.Empty;
    public string Especie => Mascota?.Especie ?? string.Empty;
    public string Raza => Mascota?.Raza ?? string.Empty;
    public string Edad => Mascota?.Edad ?? string.Empty;
    public string Sexo => Mascota?.Sexo ?? string.Empty;
    public string Tamaño => Mascota?.Tamano ?? string.Empty;
    public string Descripcion => Mascota?.Descripcion ?? string.Empty;
    public string ImagenUrl => Mascota?.ImagenUrl ?? string.Empty;

    public DetalleMascotaViewModel() : this(new MascotaService()) { }

    public async Task CargarMascotaAsync(Guid id, CancellationToken cancellationToken = default) =>
        Mascota = await _mascotaService.ObtenerPorIdAsync(id, cancellationToken);

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}


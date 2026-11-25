using PetMatch.Models;
using SQLite;

namespace PetMatch.Services;

/// <summary>Acceso local al catálogo de mascotas almacenado en SQLite.</summary>
public sealed class MascotaService
{
    private const string NombreBaseDatos = "petmatch.db3";
    private static readonly Lazy<SQLiteAsyncConnection> Conexion = new(() =>
    {
        var ruta = Path.Combine(FileSystem.AppDataDirectory, NombreBaseDatos);
        return new SQLiteAsyncConnection(ruta);
    });
    private static readonly Lazy<Task> Inicializacion = new(() =>
        Conexion.Value.CreateTableAsync<MascotaDb>());

    public async Task<IReadOnlyList<Mascota>> ObtenerMascotasAsync(CancellationToken cancellationToken = default)
    {
        await PrepararAsync(cancellationToken);
        var registros = await Conexion.Value.Table<MascotaDb>().ToListAsync();
        cancellationToken.ThrowIfCancellationRequested();
        return registros
            .Where(m => m.Estado == "Disponible")
            .OrderByDescending(m => m.FechaRegistro)
            .Select(Mapear)
            .ToList();
    }

    public async Task<Mascota?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await PrepararAsync(cancellationToken);
        var registro = await Conexion.Value.FindAsync<MascotaDb>(id.ToString());
        cancellationToken.ThrowIfCancellationRequested();
        return registro is null ? null : Mapear(registro);
    }

    /// <summary>Inserta una mascota nueva o actualiza la existente.</summary>
    public async Task GuardarMascotaAsync(Mascota mascota, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mascota);
        await PrepararAsync(cancellationToken);
        if (mascota.Id == Guid.Empty)
            mascota.Id = Guid.NewGuid();

        var registro = Mapear(mascota);
        registro.FechaRegistro = DateTime.UtcNow.Ticks;
        await Conexion.Value.InsertOrReplaceAsync(registro);
        cancellationToken.ThrowIfCancellationRequested();
    }

    public async Task EliminarMascotaAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await PrepararAsync(cancellationToken);
        await Conexion.Value.DeleteAsync<MascotaDb>(id.ToString());
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static async Task PrepararAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Inicializacion.Value;
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static Mascota Mapear(MascotaDb registro) => new()
    {
        Id = Guid.TryParse(registro.Id, out var id) ? id : Guid.Empty,
        Nombre = registro.Nombre,
        Especie = registro.Especie,
        Raza = registro.Raza,
        EdadAnios = registro.EdadAnios,
        EdadMeses = registro.EdadMeses,
        Sexo = registro.Sexo,
        Tamano = registro.Tamano,
        Color = registro.Color,
        Descripcion = registro.Descripcion,
        ImagenUrl = registro.ImagenUrl,
        Estado = registro.Estado
    };

    private static MascotaDb Mapear(Mascota mascota) => new()
    {
        Id = mascota.Id.ToString(),
        Nombre = mascota.Nombre,
        Especie = mascota.Especie,
        Raza = mascota.Raza,
        EdadAnios = mascota.EdadAnios,
        EdadMeses = mascota.EdadMeses,
        Sexo = mascota.Sexo,
        Tamano = mascota.Tamano,
        Color = mascota.Color,
        Descripcion = mascota.Descripcion,
        ImagenUrl = mascota.ImagenUrl,
        Estado = string.IsNullOrWhiteSpace(mascota.Estado) ? "Disponible" : mascota.Estado
    };

    [Table("mascotas")]
    private sealed class MascotaDb
    {
        public MascotaDb() { }

        [PrimaryKey] public string Id { get; set; } = string.Empty;
        [NotNull] public string Nombre { get; set; } = string.Empty;
        public string Especie { get; set; } = string.Empty;
        public string Raza { get; set; } = string.Empty;
        public int EdadAnios { get; set; }
        public int EdadMeses { get; set; }
        public string Sexo { get; set; } = string.Empty;
        public string Tamano { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string ImagenUrl { get; set; } = string.Empty;
        public string Estado { get; set; } = "Disponible";
        public long FechaRegistro { get; set; }
    }
}

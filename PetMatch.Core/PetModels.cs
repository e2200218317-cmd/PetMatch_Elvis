namespace PetMatch.Core;

public sealed class PetPost
{
    public Guid id { get; set; }
    public string? nombre { get; set; }
    public string especie { get; set; } = "";
    public string raza { get; set; } = "";
    public string edad { get; set; } = "";
    public string sexo { get; set; } = "";
    public string tamano { get; set; } = "";
    public string? color { get; set; }
    public string salud { get; set; } = "";
    public string? vacunas { get; set; }
    public string ubicacion { get; set; } = "";
    public string? descripcion { get; set; }
    public string? foto_url { get; set; }
    public bool disponible { get; set; } = true;
    public bool vencida { get; set; }
    public DateTimeOffset vence_en { get; set; }

    public string Titulo => string.IsNullOrWhiteSpace(nombre) ? especie : nombre;

    public string Estado(DateTimeOffset now) =>
        vencida || (disponible && vence_en <= now) ? "Vencida" : disponible ? "Disponible" : "Adoptada";
}

public sealed class ReceivedRequest
{
    public Guid id { get; set; }
    public Guid mascota_id { get; set; }
    public string mascota { get; set; } = "";
    public string estado { get; set; } = "";
    public string? mensaje { get; set; }
    public DateTimeOffset creado_en { get; set; }
    public Guid solicitante_id { get; set; }
    public string nombres { get; set; } = "";
    public string apellidos { get; set; } = "";
    public string ciudad { get; set; } = "";
    public string celular { get; set; } = "";
}

public sealed class AppNotification
{
    public Guid id { get; set; }
    public string mensaje { get; set; } = "";
    public bool leida { get; set; }
    public DateTimeOffset creado_en { get; set; }
}

public sealed class PetDetails
{
    public Guid id { get; set; }
    public string? nombre { get; set; }
    public string especie { get; set; } = "";
    public string raza { get; set; } = "";
    public string edad { get; set; } = "";
    public string sexo { get; set; } = "";
    public string tamano { get; set; } = "";
    public string? color { get; set; }
    public string salud { get; set; } = "";
    public string? vacunas { get; set; }
    public string ubicacion { get; set; } = "";
    public string? descripcion { get; set; }
    public string? foto_url { get; set; }
    public string? propietario_id { get; set; }
    public string[] fotos { get; set; } = [];

    public string Titulo => string.IsNullOrWhiteSpace(nombre) ? especie : nombre;
}

public sealed class PublicProfile
{
    public Guid id { get; set; }
    public string nombres { get; set; } = "";
    public string apellidos { get; set; } = "";
    public string ciudad { get; set; } = "";
    public string celular { get; set; } = "";
}

public sealed class AdoptionRequest
{
    public Guid id { get; set; }
    public Guid mascota_id { get; set; }
    public string mascota { get; set; } = "";
    public string estado { get; set; } = "";
    public string? mensaje { get; set; }
    public DateTimeOffset creado_en { get; set; }
}

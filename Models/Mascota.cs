namespace PetMatch.Models;

/// <summary>Modelo de presentación de una mascota del catálogo.</summary>
public class Mascota
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Especie { get; set; } = string.Empty;
    public string Raza { get; set; } = string.Empty;
    public int EdadAnios { get; set; }
    public int EdadMeses { get; set; }
    public string Edad => EdadAnios == 0 ? $"{EdadMeses} meses" : EdadMeses == 0 ? $"{EdadAnios} años" : $"{EdadAnios} años, {EdadMeses} meses";
    public string Sexo { get; set; } = string.Empty;
    public string Tamano { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string ImagenUrl { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
}

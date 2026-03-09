namespace Itm.Event.Api.Models;

public class EventItem
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public decimal PrecioBase { get; set; }
    public int SillasDisponibles { get; set; }
}
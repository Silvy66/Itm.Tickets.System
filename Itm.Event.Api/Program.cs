using Itm.Event.Api.Dtos;
using Itm.Event.Api.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

var events = new List<EventItem>
{
    new()
    {
        Id = 1,
        Nombre = "Concierto ITM",
        PrecioBase = 50000,
        SillasDisponibles = 100
    }
};

// Para evitar problemas de concurrencia en memoria
var stockLock = new object();

app.MapGet("/api/events/{id:int}", (int id) =>
{
    var ev = events.FirstOrDefault(e => e.Id == id);

    if (ev is null)
        return Results.NotFound(new { Message = "Evento no encontrado." });

    var dto = new EventDto(ev.Id, ev.Nombre, ev.PrecioBase, ev.SillasDisponibles);
    return Results.Ok(dto);
});

app.MapPost("/api/events/reserve", (ReserveRequestDto request) =>
{
    var ev = events.FirstOrDefault(e => e.Id == request.EventId);

    if (ev is null)
        return Results.NotFound(new { Message = "Evento no encontrado." });

    lock (stockLock)
    {
        if (request.Quantity <= 0)
            return Results.BadRequest(new { Message = "La cantidad debe ser mayor a cero." });

        if (ev.SillasDisponibles < request.Quantity)
            return Results.BadRequest(new { Message = "No hay sillas suficientes." });

        ev.SillasDisponibles -= request.Quantity;
    }

    return Results.Ok(new
    {
        Message = "Sillas reservadas correctamente."
    });
});

app.MapPost("/api/events/release", (ReserveRequestDto request) =>
{
    var ev = events.FirstOrDefault(e => e.Id == request.EventId);

    if (ev is null)
        return Results.NotFound(new { Message = "Evento no encontrado." });

    lock (stockLock)
    {
        if (request.Quantity <= 0)
            return Results.BadRequest(new { Message = "La cantidad debe ser mayor a cero." });

        ev.SillasDisponibles += request.Quantity;
    }

    return Results.Ok(new
    {
        Message = "Sillas liberadas correctamente."
    });
});

app.Run();
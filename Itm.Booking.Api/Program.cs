using System.Net;
using System.Net.Http.Json;
using Itm.Booking.Api.Dtos;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// HttpClientFactory + Resiliencia obligatoria
builder.Services.AddHttpClient("EventClient", client =>
{
    client.BaseAddress = new Uri("https://localhost:7001"); 
})
.AddStandardResilienceHandler();

builder.Services.AddHttpClient("DiscountClient", client =>
{
    client.BaseAddress = new Uri("https://localhost:7002");
})
.AddStandardResilienceHandler();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapPost("/api/bookings", async (BookingRequest request, IHttpClientFactory factory) =>
{
    if (request.EventId <= 0)
        return Results.BadRequest("El EventId debe ser válido.");

    if (request.Tickets <= 0)
        return Results.BadRequest("La cantidad de tickets debe ser mayor a cero.");

    var eventClient = factory.CreateClient("EventClient");
    var discountClient = factory.CreateClient("DiscountClient");

    // 1. LECTURA EN PARALELO
    var eventTask = eventClient.GetAsync($"/api/events/{request.EventId}");
    Task<HttpResponseMessage?> discountTask;

    if (string.IsNullOrWhiteSpace(request.DiscountCode))
    {
        discountTask = Task.FromResult<HttpResponseMessage?>(null);
    }
    else
    {
        discountTask = discountClient.GetAsync($"/api/discounts/{request.DiscountCode}");
    }

    await Task.WhenAll(eventTask, discountTask!);

    var eventResponse = await eventTask;
    var discountResponse = await discountTask!;

    if (!eventResponse.IsSuccessStatusCode)
        return Results.BadRequest("El evento no existe o no está disponible.");

    var eventDto = await eventResponse.Content.ReadFromJsonAsync<EventDto>();
    if (eventDto is null)
        return Results.Problem("No fue posible leer la información del evento.");

    DiscountDto? discountDto = null;

    // Si el descuento no existe, no tumbamos la compra:
    // simplemente tomamos descuento 0
    if (discountResponse is not null)
    {
        if (discountResponse.StatusCode == HttpStatusCode.NotFound)
        {
            discountDto = null;
        }
        else if (discountResponse.IsSuccessStatusCode)
        {
            discountDto = await discountResponse.Content.ReadFromJsonAsync<DiscountDto>();
        }
        else
        {
            return Results.Problem("Error consultando el servicio de descuentos.");
        }
    }

    // 2. MATEMÁTICAS
    decimal subtotal = eventDto.PrecioBase * request.Tickets;
    decimal porcentaje = discountDto?.Porcentaje ?? 0m;
    decimal descuentoAplicado = subtotal * porcentaje;
    decimal totalPagar = subtotal - descuentoAplicado;

    // 3. RESERVA (Inicio SAGA)
    var reserveRequest = new ReserveRequestDto(request.EventId, request.Tickets);

    var reserveResponse = await eventClient.PostAsJsonAsync("/api/events/reserve", reserveRequest);

    if (!reserveResponse.IsSuccessStatusCode)
        return Results.BadRequest("No hay sillas suficientes o el evento no existe.");

    try
    {
        // 4. SIMULACIÓN DE PAGO
        bool paymentSuccess = Random.Shared.Next(1, 11) > 5;

        if (!paymentSuccess)
            throw new Exception("Fondos insuficientes en la tarjeta de crédito.");

        var response = new BookingResponseDto(
            Status: "Éxito",
            Message: "¡Disfruta el concierto ITM!",
            EventId: request.EventId,
            Tickets: request.Tickets,
            PrecioUnitario: eventDto.PrecioBase,
            Subtotal: subtotal,
            DescuentoAplicado: descuentoAplicado,
            TotalPagar: totalPagar
        );

        return Results.Ok(response);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[SAGA] Error en pago: {ex.Message}. Liberando sillas...");

        var releaseResponse = await eventClient.PostAsJsonAsync("/api/events/release", reserveRequest);

        if (!releaseResponse.IsSuccessStatusCode)
        {
            return Results.Problem(
                "El pago falló y además no se pudo ejecutar correctamente la compensación. Revisar Event.Api.");
        }

        return Results.Problem(
            detail: "Tu pago fue rechazado. No te preocupes, no te cobramos y tus sillas fueron liberadas.",
            title: "Pago rechazado",
            statusCode: 500);
    }
});

app.Run();
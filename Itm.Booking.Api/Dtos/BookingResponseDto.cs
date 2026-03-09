namespace Itm.Booking.Api.Dtos;

public record BookingResponseDto(
    string Status,
    string Message,
    int EventId,
    int Tickets,
    decimal PrecioUnitario,
    decimal Subtotal,
    decimal DescuentoAplicado,
    decimal TotalPagar
);
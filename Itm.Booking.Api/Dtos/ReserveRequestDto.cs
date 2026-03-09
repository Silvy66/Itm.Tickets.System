namespace Itm.Booking.Api.Dtos;

public record ReserveRequestDto(
    int EventId,
    int Quantity
);
namespace Itm.Event.Api.Dtos;

public record ReserveRequestDto(
    int EventId,
    int Quantity
);
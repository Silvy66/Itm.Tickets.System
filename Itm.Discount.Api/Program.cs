using Itm.Discount.Api.Dtos;
using Itm.Discount.Api.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

var discounts = new List<DiscountItem>
{
    new()
    {
        Codigo = "ITM50",
        Porcentaje = 0.5m
    }
};

app.MapGet("/api/discounts/{code}", (string code) =>
{
    var discount = discounts.FirstOrDefault(d =>
        d.Codigo.Equals(code, StringComparison.OrdinalIgnoreCase));

    if (discount is null)
        return Results.NotFound(new { Message = "Código de descuento no existe." });

    var dto = new DiscountDto(discount.Codigo, discount.Porcentaje);
    return Results.Ok(dto);
});

app.Run();
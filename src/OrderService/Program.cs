using OrderService.Messaging;
using OrderService.Models;
using OrderService.Validation;
using Serilog;
using Shared.Messaging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSerilog(configuration => configuration
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .Enrich.WithProperty("MachineName", Environment.MachineName)
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss.fff} {Level:u3}] ({MachineName}) {Message:lj}{NewLine}{Exception}"));

builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMq"));
builder.Services.AddSingleton<IRabbitMqConnectionFactory, RabbitMqConnectionFactory>();
builder.Services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();
builder.Services.AddHostedService<RabbitMqPublisherInitializer>();

var app = builder.Build();

app.MapPost("/orders", async (CreateOrderRequest? request, IRabbitMqPublisher publisher,
    CancellationToken cancellationToken) =>
{
    var validationErrors = OrderRequestValidator.Validate(request);
    if (validationErrors.Count > 0)
    {
        return Results.BadRequest(new { errors = validationErrors });
    }

    var order = new Order
    {
        OrderId = Guid.NewGuid(),
        Sku = request!.Sku.Trim(),
        Quantity = request.Quantity,
        CustomerEmail = request.CustomerEmail,
        Status = "submitted",
    };

    var orderCreated = new OrderCreatedEvent
    {
        EventId = Guid.NewGuid(),
        EventType = OrderCreatedEvent.EventTypeValue,
        OccurredAt = DateTimeOffset.UtcNow,
        OrderId = order.OrderId,
        Sku = order.Sku,
        Quantity = order.Quantity,
        CustomerEmail = order.CustomerEmail,
    };

    try
    {
        await publisher.PublishAsync(orderCreated, OrderCreatedEvent.RoutingKey, cancellationToken);
    }
    catch (Exception exception)
    {
        return Results.Problem(
            $"failed to publish {OrderCreatedEvent.EventTypeValue} event: {exception.Message}",
            statusCode: StatusCodes.Status502BadGateway);
    }

    return Results.Accepted(value: new OrderAcceptedResponse(order.OrderId, order.Status));
});

app.MapGet("/health", (IRabbitMqPublisher publisher) => publisher.IsConnected
    ? Results.Ok(new { status = "ok", rabbitMq = "connected" })
    : Results.Problem(title: "RabbitMQ connection is not open",
        statusCode: StatusCodes.Status503ServiceUnavailable));

app.Run();

internal sealed class RabbitMqPublisherInitializer(IRabbitMqPublisher publisher) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        publisher.InitializeAsync(stoppingToken);
}

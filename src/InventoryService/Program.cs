using InventoryService.Messaging;
using InventoryService.Services;
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
builder.Services.Configure<ConsumingOptions>(builder.Configuration.GetSection(ConsumingOptions.SectionName));
builder.Services.AddSingleton<IRabbitMqConnectionFactory, RabbitMqConnectionFactory>();
builder.Services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();
builder.Services.AddSingleton<IStockTable, StockTable>();
builder.Services.AddSingleton<IProcessedOrderTable>(_ => new ProcessedOrderTable());
builder.Services.AddHostedService<RabbitMqPublisherInitializer>();
builder.Services.AddHostedService<OrderCreatedConsumer>();

var app = builder.Build();

app.MapGet("/stock", (IStockTable stockTable) => Results.Ok(stockTable.Snapshot()
    .Select(item => new { item.Sku, item.Quantity })));

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

using NotificationService.Messaging;
using NotificationService.Services;
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
builder.Services.AddSingleton<INotificationLog, NotificationLog>();
builder.Services.AddSingleton<IProcessedEventTable>(_ => new ProcessedEventTable());
builder.Services.AddHostedService<OrderReservedConsumer>();
builder.Services.AddHostedService<OrderRejectedConsumer>();

var app = builder.Build();

const int DefaultPageSize = 100;
const int MaxPageSize = NotificationLog.DefaultCapacity;

app.MapGet("/notifications", (INotificationLog notificationLog, int? limit, int? offset) =>
{
    var records = notificationLog.LatestFirst();
    var skip = Math.Max(offset ?? 0, 0);
    var take = Math.Clamp(limit ?? DefaultPageSize, 1, MaxPageSize);

    return Results.Ok(new
    {
        total = records.Count,
        items = records.Skip(skip).Take(take),
    });
});

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

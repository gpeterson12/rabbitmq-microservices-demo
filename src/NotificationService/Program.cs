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

app.MapGet("/notifications", (INotificationLog notificationLog) =>
    Results.Ok(notificationLog.LatestFirst()));

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

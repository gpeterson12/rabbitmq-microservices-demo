var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => "NotificationService placeholder");

app.Run();

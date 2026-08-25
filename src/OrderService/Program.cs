var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => "OrderService placeholder");

app.Run();

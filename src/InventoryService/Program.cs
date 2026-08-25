var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => "InventoryService placeholder");

app.Run();

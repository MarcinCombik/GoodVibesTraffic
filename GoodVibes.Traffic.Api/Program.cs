using GoodVibes.Traffic.Api.ws;
using GoodVibes.Traffic.Api.Ws;
using GoodVibes.Traffic.Domain;
using Newtonsoft.Json;


var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<WebSocketConnectionManager>();
builder.Services.AddSingleton<WebSocketHandler>();
// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy
            .AllowAnyOrigin()   // Pozwala na dowolne źródło (domena, port, itp.)
            .AllowAnyMethod()   // Pozwala na dowolne metody HTTP (GET, POST, PUT, DELETE, ...)
            .AllowAnyHeader();  // Pozwala na dowolne nagłówki
    });
});
var app = builder.Build();
app.UseCors("AllowAll");

app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30),
    // AllowedOrigins, ReceiveBufferSize etc. możesz dopasować
});

app.Map("/ws", async (HttpContext http, WebSocketConnectionManager manager, WebSocketHandler handler) =>
{
    if (!http.WebSockets.IsWebSocketRequest)
    {
        http.Response.StatusCode = 400;
        return;
    }

    var socket = await http.WebSockets.AcceptWebSocketAsync();
    var connectionId = manager.AddSocket(socket);
    try
    {
        await handler.ReceiveAsync(connectionId, socket);
    }
    finally
    {
        manager.RemoveSocket(connectionId);
    }
});

app.MapGet("/", () => "WebSocket server is running. Connect to /ws");
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
    {
        var forecast = Enumerable.Range(1, 5).Select(index =>
                new WeatherForecast
                (
                    DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                    Random.Shared.Next(-20, 55),
                    summaries[Random.Shared.Next(summaries.Length)]
                ))
            .ToArray();
        return forecast;
    })
    .WithName("GetWeatherForecast");

app.MapGet("/ships", () =>
    {
        var ships = JsonConvert.DeserializeObject<IEnumerable<ShipPosition>>(File.ReadAllText("ships.json"));
        return ships;
    })
    .WithName("GetShips");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
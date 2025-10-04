using System.Net.WebSockets;
using System.Text;
using GoodVibes.Traffic.Api.ws;
using GoodVibes.Traffic.Api.Ws;
using GoodVibes.Traffic.Domain;
using Newtonsoft.Json;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<WebSocketConnectionManager>();
builder.Services.AddSingleton<WebSocketHandler>();
builder.Host.UseSerilog((context, loggerConfig) => loggerConfig.ReadFrom.Configuration(context.Configuration));

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

var manager = app.Services.GetRequiredService<WebSocketConnectionManager>();

// 1️⃣ Local WS endpoint for Angular
app.Map("/ws", async (HttpContext context) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        return;
    }

    var socket = await context.WebSockets.AcceptWebSocketAsync();
    var id = manager.AddSocket(socket);

    // optional: send welcome
    await manager.SendToAsync(id, "{ \"msg\": \"Connected to local WS bridge\" }");

    // keep socket open (could use WebSocketHandler for logic)
    var buffer = new byte[1024 * 4];
    while (socket.State == WebSocketState.Open)
    {
        var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        if (result.MessageType == WebSocketMessageType.Close)
        {
            manager.RemoveSocket(id);
            break;
        }
    }
});

// 2️⃣ Connect to external AISStream WS
_ = Task.Run(async () =>
{
    using var clientWs = new ClientWebSocket();
    await clientWs.ConnectAsync(new Uri("wss://stream.aisstream.io/v0/stream"), CancellationToken.None);
    var apikey = builder.Configuration.GetValue<string>("AISStream");
    // send API key + initial message
    var initMsg = $@"{{
    ""Apikey"": ""{apikey}"",
    ""BoundingBoxes"": [[[53.0, 9.5], [66.0, 30.0]]],
    ""FilterMessageTypes"": [""PositionReport"", ""StaticDataReport""]
}}";
    await clientWs.SendAsync(Encoding.UTF8.GetBytes(initMsg), WebSocketMessageType.Text, true, CancellationToken.None);

    var buffer = new byte[8192];
    while (clientWs.State == WebSocketState.Open)
    {
        var result = await clientWs.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        if (result.MessageType == WebSocketMessageType.Text)
        {
            var msg = Encoding.UTF8.GetString(buffer, 0, result.Count);
            // broadcast to all connected local clients
            await manager.BroadcastAsync(msg);
            Console.WriteLine("📡 AIS message broadcasted");
        }
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
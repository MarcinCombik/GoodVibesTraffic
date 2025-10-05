using System.Net.WebSockets;
using System.Text;
using GoodVibes.Traffic.Api.ws;
using GoodVibes.Traffic.Application;
using GoodVibes.Traffic.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using GoodVibes.Traffic.Infrastructure;
using Newtonsoft.Json;

var builder = WebApplication.CreateBuilder(args);

// Konfiguracja API key w appsettings.json
// "AISStream": "TWÓJ_API_KEY"
var apiKey = builder.Configuration.GetValue<string>("AISStream");

builder.Services.AddSingleton<WebSocketConnectionManager>();
builder.Services.AddSingleton<WebSocketHandler>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

builder.Services.AddOpenAiClient(builder.Configuration.GetValue<string>("OpenAIApiKey")!, builder.Configuration.GetValue<string>("OpenAIOrganizationId")!);
builder.Services.AddScoped<IOpenAiApiClient, OpenAiApiClient>();

var app = builder.Build();
app.UseCors("AllowAll");

app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30),
    AllowedOrigins = { "https://good-vibes-traffic.netlify.app" }
});



var manager = app.Services.GetRequiredService<WebSocketConnectionManager>();

// 1️⃣ Local WS endpoint for Angular / Node.js
app.Map("/ws", async (HttpContext context) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();

    
    Console.WriteLine($"Web socket has been started");
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        return;
    }
    Console.WriteLine($"Accept web socket");
    var socket = await context.WebSockets.AcceptWebSocketAsync();
    logger.LogInformation("New WebScocket connection {Path}", context.Request.Path);

    Console.WriteLine($"ws id");
    var id = manager.AddSocket(socket);
    logger.LogInformation(id);

    logger.LogInformation($"Client connected: {id} | Total clients: {manager.GetAllIds().Count()}");

    Console.WriteLine(id);    
    Console.WriteLine($"Client connected: {id} | Total clients: {manager.GetAllIds().Count()}");

    await manager.SendToAsync(id, "{ \"msg\": \"Connected to local WS bridge\" }");

    var buffer = new byte[32 * 1024]; // większy bufor
    while (socket.State == WebSocketState.Open)
    {
        var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        if (result.MessageType == WebSocketMessageType.Close)
        {
            manager.RemoveSocket(id);
            logger.LogInformation("client");
            break;
        }
    }
});

// 2️⃣ Connect to external AISStream WS
_ = Task.Run(async () =>
{
    Console.WriteLine("Start logic");
    using var clientWs = new ClientWebSocket();
    await clientWs.ConnectAsync(new Uri("wss://stream.aisstream.io/v0/stream"), CancellationToken.None);
    
    // Wyślij API key + konfigurację
    var initMsg = $@"{{
        ""Apikey"": ""{apiKey}"",
        ""BoundingBoxes"": [[[53.0, 9.5], [66.0, 30.0]]],
        ""FilterMessageTypes"": [""PositionReport"", ""StaticDataReport""]
    }}";

    Console.WriteLine("Sending init message to AISStream...");
    await clientWs.SendAsync(
        Encoding.UTF8.GetBytes(initMsg),
        WebSocketMessageType.Text,
        true,
        CancellationToken.None
    );

    var buffer = new byte[32 * 1024];
    while (clientWs.State == WebSocketState.Open)
    {
        var result = await clientWs.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

        string msg;
        await manager.BroadcastAsync((@$"logowanie apikey: {apiKey}"));
        if (result.MessageType == WebSocketMessageType.Text)
        {
            msg = Encoding.UTF8.GetString(buffer, 0, result.Count);
        }
        else if (result.MessageType == WebSocketMessageType.Binary)
        {
            // jeśli wiadomość binarna to np. UTF8 w środku, dekoduj
            msg = Encoding.UTF8.GetString(buffer, 0, result.Count);

            // jeśli to czysty binarny format np. Protobuf, musisz sparsować odpowiednio
            // msg = Convert.ToBase64String(buffer, 0, result.Count); // opcjonalnie
        }
        else if (result.MessageType == WebSocketMessageType.Close)
        {
            await clientWs.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by server", CancellationToken.None);
            break;
        }
        else
        {
            continue; // inne typy ignorujemy
        }

        // broadcast do lokalnych klientów
        await manager.BroadcastAsync(msg);
        Console.WriteLine($"📡 AIS message broadcasted to {manager.GetAllIds().Count()} clients | Length: {msg.Length}");
    }

});

app.Map("/ws-test", async context =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();

    if (context.WebSockets.IsWebSocketRequest)
    {
        using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        logger.LogInformation("Nowe połączenie WebSocket na {Path}", context.Request.Path);

        try
        {
            var buffer = new byte[1024 * 4];
            WebSocketReceiveResult result;

            do
            {
                result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), context.RequestAborted);
                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                logger.LogInformation("Odebrano wiadomość: {Message}", message);
            }
            while (!result.CloseStatus.HasValue);

            await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, context.RequestAborted);
            logger.LogInformation("Połączenie WebSocket zamknięte: {Reason}", result.CloseStatusDescription);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Błąd podczas obsługi WebSocket");
        }
    }
    else
    {
        context.Response.StatusCode = 400;
    }
});

app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    try
    {
        await next.Invoke();
    }
    catch (WebSocketException wsEx)
    {
        logger.LogError(wsEx, "WebSocketException przechwycony globalnie");
        context.Response.StatusCode = 500;
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Nieoczekiwany błąd");
        context.Response.StatusCode = 500;
    }
});

app.MapGet("/ships", () =>
    {
        var ships = JsonConvert.DeserializeObject<IEnumerable<ShipPosition>>(File.ReadAllText("ships-demo.json"));
        return ships;
    })
    .WithName("GetShips");

app.MapGet("/alerts", () =>
    {
        var ships = JsonConvert.DeserializeObject<IEnumerable<Alert>>(File.ReadAllText("alerts.json"));
        return ships;
    })
    .WithName("GetAlerts");

app.MapPost("/checkPositions", (List<ShipPosition> positions) => Results.Ok((object?)positions));

app.MapGet("/", () => "WebSocket server is running. Connect to /ws");

app.MapPost("/OpenAiApiRequest/Alerts", async (IOpenAiApiClient client) =>
{
    var alerts = "[";

    string fileContent = File.ReadAllText("ships-analitic.json");

    int chunkSize = 300000;
    int totalLength = fileContent.Length;
    

    // var fileId = await client.UploadFile(jsonString);

    for (int i = 0; i < totalLength; i += chunkSize)
    {
        string chunk = fileContent.Substring(i, Math.Min(chunkSize, totalLength - i));

        OpenAiRequest request = new($@"
                                    You are a system that monitors maritime situations and you must return responses exclusively in JSON format but without [ ].
                                    The JSON structure must look like this:
                                    {{
                                        ""ALERT_TYPE"": ""<TYP_ALERTU: WARNING / DANGER >"",
                                        ""SHIP_ID"": ""<ID_STATKU"",
                                        ""REASON"": ""<DESCRIPTION>""
                                    }} separated by commas.
                                    Gnerate short reason.
                                    Genereate at least five alerts.
                                    Set ALERT_TYPE as WARNING when reason is contains speed.
                                    The data to analyze is: ""{chunk}""""
                                    ");
        var result = await client.GetResponse<OpenAiResponse>(request.Prompt);
        if (i > 0)
        {
            alerts += ",";
        }

        alerts += result ;
    }

    // var alerts = JsonConvert.DeserializeObject<Alert>(finalSummary);
    alerts += "]";

    return alerts;
});

app.Run();



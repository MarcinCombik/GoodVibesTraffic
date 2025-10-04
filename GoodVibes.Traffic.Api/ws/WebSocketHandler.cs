using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using GoodVibes.Traffic.Api.ws;

namespace GoodVibes.Traffic.Api.Ws 
{
    public class WebSocketHandler
    {
        private readonly WebSocketConnectionManager _manager;
        private const int BufferSize = 4 * 1024; // 4 KB bufor

        public WebSocketHandler(WebSocketConnectionManager manager)
        {
            _manager = manager;
        }

        // Główna pętla obsługująca komunikację z klientem
        public async Task ReceiveAsync(string connectionId, WebSocket socket)
        {
            var buffer = new byte[BufferSize];
            var segment = new ArraySegment<byte>(buffer);

            while (socket.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result;
                using var ms = new MemoryStream();

                do
                {
                    result = await socket.ReceiveAsync(segment, CancellationToken.None);
                    ms.Write(segment.Array!, segment.Offset, result.Count);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed", CancellationToken.None);
                        _manager.RemoveSocket(connectionId);
                        return;
                    }
                } while (!result.EndOfMessage);

                ms.Seek(0, SeekOrigin.Begin);
                var message = Encoding.UTF8.GetString(ms.ToArray());

                // Prosty format: { "type": "broadcast", "payload": "Hello" }
                try
                {
                    var json = JsonDocument.Parse(message);
                    var type = json.RootElement.GetProperty("type").GetString();
                    var payload = json.RootElement.TryGetProperty("payload", out var payloadEl)
                        ? payloadEl.GetRawText()
                        : "\"\"";

                    switch (type)
                    {
                        case "broadcast":
                            await _manager.BroadcastAsync(payload);
                            break;

                        case "echo":
                            await _manager.SendToAsync(connectionId, payload);
                            break;

                        default:
                            // Nieznany typ - broadcast
                            await _manager.BroadcastAsync(message);
                            break;
                    }
                }
                catch
                {
                    // Nie JSON - broadcast jako tekst
                    await _manager.BroadcastAsync(message);
                }
            }
        }
    }
}

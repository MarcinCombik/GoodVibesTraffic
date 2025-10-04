using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace GoodVibes.Traffic.Api.ws
{
    public class WebSocketHandler
    {
        private readonly WebSocketConnectionManager _manager;
        public WebSocketHandler(WebSocketConnectionManager manager) => _manager = manager;

        public async Task ReceiveAsync(string connectionId, WebSocket socket)
        {
            var buffer = new byte[32 * 1024];
            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _manager.RemoveSocket(connectionId);
                    break;
                }

                var msg = Encoding.UTF8.GetString(buffer, 0, result.Count);
                Console.WriteLine($"Received from client {connectionId}: {msg}");
            }
        }
    }
}
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;

namespace GoodVibes.Traffic.Api.ws
{
    public class WebSocketConnectionManager
    {
        private readonly ConcurrentDictionary<string, WebSocket> _sockets = new();

        public string AddSocket(WebSocket socket)
        {
            var id = Guid.NewGuid().ToString();
            _sockets[id] = socket;
            return id;
        }

        public void RemoveSocket(string id)
        {
            if (_sockets.TryRemove(id, out var socket))
            {
                if (socket.State == WebSocketState.Open)
                    socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by server", CancellationToken.None).Wait();
            }
        }

        public WebSocket? GetSocketById(string id)
        {
            _sockets.TryGetValue(id, out var socket);
            return socket;
        }

        public IEnumerable<string> GetAllIds() => _sockets.Keys;

        public async Task BroadcastAsync(string message)
        {
            var buffer = Encoding.UTF8.GetBytes(message);
            var tasks = _sockets.Values
                .Where(s => s.State == WebSocketState.Open)
                .Select(s => s.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None));
            await Task.WhenAll(tasks);
        }

        public async Task SendToAsync(string id, string message)
        {
            var socket = GetSocketById(id);
            if (socket == null || socket.State != WebSocketState.Open) return;

            var buffer = Encoding.UTF8.GetBytes(message);
            await socket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}
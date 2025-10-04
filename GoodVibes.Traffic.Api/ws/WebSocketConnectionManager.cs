using System.Collections.Concurrent;
using System.Net.WebSockets;

namespace GoodVibes.Traffic.Api.ws
{
    public class WebSocketConnectionManager
    {
        private readonly ConcurrentDictionary<string, WebSocket> _sockets = new();

        // Dodaje nowe połączenie i zwraca jego ID
        public string AddSocket(WebSocket socket)
        {
            var id = Guid.NewGuid().ToString();
            _sockets[id] = socket;
            return id;
        }

        // Zwraca WebSocket po ID
        public WebSocket? GetSocketById(string id)
        {
            _sockets.TryGetValue(id, out var socket);
            return socket;
        }

        // Zwraca wszystkie aktywne ID połączeń
        public IEnumerable<string> GetAllIds() => _sockets.Keys;

        // Usuwa socket z listy i zamyka połączenie
        public void RemoveSocket(string id)
        {
            if (_sockets.TryRemove(id, out var socket))
            {
                if (socket.State == WebSocketState.Open)
                {
                    socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by server", CancellationToken.None).Wait();
                }
            }
        }

        // Wysyła wiadomość do wszystkich klientów
        public async Task BroadcastAsync(string message)
        {
            var buffer = System.Text.Encoding.UTF8.GetBytes(message);
            var segment = new ArraySegment<byte>(buffer);

            var tasks = _sockets.Values
                .Where(s => s.State == WebSocketState.Open)
                .Select(s => s.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None));

            await Task.WhenAll(tasks);
        }

        // Wysyła wiadomość tylko do jednego klienta
        public async Task SendToAsync(string id, string message)
        {
            var socket = GetSocketById(id);
            if (socket == null || socket.State != WebSocketState.Open)
                return;

            var buffer = System.Text.Encoding.UTF8.GetBytes(message);
            await socket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}

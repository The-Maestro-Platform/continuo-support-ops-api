using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using SupportOpsApi.Endpoints.Contracts;

namespace SupportOpsApi.Services;

public sealed class NotificationHub {
    private readonly ConcurrentDictionary<Guid, WebSocket> _sockets = new();
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

    public void Register(Guid id, WebSocket socket) => _sockets.TryAdd(id, socket);

    public void Remove(Guid id) {
        if (_sockets.TryRemove(id, out var socket)) {
            try {
                socket.Abort();
            }
            catch {
                // ignore
            }
        }
    }

    public async Task BroadcastAsync(WorkItemNotificationDto notification, CancellationToken ct = default) {
        var payload = JsonSerializer.Serialize(new { type = "notification", data = notification }, _serializerOptions);
        var bytes = Encoding.UTF8.GetBytes(payload);

        foreach (var entry in _sockets.ToArray()) {
            var socket = entry.Value;
            if (socket.State != WebSocketState.Open) {
                Remove(entry.Key);
                continue;
            }

            try {
                await socket.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
            }
            catch {
                Remove(entry.Key);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WebsocketManager
{
    public abstract class WebsocketHandler
    {
        protected WebsocketConnection WebSocketConnection { get; set; }

        public WebsocketHandler(WebsocketConnection socketManager)
        {
            this.WebSocketConnection = socketManager;
        }

        public virtual async Task Connected(WebSocket socket)
        {
            await Task.Delay(10);
            this.WebSocketConnection.AddSocket(socket);
        }

        public virtual async Task Disconnected(WebSocket socket)
        {
            var id = this.WebSocketConnection.GetSocketId(socket);
            await this.WebSocketConnection.RemoveSocket(id);
        }

        public async Task SendMessageAsync(WebSocket socket, string message)
        {
            if (socket == null)
                return;
            Debug.Print(message);
            if (socket.State != WebSocketState.Open) { return; }
            var bytes = Encoding.UTF8.GetBytes(message);
            var buffer = new ArraySegment<byte>(bytes, 0, bytes.Length);
            await socket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
        }

        public async Task SendMessageAsync(string socketId, string message)
        {
            var socket = this.WebSocketConnection.GetSocketById(socketId);
            await SendMessageAsync(socket, message);
        }

        public async Task SendMessageToAllAsync(string message)
        {
            foreach (var socket in WebSocketConnection.GetAllSockets())
            {
                if (socket.Value.State == WebSocketState.Open)
                {
                    await SendMessageAsync(socket.Value, message);
                }
            }
        }        

        public abstract Task ReceiveAsync(WebSocket socket, WebSocketReceiveResult result, byte[] buffer);
    }
}

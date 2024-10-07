using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace WebsocketManager
{
    public class WebsocketMiddleware
    {
        private readonly RequestDelegate _next;
        private WebsocketHandler _webSocketHandler { get; set; }
        public WebsocketMiddleware(RequestDelegate next, WebsocketHandler webSocketHandler)
        {
            this._next = next;
            this._webSocketHandler = webSocketHandler;
        }

        public async Task Invoke(HttpContext context)
        {
            if (!context.WebSockets.IsWebSocketRequest) { return; }
            var socket = await context.WebSockets.AcceptWebSocketAsync();
            await this._webSocketHandler.Connected(socket);
            await Receive(socket, async (result, buffer) =>
            {
                var str = System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count);
                Debug.Print(str);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    await this._webSocketHandler.ReceiveAsync(socket, result, buffer);
                    return;
                }                
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    await this._webSocketHandler.Disconnected(socket);
                    return;
                }
            });
        }

        private async Task Receive(WebSocket socket, Action<WebSocketReceiveResult, byte[]> handleMessage)
        {
            const int BUFFER_LENGTG = 4096;
            var buffer = new byte[BUFFER_LENGTG];
            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                handleMessage(result, buffer);
            }
        }
    }
}

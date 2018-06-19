using System;
using System.Threading;
using System.Threading.Tasks;
using System.Net.WebSockets;
using System.IO;

namespace Lib.AspNetCore.WebSocketsCompression.Net.WebSockets
{
    internal class CompressionWebSocket : WebSocket
    {
        #region Fields
        private readonly WebSocket _webSocket;
        #endregion

        #region Properties
        public override WebSocketCloseStatus? CloseStatus => _webSocket.CloseStatus;

        public override string CloseStatusDescription => _webSocket.CloseStatusDescription;

        public override WebSocketState State => _webSocket.State;

        public override string SubProtocol => _webSocket.SubProtocol;
        #endregion

        #region Constructor
        internal CompressionWebSocket(Stream stream, string subprotocol, TimeSpan keepAliveInterval)
        {
            _webSocket = WebSocketProtocol.CreateFromStream(stream, true, subprotocol, keepAliveInterval);
        }
        #endregion

        #region Methods
        public override void Abort()
        {
            _webSocket.Abort();
        }

        public override Task CloseAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken)
        {
            return _webSocket.CloseAsync(closeStatus, statusDescription, cancellationToken);
        }

        public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken)
        {
            return _webSocket.CloseOutputAsync(closeStatus, statusDescription, cancellationToken);
        }

        public override void Dispose()
        {
            _webSocket.Dispose();
        }

        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            return _webSocket.ReceiveAsync(buffer, cancellationToken);
        }

        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            return _webSocket.SendAsync(buffer, messageType, endOfMessage, cancellationToken);
        }

        public Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool compressed, bool endOfMessage, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
        #endregion
    }
}

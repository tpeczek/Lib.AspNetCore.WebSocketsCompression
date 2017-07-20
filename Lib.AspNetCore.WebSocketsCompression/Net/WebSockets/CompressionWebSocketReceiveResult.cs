using System.Net.WebSockets;

namespace Lib.AspNetCore.WebSocketsCompression.Net.WebSockets
{
    /// <summary>
    /// An instance of this class represents the result of performing a single ReceiveAsync operation on a CompressionWebSocket.
    /// </summary>
    public class CompressionWebSocketReceiveResult : WebSocketReceiveResult
    {
        #region Properties
        /// <summary>
        /// Indicates whether the message is compressed.
        /// </summary>
        public bool Compressed { get; }
        #endregion

        #region Constructors
        /// <summary>
        /// Creates an instance of the <see cref="CompressionWebSocketReceiveResult"/> class.
        /// </summary>
        /// <param name="count">The number of bytes received.</param>
        /// <param name="messageType">The type of message that was received.</param>
        /// <param name="compressed">Indicates whether this is compressed message.</param>
        /// <param name="endOfMessage">Indicates whether this is the final message.</param>
        public CompressionWebSocketReceiveResult(int count, WebSocketMessageType messageType, bool compressed, bool endOfMessage)
            : base(count, messageType, endOfMessage)
        {
            Compressed = compressed;
        }

        /// <summary>
        /// Creates an instance of the <see cref="CompressionWebSocketReceiveResult"/> class.
        /// </summary>
        /// <param name="count">The number of bytes received.</param>
        /// <param name="messageType">The type of message that was received.</param>
        /// <param name="endOfMessage">Indicates whether this is the final message.</param>
        /// <param name="closeStatus">Indicates the WebSocketCloseStatus of the connection.</param>
        /// <param name="closeStatusDescription">The description of the WebSocketCloseStatus of the connection.</param>
        public CompressionWebSocketReceiveResult(int count, WebSocketMessageType messageType, bool endOfMessage, WebSocketCloseStatus? closeStatus, string closeStatusDescription)
            : base(count, messageType, endOfMessage, closeStatus, closeStatusDescription)
        {
            Compressed = false;
        }
        #endregion
    }
}

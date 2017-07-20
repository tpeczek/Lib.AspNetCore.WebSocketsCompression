using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Lib.AspNetCore.WebSocketsCompression.Providers
{
    /// <summary>
    /// An interface representing WebSocket per-message compression provider. 
    /// </summary>
    public interface IWebSocketCompressionProvider
    {
        /// <summary>
        /// Compresses and sends binary message over the WebSocket connection asynchronously.
        /// </summary>
        /// <param name="webSocket">The WebSocket representing the connection.</param>
        /// <param name="message">The binary message to be sent over the connection.</param>
        /// <param name="cancellationToken">The token that propagates the notification that operations should be canceled.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        Task CompressBinaryMessageAsync(WebSocket webSocket, byte[] message, CancellationToken cancellationToken);

        /// <summary>
        /// Compresses and sends text message over the WebSocket connection asynchronously.
        /// </summary>
        /// <param name="webSocket">The WebSocket representing the connection.</param>
        /// <param name="message">The text message to be sent over the connection.</param>
        /// <param name="cancellationToken">The token that propagates the notification that operations should be canceled.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        Task CompressTextMessageAsync(WebSocket webSocket, string message, CancellationToken cancellationToken);

        /// <summary>
        /// Receives and decompresses binary message from the WebSocket connection asynchronously.
        /// </summary>
        /// <param name="webSocket">The WebSocket representing the connection.</param>
        /// <param name="webSocketReceiveResult">The result of performing a first ReceiveAsync operation for this binary message.</param>
        /// <param name="receivePayloadBuffer">The application buffer which was the storage location for first ReceiveAsync operation and will be internally used for subsequent ones in context of this binary message.</param>
        /// <returns>The task object representing the asynchronous operation. The Result property on the task object returns a Byte array containing the received message.</returns>
        Task<byte[]> DecompressBinaryMessageAsync(WebSocket webSocket, WebSocketReceiveResult webSocketReceiveResult, byte[] receivePayloadBuffer);

        /// <summary>
        /// Receives and decompresses text message from the WebSocket connection asynchronously.
        /// </summary>
        /// <param name="webSocket">The WebSocket representing the connection.</param>
        /// <param name="webSocketReceiveResult">The result of performing a first ReceiveAsync operation for this text message.</param>
        /// <param name="receivePayloadBuffer">The application buffer which was the storage location for first ReceiveAsync operation and will be internally used for subsequent ones in context of this text message.</param>
        /// <returns>The task object representing the asynchronous operation. The Result property on the task object returns a String containing the received message.</returns>
        Task<string> DecompressTextMessageAsync(WebSocket webSocket, WebSocketReceiveResult webSocketReceiveResult, byte[] receivePayloadBuffer);
    }
}

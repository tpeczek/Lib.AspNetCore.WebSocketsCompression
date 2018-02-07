using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.WebSockets;
using System.Collections.Generic;
using Lib.AspNetCore.WebSocketsCompression.Net.WebSockets;
using System.IO;

namespace Lib.AspNetCore.WebSocketsCompression.Providers
{
    /// <summary>
    /// Identity (no compression) WebSocket per-message compression provider. 
    /// </summary>
    public class WebSocketIdentityCompressionProvider : IWebSocketCompressionProvider
    {
        #region Fields
        private readonly int? _sendSegmentSize;
        #endregion

        #region Constructor
        /// <summary>
        /// Initializes new instance of <see cref="WebSocketIdentityCompressionProvider"/>.
        /// </summary>
        public WebSocketIdentityCompressionProvider()
            : this(null)
        { }

        /// <summary>
        /// Initializes new instance of <see cref="WebSocketIdentityCompressionProvider"/>.
        /// </summary>
        /// <param name="sendSegmentSize">The maximum size of single segment which will be sent over the WebSocket connection.</param>
        public WebSocketIdentityCompressionProvider(int? sendSegmentSize)
        {
            _sendSegmentSize = sendSegmentSize;
        }
        #endregion

        #region Methods
        /// <summary>
        /// Compresses and sends binary message over the WebSocket connection asynchronously.
        /// </summary>
        /// <param name="webSocket">The WebSocket representing the connection.</param>
        /// <param name="message">The binary message to be sent over the connection.</param>
        /// <param name="cancellationToken">The token that propagates the notification that operations should be canceled.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public virtual Task CompressBinaryMessageAsync(WebSocket webSocket, byte[] message, CancellationToken cancellationToken)
        {
            return SendMessageAsync(webSocket, message, WebSocketMessageType.Binary, false, cancellationToken);
        }

        /// <summary>
        /// Compresses and sends text message over the WebSocket connection asynchronously.
        /// </summary>
        /// <param name="webSocket">The WebSocket representing the connection.</param>
        /// <param name="message">The text message to be sent over the connection.</param>
        /// <param name="cancellationToken">The token that propagates the notification that operations should be canceled.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public virtual Task CompressTextMessageAsync(WebSocket webSocket, string message, CancellationToken cancellationToken)
        {
            return SendMessageAsync(webSocket, Encoding.UTF8.GetBytes(message), WebSocketMessageType.Text, false, cancellationToken);
        }

        /// <summary>
        /// Receives and decompresses binary message from the WebSocket connection asynchronously.
        /// </summary>
        /// <param name="webSocket">The WebSocket representing the connection.</param>
        /// <param name="webSocketReceiveResult">The result of performing a first ReceiveAsync operation for this binary message.</param>
        /// <param name="receivePayloadBuffer">The application buffer which was the storage location for first ReceiveAsync operation and will be internally used for subsequent ones in context of this binary message.</param>
        /// <returns>The task object representing the asynchronous operation. The Result property on the task object returns a Byte array containing the received message.</returns>
        public virtual Task<byte[]> DecompressBinaryMessageAsync(WebSocket webSocket, WebSocketReceiveResult webSocketReceiveResult, byte[] receivePayloadBuffer)
        {
            return ReceiveMessagePayloadAsync(webSocket, webSocketReceiveResult, receivePayloadBuffer);
        }

        /// <summary>
        /// Receives and decompresses text message from the WebSocket connection asynchronously.
        /// </summary>
        /// <param name="webSocket">The WebSocket representing the connection.</param>
        /// <param name="webSocketReceiveResult">The result of performing a first ReceiveAsync operation for this text message.</param>
        /// <param name="receivePayloadBuffer">The application buffer which was the storage location for first ReceiveAsync operation and will be internally used for subsequent ones in context of this text message.</param>
        /// <returns>The task object representing the asynchronous operation. The Result property on the task object returns a String containing the received message.</returns>
        public virtual async Task<string> DecompressTextMessageAsync(WebSocket webSocket, WebSocketReceiveResult webSocketReceiveResult, byte[] receivePayloadBuffer)
        {
            byte[] messagePayload = await ReceiveMessagePayloadAsync(webSocket, webSocketReceiveResult, receivePayloadBuffer);

            return Encoding.UTF8.GetString(messagePayload);
        }

        protected async Task SendMessageAsync(WebSocket webSocket, byte[] message, WebSocketMessageType messageType, bool compressed, CancellationToken cancellationToken)
        {
            if (webSocket.State == WebSocketState.Open)
            {
                if (_sendSegmentSize.HasValue && (_sendSegmentSize.Value < message.Length))
                {
                    int messageOffset = 0;
                    int messageBytesToSend = message.Length;
                    
                    while (messageBytesToSend > 0)
                    {
                        int messageSegmentSize = Math.Min(_sendSegmentSize.Value, messageBytesToSend);
                        ArraySegment<byte> messageSegment = new ArraySegment<byte>(message, messageOffset, messageSegmentSize);

                        messageOffset += messageSegmentSize;
                        messageBytesToSend -= messageSegmentSize;

                        await SendAsync(webSocket, messageSegment, messageType, compressed, (messageBytesToSend == 0), cancellationToken);
                    }
                }
                else
                {
                    ArraySegment<byte> messageSegment = new ArraySegment<byte>(message, 0, message.Length);

                    await SendAsync(webSocket, messageSegment, messageType, compressed, true, cancellationToken);
                }
            }
        }

        private static Task SendAsync(WebSocket webSocket, ArraySegment<byte> messageSegment, WebSocketMessageType messageType, bool compressed, bool endOfMessage, CancellationToken cancellationToken)
        {
            if (compressed)
            {
                CompressionWebSocket compressionWebSocket = (webSocket as CompressionWebSocket) ?? throw new InvalidOperationException($"In order to send compressed message the used {nameof(WebSocket)} implementation must be {nameof(CompressionWebSocket)}.");

                return compressionWebSocket.SendAsync(messageSegment, messageType, true, endOfMessage, cancellationToken);
            }
            else
            {
                return webSocket.SendAsync(messageSegment, messageType, endOfMessage, cancellationToken);
            }
        }

        protected static async Task<byte[]> ReceiveMessagePayloadAsync(WebSocket webSocket, WebSocketReceiveResult webSocketReceiveResult, byte[] receivePayloadBuffer)
        {
            byte[] messagePayload = null;

            if (webSocketReceiveResult.EndOfMessage)
            {
                messagePayload = new byte[webSocketReceiveResult.Count];
                Array.Copy(receivePayloadBuffer, messagePayload, webSocketReceiveResult.Count);
            }
            else
            {
                using (MemoryStream messagePayloadStream = new MemoryStream())
                {
                    messagePayloadStream.Write(receivePayloadBuffer, 0, webSocketReceiveResult.Count);
                    while (!webSocketReceiveResult.EndOfMessage)
                    {
                        webSocketReceiveResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(receivePayloadBuffer), CancellationToken.None);
                        messagePayloadStream.Write(receivePayloadBuffer, 0, webSocketReceiveResult.Count);
                    }

                    messagePayload = messagePayloadStream.ToArray();
                }
            }

            return messagePayload;
        }
        #endregion
    }
}

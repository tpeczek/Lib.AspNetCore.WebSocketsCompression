using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Lib.AspNetCore.WebSocketsCompression.Net.WebSockets;

namespace Lib.AspNetCore.WebSocketsCompression.Providers
{
    /// <summary>
    /// Deflate WebSocket per-message compression provider. 
    /// </summary>
    public sealed class WebSocketDeflateCompressionProvider : WebSocketIdentityCompressionProvider
    {
        #region Fields
        private static readonly Encoding UTF8_WITHOUT_BOM = new UTF8Encoding(false);
        private static readonly byte[] LAST_FOUR_OCTETS_OF_EMPTY_NON_COMPRESSED_DEFLATE_BLOCK = new byte[] { 0x00, 0x00, 0xFF, 0xFF };

        private readonly WebSocketDeflateCompressionOptions _compressionOptions;
        #endregion

        #region Properties
        /// <summary>
        /// Indicates whether the client can use context takeover.
        /// </summary>
        public bool ClientNoContextTakeover => _compressionOptions.ClientNoContextTakeover;

        /// <summary>
        /// Indicates whether the server can use context takeover.
        /// </summary>
        public bool ServerNoContextTakeover => _compressionOptions.ServerNoContextTakeover;
        #endregion

        #region Constructor
        private WebSocketDeflateCompressionProvider(WebSocketDeflateCompressionOptions compressionOptions, int? sendSegmentSize)
            : base(sendSegmentSize)
        {
            _compressionOptions = compressionOptions;
        }
        #endregion

        #region Methods
        /// <summary>
        /// Creates new instance of <see cref="WebSocketDeflateCompressionProvider"/>
        /// </summary>
        /// <param name="compressionOptions">The compression options.</param>
        /// <param name="sendSegmentSize">The maximum size of single segment which will be sent over the WebSocket connection.</param>
        /// <returns>New instance of <see cref="WebSocketDeflateCompressionProvider"/> or NULL if provided options are not supported.</returns>
        public static WebSocketDeflateCompressionProvider Create(WebSocketDeflateCompressionOptions compressionOptions, int? sendSegmentSize = null)
        {
            if (compressionOptions.ServerMaxWindowBits.HasValue)
            {
                return null;
            }

            compressionOptions.ClientNoContextTakeover = true;

            return new WebSocketDeflateCompressionProvider(compressionOptions, sendSegmentSize);
        }

        /// <summary>
        /// Compresses and sends binary message over the WebSocket connection asynchronously.
        /// </summary>
        /// <param name="webSocket">The WebSocket representing the connection.</param>
        /// <param name="message">The binary message to be sent over the connection.</param>
        /// <param name="cancellationToken">The token that propagates the notification that operations should be canceled.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public override async Task CompressBinaryMessageAsync(WebSocket webSocket, byte[] message, CancellationToken cancellationToken)
        {
            byte[] compressedMessagePayload = await CompressBinaryWithDeflateAsync(message);

            await SendCompressedMessageAsync(webSocket, compressedMessagePayload, WebSocketMessageType.Binary, true, cancellationToken);
        }

        /// <summary>
        /// Compresses and sends text message over the WebSocket connection asynchronously.
        /// </summary>
        /// <param name="webSocket">The WebSocket representing the connection.</param>
        /// <param name="message">The text message to be sent over the connection.</param>
        /// <param name="cancellationToken">The token that propagates the notification that operations should be canceled.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public override async Task CompressTextMessageAsync(WebSocket webSocket, string message, CancellationToken cancellationToken)
        {
            byte[] compressedMessagePayload = await CompressTextWithDeflateAsync(message);

            await SendCompressedMessageAsync(webSocket, compressedMessagePayload, WebSocketMessageType.Text, true, cancellationToken);
        }

        /// <summary>
        /// Receives and decompresses binary message from the WebSocket connection asynchronously.
        /// </summary>
        /// <param name="webSocket">The WebSocket representing the connection.</param>
        /// <param name="webSocketReceiveResult">The result of performing a first ReceiveAsync operation for this binary message.</param>
        /// <param name="receivePayloadBuffer">The application buffer which was the storage location for first ReceiveAsync operation and will be internally used for subsequent ones in context of this binary message.</param>
        /// <returns>The task object representing the asynchronous operation. The Result property on the task object returns a Byte array containing the received message.</returns>
        public override async Task<byte[]> DecompressBinaryMessageAsync(WebSocket webSocket, WebSocketReceiveResult webSocketReceiveResult, byte[] receivePayloadBuffer)
        {
            byte[] message = null;

            if (IsCompressed(webSocketReceiveResult))
            {
                byte[] compressedMessagePayload = await ReceiveCompressedMessagePayloadAsync(webSocket, webSocketReceiveResult, receivePayloadBuffer);

                message = await DecompressBinaryWithDeflateAsync(compressedMessagePayload);
            }
            else
            {
                message = await base.DecompressBinaryMessageAsync(webSocket, webSocketReceiveResult, receivePayloadBuffer);
            }

            return message;
        }

        /// <summary>
        /// Receives and decompresses text message from the WebSocket connection asynchronously.
        /// </summary>
        /// <param name="webSocket">The WebSocket representing the connection.</param>
        /// <param name="webSocketReceiveResult">The result of performing a first ReceiveAsync operation for this text message.</param>
        /// <param name="receivePayloadBuffer">The application buffer which was the storage location for first ReceiveAsync operation and will be internally used for subsequent ones in context of this text message.</param>
        /// <returns>The task object representing the asynchronous operation. The Result property on the task object returns a String containing the received message.</returns>
        public override async Task<string> DecompressTextMessageAsync(WebSocket webSocket, WebSocketReceiveResult webSocketReceiveResult, byte[] receivePayloadBuffer)
        {
            string message = null;

            if (IsCompressed(webSocketReceiveResult))
            {
                byte[] compressedMessagePayload = await ReceiveCompressedMessagePayloadAsync(webSocket, webSocketReceiveResult, receivePayloadBuffer);

                message = await DecompressTextWithDeflateAsync(compressedMessagePayload);
            }
            else
            {
                message = await base.DecompressTextMessageAsync(webSocket, webSocketReceiveResult, receivePayloadBuffer);
            }

            return message;
        }

        private static async Task<byte[]> CompressBinaryWithDeflateAsync(byte[] message)
        {
            byte[] compressedMessagePayload = null;

            using (MemoryStream compressedMessagePayloadStream = new MemoryStream())
            {
                using (DeflateStream compressedMessagePayloadCompressStream = new DeflateStream(compressedMessagePayloadStream, CompressionMode.Compress))
                {
                    await compressedMessagePayloadCompressStream.WriteAsync(message, 0, message.Length);
                }

                compressedMessagePayload = compressedMessagePayloadStream.ToArray();
            }

            return compressedMessagePayload;
        }

        private static async Task<byte[]> CompressTextWithDeflateAsync(string message)
        {
            byte[] compressedMessagePayload = null;

            using (MemoryStream compressedMessagePayloadStream = new MemoryStream())
            {
                using (DeflateStream compressedMessagePayloadCompressStream = new DeflateStream(compressedMessagePayloadStream, CompressionMode.Compress))
                {
                    using (StreamWriter compressedMessagePayloadCompressWriter = new StreamWriter(compressedMessagePayloadCompressStream, UTF8_WITHOUT_BOM))
                    {
                        await compressedMessagePayloadCompressWriter.WriteAsync(message);
                    }
                }

                compressedMessagePayload = compressedMessagePayloadStream.ToArray();
            }

            return compressedMessagePayload;
        }

        private static byte[] TrimLastFourOctetsOfEmptyNonCompressedDeflateBlock(byte[] compressedMessagePayload)
        {
            int lastFourOctetsOfEmptyNonCompressedDeflateBlockPosition = 0;
            for (int position = compressedMessagePayload.Length - 1; position >= 4; position--)
            {
                if ((compressedMessagePayload[position - 3] == LAST_FOUR_OCTETS_OF_EMPTY_NON_COMPRESSED_DEFLATE_BLOCK[0])
                    && (compressedMessagePayload[position - 2] == LAST_FOUR_OCTETS_OF_EMPTY_NON_COMPRESSED_DEFLATE_BLOCK[1])
                    && (compressedMessagePayload[position - 1] == LAST_FOUR_OCTETS_OF_EMPTY_NON_COMPRESSED_DEFLATE_BLOCK[2])
                    && (compressedMessagePayload[position] == LAST_FOUR_OCTETS_OF_EMPTY_NON_COMPRESSED_DEFLATE_BLOCK[3]))
                {
                    lastFourOctetsOfEmptyNonCompressedDeflateBlockPosition = position - 3;
                    break;
                }
            }
            Array.Resize(ref compressedMessagePayload, lastFourOctetsOfEmptyNonCompressedDeflateBlockPosition);

            return compressedMessagePayload;
        }

        private Task SendCompressedMessageAsync(WebSocket webSocket, byte[] compressedMessagePayload, WebSocketMessageType messageType, bool compressed, CancellationToken cancellationToken)
        {
            compressedMessagePayload = TrimLastFourOctetsOfEmptyNonCompressedDeflateBlock(compressedMessagePayload);

            return SendMessageAsync(webSocket, compressedMessagePayload, WebSocketMessageType.Text, true, cancellationToken);
        }

        private static bool IsCompressed(WebSocketReceiveResult webSocketReceiveResult)
        {
            CompressionWebSocketReceiveResult compressionWebSocketReceiveResult = webSocketReceiveResult as CompressionWebSocketReceiveResult;

            return ((compressionWebSocketReceiveResult != null) && compressionWebSocketReceiveResult.Compressed);
        }

        private static byte[] AppendLastFourOctetsOfEmptyNonCompressedDeflateBlock(byte[] compressedMessagePayload)
        {
            Array.Resize(ref compressedMessagePayload, compressedMessagePayload.Length + 4);

            compressedMessagePayload[compressedMessagePayload.Length - 4] = LAST_FOUR_OCTETS_OF_EMPTY_NON_COMPRESSED_DEFLATE_BLOCK[0];
            compressedMessagePayload[compressedMessagePayload.Length - 3] = LAST_FOUR_OCTETS_OF_EMPTY_NON_COMPRESSED_DEFLATE_BLOCK[1];
            compressedMessagePayload[compressedMessagePayload.Length - 2] = LAST_FOUR_OCTETS_OF_EMPTY_NON_COMPRESSED_DEFLATE_BLOCK[2];
            compressedMessagePayload[compressedMessagePayload.Length - 1] = LAST_FOUR_OCTETS_OF_EMPTY_NON_COMPRESSED_DEFLATE_BLOCK[3];

            return compressedMessagePayload;
        }

        private static async Task<byte[]> ReceiveCompressedMessagePayloadAsync(WebSocket webSocket, WebSocketReceiveResult webSocketReceiveResult, byte[] receivePayloadBuffer)
        {
            byte[] compressedMessagePayload = await ReceiveMessagePayloadAsync(webSocket, webSocketReceiveResult, receivePayloadBuffer);

            compressedMessagePayload = AppendLastFourOctetsOfEmptyNonCompressedDeflateBlock(compressedMessagePayload);

            return compressedMessagePayload;
        }

        private static async Task<string> DecompressTextWithDeflateAsync(byte[] compressedMessagePayload)
        {
            string message = null;

            using (MemoryStream compressedMessagePayloadStream = new MemoryStream(compressedMessagePayload))
            {
                using (DeflateStream compressedMessagePayloadDecompressStream = new DeflateStream(compressedMessagePayloadStream, CompressionMode.Decompress))
                {
                    using (StreamReader compressedMessagePayloadDecompressReader = new StreamReader(compressedMessagePayloadDecompressStream, UTF8_WITHOUT_BOM))
                    {
                        message = await compressedMessagePayloadDecompressReader.ReadToEndAsync();
                    }
                }
            }

            return message;
        }

        private static async Task<byte[]> DecompressBinaryWithDeflateAsync(byte[] compressedMessagePayload)
        {
            byte[] message = null;

            using (MemoryStream compressedMessagePayloadStream = new MemoryStream(compressedMessagePayload))
            {
                using (DeflateStream compressedMessagePayloadDecompressStream = new DeflateStream(compressedMessagePayloadStream, CompressionMode.Decompress))
                {
                    byte[] messageBuffer = new byte[16 * 1024];
                    using (MemoryStream messageStream = new MemoryStream())
                    {
                        int messageReadBytes;
                        while ((messageReadBytes = await compressedMessagePayloadDecompressStream.ReadAsync(messageBuffer, 0, messageBuffer.Length)) > 0)
                        {
                            messageStream.Write(messageBuffer, 0, messageReadBytes);
                        }

                        message = messageStream.ToArray();
                    }
                }
            }

            return message;
        }
        #endregion
    }
}

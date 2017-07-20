using Microsoft.AspNetCore.Http;
using Lib.AspNetCore.WebSocketsCompression.Providers;

namespace Lib.AspNetCore.WebSocketsCompression
{
    /// <summary>
    /// Interface which represents service which provides support for WebSocket per-message compression.
    /// </summary>
    public interface IWebSocketCompressionService
    {
        /// <summary>
        /// Negotiates the WebSocket per-message compression.
        /// </summary>
        /// <param name="context">The current context.</param>
        /// <param name="sendSegmentSize">The maximum size of single segment which will be sent over the WebSocket connection.</param>
        /// <returns>The negotiated compression provider or NULL.</returns>
        IWebSocketCompressionProvider NegotiateCompression(HttpContext context, int? sendSegmentSize = null);
    }
}

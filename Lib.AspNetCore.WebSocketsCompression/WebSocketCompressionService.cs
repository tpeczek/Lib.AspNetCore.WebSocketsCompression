using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;
using Lib.AspNetCore.WebSocketsCompression.Providers;
using Lib.AspNetCore.WebSocketsCompression.Http.Headers;

namespace Lib.AspNetCore.WebSocketsCompression
{
    /// <summary>
    /// Service which provides support for WebSocket per-message compression.
    /// </summary>
    public class WebSocketCompressionService : IWebSocketCompressionService
    {
        #region Fields
        private const string WEBSOCKET_PERMESSAGE_DEFLATE_EXTENSION = "permessage-deflate";
        #endregion

        #region Methods
        /// <summary>
        /// Negotiates the WebSocket per-message compression.
        /// </summary>
        /// <param name="context">The current context.</param>
        /// <param name="sendSegmentSize">The maximum size of single segment which will be sent over the WebSocket connection.</param>
        /// <returns>The negotiated compression provider or NULL.</returns>
        public IWebSocketCompressionProvider NegotiateCompression(HttpContext context, int? sendSegmentSize = null)
        {
            return NegotiateDeflateCompression(context, sendSegmentSize) ?? new WebSocketIdentityCompressionProvider(sendSegmentSize);
        }

        private static WebSocketDeflateCompressionProvider NegotiateDeflateCompression(HttpContext context, int? sendSegmentSize)
        {
            WebSocketDeflateCompressionProvider deflateCompressionProvider = null;

            WebSocketDeflateCompressionOptions deflateCompressionOptions = GetDeflateNegotiationOffer(context);

            if (deflateCompressionOptions != null)
            {
                deflateCompressionProvider = WebSocketDeflateCompressionProvider.Create(deflateCompressionOptions, sendSegmentSize);

                if (deflateCompressionProvider != null)
                {
                    SetDeflateNegotiationResponse(context, deflateCompressionProvider);
                }
            }

            return deflateCompressionProvider;
        }

        private static WebSocketDeflateCompressionOptions GetDeflateNegotiationOffer(HttpContext context)
        {
            WebSocketDeflateCompressionOptions compressionOptions = null;

            foreach (string headerValue in context.Request.Headers.GetCommaSeparatedValues(HeaderNames.SecWebSocketExtensions))
            {
                NameValueWithParametersHeaderValue parsedHeaderValue;
                if (NameValueWithParametersHeaderValue.TryParse(headerValue, out parsedHeaderValue) && parsedHeaderValue.Name == WEBSOCKET_PERMESSAGE_DEFLATE_EXTENSION)
                {
                    compressionOptions = WebSocketDeflateCompressionOptions.FromHeaderValue(parsedHeaderValue);

                    break;
                }
            }

            return compressionOptions;
        }

        private static void SetDeflateNegotiationResponse(HttpContext context, WebSocketDeflateCompressionProvider deflateCompressionProvider)
        {
            NameValueWithParametersHeaderValue secWebSocketExtensionsHeaderValue = new NameValueWithParametersHeaderValue(WEBSOCKET_PERMESSAGE_DEFLATE_EXTENSION);

            if (deflateCompressionProvider.ClientNoContextTakeover)
            {
                secWebSocketExtensionsHeaderValue.Parameters.Add(new NameValueHeaderValue(WebSocketDeflateCompressionOptions.CLIENT_NO_CONTEXT_TAKEOVER_OPTION));
            }

            if (deflateCompressionProvider.ServerNoContextTakeover)
            {
                secWebSocketExtensionsHeaderValue.Parameters.Add(new NameValueHeaderValue(WebSocketDeflateCompressionOptions.SERVER_NO_CONTEXT_TAKEOVER_OPTION));
            }

            context.Response.Headers[HeaderNames.SecWebSocketExtensions] = secWebSocketExtensionsHeaderValue.ToString();
        }
        #endregion
    }
}

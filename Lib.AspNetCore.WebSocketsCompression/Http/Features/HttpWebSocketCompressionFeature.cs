using System;
using System.Text;
using System.Net.WebSockets;
using System.Threading.Tasks;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Lib.AspNetCore.WebSocketsCompression.Http.Headers;
using Lib.AspNetCore.WebSocketsCompression.Net.WebSockets;

namespace Lib.AspNetCore.WebSocketsCompression.Http.Features
{
    internal class HttpWebSocketCompressionFeature : IHttpWebSocketFeature
    {
        #region Fields
        private readonly HttpContext _context;
        private readonly IHttpUpgradeFeature _upgradeFeature;
        private readonly WebSocketCompressionOptions _options;

        private const string WEBSOCKET_HANDSHAKE_METHOD = "GET";
        private const string WEBSOCKET_HANDSHAKE_CONNECTION_HEADER_VALUE = "Upgrade";
        private const string WEBSOCKET_HANDSHAKE_UPGRADE_HEADER_VALUE = "websocket";
        private const string WEBSOCKET_SUPPORTED_PROTOCOL_VERSION = "13";
        #endregion

        #region Properties
        public bool IsWebSocketRequest
        {
            get { return _upgradeFeature.IsUpgradableRequest && IsValidWebSocketHandshake(_context); }
        }
        #endregion

        #region Constructor
        internal HttpWebSocketCompressionFeature(HttpContext context, IHttpUpgradeFeature upgradeFeature, WebSocketCompressionOptions options)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _upgradeFeature = upgradeFeature ?? throw new ArgumentNullException(nameof(upgradeFeature));
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }
        #endregion

        #region Methods
        public Task<WebSocket> AcceptAsync(WebSocketAcceptContext acceptContext)
        {
            string subProtocol = acceptContext?.SubProtocol;

            if (!IsWebSocketRequest)
            {
                throw new InvalidOperationException("The current request is not a supported WebSocket handshake request.");
            }

            SetAcceptWebSocketHandshakeHeaders(_context, subProtocol);

            return CreateCompressionWebSocket(acceptContext, subProtocol);
        }

        private static bool IsValidWebSocketHandshake(HttpContext context)
        {
            return String.Equals(context.Request.Method, WEBSOCKET_HANDSHAKE_METHOD, StringComparison.OrdinalIgnoreCase)
                && HasHeaderWithRequiredValue(context, HeaderNames.Connection, WEBSOCKET_HANDSHAKE_CONNECTION_HEADER_VALUE)
                && HasHeaderWithRequiredValue(context, HeaderNames.Upgrade, WEBSOCKET_HANDSHAKE_UPGRADE_HEADER_VALUE)
                && HasHeaderWithRequiredValue(context, HeaderNames.SecWebSocketVersion, WEBSOCKET_SUPPORTED_PROTOCOL_VERSION)
                && HasHeaderWithValueMeetingCondition(context, HeaderNames.SecWebSocketKey, HeaderValueIsValidRequestKeyCondition);
        }

        private static bool HasHeaderWithRequiredValue(HttpContext context, string headerName, string requiredHeaderValue)
        {
            Func<string, bool> requiredHeaderValueCondition = (string headerValue) => String.Equals(requiredHeaderValue, headerValue, StringComparison.OrdinalIgnoreCase);

            return HasHeaderWithValueMeetingCondition(context, headerName, requiredHeaderValueCondition);
        }

        private static bool HasHeaderWithValueMeetingCondition(HttpContext context, string headerName, Func<string, bool> headerValueCondition)
        {
            bool hasHeaderWithValueMeetingCondition = false;

            foreach (string headerValue in context.Request.Headers.GetCommaSeparatedValues(headerName))
            {
                if (headerValueCondition(headerValue))
                {
                    hasHeaderWithValueMeetingCondition = true;
                    break;
                }
            }

            return hasHeaderWithValueMeetingCondition;
        }

        private static bool HeaderValueIsValidRequestKeyCondition(string headerValue)
        {
            bool headerValueIsValidRequestKeyCondition = false;

            try
            {
                headerValueIsValidRequestKeyCondition = !String.IsNullOrWhiteSpace(headerValue) && (Convert.FromBase64String(headerValue).Length == 16);
            }
            catch (Exception)
            { }

            return headerValueIsValidRequestKeyCondition;
        }

        private static void SetAcceptWebSocketHandshakeHeaders(HttpContext context, string subProtocol)
        {
            context.Response.Headers[HeaderNames.Connection] = WEBSOCKET_HANDSHAKE_CONNECTION_HEADER_VALUE;
            context.Response.Headers[HeaderNames.Upgrade] = WEBSOCKET_HANDSHAKE_UPGRADE_HEADER_VALUE;
            context.Response.Headers[HeaderNames.SecWebSocketAccept] = CreateResponseKey(context);

            if (!String.IsNullOrWhiteSpace(subProtocol))
            {
                context.Response.Headers[HeaderNames.SecWebSocketProtocol] = subProtocol;
            }
        }

        private static string CreateResponseKey(HttpContext context)
        {
            string responseKey = null;
            string requestKey = String.Join(", ", context.Request.Headers[HeaderNames.SecWebSocketKey]);

            using (SHA1 cryptographyAlgorithm = SHA1.Create())
            {
                string concatenatedRequestKey = requestKey + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
                byte[] concatenatedRequestKeyBytes = Encoding.UTF8.GetBytes(concatenatedRequestKey);
                byte[] hashedConcatenatedRequestKeyBytes = cryptographyAlgorithm.ComputeHash(concatenatedRequestKeyBytes);
                responseKey = Convert.ToBase64String(hashedConcatenatedRequestKeyBytes);
            }

            return responseKey;
        }

        private async Task<WebSocket> CreateCompressionWebSocket(WebSocketAcceptContext acceptContext, string subProtocol)
        {
            WebSocketCompressionAcceptContext compressionAcceptContext = acceptContext as WebSocketCompressionAcceptContext;

            return new CompressionWebSocket(await _upgradeFeature.UpgradeAsync(), subProtocol, compressionAcceptContext?.KeepAliveInterval ?? _options.KeepAliveInterval);
        }
        #endregion
    }
}

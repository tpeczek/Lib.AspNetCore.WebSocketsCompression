using System;
using System.Net.Http.Headers;

namespace Lib.AspNetCore.WebSocketsCompression.Providers
{
    /// <summary>
    /// Configuration options for the <see cref="WebSocketDeflateCompressionProvider"/>.
    /// </summary>
    public class WebSocketDeflateCompressionOptions
    {
        #region Fields
        internal const string SERVER_NO_CONTEXT_TAKEOVER_OPTION = "server_no_context_takeover";
        internal const string CLIENT_NO_CONTEXT_TAKEOVER_OPTION = "client_no_context_takeover";
        private const string SERVER_MAX_WINDOW_BITS_OPTION = "server_max_window_bits";
        private const string CLIENT_MAX_WINDOW_BITS_OPTION = "client_max_window_bits";

        /// <summary>
        /// The <see cref="ClientMaxWindowBits"/> value which indicates that client supports configuring LZ77 sliding window size without hinting a value. 
        /// </summary>
        public const int ClientMaxWindowBitsWithoutValue = -1;
        #endregion

        #region Properties
        /// <summary>
        /// Indicates whether the client can use context takeover.
        /// </summary>
        public bool ClientNoContextTakeover { get; set; }

        /// <summary>
        /// Indicates that client supports configuring LZ77 sliding window size optionally hinting a value.
        /// </summary>
        public int? ClientMaxWindowBits { get; set; }

        /// <summary>
        /// Indicates whether the server can use context takeover.
        /// </summary>
        public bool ServerNoContextTakeover { get; set; }

        /// <summary>
        /// The maximum base-2 logarithm of the LZ77 sliding window size to be used by server.
        /// </summary>
        public int? ServerMaxWindowBits { get; set; }
        #endregion

        #region Constructor
        /// <summary>
        /// Initializes new instance of <see cref="WebSocketDeflateCompressionOptions"/>.
        /// </summary>
        public WebSocketDeflateCompressionOptions()
        { }
        #endregion

        #region Methods
        /// <summary>
        /// Creates new instance of <see cref="WebSocketDeflateCompressionOptions"/> from header value.
        /// </summary>
        /// <param name="headerValue">The header value.</param>
        /// <returns>New instance of <see cref="WebSocketDeflateCompressionOptions"/>.</returns>
        public static WebSocketDeflateCompressionOptions FromHeaderValue(NameValueWithParametersHeaderValue headerValue)
        {
            WebSocketDeflateCompressionOptions compressionOptions = new WebSocketDeflateCompressionOptions();

            foreach (NameValueHeaderValue parameterValue in headerValue.Parameters)
            {
                switch (parameterValue.Name.ToLowerInvariant())
                {
                    case SERVER_NO_CONTEXT_TAKEOVER_OPTION:
                        compressionOptions = SetAndEnsureServerNoContextTakeover(compressionOptions);
                        break;
                    case CLIENT_NO_CONTEXT_TAKEOVER_OPTION:
                        compressionOptions = SetAndEnsureClientNoContextTakeover(compressionOptions);
                        break;
                    case SERVER_MAX_WINDOW_BITS_OPTION:
                        compressionOptions = SetAndEnsureServerMaxWindowBits(compressionOptions, parameterValue.Value);
                        break;
                    case CLIENT_MAX_WINDOW_BITS_OPTION:
                        compressionOptions = SetAndEnsureClientMaxWindowBits(compressionOptions, parameterValue.Value);
                        break;
                    default:
                        compressionOptions = null;
                        break;
                }

                if (compressionOptions == null)
                {
                    break;
                }
            }

            return compressionOptions;
        }

        private static WebSocketDeflateCompressionOptions SetAndEnsureServerNoContextTakeover(WebSocketDeflateCompressionOptions compressionOptions)
        {
            if (compressionOptions.ServerNoContextTakeover)
            {
                compressionOptions = null;
            }
            else
            {
                compressionOptions.ServerNoContextTakeover = true;
            }

            return compressionOptions;
        }

        private static WebSocketDeflateCompressionOptions SetAndEnsureClientNoContextTakeover(WebSocketDeflateCompressionOptions compressionOptions)
        {
            if (compressionOptions.ClientNoContextTakeover)
            {
                compressionOptions = null;
            }
            else
            {
                compressionOptions.ClientNoContextTakeover = true;
            }

            return compressionOptions;
        }

        private static WebSocketDeflateCompressionOptions SetAndEnsureServerMaxWindowBits(WebSocketDeflateCompressionOptions compressionOptions, string serverMaxWindowBitsValue)
        {
            int serverMaxWindowBits;

            if (compressionOptions.ServerMaxWindowBits.HasValue || !TryParseMaxWindowBits(serverMaxWindowBitsValue, out serverMaxWindowBits))
            {
                compressionOptions = null;
            }
            else
            {
                compressionOptions.ServerMaxWindowBits = serverMaxWindowBits;
            }

            return compressionOptions;
        }

        private static WebSocketDeflateCompressionOptions SetAndEnsureClientMaxWindowBits(WebSocketDeflateCompressionOptions compressionOptions, string clientMaxWindowBitsValue)
        {
            int clientMaxWindowBits = ClientMaxWindowBitsWithoutValue;
            if (compressionOptions.ClientMaxWindowBits.HasValue || (!String.IsNullOrEmpty(clientMaxWindowBitsValue) && TryParseMaxWindowBits(clientMaxWindowBitsValue, out clientMaxWindowBits)))
            {
                compressionOptions = null;
            }
            else
            {
                compressionOptions.ClientMaxWindowBits = clientMaxWindowBits;
            }

            return compressionOptions;
        }

        private static bool TryParseMaxWindowBits(string maxWindowBitsValue, out int maxWindowBits)
        {
            bool parsed = false;

            if (Int32.TryParse(maxWindowBitsValue, out maxWindowBits))
            {
                if ((maxWindowBits < 8) || (maxWindowBits > 15))
                {
                    maxWindowBits = default(int);
                }
                else
                {
                    parsed = true;
                }
            }

            return parsed;
        }
        #endregion
    }
}

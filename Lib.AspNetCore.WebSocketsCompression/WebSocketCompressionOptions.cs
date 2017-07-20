using System;

namespace Lib.AspNetCore.WebSocketsCompression
{
    /// <summary>
    /// Configuration options for the <see cref="WebSocketCompressionMiddleware"/>.
    /// </summary>
    public class WebSocketCompressionOptions
    {
        #region Properties
        /// <summary>
        /// Gets or sets the frequency at which to send Ping/Pong keep-alive control frames.
        /// The default is two minutes.
        /// </summary>
        public TimeSpan KeepAliveInterval { get; set; }

        /// <summary>
        /// Gets or sets the size of the protocol buffer used to receive and parse frames.
        /// The default is 4kb.
        /// </summary>
        public int ReceiveBufferSize { get; set; }
        #endregion

        #region Constructor
        /// <summary>
        /// Initializes new instance of <see cref="WebSocketCompressionOptions"/>.
        /// </summary>
        public WebSocketCompressionOptions()
        {
            KeepAliveInterval = TimeSpan.FromMinutes(2);
            ReceiveBufferSize = 4 * 1024;
        }
        #endregion
    }
}

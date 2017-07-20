using System;
using Microsoft.AspNetCore.Http;

namespace Lib.AspNetCore.WebSocketsCompression
{
    /// <summary>
    /// The WebSocket accept context.
    /// </summary>
    public class WebSocketCompressionAcceptContext : WebSocketAcceptContext
    {
        #region Properties
        /// <summary>
        /// The interval for sending "Ping" frames.
        /// </summary>
        public TimeSpan? KeepAliveInterval { get; set; }

        /// <summary>
        /// The receive buffer size.
        /// </summary>
        public int? ReceiveBufferSize { get; set; }
        #endregion
    }
}

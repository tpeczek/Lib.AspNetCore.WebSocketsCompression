using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using Lib.AspNetCore.WebSocketsCompression.Http.Features;

namespace Lib.AspNetCore.WebSocketsCompression
{
    /// <summary>
    /// Middleware providing support for WebSocket with per-message compression.
    /// </summary>
    public class WebSocketCompressionMiddleware
    {
        #region Fields
        private readonly RequestDelegate _next;
        private readonly WebSocketCompressionOptions _options;
        #endregion

        #region Constructor
        /// <summary>
        /// Initializes new instance of <see cref="WebSocketCompressionMiddleware"/>.
        /// </summary>
        /// <param name="next">The next middleware in the pipeline.</param>
        /// <param name="options">The options.</param>
        public WebSocketCompressionMiddleware(RequestDelegate next, IOptions<WebSocketCompressionOptions> options)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }
        #endregion

        #region Methods
        /// <summary>
        /// Process an individual request.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public Task Invoke(HttpContext context)
        {
            IHttpUpgradeFeature upgradeFeature = context.Features.Get<IHttpUpgradeFeature>();
            if (upgradeFeature != null)
            {
                context.Features.Set<IHttpWebSocketFeature>(new HttpWebSocketCompressionFeature(context, upgradeFeature, _options));
            }

            return _next(context);
        }
        #endregion
    }
}

using System;
using Microsoft.Extensions.Options;
using Lib.AspNetCore.WebSocketsCompression;

namespace Microsoft.AspNetCore.Builder
{
    /// <summary>
    /// The <see cref="IApplicationBuilder"/> extensions for adding <see cref="WebSocketCompressionMiddleware"/> to pipeline.
    /// </summary>
    public static class WebSocketCompressionMiddlewareExtensions
    {
        /// <summary>
        /// Adds a <see cref="WebSocketCompressionMiddleware"/> to application pipeline with default options.
        /// </summary>
        /// <param name="app">The <see cref="IApplicationBuilder"/> passed to Configure method.</param>
        /// <returns>The original app parameter.</returns>
        public static IApplicationBuilder UseWebSocketsCompression(this IApplicationBuilder app)
        {
            if (app == null)
            {
                throw new ArgumentNullException(nameof(app));
            }

            return app.UseMiddleware<WebSocketCompressionMiddleware>();
        }

        /// <summary>
        /// Adds a <see cref="WebSocketCompressionMiddleware"/> to application pipeline with given options.
        /// </summary>
        /// <param name="app">The <see cref="IApplicationBuilder"/> passed to Configure method.</param>
        /// <param name="options">The options.</param>
        /// <returns>The original app parameter.</returns>
        public static IApplicationBuilder UseWebSocketsCompression(this IApplicationBuilder app, WebSocketCompressionOptions options)
        {
            if (app == null)
            {
                throw new ArgumentNullException(nameof(app));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            return app.UseMiddleware<WebSocketCompressionMiddleware>(Options.Create(options));
        }
    }
}

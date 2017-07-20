using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Lib.AspNetCore.WebSocketsCompression;

namespace Microsoft.AspNetCore.Builder
{
    /// <summary>
    /// The <see cref="IServiceCollection"/> extensions for registering <see cref="IWebSocketCompressionService"/>.
    /// </summary>
    public static class WebSocketCompressionServicesExtensions
    {
        /// <summary>
        /// Registers service which provides support for WebSocket per-message compression.
        /// </summary>
        /// <param name="services">The collection of service descriptors.</param>
        /// <returns>The collection of service descriptors.</returns>
        public static IServiceCollection AddWebSocketCompression(this IServiceCollection services)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.TryAddSingleton<IWebSocketCompressionService, WebSocketCompressionService>();

            return services;
        }
    }
}

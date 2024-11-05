// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServerDiscovery
{
    using LoRaWan;
    using Prometheus;
    using LoRaTools.NetworkServerDiscovery;

#pragma warning disable CA1052 // Static holder types should be Static or NotInheritable, but needed as a type argument for logger.
    public class Program
#pragma warning restore CA1052 // Static holder types should be Static or NotInheritable
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            _ = builder.Configuration.AddJsonFile("appsettings.local.json", optional: true);
            _ = builder.Services
                .AddSingleton<DiscoveryService>()
                .AddSingleton<ILnsDiscovery, TagBasedLnsDiscovery>()
                .AddMemoryCache()
                .AddApplicationInsightsTelemetry()
                .AddHttpClient();

            var app = builder.Build();

            // Configure the HTTP request pipeline.

            _ = app.UseWebSockets();

            _ = app.MapGet(ILnsDiscovery.EndpointName, async (DiscoveryService discoveryService, HttpContext httpContext, ILogger<Program> logger, CancellationToken cancellationToken) =>
            {
                try
                {
                    await discoveryService.HandleDiscoveryRequestAsync(httpContext, cancellationToken);
                }
                catch (Exception ex) when (ExceptionFilterUtility.False(() => logger.LogError(ex, "Exception when executing discovery endpoint: '{Exception}'.", ex)))
                { }
            });

            _ = app.MapMetrics();

            app.Run();

        }
    }
}

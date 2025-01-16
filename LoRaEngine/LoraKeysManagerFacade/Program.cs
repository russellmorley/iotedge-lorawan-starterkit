// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


// upgraded based on this MS beast: https://learn.microsoft.com/en-us/azure/azure-functions/migrate-dotnet-to-isolated-model?tabs=net8

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using LoRaTools.ADR;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using LoRaTools.IoTHubImpl;
using Microsoft.Azure.Devices;
using System.Net.Http;
using System.Linq;
using LoraDeviceManager;
using LoRaTools.AzureBlobStorage;
using Azure.Storage.Blobs;
using LoraDeviceManager.FunctionBundler;
using LoraDeviceManager.ADR;
using LoRaTools.ChannelPublisher;
using LoRaTools.EdgeDeviceGetter;
using LoRaTools.ServiceClient;
using LoRaTools.CacheStore;
using LoRaTools.Services;
using LoRaTools;
using Microsoft.Azure.Functions.Worker;
using LoraKeysManagerFacade.LoraDeviceManagerServices;
using LoraDeviceManager.Cache;

// for configuration, see: https://learn.microsoft.com/en-us/azure/azure-functions/dotnet-isolated-process-guide?tabs=hostbuilder%2Cwindows#register-azure-clients
// for running locally, see: 

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((hostContext, services) =>
    {
        const string iotHubConnectionStringKey = "IoTHubConnectionString";
        const string redisConnectionStringKey = "RedisConnectionString";
        const string storageConnectionStringKey = "AzureWebJobsStorage";
        const string serviceValidatesTenant = "ServiceValidatesTenant";

        var redis = ConnectionMultiplexer.Connect(hostContext.Configuration.GetConnectionString(redisConnectionStringKey));
        var redisCache = redis.GetDatabase();
        var deviceCacheStore = new LoRaDeviceCacheRedisStore(redisCache);

        //services
        //    .AddAzureClients(builder =>
        //    {
        //        _ = builder.AddBlobServiceClient(hostContext.Configuration.GetConnectionString(storageConnectionStringKey))
        //            .WithName(Globals.WebJobsStorageClientName);
        //    });
        _ = services
            .AddHttpClient()
            .AddSingleton(sp => AzureBlobStorageManager.CreateWithProvider(() =>
                new BlobServiceClient(hostContext.Configuration.GetConnectionString(storageConnectionStringKey)),
                sp.GetRequiredService<ILogger<AzureBlobStorageManager>>()))
            .AddSingleton(sp => IoTHubRegistryManager.CreateWithProvider(() =>
                RegistryManager.CreateFromConnectionString(hostContext.Configuration.GetSection("appSettings")[iotHubConnectionStringKey]),
                sp.GetRequiredService<IHttpClientFactory>(),
                sp.GetRequiredService<ILogger<IoTHubRegistryManager>>()))
            .AddSingleton<IServiceClient>(new ServiceClientAdapter(ServiceClient.CreateFromConnectionString(hostContext.Configuration.GetConnectionString(iotHubConnectionStringKey))))
            .AddSingleton<ICacheStore>(deviceCacheStore)
            .AddSingleton<ILoraDeviceManager, LoraDeviceManagerImpl>()
            .AddSingleton<ILoRaADRManager>(sp => new LoRaADRServerManager(
                new LoRaADRRedisStore(redisCache, sp.GetRequiredService<ILogger<LoRaADRRedisStore>>()),
                new LoRaADRStrategyProvider(sp.GetRequiredService<ILoggerFactory>()),
                deviceCacheStore,
                sp.GetRequiredService<ILoggerFactory>(),
                sp.GetRequiredService<ILogger<LoRaADRServerManager>>()))
            .AddSingleton<IChannelPublisher>(sp => new RedisChannelPublisher(redis, sp.GetRequiredService<ILogger<RedisChannelPublisher>>()))
            .AddSingleton<GetDeviceFunction>()
            .AddSingleton<IEdgeDeviceGetter, EdgeDeviceGetter>()
            .AddSingleton<NextFCntDownFunction>()
            .AddSingleton<FunctionBundlerFunction>()
            .AddSingleton<IFunctionBundlerExecutionItem, NextFCntDownExecutionItem>()
            .AddSingleton<IFunctionBundlerExecutionItem, DeduplicationExecutionItem>()
            .AddSingleton<IFunctionBundlerExecutionItem, ADRExecutionItem>()
            .AddSingleton<IFunctionBundlerExecutionItem, PreferredGatewayExecutionItem>()
            .AddSingleton<ITenantValidationStrategy>(sp =>
                new Sha256TenantValidationStrategy(sp.GetRequiredService<IDeviceRegistryManager>()) { 
                    DoValidation =
                        hostContext.Configuration.GetSection("appSettings")[serviceValidatesTenant] == "true"
                })
            .AddSingleton<LoRaDevAddrCache>()
            //.AddApplicationInsightsTelemetry() //for aspnetcore? Still needed?
            .AddApplicationInsightsTelemetryWorkerService()
            .ConfigureFunctionsApplicationInsights();
    })
    .ConfigureLogging(logging =>
    { // to remove the default app insights logging filter that instructs logger to capture only warnings and more severe logs. See 
        // https://learn.microsoft.com/en-us/azure/azure-functions/dotnet-isolated-process-guide?tabs=hostbuilder%2Cwindows#managing-log-levels
        logging.Services.Configure<LoggerFilterOptions>(options =>
        {
            LoggerFilterRule defaultRule = options.Rules.FirstOrDefault(rule => rule.ProviderName
                == "Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider");
            if (defaultRule is not null)
            {
                options.Rules.Remove(defaultRule);
            }
        });
    })
    .Build();

    host.Run();

namespace LoraKeysManagerFacade
{
    public static class Globals
    {
        public static readonly string WebJobsStorageClientName = "WebJobsStorage";
    }
}

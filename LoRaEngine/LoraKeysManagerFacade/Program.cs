// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


// upgraded based on this MS beast: https://learn.microsoft.com/en-us/azure/azure-functions/migrate-dotnet-to-isolated-model?tabs=net8

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using LoraKeysManagerFacade.FunctionBundler;
using LoraKeysManagerFacade;
using LoRaTools.ADR;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using LoRaTools.IoTHubImpl;
using Microsoft.Azure.Devices;
using System.Net.Http;
using Microsoft.Extensions.Azure;

// for configuration, see: https://learn.microsoft.com/en-us/azure/azure-functions/dotnet-isolated-process-guide?tabs=hostbuilder%2Cwindows#register-azure-clients
// for running locally, see: 

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((hostContext, services) =>
    {
        const string iotHubConnectionStringKey = "IoTHubConnectionString";
        const string redisConnectionStringKey = "RedisConnectionString";
        const string storageConnectionStringKey = "AzureWebJobsStorage";

        var redis = ConnectionMultiplexer.Connect(hostContext.Configuration.GetConnectionString(redisConnectionStringKey));
        var redisCache = redis.GetDatabase();
        var deviceCacheStore = new LoRaDeviceCacheRedisStore(redisCache);

        services
            .AddAzureClients(builder =>
            {
                _ = builder.AddBlobServiceClient(hostContext.Configuration.GetConnectionString(storageConnectionStringKey))
                    .WithName(Globals.WebJobsStorageClientName);
            });
        _ = services
            .AddHttpClient()
            .AddSingleton(sp => IoTHubRegistryManager.CreateWithProvider(() =>
                RegistryManager.CreateFromConnectionString(hostContext.Configuration.GetConnectionString(iotHubConnectionStringKey)),
                sp.GetRequiredService<IHttpClientFactory>(),
                sp.GetRequiredService<ILogger<IoTHubRegistryManager>>()))
            .AddSingleton<IServiceClient>(new ServiceClientAdapter(ServiceClient.CreateFromConnectionString(hostContext.Configuration.GetConnectionString(iotHubConnectionStringKey))))
            .AddSingleton<ILoRaDeviceCacheStore>(deviceCacheStore)
            .AddSingleton<ILoRaADRManager>(sp => new LoRaADRServerManager(new LoRaADRRedisStore(redisCache, sp.GetRequiredService<ILogger<LoRaADRRedisStore>>()),
                new LoRaADRStrategyProvider(sp.GetRequiredService<ILoggerFactory>()),
                deviceCacheStore,
                sp.GetRequiredService<ILoggerFactory>(),
                sp.GetRequiredService<ILogger<LoRaADRServerManager>>()))
            .AddSingleton<IChannelPublisher>(sp => new RedisChannelPublisher(redis, sp.GetRequiredService<ILogger<RedisChannelPublisher>>()))
            .AddSingleton<DeviceGetter>()
            .AddSingleton<IEdgeDeviceGetter, EdgeDeviceGetter>()
            .AddSingleton<FCntCacheCheck>()
            .AddSingleton<FunctionBundlerFunction>()
            .AddSingleton<IFunctionBundlerExecutionItem, NextFCntDownExecutionItem>()
            .AddSingleton<IFunctionBundlerExecutionItem, DeduplicationExecutionItem>()
            .AddSingleton<IFunctionBundlerExecutionItem, ADRExecutionItem>()
            .AddSingleton<IFunctionBundlerExecutionItem, PreferredGatewayExecutionItem>()
            .AddSingleton<LoRaDevAddrCache>()
            .AddApplicationInsightsTelemetry();
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

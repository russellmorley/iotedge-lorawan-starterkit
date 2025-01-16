// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraDeviceManager.ADR
{
    using System.Threading.Tasks;
    using LoraDeviceManager;
    using LoRaTools.ADR;
    using LoRaTools.CacheStore;
    using LoRaWan;
    using Microsoft.Extensions.Logging;

    public class LoRaADRServerManager : LoRaADRManagerBase
    {
        private readonly ICacheStore cacheStore;
        private readonly ILoggerFactory loggerFactory;

        public LoRaADRServerManager(ILoRaADRStore store,
                                    ILoRaADRStrategyProvider strategyProvider,
                                    ICacheStore cacheStore,
                                    ILoggerFactory loggerFactory,
                                    ILogger<LoRaADRServerManager> logger)
            : base(store, strategyProvider, logger)
        {
            this.cacheStore = cacheStore;
            this.loggerFactory = loggerFactory;
        }

        public override async Task<uint> NextFCntDown(DevEui devEUI, string gatewayId, uint clientFCntUp, uint clientFCntDown)
        {
            var loraDeviceManager = new LoraDeviceManagerImpl(null, cacheStore, null, null, null, loggerFactory.CreateLogger<LoraDeviceManagerImpl>());
            return await loraDeviceManager.GetNextFCntDownAsync(devEUI, gatewayId, clientFCntUp, clientFCntDown);
        }
    }
}

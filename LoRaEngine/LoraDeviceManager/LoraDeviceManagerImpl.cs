using LoRaTools;
using LoRaWan;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LoRaTools.Utils;
using System.Globalization;
using System.IO;
using Microsoft.Azure.Devices.Common.Exceptions;
using Newtonsoft.Json.Linq;
using LoRaTools.CacheStore;
using System.Linq;
using LoraDeviceManager.FunctionBundler;
using LoraDeviceManager.Cache;
using LoraDeviceManager.Exceptions;
using LoRaTools.FunctionBundler;
using LoRaTools.BasicsStation.Processors;


namespace LoraDeviceManager
{
    public class LoraDeviceManagerImpl : ILoraDeviceManager
    {
        internal const string CupsPropertyName = "cups";
        internal const string CupsCredentialsUrlPropertyName = "cupsCredentialUrl";
        internal const string LnsCredentialsUrlPropertyName = "tcCredentialUrl";
        internal const string CupsFwUrlPropertyName = "fwUrl";

        private readonly IDeviceRegistryManager deviceRegistryManager;
        private readonly ICacheStore cacheStore;
        private readonly IBlobStorageManager blobStorageManager;
        private readonly LoRaDevAddrCache loRaDevAddrCache;
        private readonly FunctionBundler.IFunctionBundlerExecutionItem[] executionItems;
        private readonly ILogger<LoraDeviceManagerImpl> logger;

        public LoraDeviceManagerImpl(
            IDeviceRegistryManager deviceRegistryManager,
            ICacheStore cacheStore,
            IBlobStorageManager blobStorageManager,
            LoRaDevAddrCache loRaDevAddrCache,
            FunctionBundler.IFunctionBundlerExecutionItem[] items,
            ILogger<LoraDeviceManagerImpl> logger)
        {
            this.deviceRegistryManager = deviceRegistryManager;
            this.cacheStore = cacheStore;
            this.blobStorageManager = blobStorageManager;
            this.loRaDevAddrCache = loRaDevAddrCache;
            
            items = items ?? Array.Empty<IFunctionBundlerExecutionItem>();
            this.executionItems = items.OrderBy(x => x.Priority).ToArray();
            
            this.logger = logger;
        }

        public async Task AbpFcntCacheReset(DevEui devEui, string gatewayId)
        {
            using (var loraDeviceCache = new LoRaDeviceCache(this.cacheStore, devEui, gatewayId))
            {
                if (await loraDeviceCache.TryToLockAsync())
                {
                    if (loraDeviceCache.TryGetInfo(out var deviceInfo))
                    {
                        // only reset the cache if the current value is larger
                        // than 1 otherwise we likely reset it from another device
                        // and continued processing
                        if (deviceInfo.FCntUp > 1)
                        {
                            this.logger.LogDebug("Resetting cache for device {devEUI}. FCntUp: {fcntup}", devEui, deviceInfo.FCntUp);
                            loraDeviceCache.ClearCache();
                        }
                    }
                }
            }
        }

        public async Task<List<IoTHubDeviceInfo>> GetDeviceList(DevEui? devEUI, string gatewayId, DevNonce? devNonce, DevAddr? devAddr)
        {
            var results = new List<IoTHubDeviceInfo>();

            if (devEUI is { } someDevEui)
            {
                var joinInfo = await TryGetJoinInfoAndValidateAsync(someDevEui, gatewayId);

                // OTAA join
                using var deviceCache = new LoRaDeviceCache(this.cacheStore, someDevEui, gatewayId);
                var cacheKeyDevNonce = string.Concat(devEUI, ":", devNonce);

                if (this.cacheStore.StringSet(cacheKeyDevNonce, devNonce?.ToString(), TimeSpan.FromMinutes(5), onlyIfNotExists: true))
                {
                    var iotHubDeviceInfo = new IoTHubDeviceInfo
                    {
                        DevEUI = someDevEui,
                        PrimaryKey = joinInfo.PrimaryKey
                    };

                    results.Add(iotHubDeviceInfo);

                    if (await deviceCache.TryToLockAsync())
                    {
                        deviceCache.ClearCache(); // clear the fcnt up/down after the join
                        this.logger.LogDebug("Removed key '{key}':{gwid}", someDevEui, gatewayId);
                    }
                    else
                    {
                        this.logger.LogWarning("Failed to acquire lock for '{key}'", someDevEui);
                    }
                }
                else
                {
                    this.logger.LogDebug("dev nonce already used. Ignore request '{key}':{gwid}", someDevEui, gatewayId);
                    throw new DeviceNonceUsedException();
                }
            }
            else if (devAddr is { } someDevAddr)
            {
                // ABP or normal message

                // TODO check for sql injection
                var devAddrCache = new LoRaDevAddrCache(this.cacheStore, this.deviceRegistryManager, this.logger, gatewayId);
                if (await devAddrCache.TryTakeDevAddrUpdateLock(someDevAddr))
                {
                    try
                    {
                        if (devAddrCache.TryGetInfo(someDevAddr, out var devAddressesInfo))
                        {
                            for (var i = 0; i < devAddressesInfo.Count; i++)
                            {
                                if (devAddressesInfo[i].DevEUI is { } someDevEuiPrime)
                                {
                                    // device was not yet populated
                                    if (!string.IsNullOrEmpty(devAddressesInfo[i].PrimaryKey))
                                    {
                                        results.Add(devAddressesInfo[i]);
                                    }
                                    else
                                    {
                                        // we need to load the primaryKey from IoTHub
                                        // Add a lock loadPrimaryKey get lock get
                                        devAddressesInfo[i].PrimaryKey = await LoadPrimaryKeyAsync(someDevEuiPrime);
                                        results.Add(devAddressesInfo[i]);
                                        devAddrCache.StoreInfo(devAddressesInfo[i]);
                                    }

                                    // even if we fail to acquire the lock we wont enter in the next condition as devaddressinfo is not null
                                }
                            }
                        }

                        // if the cache results are null, we query the IoT Hub.
                        // if the device is not found is the cache we query, if there was something, it is probably not our device.
                        if (results.Count == 0 && devAddressesInfo == null)
                        {
                            var query = this.deviceRegistryManager.FindLoRaDeviceByDevAddr(someDevAddr);
                            var resultCount = 0;
                            while (query.HasMoreResults)
                            {
                                var page = await query.GetNextPageAsync();

                                foreach (var twin in page)
                                {
                                    if (twin.DeviceId != null)
                                    {
                                        var iotHubDeviceInfo = new DevAddrCacheInfo
                                        {
                                            DevAddr = someDevAddr,
                                            DevEUI = DevEui.Parse(twin.DeviceId),
                                            PrimaryKey = await this.deviceRegistryManager.GetDevicePrimaryKeyAsync(twin.DeviceId),
                                            GatewayId = twin.GetGatewayID(),
                                            NwkSKey = twin.GetNwkSKey(),
                                            LastUpdatedTwins = twin.Properties.Desired.GetLastUpdated()
                                        };
                                        results.Add(iotHubDeviceInfo);
                                        devAddrCache.StoreInfo(iotHubDeviceInfo);
                                    }

                                    resultCount++;
                                }
                            }

                            // todo save when not our devaddr
                            if (resultCount == 0)
                            {
                                devAddrCache.StoreInfo(new DevAddrCacheInfo()
                                {
                                    DevAddr = someDevAddr
                                });
                            }
                        }
                    }
                    finally
                    {
                        _ = devAddrCache.ReleaseDevAddrUpdateLock(someDevAddr);
                    }
                }
            }
            else
            {
                throw new ArgumentException("Missing devEUI or devAddr");
            }

            return results;
        }

        public async Task<uint> GetNextFCntDownAsync(DevEui devEui, string gatewayId, uint fCntUp, uint fCntDown)
        {
            uint newFCntDown = 0;
            using (var loraDeviceCache = new LoRaDeviceCache(this.cacheStore, devEui, gatewayId))
            {
                if (await loraDeviceCache.TryToLockAsync())
                {
                    if (loraDeviceCache.TryGetInfo(out var serverStateForDeviceInfo))
                    {
                        newFCntDown = ProcessExistingDeviceInfo(loraDeviceCache, serverStateForDeviceInfo, gatewayId, fCntUp, fCntDown);
                    }
                    else
                    {
                        newFCntDown = fCntDown + 1;
                        var state = loraDeviceCache.Initialize(fCntUp, newFCntDown);
                    }
                }
            }

            try
            {
                _ = (ushort)newFCntDown;
                return newFCntDown;
            }
            catch (InvalidCastException)
            {
                return 0;
            }
        }
        internal static uint ProcessExistingDeviceInfo(LoRaDeviceCache deviceCache, DeviceCacheInfo cachedDeviceState, string gatewayId, uint clientFCntUp, uint clientFCntDown)
        {
            uint newFCntDown = 0;

            if (cachedDeviceState != null)
            {
                // we have a state in the cache matching this device and now we own the lock
                if (clientFCntUp > cachedDeviceState.FCntUp)
                {
                    // it is a new message coming up by the first gateway
                    if (clientFCntDown >= cachedDeviceState.FCntDown)
                        newFCntDown = clientFCntDown + 1;
                    else
                        newFCntDown = cachedDeviceState.FCntDown + 1;

                    cachedDeviceState.FCntUp = clientFCntUp;
                    cachedDeviceState.FCntDown = newFCntDown;
                    cachedDeviceState.GatewayId = gatewayId;

                    _ = deviceCache.StoreInfo(cachedDeviceState);
                }
                else if (clientFCntUp == cachedDeviceState.FCntUp && gatewayId == cachedDeviceState.GatewayId)
                {
                    // it is a retry message coming up by the same first gateway
                    newFCntDown = cachedDeviceState.FCntDown + 1;
                    cachedDeviceState.FCntDown = newFCntDown;

                    _ = deviceCache.StoreInfo(cachedDeviceState);
                }
            }

            return newFCntDown;
        }

        private async Task<string> LoadPrimaryKeyAsync(DevEui devEUI)
        {
            return await this.deviceRegistryManager.GetDevicePrimaryKeyAsync(devEUI.ToString());
        }

        private async Task<JoinInfo> TryGetJoinInfoAndValidateAsync(DevEui devEUI, string gatewayId)
        {
            var cacheKeyJoinInfo = string.Concat(devEUI, ":joininfo");
            var lockKeyJoinInfo = string.Concat(devEUI, ":joinlockjoininfo");
            JoinInfo joinInfo = null;

            if (await this.cacheStore.LockTakeAsync(lockKeyJoinInfo, gatewayId, TimeSpan.FromMinutes(5)))
            {
                try
                {
                    joinInfo = this.cacheStore.GetObject<JoinInfo>(cacheKeyJoinInfo);
                    if (joinInfo == null)
                    {
                        joinInfo = new JoinInfo
                        {
                            PrimaryKey = await this.deviceRegistryManager.GetDevicePrimaryKeyAsync(devEUI.ToString())
                        };

                        var twin = await this.deviceRegistryManager.GetLoRaDeviceTwinAsync(devEUI.ToString());
                        var deviceGatewayId = twin.GetGatewayID();
                        if (!string.IsNullOrEmpty(deviceGatewayId))
                        {
                            joinInfo.DesiredGateway = deviceGatewayId;
                        }

                        _ = this.cacheStore.ObjectSet(cacheKeyJoinInfo, joinInfo, TimeSpan.FromMinutes(60));
                        this.logger.LogDebug("updated cache with join info '{key}':{gwid}", devEUI, gatewayId);
                    }
                }
                finally
                {
                    _ = this.cacheStore.LockRelease(lockKeyJoinInfo, gatewayId);
                }

                if (string.IsNullOrEmpty(joinInfo.PrimaryKey))
                {
                    throw new JoinRefusedException("Not in our network.");
                }

                if (!string.IsNullOrEmpty(joinInfo.DesiredGateway) &&
                    !joinInfo.DesiredGateway.Equals(gatewayId, StringComparison.OrdinalIgnoreCase))
                {
                    throw new JoinRefusedException($"Not the owning gateway. Owning gateway is '{joinInfo.DesiredGateway}'");
                }

                this.logger.LogDebug("got LogInfo '{key}':{gwid} attached gw: {desiredgw}", devEUI, gatewayId, joinInfo.DesiredGateway);
            }
            else
            {
                throw new JoinRefusedException("Failed to acquire lock for joininfo");
            }

            return joinInfo;
        }

        public async Task<string> GetDevicePrimaryKey(string eui)
        {
            return await deviceRegistryManager.GetDevicePrimaryKeyAsync(eui);
        }

        public async Task<string> GetStationCredentials(StationEui eui, ConcentratorCredentialType credentialtype, CancellationToken cancellationToken)
        {
            using var stationScope = this.logger.BeginEuiScope(eui);

            var twin = await this.deviceRegistryManager.GetTwinAsync(eui.ToString(), cancellationToken);
            if (twin != null)
            {
                this.logger.LogInformation("Retrieving '{CredentialType}' for '{StationEui}'.", credentialtype.ToString(), eui);

                if (!twin.Properties.Desired.TryReadJsonBlock(CupsPropertyName, out var cupsProperty))
                    throw new ArgumentOutOfRangeException(CupsPropertyName, "failed to read");

                var parsedJson = JObject.Parse(cupsProperty);
                var url = credentialtype is ConcentratorCredentialType.Lns ? parsedJson[LnsCredentialsUrlPropertyName].ToString()
                                                                            : parsedJson[CupsCredentialsUrlPropertyName].ToString();
                return await blobStorageManager.GetBase64EncodedBlobAsync(url, cancellationToken);
            }
            else
            {
                this.logger.LogInformation($"Searching for {eui} returned 0 devices");
                return string.Empty;
            }
        }

        public async Task<(long length, Stream contentStream)> GetStationFirmware(StationEui eui, CancellationToken token)
        {
            using var stationScope = this.logger.BeginEuiScope(eui);

            var twin = await this.deviceRegistryManager.GetTwinAsync(eui.ToString("N", CultureInfo.InvariantCulture), token);
            if (twin != null)
            {
                this.logger.LogDebug("Retrieving firmware url for '{StationEui}'.", eui);
                if (!twin.Properties.Desired.TryReadJsonBlock(CupsPropertyName, out var cupsProperty))
                    throw new ArgumentOutOfRangeException(CupsPropertyName, "Failed to read CUPS config");

                var fwUrl = JObject.Parse(cupsProperty)[CupsFwUrlPropertyName].ToString();
                return await blobStorageManager.GetBlobStreamAsync(fwUrl, token);
            }
            else
            {
                throw new DeviceNotFoundException(eui.ToString());
            }
        }

        public Task AddDevice(DevAddr devAddr, DevEui? devEui, string gatewayId, string NwkSKeyString)
        {
            this.loRaDevAddrCache.StoreInfo(new DevAddrCacheInfo
            {
                DevAddr = devAddr,
                DevEUI = devEui,
                GatewayId = gatewayId,
                NwkSKey = NwkSKeyString
            });
            return Task.CompletedTask;
        }

        public async Task SyncLoraDevAddrCacheWithRegistry()
        {
            await loRaDevAddrCache.PerformNeededSyncs(deviceRegistryManager);
        }

        public async Task<FunctionBundlerResult> ExecuteFunctionBundlerAsync(DevEui devEUI, FunctionBundlerRequest request)
        {
            var pipeline = new FunctionBundlerPipelineExecuter(executionItems, devEUI, request, this.logger);
            return await pipeline.Execute();
        }
    }
}

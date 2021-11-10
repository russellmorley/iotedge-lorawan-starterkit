# Caching

## Function

The function is utilizing a [Redis](https://redis.io/) cache to store device related information. It is composed of multiple cache entries:

### LoRaDeviceCache

Stores an instance of type [`DeviceCacheInfo`](https://github.com/Azure/iotedge-lorawan-starterkit/blob/dev/LoRaEngine/LoraKeysManagerFacade/DeviceCacheInfo.cs) by DevEUI to keep track of FCntUp, FCntDown, GatewayId per LoRaWAN Network Server (LNS). The cache is used to have a distributed lock in a multi gateway scenario. The info per gateway is stored using the DevEUI to determine what GW is allowed to process a particular message and respond to the sending device.

All the values in this cache are LoRaWAN related and don't require any other information than what we get from the device and the gateway handling a particular message.

This cache needs to be reset, when a device re-joins.

```c#
public class DeviceCacheInfo
{
  public uint FCntUp { get; set; }
  public uint FCntDown { get; set; }
  public string GatewayId { get; set; }
}
```

[iotedge-lorawan-starterkit/DeviceCacheInfo.cs at dev · Azure/iotedge-lorawan-starterkit (github.com)](https://github.com/Azure/iotedge-lorawan-starterkit/blob/dev/LoRaEngine/LoraKeysManagerFacade/DeviceCacheInfo.cs)

### LoRaDevAddrCache

The `LoRaDevAddrCache` contains important information from the IoT Hub we require for different scenarios. Most of the information is stored in device twins that are loaded and synchronized on a predefined schedule. Twins queries have strict limits in terms of reads/device and module [Understand Azure IoT Hub quotas and throttling | Microsoft Docs](https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-devguide-quotas-throttling#operation-throttles). Therefore this cache was put in the middle to handle the higher load we would generate to read out the information stored in IoT Hub.

The cache is organized as a HSET - [HSET – Redis](https://redis.io/commands/hset) - The key being the DevAddr and individual DevEUI as the field as multiple devices can have the same DevAddr. The values are [`DevAddrCacheInfo`](https://github.com/Azure/iotedge-lorawan-starterkit/blob/dev/LoRaEngine/LoraKeysManagerFacade/DevAddrCacheInfo.cs).

```c#
public class DevAddrCacheInfo : IoTHubDeviceInfo
{
  public string GatewayId { get; set; }
  public DateTime LastUpdatedTwins { get; set; }
  public string NwkSKey { get; set; }
}

public class IoTHubDeviceInfo
{
  public string DevAddr { get; set; }
  public string DevEUI { get; set; }
  public string PrimaryKey { get; set; }
}
```

[iotedge-lorawan-starterkit/DevAddrCacheInfo.cs at dev · Azure/iotedge-lorawan-starterkit (github.com)](https://github.com/Azure/iotedge-lorawan-starterkit/blob/dev/LoRaEngine/LoraKeysManagerFacade/DevAddrCacheInfo.cs)

This cache is automatically being populated on a schedule. We have a function trigger [`SyncDevAddrCache`](https://github.com/Azure/iotedge-lorawan-starterkit/blob/dev/LoRaEngine/LoraKeysManagerFacade/SyncDevAddrCache.cs) that is triggered on a regular basis (currently every 5min) to validate what synchronization is required.

If the system does warm up, it will trigger a full reload. The full reload fetches all devices from the registry and synchronizes the relevant values from the twins. The sync process, does not synchronize the private key of the device from IoT hub (they will be loaded on request).

The full reload will be performed at most once every 24h (unless the redis cache is completely cleared). The incremental updates do make sure we only load the delta using the timestamps on the desired and reported property:

```c#
var query = $"SELECT * FROM devices where properties.desired.$metadata.$lastUpdated >= '{lastUpdate}' OR properties.reported.$metadata.DevAddr.$lastUpdated >= '{lastUpdate}'";
```

### Join related caching

When we receive OTAA requests, we manage the potential of conflicting with multiple gateways as well with the redis cache. We maintain 2 caches:

1. #### devnonce

   The devnonce keeps track of nonce values sent by the device for a join request. It makes sure the same join request is only handled once by one Gateway. The key is composed of the [DevEUI]:[DevNonce] values. It's evicted after 5min it was added to the cache.

2. #### Join-info

The Join-info cache contains information required when a new device joins the network. The cache is keyed by [DevEUI]:joininfo and is valid for 60min after initial creation.

   ```c#
   public class JoinInfo
   {
     public string PrimaryKey { get; set; }
     public string DesiredGateway { get; set; }
   }
   ```

>[iotedge-lorawan-starterkit/JoinInfo.cs at dev · Azure/iotedge-lorawan-starterkit (github.com)](https://github.com/Azure/iotedge-lorawan-starterkit/blob/dev/LoRaEngine/LoraKeysManagerFacade/JoinInfo.cs)

The DesiredGateway is used to set if a specific gateway needs to process requests coming from a device. If the value is not set, the first one to win the race, will handle the join.

The PrimaryKey is used to create the device connection from the edge gateway to IoT Hub.

## Edge Gateway

### Device Cache

Every device sending messages to the edge, is validated if it belongs to our network and our gateway. If it is our gateway, we build up a local representation of the device in memory including a connection to IoT Hub. The devices are cached for a specific amount of time in the [`LoRaDeviceRegistry`](https://github.com/Azure/iotedge-lorawan-starterkit/blob/dev/LoRaEngine/modules/LoRaWanNetworkSrvModule/LoRaWan.NetworkServer/LoRaDeviceRegistry.cs).

The [`LoRaDeviceRegistry`](https://github.com/Azure/iotedge-lorawan-starterkit/blob/dev/LoRaEngine/modules/LoRaWanNetworkSrvModule/LoRaWan.NetworkServer/LoRaDeviceRegistry.cs) stores the [`LoRaDevice`](https://github.com/Azure/iotedge-lorawan-starterkit/blob/dev/LoRaEngine/modules/LoRaWanNetworkSrvModule/LoRaWan.NetworkServer/LoRaDevice.cs) with 3 entries:

1. We maintain a dictionary by DevAddr that contains an entry per DevEUI for a particular device - valid for 2 days
2. The device is stored directly using the DevEUI for fast lookup using deveui:[DevEUI] - no expiration
3. When a DevAddr entry (1) is not ready, we initialize a [`DeviceLoaderSynchronizer`](https://github.com/Azure/iotedge-lorawan-starterkit/blob/dev/LoRaEngine/modules/LoRaWanNetworkSrvModule/LoRaWan.NetworkServer/DeviceLoaderSynchronizer.cs) to fetch matching devices from the function API. The loader itself is put in cache, to be able to handle requests, while we are in the process of loading them. - valid for 30s.

Class C device cases use the DevEUI directly for downstream message sending (2). All other cases make use of the first cache by DevAddr.

The Device Cache can be forcefully invalidated - [Quickstart - Cache Clearing](quickstart.md#cache-clearing).

### Connections

We maintain connections to the IoT hub for all devices that belong to us for which we received messages. The connection is cached per device in the [`LoRaDeviceClientConnectionManager`](https://github.com/Azure/iotedge-lorawan-starterkit/blob/dev/LoRaEngine/modules/LoRaWanNetworkSrvModule/LoRaWan.NetworkServer/LoRaDeviceClientConnectionManager.cs). We do establish the connection to IoT hub with the PrimaryKey of the device using the standard [DeviceClient Class (Microsoft.Azure.Devices.Client) - Azure for .NET Developers | Microsoft Docs](https://docs.microsoft.com/en-us/dotnet/api/microsoft.azure.devices.client.deviceclient?view=azure-dotnet).

Connections are closed when the [`LoRaDevice`](https://github.com/Azure/iotedge-lorawan-starterkit/blob/dev/LoRaEngine/modules/LoRaWanNetworkSrvModule/LoRaWan.NetworkServer/LoRaDevice.cs) is disposed.
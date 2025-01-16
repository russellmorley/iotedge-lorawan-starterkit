// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System.Linq;
    using global::LoRaTools.Services;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Newtonsoft.Json;
    using Xunit;

    public class IotHubDeviceServiceInfoTests
    {
        public static TheoryData<IoTHubDeviceServiceInfo> Serialize_Deserialize_Composition_Should_Preserve_Information_TheoryData() => TheoryDataFactory.From(
            from networkSessionKey in new[] { (NetworkSessionKey?)null, TestKeys.CreateNetworkSessionKey(3) }
            select new IoTHubDeviceServiceInfo
            {
                DevAddr = new DevAddr(1),
                DevEUI = new DevEui(2),
                GatewayId = "foo",
                NwkSKey = networkSessionKey,
                PrimaryKey = TestKeys.CreateAppKey(4).ToString()
            });

        [Theory]
        [MemberData(nameof(Serialize_Deserialize_Composition_Should_Preserve_Information_TheoryData))]
        public void Serialize_Deserialize_Composition_Should_Preserve_Information(IoTHubDeviceServiceInfo initial)
        {
            // act
            var result = JsonConvert.DeserializeObject<IoTHubDeviceServiceInfo>(JsonConvert.SerializeObject(initial));

            // assert
            Assert.Equal(initial.DevAddr, result.DevAddr);
            Assert.Equal(initial.DevEUI, result.DevEUI);
            Assert.Equal(initial.GatewayId, result.GatewayId);
            Assert.Equal(initial.NwkSKey, result.NwkSKey);
            Assert.Equal(initial.PrimaryKey, result.PrimaryKey);
        }
    }
}

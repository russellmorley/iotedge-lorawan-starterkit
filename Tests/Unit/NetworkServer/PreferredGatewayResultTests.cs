// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using Newtonsoft.Json;
    using Xunit;
    using global::LoRaTools.CacheStore;
    using global::LoRaTools.FunctionBundler;

    public sealed class PreferredGatewayResultTests
    {
        [Fact]
        public void Can_Deserialize()
        {
            // arrange
            var input = new PreferredGatewayResult(12, new LoRaDevicePreferredGateway("gateway", 13));

            // act
            var result = JsonConvert.DeserializeObject<PreferredGatewayResult>(JsonConvert.SerializeObject(input));

            // assert
            Assert.Equal(input.RequestFcntUp, result!.RequestFcntUp);
            Assert.Equal(input.PreferredGatewayID, result.PreferredGatewayID);
            Assert.Equal(input.Conflict, result.Conflict);
            Assert.Equal(input.CurrentFcntUp, result.CurrentFcntUp);
            Assert.Equal(input.ErrorMessage, result.ErrorMessage);
        }
    }
}

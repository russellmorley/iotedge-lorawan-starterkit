// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.Tests.Unit.LoRaTools
{
    using System;
    using System.Collections.Generic;
    using System.Drawing.Imaging;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Net.NetworkInformation;
    using System.Net.WebSockets;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using global::LoRaTools;
    using global::LoRaTools.BasicsStation.Processors;
    using global::LoRaTools.IoTHubImpl;
    using global::LoRaTools.NetworkServerDiscovery;
    using global::LoRaTools.Services;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Azure.Amqp.Framing;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.Extensions.Primitives;
    using Moq;
    using Xunit;

    public sealed class Sha256TenantValidationStrategyTests
    {
        private string _tenantId = Guid.NewGuid().ToString();
        private string _tenantKeyBase64;
        private string _anotherTenantId = Guid.NewGuid().ToString();
        private string _anotherTenantKeyBase64;
        public Sha256TenantValidationStrategyTests()
        {
            byte[] tenantKey = new Byte[64];
            byte[] anotherTenantKey = new byte[64];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                // The arrays are now filled with cryptographically strong random bytes.
                rng.GetBytes(tenantKey);
                rng.GetBytes(anotherTenantKey);
            }
            _tenantKeyBase64 = Convert.ToBase64String(tenantKey);
            _anotherTenantKeyBase64 = Convert.ToBase64String(anotherTenantKey);
        }

        [Fact]
        public Task Create_and_validate_signature()
        {
            var tenantValidationStrategy = new Sha256TenantValidationStrategy() { DoValidation = true };
            // correct key for tenantId
            (string tenantSignature, string tenantSignatureExpireAtTicks) = tenantValidationStrategy.CreateSignature(_tenantId, DateTime.Now + TimeSpan.FromSeconds(120), _tenantKeyBase64);
            Assert.True(tenantValidationStrategy.ValidateSignature(_tenantId, tenantSignatureExpireAtTicks, tenantSignature, _tenantKeyBase64));

            // incorrect key for tenantId
            (tenantSignature, tenantSignatureExpireAtTicks) = tenantValidationStrategy.CreateSignature(_tenantId, DateTime.Now + TimeSpan.FromSeconds(120), _tenantKeyBase64);
            Assert.False(tenantValidationStrategy.ValidateSignature(_tenantId, tenantSignatureExpireAtTicks, tenantSignature, _anotherTenantKeyBase64));

            // valid signature but altered
            (tenantSignature, tenantSignatureExpireAtTicks) = tenantValidationStrategy.CreateSignature(_tenantId, DateTime.Now + TimeSpan.FromSeconds(120), _tenantKeyBase64);
            (string alteredTenantSignature, _) = tenantValidationStrategy.CreateSignature(_tenantId.Substring(1), DateTime.Now + TimeSpan.FromSeconds(120), _tenantKeyBase64);
            Assert.False(tenantValidationStrategy.ValidateSignature(_tenantId, tenantSignatureExpireAtTicks, alteredTenantSignature, _tenantKeyBase64));

            // invalid signature
            (tenantSignature, tenantSignatureExpireAtTicks) = tenantValidationStrategy.CreateSignature(_tenantId, DateTime.Now + TimeSpan.FromSeconds(120), _tenantKeyBase64);
            alteredTenantSignature = tenantSignature.Remove(tenantSignature.Length - 1);
            Assert.False(tenantValidationStrategy.ValidateSignature(_tenantId, tenantSignatureExpireAtTicks, alteredTenantSignature, _tenantKeyBase64));

            // expire at ticks altered
            (tenantSignature, tenantSignatureExpireAtTicks) = tenantValidationStrategy.CreateSignature(_tenantId, DateTime.Now + TimeSpan.FromSeconds(120), _tenantKeyBase64);
            var alteredTenantSignatureExpireAtTicks = (new DateTime(long.Parse(tenantSignatureExpireAtTicks, CultureInfo.InvariantCulture)) + TimeSpan.FromSeconds(1)).Ticks.ToString(CultureInfo.InvariantCulture);
            Assert.False(tenantValidationStrategy.ValidateSignature(_tenantId, alteredTenantSignatureExpireAtTicks, tenantSignature, _tenantKeyBase64));

            return Task.CompletedTask;
        }

        [Fact]
        public async Task Validate_request()
        {
            var callingGatewayId = "gatewayId";
            var expireAtInFuture = DateTime.Now + TimeSpan.FromMinutes(5);
            var expireAtInPast = DateTime.Now - TimeSpan.FromMinutes(5);

            var twin = new Twin();
            twin.Tags["tenantid"] = _tenantId;
            twin.Properties.Desired[TwinPropertiesConstants.TenantKeyName] = _tenantKeyBase64;
            var deviceRegistryManagerMock = new Mock<IDeviceRegistryManager>();
            deviceRegistryManagerMock
                .Setup(drm => drm.GetTwinAsync(
                    It.IsAny<string>(), 
                    It.IsAny<CancellationToken?>()))
                .Returns(Task.FromResult((IDeviceTwin) new IoTHubDeviceTwin(twin)));

            var tenantValidationStrategy = new Sha256TenantValidationStrategy(deviceRegistryManagerMock.Object) { DoValidation = true };

            var httpRequest = new Mock<HttpRequest>();
            var queryParameters = new Dictionary<string, string>();
            tenantValidationStrategy.AddQueryParameters(queryParameters, callingGatewayId, _tenantId, _tenantKeyBase64, expireAtInFuture);
            var queryCollection = new QueryCollection(new Dictionary<string, StringValues>(
                queryParameters
                    .Select(kv => new KeyValuePair<string, StringValues>(kv.Key, new StringValues(kv.Value)))
                ));
            httpRequest.SetupGet(x => x.Query).Returns(queryCollection);

            var tenantId = await tenantValidationStrategy.ValidateRequest(httpRequest.Object);
            Assert.Equal(_tenantId, tenantId);
        }
    }
}

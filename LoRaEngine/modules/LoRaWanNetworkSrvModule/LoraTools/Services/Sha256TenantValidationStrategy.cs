using LoRaWan;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace LoRaTools.Services
{
    public class Sha256TenantValidationStrategy : ITenantValidationStrategy
    {
        private readonly string _stringToSignTemplate = "{0}%{1}";
        private readonly IDeviceRegistryManager? _deviceRegistryManager;
        private readonly string _callerIdKeyName = "callerid";
        private readonly string _signatureExpiresAtTicksKeyName = "signatureexpiresatticks";
        private readonly string _signatureKeyName = "signature";
        public string IdKeyName { get; set; } = TwinPropertiesConstants.TenantIdName;

        public bool DoValidation { get; set; } //default of bool is false

        public Sha256TenantValidationStrategy(IDeviceRegistryManager? deviceRegistryManager = null)
        {
            this._deviceRegistryManager = deviceRegistryManager;
        }

        public Dictionary<string, string> AddQueryParameters(
            Dictionary<string, string> queryParameters,
            string callerId,
            string id,
            string key,
            DateTime signatureExpireAt)
        {
            if (DoValidation)
            {
                var (signature, signatureExpireAtTicks) = CreateSignature(id, signatureExpireAt, key);

                queryParameters.Add(_callerIdKeyName, callerId);
                queryParameters.Add(IdKeyName, id);
                queryParameters.Add(_signatureExpiresAtTicksKeyName, signatureExpireAtTicks);
                queryParameters.Add(_signatureKeyName, signature);
            }
            return queryParameters;
        }

        public async Task<string?> ValidateRequestAndEui(string euiString, HttpRequest request)
        {
            var id = await ValidateRequest(request);
            if (id != null)
            {
                var deviceTwin = await (_deviceRegistryManager?.GetTwinAsync(euiString) ?? throw new NotSupportedException("deviceRegistryManager not set"));
                string deviceTwinIdTag = deviceTwin.GetTag(IdKeyName);
                if (deviceTwinIdTag != id)
                {
                    throw new ArgumentException($"Twin for device {euiString} either doesn't have a tag {IdKeyName} or its value doesn't match query value {IdKeyName} .");
                }
            }
            return id;
        }

        public async Task<string?> ValidateRequest(HttpRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (DoValidation)
            {
                string? id = request.Query[IdKeyName];
                if (id == null || id.Length == 0)
                {
                    throw new InvalidDataException($"Query parameter {IdKeyName} was not found or empty string.");
                }
                var signature = request.Query[_signatureKeyName];
                if (signature == StringValues.Empty)
                {
                    throw new InvalidDataException($"Query parameter {_signatureKeyName} was not present");
                }
                var signatureExpiresAtTicks = request.Query[_signatureExpiresAtTicksKeyName];
                var parsedTicksSuccessfully = long.TryParse(signatureExpiresAtTicks, out long ticks);
                if (!parsedTicksSuccessfully)
                {
                    throw new InvalidDataException($"Query parameter {_signatureExpiresAtTicksKeyName} was either not provided or not a valid value ");
                }
                if (DateTime.Now.Ticks > ticks)
                {
                    throw new InvalidDataException($"Query parameter {_signatureExpiresAtTicksKeyName} was is in the past");
                }

                var callerId = request.Query[_callerIdKeyName];

                //Get the tenantid and key for the calling gatewayid and validate against the signature.
                var callerDeviceTwin = await _deviceRegistryManager?.GetTwinAsync(callerId!) ?? throw new NotSupportedException("deviceRegistryManager not set");
                string callerTwinIdTag = callerDeviceTwin.GetTag(IdKeyName);
                if (callerTwinIdTag != id)
                {
                    throw new InvalidDataException($"Twin for caller device {callerId} either doesn't have a tag {IdKeyName} or its value doesn't match query value {IdKeyName} ."); 
                }
                if (!ValidateSignature(
                    id,
                    signatureExpiresAtTicks!,
                    signature!,
                    (string)callerDeviceTwin.Properties.Desired[TwinPropertiesConstants.TenantKeyName]))
                    throw new InvalidDataException("Invalid tenant key.");

                return id;
            }
            else
            {
                return null;
            }
        }

        public (string, string) CreateSignature(string id, DateTime signatureExpireAt, string key)
        {
            if (!DoValidation)
                return (string.Empty, string.Empty);

            using HMACSHA256 hmac = new HMACSHA256(Convert.FromBase64String(key));
            var stringToHash = string.Format(CultureInfo.InvariantCulture, _stringToSignTemplate, id, signatureExpireAt.Ticks);
            return (Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToHash))), signatureExpireAt.Ticks.ToString(CultureInfo.InvariantCulture));
        }

        public bool ValidateSignature(string id, string signatureExpiresAtTicks, string signature, string key)
        {
            if (!DoValidation)
                return true;

            using HMACSHA256 hmac = new HMACSHA256(Convert.FromBase64String(key));
            var stringToSign = string.Format(CultureInfo.InvariantCulture, _stringToSignTemplate, id, signatureExpiresAtTicks);
            return signature == Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign)));
        }
    }
}

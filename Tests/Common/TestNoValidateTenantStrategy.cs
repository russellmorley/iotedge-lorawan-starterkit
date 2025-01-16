using LoRaTools.Services;
using Microsoft.AspNetCore.Http;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LoRaWan.Tests.Common
{
    public class TestNoValidateTenantStrategy : ITenantValidationStrategy
    {
        public bool DoValidation { get => false; set => throw new NotImplementedException(); }
        public string IdKeyName { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public Dictionary<string, string> AddQueryParameters(Dictionary<string, string> queryParameters, string callerId, string id, string key, DateTime signatureExpireAt)
        {
            return queryParameters;
        }

        public (string, string) CreateSignature(string id, DateTime signatureExpireAt, string key)
        {
            return new(string.Empty, string.Empty);
        }

        public Task<string> ValidateRequestAndEui(string euiString, HttpRequest request)
        {
            return Task.FromResult<string>(null);
        }

        public bool ValidateSignature(string id, string signatureExpiresAtTicks, string signature, string key)
        {
            return true;
        }

        public Task<string> ValidateRequest(HttpRequest request)
        {
            return Task.FromResult<string>(null);
        }
    }
}

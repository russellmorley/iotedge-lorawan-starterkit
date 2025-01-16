using LoRaWan;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LoRaTools.Services
{
    public interface ITenantValidationStrategy
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="queryParameters"></param>
        /// <param name="callerId"></param>
        /// <param name="id"></param>
        /// <param name="key">key in base64 format</param>
        /// <param name="signatureExpireAt"></param>
        /// <returns></returns>
        Dictionary<string, string> AddQueryParameters(
            Dictionary<string, string> queryParameters,
            string callerId,
            string id,
            string key,
            DateTime signatureExpireAt);

        /// <summary>
        /// Validates that the IdKeyName provided in the http request's query is valid, matches tag IdKeyName in callerId's twin, and matches tag IdKeyName in device eui's twin.
        /// </summary>
        /// <param name="euiString"></param>
        /// <param name="request"></param>
        /// <returns>the value of IdKeyName if valid and ServiceValidatesId, else null</returns>
        /// <exception cref="InvalidDataException"></exception>
        /// <exception cref="ArgumentException">eui doesn't have an IdKeyName tag or it's value doesn't match value for requests' IdKeyName parameter.</exception>
        Task<string> ValidateRequestAndEui(string euiString, HttpRequest request);

        /// <summary>
        /// Validates that the IdKeyName provided in the http request's query is valid and matches tag IdKeyName in callerId's twin.
        /// <param name="request"></param>
        /// <returns>the value of IdKeyName if valid and ServiceValidatesId, else null</returns>
        /// <exception cref="InvalidDataException"></exception>
        Task<string?> ValidateRequest(HttpRequest request);

        /// <summary>
        /// Creates a signature based on the combination of id and expiration given the key
        /// </summary>
        /// <param name="id">can contain any valid string character but %</param>
        /// <param name="key">a base64 string. Can be any length, but it is recommended it results in a byte array 64 bytes in length. 
        /// If the resulting byte array is more than 64 bytes long, it is hashed (using SHA-256) to derive a 32-byte key.</param>
        /// <param name="signatureExpireAt"></param>
        /// <returns>a tuple of:
        /// 1. base64 signature if id is not null or empty, otherwise an empty string
        /// 2. expireAt as culture invariant ticks</returns>
        (string, string) CreateSignature(string id, DateTime signatureExpireAt, string key);

        /// <summary>
        /// Validates that the combination of id and signatureExpiresAtTicks match the provided signature given the key 
        /// </summary>
        /// <param name="id"></param>
        /// <param name="key"></param>
        /// <param name="signatureExpiresAtTicks"></param>
        /// <param name="signature"></param>
        /// <returns>true if the signature matches the parameters or id is null or empty, otherwise false.</returns>
        bool ValidateSignature(string id, string signatureExpiresAtTicks, string signature, string key);

        public bool DoValidation { get; set; }
        public string IdKeyName { get; set; }
    }
}

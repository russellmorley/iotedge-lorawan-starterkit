// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Integration
{
    using LoraDeviceManager.Cache;
    using LoRaTools.CacheStore;
    using System;
    using System.Threading.Tasks;

    internal static class LockDevAddrHelper
    {
        private const string FullUpdateKey = "fullUpdateKey";
        private const string GlobalDevAddrUpdateKey = "globalUpdateKey";

        public static async Task TakeLocksAsync(ICacheStore cacheStore, string[] lockNames)
        {
            if (lockNames?.Length > 0)
            {
                foreach (var locks in lockNames)
                {
                    await cacheStore.LockTakeAsync(locks, locks, TimeSpan.FromMinutes(3));
                }
            }
        }

        public static void ReleaseAllLocks(ICacheStore cacheStore)
        {
            cacheStore.KeyDelete(GlobalDevAddrUpdateKey);
            cacheStore.KeyDelete(FullUpdateKey);
        }

        public static async Task PrepareLocksForTests(ICacheStore cacheStore, string[] locksGuideTest = null)
        {
            LockDevAddrHelper.ReleaseAllLocks(cacheStore);
            await LockDevAddrHelper.TakeLocksAsync(cacheStore, locksGuideTest);
        }

        public static DevAddrCacheInfo GetCachedDevAddr(ICacheStore cacheStore, string key) => cacheStore.GetObject<DevAddrCacheInfo>(key);
    }
}

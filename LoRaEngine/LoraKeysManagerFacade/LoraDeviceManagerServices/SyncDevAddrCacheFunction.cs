
namespace LoraKeysManagerFacade.LoraDeviceManagerServices
{
    using System;
    using System.Threading.Tasks;
    using LoRaTools;
    using Microsoft.Extensions.Logging;
    using Microsoft.Azure.Functions.Worker;
    using LoraDeviceManager;

    public class SyncDevAddrCacheFunction
    {
        private readonly ILoraDeviceManager loraDeviceManager;
        private readonly IDeviceRegistryManager registryManager;

        public SyncDevAddrCacheFunction(ILoraDeviceManager loraDeviceManager)
        {
            this.loraDeviceManager = loraDeviceManager;
        }

        [Function(nameof(SyncDevAddrCache))]
        public async Task SyncDevAddrCache([TimerTrigger("0 */5 * * * *")] TimerInfo myTimer, ILogger log)
        {
            if (myTimer is null) throw new ArgumentNullException(nameof(myTimer));

            log.LogDebug($"{(myTimer.IsPastDue ? "The timer is past due" : "The timer is on schedule")}, Function last ran at {myTimer.ScheduleStatus.Last} Function next scheduled run at {myTimer.ScheduleStatus.Next})");

            await loraDeviceManager.SyncLoraDevAddrCacheWithRegistry();
        }
    }
}

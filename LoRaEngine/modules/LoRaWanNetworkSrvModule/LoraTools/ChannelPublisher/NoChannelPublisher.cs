namespace LoRaTools.ChannelPublisher
{

    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using LoRaTools;
    using System.Text.Json;
    using System;

    public class NoChannelPublisher : IChannelPublisher
    {
        public NoChannelPublisher()
        {
        }

        public Task PublishAsync(string channel, LnsRemoteCall lnsRemoteCall)
        {
            throw new NotImplementedException();
        }
    }
}

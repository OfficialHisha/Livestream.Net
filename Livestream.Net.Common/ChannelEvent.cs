using System;
using System.Collections.Generic;
using System.Text;

namespace Livestream.Net.Common
{
    public abstract class ChannelEvent
    {
        public Platform Platform { get; }
        public string Channel { get; }
        public string Sender { get; }

        public ChannelEvent(Platform platform, string channel, string sender)
        {
            Platform = platform;
            Channel = channel;
            Sender = sender;
        }
    }
}

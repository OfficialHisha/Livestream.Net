using System;
using System.Collections.Generic;
using System.Text;

namespace Livestream.Net.Common
{
    public class DonationEvent : ChannelEvent
    {
        public string Symbol { get; }
        public decimal Quantity { get; }

        public DonationEvent(Platform platform, string channel, string sender, string symbol, decimal quantity) : base(platform, channel, sender)
        {
            Symbol = symbol;
            Quantity = quantity;
        }
    }
}

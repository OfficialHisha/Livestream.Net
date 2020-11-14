using Livestream.Net.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace Livestream.Net.Dlive.Chat
{
    public class DliveDonationEvent : DonationEvent
    {
        public DliveDonationEvent(string channel, string user, string symbol, decimal quantity) : base(Platform.Dlive, channel, user, symbol, quantity)
        {

        }
    }
}

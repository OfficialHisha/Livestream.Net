using System;
using System.Collections.Generic;
using System.Text;

namespace Livestream.Net.Common
{
    public class ChatMessage : ChannelEvent
    {
        public string Content { get; }

        public ChatMessage(Platform platform, string channel, string sender, string content) : base(platform, channel, sender)
        {
            Content = content;
        }
    }
}

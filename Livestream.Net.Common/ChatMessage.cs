using System;
using System.Collections.Generic;
using System.Text;

namespace Livestream.Net.Common
{
    public class ChatMessage
    {
        public Platform Platform { get; }
        public string Channel { get; protected set; }
        public string Sender { get; protected set; }
        public string Content { get; protected set; }

        public ChatMessage(Platform platform)
        {
            Platform = platform;
        }
    }
}

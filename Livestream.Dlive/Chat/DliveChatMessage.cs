using Livestream.Net.Common;

namespace Livestream.Net.Dlive.Chat
{
    public class DliveChatMessage : ChatMessage
    {
        public DliveChatMessage(string channel, string displayName, string message) : base(Platform.Dlive)
        {
            Channel = channel;
            Sender = displayName;
            Content = message;
        }
    }
}

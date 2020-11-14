using Livestream.Net.Common;

namespace Livestream.Net.Dlive.Chat
{
    public class DliveChatMessage : ChatMessage
    {
        public DliveChatEventType EventType { get; }
        //public DliveChatUser User { get; }
        public DliveChatMessage(DliveChatEventType eventType, string channel, string displayName, string message) : base(Platform.Dlive, channel, displayName, message)
        {
            EventType = eventType;
        }
    }
}

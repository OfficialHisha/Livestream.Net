using Livestream.Net.Common;

namespace Livestream.Net.Trovo.Chat
{
    public class TrovoChatMessage : ChatMessage
    {
        public TrovoChatEventType EventType { get; }
        public TrovoChatUser User { get; }

        public TrovoChatMessage(TrovoChatEventType eventType, string channel, TrovoChatUser user, string message) : base(Platform.Trovo, channel, user.DisplayName, message)
        {
            EventType = eventType;
            User = user;
        }
    }
}

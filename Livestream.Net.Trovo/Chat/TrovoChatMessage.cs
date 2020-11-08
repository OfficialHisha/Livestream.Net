using Livestream.Net.Common;

namespace Livestream.Net.Trovo.Chat
{
    public class TrovoChatMessage : ChatMessage
    {
        public TrovoChatEventType EventType { get; }
        public int? GiftAmount { get; }
        public TrovoChatUser User { get; }

        public TrovoChatMessage(TrovoChatEventType eventType, string channel, TrovoChatUser user, string message, int? giftAmount = null) : base(Platform.Trovo)
        {
            EventType = eventType;
            Channel = channel;
            User = user;
            Sender = user.DisplayName;
            Content = message;
            GiftAmount = giftAmount;
        }
    }
}

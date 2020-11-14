using Livestream.Net.Common;

namespace Livestream.Net.Trovo.Chat
{
    public class TrovoDonationEvent : DonationEvent
    {
        public TrovoChatUser User { get; }

        public TrovoDonationEvent(string channel, TrovoChatUser user, string spell, int amount) : base(Platform.Trovo, channel, user.DisplayName, spell, amount)
        {
            User = user;
        }
    }
}

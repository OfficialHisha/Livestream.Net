using Livestream.Net.Common;
using Livestream.Net.Twitch.Chat;

namespace Livestream.Net.Trovo
{
    public class TwitchAccount : Account
    {
        public TwitchAccount(string authorization) : base(new TwitchChatListener() { Authorization = authorization }, authorization) { }
    }
}

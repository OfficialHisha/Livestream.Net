using Livestream.Net.Common;
using Livestream.Net.Trovo.Chat;

namespace Livestream.Net.Trovo
{
    public class TrovoAccount : Account
    {
        public TrovoAccount()
        {
            ChatListener = new TrovoChatListener() { Authorization = Authorization };
        }
    }
}

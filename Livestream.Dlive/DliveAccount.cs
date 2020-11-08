using Livestream.Net.Common;
using Livestream.Net.Dlive.Chat;

namespace Livestream.Net.Dlive
{
    public class DliveAccount : Account
    {
        public DliveAccount()
        {
            ChatListener = new DliveChatListener() { Authorization = Authorization };
        }
    }
}

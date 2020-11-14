using Livestream.Net.Common;
using Livestream.Net.Trovo.Chat;

namespace Livestream.Net.Trovo
{
    public class TrovoAccount : Account
    {
        public TrovoAccount(string authorization) : base(new TrovoChatListener() { Authorization = authorization }, authorization) { }
    }
}

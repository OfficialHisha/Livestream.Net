using System;

namespace Livestream.Net.Common
{
    public abstract class Account
    {
        public IChatListener ChatListener { get; protected set; }
        public string Authorization { get; set; }

        public Account(IChatListener chatListener, string authorization)
        {
            ChatListener = chatListener;
            Authorization = authorization;
        }
    }
}

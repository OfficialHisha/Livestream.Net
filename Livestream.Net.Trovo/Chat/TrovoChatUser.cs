using System.Collections.Generic;

namespace Livestream.Net.Trovo.Chat
{
    public class TrovoChatUser
    {
        public string DisplayName { get; private set; }
        public string Avatar { get; private set; }
        public IList<string> Medals { get; private set; }
        public int SubLevel { get; private set; }
        public bool IsModerator => Medals.Contains("moderator");
        public bool IsSubscribed => SubLevel > 0;

        public TrovoChatUser(string displayName, string avatar, IList<string> medals, int subLevel)
        {
            DisplayName = displayName;
            Avatar = avatar;
            Medals = medals;
            SubLevel = subLevel;
        }
    }
}

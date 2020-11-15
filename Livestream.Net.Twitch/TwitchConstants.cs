namespace Livestream.Net.Twitch
{
    public static class TwitchConstants
    {
        public const int ConnectionLimit = 10;
        public const string ChatEndpoint = "wss://irc-ws.chat.twitch.tv";
        public const string PubSubEndpoint = "wss://pubsub-edge.twitch.tv";
        public const string ValidateAuthorizationUrl = "https://id.twitch.tv/oauth2/validate";
        public const string GetChannelUrl = "https://api.twitch.tv/helix/search/channels?query=";
    }
}

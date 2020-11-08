using Livestream.Net.Common;

namespace Livestream.Net
{
    public struct PlatformData
    {
        public Platform Platform { get; }
        public string Authorization { get; }

        public PlatformData(Platform platform, string authorization)
        {
            Platform = platform;
            Authorization = authorization;
        }
    }
}

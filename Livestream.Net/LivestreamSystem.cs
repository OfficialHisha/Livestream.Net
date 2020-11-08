using Livestream.Net.Dlive;
using Livestream.Net.Common;
using Livestream.Net.Trovo;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Livestream.Net
{
    public class LivestreamSystem
    {
        private readonly List<PlatformData> _platforms = new List<PlatformData>();

        public event EventHandler<LogMessage> Log;
        public event EventHandler<ChatMessage> ChatMessage;

        public Dictionary<Platform, Account> ConnectedAccounts { get; set; } = new Dictionary<Platform, Account>();

        public LivestreamSystem(List<PlatformData> platforms)
        {
            _platforms = platforms;
        }

        public async Task InitializePlatforms()
        {
            foreach (PlatformData platform in _platforms)
            {
                switch (platform.Platform)
                {
                    case Platform.Dlive:
                        ConnectedAccounts.Add(platform.Platform, new DliveAccount() { Authorization = platform.Authorization });
                        break;
                    case Platform.Trovo:
                        ConnectedAccounts.Add(platform.Platform, new TrovoAccount() { Authorization = platform.Authorization });
                        break;
                    default:
                        continue;
                }

                ConnectedAccounts[platform.Platform].ChatListener.Connection += (sender, message) => Log?.Invoke(sender, new LogMessage(LogSeverity.INFO, message));
                ConnectedAccounts[platform.Platform].ChatListener.Chat += (sender, message) => ChatMessage?.Invoke(sender, message);
                ConnectedAccounts[platform.Platform].ChatListener.Error += (sender, error) =>
                {
                    Log?.Invoke(sender, new LogMessage(LogSeverity.ERROR, error));
                    ConnectedAccounts.Remove(platform.Platform);
                };
                await ConnectedAccounts[platform.Platform].ChatListener.Connect();
            }
        }

        public async Task AddChannelListener(Platform platform, string channel)
        {
            if (!ConnectedAccounts.ContainsKey(platform))
            {
                Log?.Invoke(this, new LogMessage(LogSeverity.WARN, $"Cannot add listener for {platform}: Platform not connected!"));
                return;
            }

            await ConnectedAccounts[platform].ChatListener.AddChannelListener(channel);
        }
    }
}

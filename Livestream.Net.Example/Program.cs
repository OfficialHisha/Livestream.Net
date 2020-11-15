using Livestream.Net.Common;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Livestream.Net.Example
{
    class Program
    {
        const LogSeverity LogLevel = LogSeverity.DEBUG;

        static async Task Main(string[] args)
        {
            JToken accounts = JToken.Parse(File.ReadAllText("C:\\Development\\accounts.json"));

            List<PlatformData> platforms = new List<PlatformData>() {
                new PlatformData(Platform.Dlive, accounts.Value<string>("hisha-dlive")),
                new PlatformData(Platform.Trovo, accounts.Value<string>("hisha-trovo")),
                new PlatformData(Platform.Twitch, accounts.Value<string>("hisha-twitch"))
            };

            LivestreamSystem system = new LivestreamSystem(platforms);

            system.Log += AddLogEntry;
            system.ChatMessage += AddChatMessage;

            await system.InitializePlatforms();

            await system.AddChannelListener(Platform.Dlive, "hisha");
            await system.AddChannelListener(Platform.Trovo, "hisha");
            await system.AddChannelListener(Platform.Twitch, "officialhisha");

            await Task.Delay(Timeout.Infinite);
        }

        static void AddLogEntry(object sender, LogMessage log)
        {
            if (LogLevel > log.Severity) return;

            switch (log.Severity)
            {
                case LogSeverity.WARN:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    break;
                case LogSeverity.ERROR:
                    Console.ForegroundColor = ConsoleColor.Red;
                    break;
                default:
                    break;
            }

            Console.WriteLine($"[{DateTime.Now}] [LOG] [{log.Severity}]: {log.Message}");

            Console.ResetColor();
        }

        static void AddChatMessage(object sender, ChannelEvent message)
        {
            switch (message.Platform)
            {
                case Platform.Dlive:
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    break;
                case Platform.Trovo:
                    Console.ForegroundColor = ConsoleColor.DarkGreen;
                    break;
                case Platform.Twitch:
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    break;
                default:
                    break;
            }

            switch (message)
            {
                case ChatMessage chatMessage:
                    Console.WriteLine($"[{DateTime.Now}] [{chatMessage.Platform}] [{chatMessage.Channel}]: ({chatMessage.Sender}) {chatMessage.Content}");
                    break;
                case DonationEvent donationEvent:
                    Console.WriteLine($"[{DateTime.Now}] [{donationEvent.Platform}] [{donationEvent.Channel}]: {donationEvent.Sender} donated {donationEvent.Quantity} {donationEvent.Symbol}");
                    break;
                default:
                    break;
            }

            Console.ResetColor();
        }
    }
}

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
                new PlatformData(Platform.Trovo, accounts.Value<string>("hisha-trovo"))
            };

            LivestreamSystem system = new LivestreamSystem(platforms);

            system.Log += AddLogEntry;
            system.ChatMessage += AddChatMessage;

            await system.InitializePlatforms();

            await system.AddChannelListener(Platform.Dlive, "hisha");
            await system.AddChannelListener(Platform.Trovo, "hisha");

            await Task.Delay(Timeout.Infinite);
        }

        public static void AddLogEntry(object sender, LogMessage log)
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

        public static void AddChatMessage(object sender, ChatMessage message)
        {
            switch (message.Platform)
            {
                case Platform.Dlive:
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    break;
                case Platform.Trovo:
                    Console.ForegroundColor = ConsoleColor.DarkGreen;
                    break;
                default:
                    break;
            }

            Console.WriteLine($"[{DateTime.Now}] [{message.Platform}] [{message.Channel}] ({message.Sender}): {message.Content}");

            Console.ResetColor();
        }
    }
}

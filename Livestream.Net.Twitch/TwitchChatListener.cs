using Livestream.Net.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Livestream.Net.Twitch.Chat
{
    struct Connection
    {
        public ClientWebSocket Socket { get; set; }
        public List<string> Channels { get; set; }
        public long LastKeepAlive { get; set; }
        public bool Connected => LastKeepAlive + 300 > DateTimeOffset.Now.ToUnixTimeSeconds();
    }

    enum PacketType
    {
        PING,
    }

    public class TwitchChatListener : IChatListener
    {
        readonly List<Connection> connections = new List<Connection>();
        public string Authorization { get; set; }

        public event EventHandler<string> Error;
        public event EventHandler<ChannelEvent> Event;
        public event EventHandler<string> Connection;

        public TwitchChatListener()
        {
            ClientWebSocket socket = new ClientWebSocket();
            connections.Add(new Connection() { Socket = socket, Channels = new List<string>() });
        }

        public async Task AddChannelListener(string channel)
        {
            foreach (Connection connection in connections)
            {
                if (connection.Channels.Contains(channel))
                {
                    Error?.Invoke(this, $"Unable to add channel '{channel}': Channel is already added");
                    return;
                }
            }


            foreach (Connection conn in connections)
            {
                // Scaffolding as I am unsure if Twitch has limits to the amount of channels per socket
                // Might refactor to a single ClientWebSocket in the future if no limits exist
                byte[] messageBuffer = Encoding.UTF8.GetBytes($"JOIN #{channel.ToLower()}");
                await conn.Socket.SendAsync(new ArraySegment<byte>(messageBuffer), WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
                conn.Channels.Add(channel);
                Connection?.Invoke(this, $"Added listener for channel '{channel}' on Twitch");
                return;
            }
        }

        public async Task Connect()
        {
            foreach (Connection connection in connections)
            {
                await Connect(connection).ConfigureAwait(false);
            }
        }

        private async Task Connect(Connection connection)
        {
            if (connection.Connected) return;

            await connection.Socket.ConnectAsync(new Uri(TwitchConstants.ChatEndpoint), CancellationToken.None).ConfigureAwait(false);

            string[] auth = Authorization.Split(':');
            byte[] messageBuffer = Encoding.UTF8.GetBytes($"PASS oauth:{auth[1]}");
            await connection.Socket.SendAsync(new ArraySegment<byte>(messageBuffer), WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
            messageBuffer = Encoding.UTF8.GetBytes($"NICK {auth[0]}");
            await connection.Socket.SendAsync(new ArraySegment<byte>(messageBuffer), WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);

            messageBuffer = new byte[128];
            await connection.Socket.ReceiveAsync(new ArraySegment<byte>(messageBuffer), CancellationToken.None).ConfigureAwait(false);

            string response = Encoding.UTF8.GetString(messageBuffer);
            if (!response.Contains("001"))
            {
                Error?.Invoke(this, $"Failed to connect to {TwitchConstants.ChatEndpoint}: {response}");
                return;
            }

            _ = Receive(connection, CancellationToken.None);

            Connection?.Invoke(this, $"Connected to {TwitchConstants.ChatEndpoint}");
        }

        async Task Receive(Connection connection, CancellationToken cancellationToken)
        {
            do
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                byte[] messageBuffer = new byte[4096];
                await connection.Socket.ReceiveAsync(new ArraySegment<byte>(messageBuffer), cancellationToken).ConfigureAwait(false);
                _ = ParseNetworkMessage(connection, Encoding.UTF8.GetString(messageBuffer));
            } while (true);
        }

        async Task ParseNetworkMessage(Connection connection, string message)
        {
            string[] messageParts = message.Split(' ');

            if(Enum.TryParse(messageParts[0], out PacketType packetType))
            {
                switch (packetType)
                {
                    case PacketType.PING:
                        connection.LastKeepAlive = DateTimeOffset.Now.ToUnixTimeSeconds();
                        byte[] messageBuffer = Encoding.UTF8.GetBytes($"PONG :tmi.twitch.tv");
                        await connection.Socket.SendAsync(new ArraySegment<byte>(messageBuffer), WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
                        break;
                    default:
                        //Unknown type
                        Error?.Invoke(this, $"An unknown message was received, please report on GitHub at https://github.com/OfficialHisha/Livestream.Net/issues/new: {message}");
                        break;
                }
            }
            else
            {
                switch (messageParts[1])
                {
                    case "PRIVMSG":
                        // Chat message
                        string userString = string.Join("", messageParts[0].Skip(1).ToArray());
                        Event?.Invoke(this, new ChatMessage(Platform.Twitch, string.Join("", messageParts[2].Skip(1).ToArray()), userString.Split('!')[0], string.Join(" ", messageParts.Skip(3).ToArray()).Substring(1).Split("\r\n")[0]));
                        break;
                    default:
                        // We don't care about other messages
                        break;
                }
            }

            
        }
    }
}
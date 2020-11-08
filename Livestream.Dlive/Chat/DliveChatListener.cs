using Livestream.Net.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Livestream.Net.Dlive.Chat
{
    struct Connection
    {
        public ClientWebSocket Socket { get; set; }
        public List<string> Channels { get; set; }
        public long LastKeepAlive { get; set; }
        public bool Connected => LastKeepAlive + 30 > DateTimeOffset.Now.ToUnixTimeSeconds();
    }
    enum PacketType
    {
        DATA,
        KA

    }

    public class DliveChatListener : IChatListener
    {
        readonly List<Connection> connections = new List<Connection>();

        public string Authorization { get; set; }

        public event EventHandler<string> Error;
        public event EventHandler<ChatMessage> Chat;
        public event EventHandler<string> Chest;
        public event EventHandler<string> Connection;

        public DliveChatListener()
        {
            ClientWebSocket socket = new ClientWebSocket();
            socket.Options.AddSubProtocol("graphql-ws");
            connections.Add(new Connection() { Socket = socket, Channels = new List<string>() });
        }

        public async Task AddChannelListener(string channel)
        {
            foreach (Connection connection in connections)
            {
                if (connection.Channels.Contains(channel))
                {
                    Error?.Invoke(this, "Unable to add channel: Channel is already added");
                    return;
                }
            }

            string id = $"{channel}_chat";
            byte[] messageBuffer = Encoding.UTF8.GetBytes($"{{\"id\":\"{id}\",\"type\":\"start\",\"payload\":{{\"query\":\"subscription{{streamMessageReceived(streamer:\\\"{channel}\\\"){{__typename}}}}\"}}}}");

            foreach (Connection conn in connections)
            {
                if (conn.Channels.Count < 50)
                {
                    await conn.Socket.SendAsync(new ArraySegment<byte>(messageBuffer), WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
                    conn.Channels.Add(channel);
                    Connection?.Invoke(this, $"Added listener for channel '{channel}' on Dlive");
                    return;
                }
            }

            if (connections.Count < DliveConstants.SubscriptionSocketLimit)
            {
                ClientWebSocket socket = new ClientWebSocket();
                socket.Options.AddSubProtocol("graphql-ws");
                Connection newConn = new Connection() { Socket = socket, Channels = new List<string>() { channel } };
                connections.Add(newConn);
                await Connect(newConn).ConfigureAwait(false);
                Connection?.Invoke(this, $"Added listener for channel '{channel}' on Dlive");
                return;
            }

            Error?.Invoke(this, "Unable to add channel: Channel limit reached");
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

            await connection.Socket.ConnectAsync(new Uri(DliveConstants.SubscriptionEndpoint), CancellationToken.None).ConfigureAwait(false);

            byte[] messageBuffer = Encoding.UTF8.GetBytes("{\"type\":\"connection_init\"}");
            await connection.Socket.SendAsync(new ArraySegment<byte>(messageBuffer), WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);

            messageBuffer = new byte[128];
            await connection.Socket.ReceiveAsync(new ArraySegment<byte>(messageBuffer), CancellationToken.None).ConfigureAwait(false);

            dynamic response = JsonConvert.DeserializeObject(Encoding.UTF8.GetString(messageBuffer));
            if (response.type.ToString() != "connection_ack")
            {
                Error?.Invoke(this, $"Failed to connect to {DliveConstants.SubscriptionEndpoint}: {response.payload.message.ToString()}");
                return;
            }

            _ = Receive(connection, CancellationToken.None);

            Connection?.Invoke(this, $"Connected to {DliveConstants.SubscriptionEndpoint}");
        }

        async Task Receive(Connection conn, CancellationToken cancellationToken)
        {
            do
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                byte[] messageBuffer = new byte[4096];
                await conn.Socket.ReceiveAsync(new ArraySegment<byte>(messageBuffer), cancellationToken).ConfigureAwait(false);
                _ = ParseNetworkMessage(conn, JObject.Parse(Encoding.UTF8.GetString(messageBuffer)));
            } while (true);
        }

        async Task ParseNetworkMessage(Connection connection, JObject message)
        {
            Enum.TryParse(message.Value<string>("type").ToUpper(), out PacketType packetType);

            switch (packetType)
            {
                case PacketType.KA:
                    connection.LastKeepAlive = DateTimeOffset.Now.ToUnixTimeSeconds();
                    break;
                case PacketType.DATA:
                    if (message.Value<string>("id").Contains("_chat"))
                    {
                        // Chat event
                        await BuildChatEvent(message.Value<string>("id"), message.SelectToken("payload.data.streamMessageReceived")[0] as JObject).ConfigureAwait(false);
                    }
                    else
                        //Chest event
                        await BuildChestMessage(message.Value<string>("id"), message.SelectToken("payload.data.treasureChestMessageReceived")[0] as JObject).ConfigureAwait(false);
                    break;
                default:
                    //Unknown type
                    Error?.Invoke(this, $"An unknown message was received, please report on GitHub at https://github.com/OfficialHisha/DSharp/issues/new: {message}");
                    break;
            }
        }

        private async Task BuildChatEvent(string channel, JObject chatEvent)
        {
            await Task.Yield();

            Chat?.Invoke(this, new DliveChatMessage(channel.Split("_")[0], chatEvent.SelectToken("sender.displayname")?.ToString() ?? "System", chatEvent.Value<string>("content")));
        }

        private async Task BuildChestMessage(string channel, dynamic data)
        {
            await Task.Yield();
        }
    }
}

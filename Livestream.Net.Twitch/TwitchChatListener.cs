using Livestream.Net.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Livestream.Net.Twitch.Chat
{
    class Connection
    {
        public ClientWebSocket Socket { get; set; }
        public List<string> Channels { get; set; }
        public long LastKeepAlive { get; set; }
        public bool Disconnected { get; set; } = true;
        public bool Connected => !Disconnected && LastKeepAlive + 300 > DateTimeOffset.Now.ToUnixTimeSeconds();

        readonly System.Timers.Timer PingTimer = new System.Timers.Timer(30000)
        {
            AutoReset = true,
            Enabled = true
        };

        public Connection()
        {
            PingTimer.Elapsed += async (sender, args) => 
            {
                if (!Connected)
                    return;

                await Socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes("{\"type\":\"PING\"}")), WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
            };
        }
    }

    struct ChatConnection
    {
        public ClientWebSocket Socket { get; set; }
        public List<string> Channels { get; set; }
        public long LastKeepAlive { get; set; }
        public bool Connected => LastKeepAlive + 300 > DateTimeOffset.Now.ToUnixTimeSeconds();
    }

    enum PubSubEvent
    {
        PONG,
        RESPONSE,
        RECONNECT,
        MESSAGE
    }

    class TwitchChatListener : IChatListener
    {
        readonly List<Connection> connections = new List<Connection>();
        readonly List<ChatConnection> chatConnections = new List<ChatConnection>();

        public string Authorization { get; set; }
        string[] auth;
        string clientId;

        public event EventHandler<string> Error;
        public event EventHandler<ChannelEvent> Event;
        public event EventHandler<string> Connection;

        public async Task AddChannelListener(string channel)
        {
            if (await AddEventChannel(channel))
            {
                await AddChatChannel(channel);
                Connection?.Invoke(this, $"Added listener for channel '{channel}' on Twitch");
            }
        }

        async Task<bool> AddEventChannel(string channel)
        {
            using HttpClient http = new HttpClient();
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            http.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Bearer {auth[2]}");
            http.DefaultRequestHeaders.TryAddWithoutValidation("Client-id", clientId);

            using HttpResponseMessage getChannelResponse = await http.SendAsync(new HttpRequestMessage(HttpMethod.Get, new Uri(TwitchConstants.GetChannelUrl + channel))).ConfigureAwait(false);

            if (getChannelResponse.StatusCode != HttpStatusCode.OK)
            {
                Error?.Invoke(this, $"Failed to connect to {TwitchConstants.GetChannelUrl + channel}: {getChannelResponse.ReasonPhrase}");
                return false;
            }

            string channelId = JObject.Parse(await getChannelResponse.Content.ReadAsStringAsync().ConfigureAwait(false)).SelectToken("data")[0].Value<string>("id");

            foreach (Connection connection in connections)
            {
                if (connection.Channels.Contains(channel))
                {
                    if (connection.Disconnected)
                        break;// If the connection is disconnected, don't count the channel as added anymore

                    Error?.Invoke(this, $"Unable to add channel '{channel}': Channel is already added");
                    return false;
                }
            }

            var request = new
            {
                type = "LISTEN",
                nounce = $"LISTEN_{DateTimeOffset.Now.ToUnixTimeSeconds()}",
                data = new
                {
                    topics = new List<string>() { $"channel-bits-events-v2.{channelId}", $"channel-points-channel-v1.{channelId}", $"channel-subscribe-events-v1.{channelId}" },
                    auth_token = auth[2]
                }
            };

            byte[] messageBuffer = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(request));

            foreach (Connection conn in connections)
            {
                if (conn.Disconnected)
                {
                    continue;// If the connection is disconnected, we don't want to add new channels to it
                }

                if (conn.Channels.Count * request.data.topics.Count() <= 50 - request.data.topics.Count())
                {
                    await conn.Socket.SendAsync(new ArraySegment<byte>(messageBuffer), WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
                    conn.Channels.Add(channel);
                    return true;
                }
            }

            if (connections.Count < TwitchConstants.ConnectionLimit)
            {
                Connection con = await CreateConnection(channel);
                _ = ReceiveChannelEvents(con);
                await con.Socket.SendAsync(new ArraySegment<byte>(messageBuffer), WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
                connections.Add(con);
                return true;
            }

            return false;
        }

        async Task AddChatChannel(string channel)
        {

            if (chatConnections.Count > 0)
            {
                await chatConnections[0].Socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes($"JOIN #{channel.ToLower()}")), WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
                chatConnections[0].Channels.Add(channel);
            }
            else
            {
                ChatConnection chatCon = await CreateChatConnection(channel);
                await chatCon.Socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes($"JOIN #{channel.ToLower()}")), WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
                _ = ReceiveChat(chatCon);
            }
        }

        public async Task Connect()
        {
            auth = Authorization.Split(':');

            // PubSub connection
            using HttpClient http = new HttpClient();
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            http.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Bearer {auth[2]}");

            using HttpResponseMessage validationResponse = await http.SendAsync(new HttpRequestMessage(HttpMethod.Get, new Uri(TwitchConstants.ValidateAuthorizationUrl))).ConfigureAwait(false);

            if (validationResponse.StatusCode != HttpStatusCode.OK)
            {
                Error?.Invoke(this, $"Failed to connect to {TwitchConstants.ValidateAuthorizationUrl}: {validationResponse.ReasonPhrase}");
                return;
            }

            clientId = JObject.Parse(await validationResponse.Content.ReadAsStringAsync().ConfigureAwait(false)).Value<string>("client_id");

            Connection?.Invoke(this, $"Connected to Twitch");
        }

        async Task<ChatConnection> CreateChatConnection(string channel)
        {
            ClientWebSocket socket = new ClientWebSocket();

            await socket.ConnectAsync(new Uri(TwitchConstants.ChatEndpoint), CancellationToken.None).ConfigureAwait(false);

            byte[] messageBuffer = Encoding.UTF8.GetBytes($"PASS oauth:{auth[1]}");
            await socket.SendAsync(new ArraySegment<byte>(messageBuffer), WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
            messageBuffer = Encoding.UTF8.GetBytes($"NICK {auth[0]}");
            await socket.SendAsync(new ArraySegment<byte>(messageBuffer), WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);

            messageBuffer = new byte[128];
            await socket.ReceiveAsync(new ArraySegment<byte>(messageBuffer), CancellationToken.None).ConfigureAwait(false);

            string response = Encoding.UTF8.GetString(messageBuffer);
            if (!response.Contains("001"))
            {
                Error?.Invoke(this, $"Failed to connect to {TwitchConstants.ChatEndpoint}: {response}");
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Failed to connect", CancellationToken.None);
            }

            return new ChatConnection() { Socket = socket, Channels = new List<string>() { channel } };
        }

        async Task<Connection> CreateConnection(string channel)
        {
            ClientWebSocket socket = new ClientWebSocket();

            await socket.ConnectAsync(new Uri(TwitchConstants.PubSubEndpoint), CancellationToken.None).ConfigureAwait(false);
            return new Connection() { Socket = socket, Channels = new List<string>() { channel } };
        }

        async Task ReceiveChat(ChatConnection connection)
        {
            do
            {
                byte[] messageBuffer = new byte[2048];
                await connection.Socket.ReceiveAsync(new ArraySegment<byte>(messageBuffer), CancellationToken.None).ConfigureAwait(false);
                _ = ParseChatMessage(connection, Encoding.UTF8.GetString(messageBuffer));
            } while (true);
        }
        async Task ParseChatMessage(ChatConnection connection, string message)
        {
            if (message.StartsWith("PING"))
            {
                connection.LastKeepAlive = DateTimeOffset.Now.ToUnixTimeSeconds();
                await connection.Socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes($"PONG :tmi.twitch.tv")), WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
            }
            else if (message.Contains("PRIVMSG"))
            {
                // Chat message
                string[] messageParts = message.Split(' ');
                string userString = string.Join("", messageParts[0].Skip(1).ToArray());
                Event?.Invoke(this, new ChatMessage(Platform.Twitch, string.Join("", messageParts[2].Skip(1).ToArray()), userString.Split('!')[0], string.Join(" ", messageParts.Skip(3).ToArray()).Substring(1).Split("\r\n")[0]));
            }
        }

        async Task ReceiveChannelEvents(Connection connection)
        {
            do
            {
                byte[] messageBuffer = new byte[2048];
                await connection.Socket.ReceiveAsync(new ArraySegment<byte>(messageBuffer), CancellationToken.None).ConfigureAwait(false);

                _ = ParseEventMessage(connection, JObject.Parse(Encoding.UTF8.GetString(messageBuffer)));
            } while (true);
        }

        async Task ParseEventMessage(Connection connection, JObject eventMessage)
        {
            await Task.Yield();

            Enum.TryParse(eventMessage.Value<string>("type"), out PubSubEvent eventType);

            switch (eventType)
            {
                case PubSubEvent.PONG:
                    connection.LastKeepAlive = DateTimeOffset.Now.ToUnixTimeSeconds();
                    break;
                case PubSubEvent.RESPONSE:
                    string error = eventMessage.Value<string>("error");
                    if (!string.IsNullOrWhiteSpace(error))
                        Error?.Invoke(this, $"Could not connect to new channel: {error}");
                    else
                    {
                        connection.Disconnected = false;
                        connection.LastKeepAlive = DateTimeOffset.Now.ToUnixTimeSeconds();
                    }
                    break;
                case PubSubEvent.RECONNECT:
                    // TODO: Implement reconnect
                    Console.WriteLine(eventMessage);
                    break;
                case PubSubEvent.MESSAGE:
                    string topic = eventMessage.Value<string>("data.topic");
                    JObject message = eventMessage.SelectToken("data.message") as JObject;

                    if (topic.Contains("channel-bits-events-v2"))
                    {
                        Event?.Invoke(this, new DonationEvent(Platform.Twitch, message.Value<string>("channel_name"), message.Value<string>("user_name"), "Bits", message.Value<decimal>("bits_used")));
                    }
                    else
                    {
                        Connection?.Invoke(this, $"Unimplemented message, please report on GitHub at https://github.com/OfficialHisha/Livestream.Net/issues/new: {eventMessage}");
                    }
                    break;
                default:
                    //Unknown type
                    Error?.Invoke(this, $"An unknown message was received, please report on GitHub at https://github.com/OfficialHisha/Livestream.Net/issues/new: {eventMessage}");
                    break;
            }
        }

    }
}

using Livestream.Net.Common;
using Livestream.Net.Trovo.GraphQL;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Livestream.Net.Trovo.Chat
{
    struct Connection
    {
        public ClientWebSocket Socket { get; set; }
        public string Channel { get; set; }
        public long LastKeepAlive { get; set; }
        public bool Connected => LastKeepAlive + 30 > DateTimeOffset.Now.ToUnixTimeSeconds();
    }

    enum PacketType
    {
        PONG,
        CHAT,
        RESPONSE,
    }

    public class TrovoChatListener : IChatListener
    {
        readonly List<Connection> connections = new List<Connection>();
        public string Authorization { get; set; }

        public event EventHandler<string> Error;
        public event EventHandler<ChannelEvent> Event;
        public event EventHandler<string> Connection;

        public async Task AddChannelListener(string channel)
        {
            ClientWebSocket socket = new ClientWebSocket();
            Connection newConn = new Connection() { Socket = socket, Channel = channel };
            connections.Add(newConn);
            await Connect(newConn).ConfigureAwait(false);
            return;
        }

        public async Task Connect()
        {
            if (connections.Count == 0)
            {
                // Indicate that connections are ready to be made
                // We don't have any connections yet, so we can't do more for the time being
                // This is just to keep it consistent in the log
                Connection?.Invoke(this, $"Connected to {TrovoConstants.SubscriptionEndpoint}");
            }

            using HttpClient http = new HttpClient();
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            http.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", Authorization);

            foreach (Connection connection in connections)
            {
                await Connect(connection, http);
            }
        }

        async Task Connect(Connection connection)
        {
            using HttpClient http = new HttpClient();
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            http.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", Authorization);

            await Connect(connection, http);
        }

        async Task Connect(Connection connection, HttpClient http)
        {
            if (connection.Connected) return;

            using HttpRequestMessage channelIdRequest = new HttpRequestMessage(HttpMethod.Post, TrovoConstants.GraphQLEndpoint)
            {
                Content = new StringContent(Queries.GetChannelId(connection.Channel), Encoding.UTF8, "application/json")
            };

            using HttpResponseMessage channelIdResponse = await http.SendAsync(channelIdRequest).ConfigureAwait(false);

            if (channelIdResponse.StatusCode != System.Net.HttpStatusCode.OK)
            {
                Error?.Invoke(this, $"Failed to connect to {channelIdRequest.RequestUri}: {channelIdResponse.ReasonPhrase}");
                return;
            }

            string pageId = JObject.Parse(await channelIdResponse.Content.ReadAsStringAsync().ConfigureAwait(false)).SelectToken("data.getLiveInfo.channelInfo.id").ToObject<string>();

            using HttpRequestMessage tokenRequest = new HttpRequestMessage(HttpMethod.Post, TrovoConstants.GraphQLEndpoint)
            {
                Content = new StringContent(Queries.GetToken(pageId), Encoding.UTF8, "application/json")
            };

            using HttpResponseMessage tokenResponse = await http.SendAsync(tokenRequest).ConfigureAwait(false);

            if (tokenResponse.StatusCode != System.Net.HttpStatusCode.OK)
            {
                Error?.Invoke(this, $"Failed to connect to {tokenRequest.RequestUri}: {tokenResponse.ReasonPhrase}");
                return;
            }

            string token = JObject.Parse(await tokenResponse.Content.ReadAsStringAsync().ConfigureAwait(false)).SelectToken("data.getToken.token").ToObject<string>();

            await connection.Socket.ConnectAsync(new Uri(TrovoConstants.SubscriptionEndpoint), CancellationToken.None).ConfigureAwait(false);

            string authMessage = $"{{\"type\":\"AUTH\",\"nonce\":\"AUTH_{DateTimeOffset.Now.ToUnixTimeSeconds()}\",\"data\":{{\"token\":\"{token}\"}}}}";

            ArraySegment<byte> bytes = new ArraySegment<byte>(Encoding.UTF8.GetBytes(authMessage));

            _ = Receive(connection);
            await connection.Socket.SendAsync(bytes, WebSocketMessageType.Binary, true, CancellationToken.None).ConfigureAwait(false);
            _ = KeepAlive(connection);

            Connection?.Invoke(this, $"Added listener for channel '{connection.Channel}' on Trovo");
        }

        async Task Receive(Connection connection)
        {
            do
            {
                WebSocketReceiveResult receiveResult;
                List<byte> receivedBytes = new List<byte>();

                do
                {
                    ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[512]);
                    try
                    {
                        receiveResult = await connection.Socket.ReceiveAsync(buffer, CancellationToken.None).ConfigureAwait(false);
                    }
                    catch (TaskCanceledException)
                    {
                        return;
                    }
                    receivedBytes.AddRange(buffer);
                } while (!receiveResult.EndOfMessage);

                _ = ParseNetworkMessage(connection, JObject.Parse(Encoding.UTF8.GetString(receivedBytes.ToArray()))).ConfigureAwait(false);
            }
            while (true);
        }

        async Task KeepAlive(Connection connection)
        {
            do
            {
                string pingMessage = $"{{\"type\":\"PING\",\"nonce\":\"PING_{DateTimeOffset.Now.ToUnixTimeSeconds()}\"}}";

                ArraySegment<byte> buffer = Encoding.UTF8.GetBytes(pingMessage);
                try
                {
                    await connection.Socket.SendAsync(buffer, WebSocketMessageType.Binary, true, CancellationToken.None).ConfigureAwait(false);
                    await Task.Delay(30000).ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    return;
                }
            }
            while (true);
        }

        async Task ParseNetworkMessage(Connection connection, JObject message)
        {
            Enum.TryParse(message.Value<string>("type").ToUpper(), out PacketType packetType);

            switch (packetType)
            {
                case PacketType.PONG:
                    connection.LastKeepAlive = DateTimeOffset.Now.ToUnixTimeSeconds();
                    break;
                case PacketType.CHAT:
                    if (!(message.SelectToken("data.chats") is JArray chats)) break;

                    foreach (JObject chat in chats)
                    {
                        await BuildChatEvent(connection.Channel, chat).ConfigureAwait(false);
                    }
                    break;
                case PacketType.RESPONSE:
                    if (message.ContainsKey("error"))
                    {
                        Error?.Invoke(this, $"Received error from {TrovoConstants.SubscriptionEndpoint}: {message.SelectToken("error")}");
                        return;
                    }
                    break;
                default:
                    Error?.Invoke(this, $"An unknown message was received, please report on GitHub at https://github.com/OfficialHisha/Livestream.Net/issues/new: {message}");
                    break;
            }
        }

        async Task BuildChatEvent(string channel, JObject chatEvent)
        {
            await Task.Yield();

            TrovoChatEventType eventType = (TrovoChatEventType)chatEvent.Value<int>("type");
            TrovoChatUser user = new TrovoChatUser(chatEvent.Value<string>("nick_name"), chatEvent.Value<string>("avatar"), chatEvent.ContainsKey("medals") ? chatEvent["medals"].ToObject<string[]>() : new string[0], chatEvent.ContainsKey("sub_lv") ? int.Parse(chatEvent.Value<string>("sub_lv").Substring(5)) : 0);

            switch (eventType)
            {
                case TrovoChatEventType.GIFT:
                    JObject giftData = JObject.Parse(chatEvent.Value<string>("content"));

                    Event?.Invoke(this, new TrovoDonationEvent(channel, user, giftData.Value<string>("gift") , giftData.Value<int>("num")));
                    break;
                case TrovoChatEventType.RAID:
                    Event?.Invoke(this, new TrovoChatMessage(eventType, channel, user, $"Raided the stream!"));
                    break;
                default:
                    Event?.Invoke(this, new TrovoChatMessage(eventType, channel, user, chatEvent.Value<string>("content")));
                    break;

            }
        }
    }
}
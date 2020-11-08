using System;
using System.Threading.Tasks;

namespace Livestream.Net.Common
{
    public interface IChatListener
    {
        event EventHandler<string> Error;
        event EventHandler<ChatMessage> Chat;
        event EventHandler<string> Connection;

        Task Connect();
        Task AddChannelListener(string channel);
    }
}

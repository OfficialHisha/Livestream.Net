using System;
using System.Threading.Tasks;

namespace Livestream.Net.Common
{
    public interface IChatListener
    {
        public string Authorization { get; set; }

        event EventHandler<string> Error;
        event EventHandler<ChannelEvent> Event;
        event EventHandler<string> Connection;

        Task Connect();
        Task AddChannelListener(string channel);
    }
}

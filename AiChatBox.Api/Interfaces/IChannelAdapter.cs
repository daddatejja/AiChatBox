using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using AiChatBox.Api.Models;

namespace AiChatBox.Api.Interfaces
{
    public interface IChannelAdapter
    {
        string ChannelName { get; }
        Task<InboundMessage> ParseInbound(HttpRequest request);
        Task SendOutbound(OutboundMessage message);
    }
}

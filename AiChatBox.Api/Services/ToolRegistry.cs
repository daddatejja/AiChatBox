using AiChatBox.Api.Interfaces;
using AiChatBox.Api.Services.Tools;

namespace AiChatBox.Api.Services
{
    public class ToolRegistry(IEnumerable<ITool> tools)
    {
        private readonly Dictionary<string, ITool> _tools = tools.ToDictionary(t => t.Name);

        public IEnumerable<ITool> GetAllTools() => _tools.Values;

        public ITool? GetTool(string name) => _tools.GetValueOrDefault(name);
    }
}

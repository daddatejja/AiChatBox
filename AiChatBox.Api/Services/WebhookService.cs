using System.Text.Json;
using AiChatBox.Api.Interfaces;
using AiChatBox.Api.Models;

namespace AiChatBox.Api.Services
{
    public class WebhookService(IHttpClientFactory httpClientFactory, ILogger<WebhookService> logger)
    {
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
        private readonly ILogger<WebhookService> _logger = logger;

        public async Task<ToolResult> ExecuteWebhookToolAsync(Project project, string toolName, string argumentsJson)
        {
            if (string.IsNullOrEmpty(project.WebhookUrl))
            {
                return new ToolResult { ToolName = toolName, Error = "Webhook URL not configured for this project." };
            }

            try
            {
                var client = _httpClientFactory.CreateClient();
                var payload = new
                {
                    ProjectName = project.Name,
                    Tool = toolName,
                    Arguments = JsonSerializer.Deserialize<JsonElement>(argumentsJson)
                };

                var content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");
                
                // Add signature if secret is present
                if (!string.IsNullOrEmpty(project.WebhookSecret))
                {
                    content.Headers.Add("X-Hub-Signature", ComputeSignature(project.WebhookSecret, JsonSerializer.Serialize(payload)));
                }

                var response = await client.PostAsync(project.WebhookUrl, content);
                if (!response.IsSuccessStatusCode)
                {
                    var errorMsg = await response.Content.ReadAsStringAsync();
                    return new ToolResult { ToolName = toolName, Error = $"Webhook failed with status {response.StatusCode}: {errorMsg}" };
                }

                var resultJson = await response.Content.ReadAsStringAsync();
                return new ToolResult { ToolName = toolName, Content = resultJson };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing webhook for tool {ToolName}", toolName);
                return new ToolResult { ToolName = toolName, Error = $"Webhook execution error: {ex.Message}" };
            }
        }

        private string ComputeSignature(string secret, string payload)
        {
            using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload));
            return Convert.ToHexString(hash).ToLower();
        }
    }
}

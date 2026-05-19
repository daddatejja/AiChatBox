# Implementation Plan — Microsoft Teams Bot Channel Support

We will implement enterprise-grade **Microsoft Teams** channel support into the AiChatBox multi-channel messaging platform. This integrates seamlessly into our unified `IChannelAdapter` infrastructure and enables high-fidelity direct AI chats, human handoffs, and interactive agent capabilities within Teams.

---

## 🛠️ Unified Architecture Overview

We will leverage Microsoft's standard **Bot Framework REST API** to connect Teams users to our AI and Human Agent backends:

```mermaid
flowchart TD
    Teams[Microsoft Teams / Tenant Client] -->|Inbound Activity| AzureBot[Azure Bot Service / Developer Portal Webhook]
    AzureBot -->|POST /api/channel/teams/{projectId}| Ctrl[ChannelController]
    
    Ctrl --> TeamsAdapter[TeamsAdapter: ParseInbound]
    TeamsAdapter -->|Extract Sender & serviceUrl| InMsg[InboundMessage]
    InMsg --> DB[Find or Create ChatSession]
    
    DB --> ChatService[AiChatService: Process Message]
    ChatService --> OutMsg[OutboundMessage]
    
    OutMsg --> TeamsAdapter2[TeamsAdapter: SendOutbound]
    TeamsAdapter2 -->|Request Token| MSLogin[Microsoft Identity Platform]
    MSLogin -->|OAuth2 Bearer Token| TeamsAdapter2
    
    TeamsAdapter2 -->|POST /v3/conversations/{convId}/activities| TeamsApi[Microsoft Bot Framework serviceUrl]
    TeamsApi -->|Deliver Reply| Teams
```

---

## 🔒 User Review Required

> [!NOTE]
> **Stateless Routing Approach**: To keep our database clean and avoid introducing specialized columns for Teams-specific metadata (such as individual service URLs or tenants), we will pack the conversation ID and Microsoft's dynamic `serviceUrl` into a stateless token in the `SessionExternalId` field (e.g. `conversationId|serviceUrl`). The system naturally unpacks this on response delivery.

> [!IMPORTANT]
> **HTTPS Webhook Requirements**: Microsoft Teams bots require a valid HTTPS webhook endpoint. For local development and testing, you will need to tunnel your local API server using **ngrok** or a similar reverse proxy.

---

## 📋 Proposed Changes

### 1. Database Model & DTO Expansion

#### [MODIFY] [ChannelModels.cs](file:///c:/Users/tejsi/OneDrive/Desktop/AiChatBox/AiChatBox.Api/Models/ChannelModels.cs)
Add `TeamsSettings` parameters to the core `ChannelSettings` model:
```csharp
public class ChannelSettings
{
    public WhatsAppSettings? WhatsApp { get; set; }
    public SlackSettings? Slack { get; set; }
    public TelegramSettings? Telegram { get; set; }
    public TeamsSettings? Teams { get; set; } // [NEW] Teams integration settings
}

public class TeamsSettings
{
    public string AppId { get; set; } = string.Empty;
    public string AppPassword { get; set; } = string.Empty;
}
```

---

### 2. Microsoft Teams Channel Adapter Implementation

#### [NEW] [TeamsAdapter.cs](file:///c:/Users/tejsi/OneDrive/Desktop/AiChatBox/AiChatBox.Api/Services/TeamsAdapter.cs)
Implement a robust, production-grade Teams connector utilizing Microsoft Bot Framework API standards:
- **`ChannelName`**: Resolves to `"teams"`.
- **`ParseInbound`**:
  - Buffers the request body to extract the standard Microsoft Bot Activity payload.
  - Ignores non-message activities (such as `ping`, `conversationUpdate`, or system pings) by returning a dummy `"bot"` payload.
  - Automatically strips Teams bot mention markup (e.g. `<at>BotName</at>`) from user prompts to keep queries clean for LLM processing.
  - Statelessly aggregates `conversation.id` and `serviceUrl` into `SessionExternalId` using a pipe (`|`) delimiter.
- **`SendOutbound`**:
  - Parses the dynamic `serviceUrl` and `conversationId` from the outbound recipient string.
  - Performs an on-demand OAuth2 client-credential handshake with the Microsoft Identity Platform (`login.microsoftonline.com/botframework.com/oauth2/v2.0/token`) using `AppId` and `AppPassword`.
  - Dispatches replies back to Microsoft's API via `POST {serviceUrl}/v3/conversations/{conversationId}/activities` under the authenticated bearer token.

---

### 3. Application Hooking and Service Registration

#### [MODIFY] [Program.cs](file:///c:/Users/tejsi/OneDrive/Desktop/AiChatBox/AiChatBox.Api/Program.cs)
Register `TeamsAdapter` as a transient singleton in the Dependency Injection container:
```csharp
builder.Services.AddTransient<IChannelAdapter, TeamsAdapter>();
```

---

### 4. Admin Dashboard UI Controls

#### [MODIFY] [ConfigDetail.vue](file:///c:/Users/tejsi/OneDrive/Desktop/AiChatBox/AiChatBox.Dashboard/src/views/ConfigDetail.vue)
- Add a dedicated **Microsoft Teams Integration** sub-card inside the **Multi-Channel Integrations** container.
- Provide form inputs for `Microsoft App ID` and `Microsoft App Password` with interactive toggle-masking.
- Display the configured Teams Webhook URL for easy copying: `https://{domain}/api/channel/teams/{projectId}`.
- Wire reactive states and serialization to ensure settings are loaded and persisted inside `ChannelSettingsJson` seamlessly on configuration save.

---

## 🛠️ Configuration & Testing Guide

Follow these exact steps to register, configure, and test your new Microsoft Teams AI ChatBot:

### Step 1: Create a Teams Bot Registration
1. Navigate to the [Microsoft Teams Developer Portal](https://dev.teams.microsoft.com/) or [Azure Portal Bot Services](https://portal.azure.com/).
2. Under **Tools**, click on **Bot Management** and select **+ New Bot**.
3. Provide a name for the bot (e.g., `AiChatBox Assistant`).
4. Click **Create** and navigate to the **Client Secrets** tab.
5. Click **Generate client secret**, copy the **App Password**, and also copy the **Microsoft App ID** displayed under the bot title.

### Step 2: Configure the AiChatBox Settings
1. Start your local tunneling utility to expose your backend port (default: `5164`):
   ```bash
   ngrok http http://localhost:5164
   ```
2. Copy the resulting forwarding HTTPS URL (e.g. `https://a1b2-34-56.ngrok-free.app`).
3. Open the **AiChatBox Dashboard**, go to your **Project Configuration Details**, and locate the **Multi-Channel Integrations** section.
4. Expand **Microsoft Teams Settings** and input:
   - **Microsoft App ID**: Paste the bot App ID.
   - **Microsoft App Password**: Paste the generated Client Secret.
5. Click **Save Configuration**.
6. Copy the dynamic webhook target URL generated by the dashboard:
   ```text
   https://a1b2-34-56.ngrok-free.app/api/channel/teams/{your-project-id}
   ```

### Step 3: Publish & Link the Bot
1. In the **Microsoft Teams Developer Portal**, edit your Bot registration.
2. Under **Endpoint Address**, paste the webhook URL you copied from Step 2.
3. In **App Features**, assign the bot to **Personal** scopes.
4. Click **Download App Package** or **Publish to Teams** to test locally inside your workspace.

### Step 4: Live Testing
1. Direct message your new bot inside Microsoft Teams.
2. Verify:
   - The bot receives the prompt and responds dynamically using the selected RAG + LLM hybrid context.
   - The message is tracked as a custom `ChatSession` in the dashboard logs.
   - Human Agent takeover works: escalation places the Teams session in the agent queue, and replies sent by dashboard agents route directly back to the Teams chat window!

---

## 🧪 Verification Plan

### Automated Verification
- Run `dotnet build` to ensure the new services, models, and controller bindings compile successfully.
- Run `npm run build` in the Dashboard workspace to confirm there are zero TypeScript compiler issues.

### Manual Verification
- We will draft and execute a verification script simulating an incoming Teams message Activity using standard mock payloads to assert that the webhook maps to the AI engine and dispatches the correct outbound HTTP client POST commands.

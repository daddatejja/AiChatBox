import * as signalR from '@microsoft/signalr';
import { getHeaders, safeJson } from './api.js';

export class LiveChatService {
  constructor(chatbox) {
    this.chatbox = chatbox;
    this.connection = null;
    this.poller = null;
    this.isUserTypingSignalSent = false;
    this.userTypingTimeout = null;
  }

  async startConnection() {
    const cb = this.chatbox;
    if (!cb.config?.handoffEnabled || !cb.currentSessionId || cb.currentSessionId === "null" || cb.currentSessionId === "undefined") return;
    
    if (!cb.apiKey) {
      this.startPoller();
      return;
    }
    
    if (this.connection) return; // already connected

    try {
      this.connection = new signalR.HubConnectionBuilder()
        .withUrl(`${cb.apiUrl}/liveChatHub`)
        .withAutomaticReconnect()
        .build();

      this.connection.on("HandoffStatus", (data) => {
        console.log("[HandoffConnection] Status update:", data);
        if (data.status) {
          cb.handoffStatus = data.status;
        }
      });

      this.connection.on("ReceiveAgentMessage", (msg) => {
        console.log("[HandoffConnection] Received agent message:", msg);
        const existingMsg = cb.shadowRoot.querySelector(`[data-message-id="${msg.id}"]`);
        if (!existingMsg) {
          this.toggleAgentTypingIndicator(false);
          const aiWrapper = cb.addMessage("agent", "", null, null, msg.id);
          const bubble = aiWrapper.querySelector(".message-bubble");
          bubble.innerHTML = `<div class="message-text-content">${cb.formatMarkdown(msg.content)}</div>`;
          cb.scrollToBottom();
        }
      });

      this.connection.on("ReceiveAgentTyping", (data) => {
        if (data.sessionId === cb.currentSessionId) {
          this.toggleAgentTypingIndicator(data.isTyping);
        }
      });

      this.connection.on("AgentJoined", (data) => {
        console.log("[HandoffConnection] Agent joined:", data);
        cb.handoffStatus = "active";
        this.addSystemNotice(data.message || "A support agent has joined the conversation.");
      });

      this.connection.on("SessionResolved", (data) => {
        console.log("[HandoffConnection] Session resolved:", data);
        cb.handoffStatus = "ai";
        this.addSystemNotice(data.message || "The support session has ended. You're now chatting with AI again.");
        this.stopConnection();
      });

      this.connection.on("ReturnedToAi", (data) => {
        console.log("[HandoffConnection] Returned to AI:", data);
        cb.handoffStatus = "ai";
        this.addSystemNotice(data.message || "You've been returned to the AI assistant.");
        this.stopConnection();
      });

      this.connection.on("ReceiveError", (msg) => {
        console.error("[HandoffConnection] Error:", msg);
      });

      this.connection.onclose(() => {
        console.log("[HandoffConnection] Connection closed.");
      });

      await this.connection.start();
      console.log("[HandoffConnection] Connection started successfully.");
      await this.connection.invoke("JoinSession", cb.currentSessionId, cb.apiKey);

    } catch (err) {
      console.error("[HandoffConnection] Start failed, falling back to polling:", err);
      this.connection = null;
      this.startPoller();
    }
  }

  stopConnection() {
    if (this.connection) {
      this.connection.stop().catch(console.error);
      this.connection = null;
    }
    this.stopPoller();
  }

  toggleAgentTypingIndicator(isTyping) {
    const cb = this.chatbox;
    const container = cb.shadowRoot.getElementById("messages-container");
    let indicator = cb.shadowRoot.getElementById("agent-typing-indicator");
    
    if (isTyping) {
      if (!indicator) {
        indicator = document.createElement("div");
        indicator.id = "agent-typing-indicator";
        indicator.className = "message-wrapper ai-side message-appear agent-side";
        indicator.innerHTML = `
          <div class="message-avatar agent-avatar">${cb.icons.user}</div>
          <div class="message ai-message">
            <div class="message-bubble">
              <div class="typing-indicator">
                <div class="typing-dot"></div>
                <div class="typing-dot"></div>
                <div class="typing-dot"></div>
              </div>
            </div>
          </div>
        `;
        container.appendChild(indicator);
        cb.scrollToBottom();
      }
    } else {
      if (indicator) {
        indicator.remove();
      }
    }
  }

  addSystemNotice(text) {
    const cb = this.chatbox;
    const container = cb.shadowRoot.getElementById("messages-container");
    const wrapper = document.createElement("div");
    wrapper.className = "message-wrapper system-notice message-appear";
    wrapper.style.display = "flex";
    wrapper.style.justifyContent = "center";
    wrapper.style.margin = "12px 0";
    wrapper.style.width = "100%";
    
    wrapper.innerHTML = `
      <div style="background:rgba(0,0,0,0.05); color:var(--secondary-text); font-size:0.85rem; font-style:italic; padding:6px 16px; border-radius:16px; text-align:center; max-width:80%">
        ${text}
      </div>
    `;
    container.appendChild(wrapper);
    cb.scrollToBottom();
    return wrapper;
  }

  handleUserTyping() {
    const cb = this.chatbox;
    if (!cb.config?.handoffEnabled || !cb.currentSessionId || !this.connection || cb.handoffStatus === "ai") return;
    
    if (!this.isUserTypingSignalSent) {
      this.isUserTypingSignalSent = true;
      this.connection.invoke("SendUserTyping", cb.currentSessionId, true, cb.apiKey).catch(console.error);
    }
    
    if (this.userTypingTimeout) clearTimeout(this.userTypingTimeout);
    this.userTypingTimeout = setTimeout(() => {
      this.isUserTypingSignalSent = false;
      if (this.connection && cb.currentSessionId) {
        this.connection.invoke("SendUserTyping", cb.currentSessionId, false, cb.apiKey).catch(console.error);
      }
    }, 2000);
  }

  startPoller() {
    const cb = this.chatbox;
    if (!cb.config?.handoffEnabled || !cb.currentSessionId || cb.currentSessionId === "null" || cb.currentSessionId === "undefined") return;
    if (this.poller) return;

    const poll = async () => {
      try {
        const url = new URL(`${cb.apiUrl}/api/chat/${cb.currentSessionId}/poll`);
        url.searchParams.append("since", cb.lastHandoffPollTime);
        
        const response = await fetch(url.toString(), {
          headers: getHeaders(cb)
        });
        
        if (!response.ok) return;
        const data = await safeJson(response);
        
        if (data.serverTime) {
          cb.lastHandoffPollTime = data.serverTime;
        } else {
          cb.lastHandoffPollTime = new Date().toISOString();
        }

        if (data.handoffStatus) {
          cb.handoffStatus = data.handoffStatus;
        }

        if (data.messages && data.messages.length > 0) {
          data.messages.forEach(msg => {
             if (msg.role === "agent") {
                 const existingMsg = cb.shadowRoot.querySelector(`[data-message-id="${msg.id}"]`);
                 if (!existingMsg) {
                     const aiWrapper = cb.addMessage("agent", "", null, null, msg.id);
                     const bubble = aiWrapper.querySelector(".message-bubble");
                     bubble.innerHTML = `<div class="message-text-content">${cb.formatMarkdown(msg.content)}</div>`;
                     cb.scrollToBottom();
                 }
             }
          });
        }

        if (data.handoffStatus === "ai" || data.handoffStatus === "resolved") {
          cb.handoffStatus = "ai";
          this.stopPoller();
        }

      } catch(e) {
        console.error("Handoff polling error:", e);
      }
    };

    poll();
    this.poller = setInterval(poll, 3000);
  }

  stopPoller() {
    if (this.poller) {
      clearInterval(this.poller);
      this.poller = null;
    }
  }

  async sendHandoffMessageViaHttp(text, attachedFileId, imageDataUrl) {
    const cb = this.chatbox;
    try {
      const { modelName: selectedModel, provider: selectedProvider } = cb.getSelectedModel();
      const response = await fetch(`${cb.apiUrl}/api/chat`, {
        method: "POST",
        headers: { ...getHeaders(cb), "Content-Type": "application/json" },
        body: JSON.stringify({
          message: text,
          sessionId: cb.currentSessionId,
          projectId: cb.projectId,
          configurationId: cb.configurationId,
          provider: selectedProvider,
          modelName: selectedModel,
          attachedFileId,
          imageDataUrl,
          context: cb.getPageContext(),
          sessionContext: cb.getAttribute("session-context") || null
        }),
      });
      
      if (response.ok) {
        const reader = response.body.getReader();
        const decoder = new TextDecoder();
        while (true) {
          const { done, value } = await reader.read();
          if (done) break;
          const chunk = decoder.decode(value);
          const lines = chunk.split("\n");
          for (const line of lines) {
            if (line.startsWith("data: ")) {
              try {
                const data = JSON.parse(line.substring(6));
                const sid = data.SessionId || data.sessionId || data.sid;
                if (sid && cb.currentSessionId !== sid) {
                  cb.currentSessionId = sid;
                  localStorage.setItem(cb.getSessionStorageKey(), sid);
                }
              } catch (e) {}
            }
          }
        }
      }
    } catch (err) {
      console.error("Failed to send HTTP handoff message:", err);
    } finally {
      this.startConnection();
    }
  }
}

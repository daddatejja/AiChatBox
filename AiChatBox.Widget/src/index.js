import { icons } from './icons.js';
import { LiveVisualizer } from './utils/visualizer.js';
import { formatMarkdown, sanitizeHtml, escapeHtml } from './utils/markdown.js';
import { setupDraggable } from './utils/draggable.js';
import { 
  getSessionStorageKey, 
  getHeaders, 
  getPageContext, 
  safeJson, 
  fetchConfig, 
  fetchCommands, 
  uploadFile, 
  exportData 
} from './services/api.js';
import { LiveChatService } from './services/live-chat.js';
import { LiveVoiceService } from './services/live-voice.js';
import { renderRichResponse, renderDataResult } from './components/rich-widgets.js';
import { getChatboxTemplate } from './components/chat-template.js';
import { setupEventListeners } from './utils/events.js';
import { VoiceRecorder } from './utils/voice-recorder.js';

class AiChatBox extends HTMLElement {
  constructor() {
    super();
    this.attachShadow({ mode: "open" });

    // Services
    this.chatService = new LiveChatService(this);
    this.voiceService = new LiveVoiceService(this);
    this.voiceRecorder = new VoiceRecorder(this);

    // Decoupled state
    this.isOpen = false;
    this.isFullscreen = false;
    this.isHistoryOpen = false;
    this.isRecording = false;
    this.isTyping = false;
    this.isLive = false;
    this.isLiveMuted = false;
    this.sessions = [];
    this.attachments = [];
    this.pastedImage = null;
    this.currentSessionId = null;
    
    this.handoffStatus = "ai";
    this.lastHandoffPollTime = new Date().toISOString();

    // Attributes
    this.apiUrl = null;
    this.apiKey = null;
    this.authToken = null;
    this.projectId = null;
    this.configurationId = null;
    this.userId = null;
    this.provider = null;
    this.modelName = null;
    this.suggestions = [];
    this.commands = [];
    this.activeCommandIndex = -1;

    // Tools
    this.toolHandlers = new Map();

    // Audio & Live state proxies
    this.visualizer = new LiveVisualizer();
    this.miniVisualizer = new LiveVisualizer();

    // SVG Icons binding
    this.icons = icons;

    this.registerTool = (name, handler) => {
      this.toolHandlers.set(name, handler);
      console.log(`Tool registered: ${name}`);
    };

    this.submitToolResult = (callId, result) => {
      const event = new CustomEvent('tool-result-submitted', {
          detail: { callId, result }
      });
      this.dispatchEvent(event);
    };
  }

  // --- API Proxy Methods ---
  getSessionStorageKey() { return getSessionStorageKey(this); }
  getHeaders() { return getHeaders(this); }
  getPageContext() { return getPageContext(); }
  safeJson(response) { return safeJson(response); }
  fetchConfig() { return fetchConfig(this); }
  fetchCommands() { return fetchCommands(this); }
  uploadFile(file) { return uploadFile(this, file); }
  exportData(format, data) { return exportData(this, format, data); }

  // --- Utility HTML Sanitizing Proxies ---
  formatMarkdown(text) { return formatMarkdown(text); }
  sanitizeHtml(html) { return sanitizeHtml(html); }
  escapeHtml(str) { return escapeHtml(str); }
  setupDraggable() { return setupDraggable(this); }

  // --- Widget Renderers Proxies ---
  renderRichResponse(ruleResponse, bubble, isHistoric = false) { 
    return renderRichResponse(this, ruleResponse, bubble, isHistoric); 
  }
  renderDataResult(result, container) { 
    return renderDataResult(this, result, container); 
  }

  // --- Handoff Real-Time Support Service Proxies ---
  get handoffPoller() { return this.chatService.poller; }
  set handoffPoller(val) { this.chatService.poller = val; }
  get handoffConnection() { return this.chatService.connection; }
  set handoffConnection(val) { this.chatService.connection = val; }
  get isUserTypingSignalSent() { return this.chatService.isUserTypingSignalSent; }
  set isUserTypingSignalSent(val) { this.chatService.isUserTypingSignalSent = val; }
  get userTypingTimeout() { return this.chatService.userTypingTimeout; }
  set userTypingTimeout(val) { this.chatService.userTypingTimeout = val; }

  startHandoffConnection() { return this.chatService.startConnection(); }
  stopHandoffConnection() { return this.chatService.stopConnection(); }
  toggleAgentTypingIndicator(isTyping) { return this.chatService.toggleAgentTypingIndicator(isTyping); }
  addSystemNotice(text) { return this.chatService.addSystemNotice(text); }
  handleUserTyping() { return this.chatService.handleUserTyping(); }
  startHandoffPoller() { return this.chatService.startPoller(); }
  stopHandoffPoller() { return this.chatService.stopPoller(); }
  sendHandoffMessageViaHttp(text, attachedFileId, imageDataUrl) { 
    return this.chatService.sendHandoffMessageViaHttp(text, attachedFileId, imageDataUrl); 
  }

  // --- Live Voice Streaming Service Proxies ---
  get audioContext() { return this.voiceService.audioContext; }
  set audioContext(val) { this.voiceService.audioContext = val; }
  get playbackContext() { return this.voiceService.playbackContext; }
  set playbackContext(val) { this.voiceService.playbackContext = val; }
  get liveConnection() { return this.voiceService.connection; }
  set liveConnection(val) { this.voiceService.connection = val; }
  get playbackAnalyser() { return this.voiceService.playbackAnalyser; }
  set playbackAnalyser(val) { this.voiceService.playbackAnalyser = val; }
  get micAnalyser() { return this.voiceService.micAnalyser; }
  set micAnalyser(val) { this.voiceService.micAnalyser = val; }
  get micStream() { return this.voiceService.micStream; }
  set micStream(val) { this.voiceService.micStream = val; }
  get nextPlayTime() { return this.voiceService.nextPlayTime; }
  set nextPlayTime(val) { this.voiceService.nextPlayTime = val; }
  get liveStartTime() { return this.voiceService.liveStartTime; }
  set liveStartTime(val) { this.voiceService.liveStartTime = val; }
  get liveTimerInterval() { return this.voiceService.liveTimerInterval; }
  set liveTimerInterval(val) { this.voiceService.liveTimerInterval = val; }
  get visibilityHandler() { return this.voiceService.visibilityHandler; }
  set visibilityHandler(val) { this.voiceService.visibilityHandler = val; }

  toggleLiveMode() { return this.voiceService.toggleLiveMode(); }
  startLiveSession() { return this.voiceService.startLiveSession(); }
  stopLiveSession() { return this.voiceService.stopLiveSession(); }
  reconnectLiveSession() { return this.voiceService.reconnectLiveSession(); }
  showLiveError(msg) { return this.voiceService.showLiveError(msg); }
  playAudioChunk(data) { return this.voiceService.playAudioChunk(data); }
  updateThought(text) { return this.voiceService.updateThought(text); }
  updateLiveTimer() { return this.voiceService.updateLiveTimer(); }
  updateLiveStatus(status, text) { return this.voiceService.updateLiveStatus(status, text); }
  addLiveMessage(role, text) { return this.voiceService.addLiveMessage(role, text); }
  toggleLiveMute() { return this.voiceService.toggleLiveMute(); }
  sendLiveTextMessage() { return this.voiceService.sendLiveTextMessage(); }

  getSelectedModel() {
    const select = this.shadowRoot.getElementById("model-select");
    const option = select?.selectedOptions?.[0];
    return {
      modelName: select?.value || this.modelName,
      provider: option?.dataset?.provider || this.provider
    };
  }

  applyTheme(theme) {
    if (!theme) return;
    if (theme.primaryColor) {
      this.style.setProperty("--primary-color", theme.primaryColor);
      this.style.setProperty("--primary-gradient", `linear-gradient(135deg, ${theme.primaryColor} 0%, this.adjustColor(theme.primaryColor, -20) 100%)`);
    }
    if (theme.bgColor) {
      this.style.setProperty("--bg-color", theme.bgColor);
    }
    if (theme.fontFamily) {
      if (theme.fontFamily === 'system-ui') {
        this.style.setProperty("--font-family", "system-ui, -apple-system, sans-serif");
      } else {
        this.style.setProperty("--font-family", `"${theme.fontFamily}", sans-serif`);
      }
    }
    if (theme.position === 'bottom-left') {
      this.style.setProperty("--widget-right", "auto");
      this.style.setProperty("--widget-left", "24px");
    } else {
      this.style.setProperty("--widget-right", "24px");
      this.style.setProperty("--widget-left", "auto");
    }

    if (theme.headerBgColor) {
      this.style.setProperty("--header-bg", theme.headerBgColor);
    } else if (theme.primaryColor) {
      this.style.setProperty("--header-bg", theme.primaryColor);
    }
    
    if (theme.headerTextColor) {
      this.style.setProperty("--header-text", theme.headerTextColor);
    }
    
    if (theme.userBubbleBgColor) {
      this.style.setProperty("--user-msg-bg", theme.userBubbleBgColor);
    } else if (theme.primaryColor) {
      this.style.setProperty("--user-msg-bg", `linear-gradient(135deg, ${theme.primaryColor} 0%, ${this.adjustColor(theme.primaryColor, -20)} 100%)`);
    }
    
    if (theme.userBubbleTextColor) {
      this.style.setProperty("--user-msg-text", theme.userBubbleTextColor);
    }
    
    if (theme.botBubbleBgColor) {
      this.style.setProperty("--bot-msg-bg", theme.botBubbleBgColor);
    }
    
    if (theme.botBubbleTextColor) {
      this.style.setProperty("--bot-msg-text", theme.botBubbleTextColor);
    }
    
    if (theme.chatBgColor) {
      this.style.setProperty("--chat-bg", theme.chatBgColor);
    }
    
    if (theme.launcherBgColor) {
      this.style.setProperty("--launcher-bg", theme.launcherBgColor);
    } else if (theme.primaryColor) {
      this.style.setProperty("--launcher-bg", `linear-gradient(135deg, ${theme.primaryColor} 0%, ${this.adjustColor(theme.primaryColor, -20)} 100%)`);
    }
    
    if (theme.launcherIconColor) {
      this.style.setProperty("--launcher-icon-color", theme.launcherIconColor);
    }
    
    if (theme.launcherBorderRadius !== undefined && theme.launcherBorderRadius !== null) {
      this.style.setProperty("--launcher-border-radius", `${theme.launcherBorderRadius}px`);
    }
    
    if (theme.chatBorderRadius !== undefined && theme.chatBorderRadius !== null) {
      this.style.setProperty("--chat-border-radius", `${theme.chatBorderRadius}px`);
    }
    
    if (theme.bubbleBorderRadius !== undefined && theme.bubbleBorderRadius !== null) {
      this.style.setProperty("--bubble-border-radius", `${theme.bubbleBorderRadius}px`);
    }
  }

  adjustColor(color, amount) {
      return '#' + color.replace(/^#/, '').replace(/../g, color => 
          ('0'+Math.min(255, Math.max(0, parseInt(color, 16) + amount)).toString(16)).substr(-2));
  }

  async connectedCallback() {
    this.apiUrl = this.getAttribute("api-base") || this.getAttribute("api-url") || window.location.origin;
    this.apiKey = this.getAttribute("api-key") || null;
    this.authToken = this.getAttribute("auth-token") || null;
    this.projectId = this.getAttribute("project-id") || null;
    this.configurationId = this.getAttribute("configuration-id") || null;
    this.currentSessionId = localStorage.getItem(this.getSessionStorageKey()) || null;
    this.userId = this.getAttribute("user-id") || "standalone-user";
    this.provider = this.getAttribute("provider") || "gemini";
    this.modelName = this.getAttribute("model") || "gemini-3.1-flash-lite-preview";
    this.suggestions = JSON.parse(
      this.getAttribute("suggestions") || '[]',
    );
    if (this.suggestions.length === 0) {
        this.suggestions = ["Good morning", "How can you help me?", "Tell me a joke"];
    }

    await this.loadExternalScripts();

    await this.fetchConfig();
    await this.fetchCommands();
    this.render();
    this._eventCleanup = setupEventListeners(this);
    this.setupDraggable();
    if (this.currentSessionId) {
      this.loadSessionMessages(this.currentSessionId);
    } else {
      this.renderEmptyState();
    }
    this.loadSessions();
  }

  disconnectedCallback() {
    if (this._eventCleanup) {
      this._eventCleanup();
    }
  }

  render() {
    this.shadowRoot.innerHTML = getChatboxTemplate(this);
  }



  renderAutocompleteDropdown(matched, triggerChar) {
    const dropdown = this.shadowRoot.getElementById("command-dropdown");
    if (!dropdown) return;
    
    dropdown.innerHTML = matched.map((cmd, index) => `
      <div class="command-item ${index === this.activeCommandIndex ? 'active' : ''}" data-index="${index}" data-name="${cmd.commandName}">
        <span class="command-item-trigger">${cmd.commandTriggerChar || triggerChar || '/'}</span>
        <span class="command-item-name">${cmd.commandName}</span>
        <span class="command-item-desc">${cmd.commandDescription || ''}</span>
      </div>
    `).join('');
    
    dropdown.style.display = "flex";
    
    dropdown.querySelectorAll(".command-item").forEach(item => {
      item.onclick = () => {
        const name = item.getAttribute("data-name");
        this.selectCommand(name);
      };
    });
  }

  updateAutocompleteActiveItem(items) {
    items.forEach((item, index) => {
      if (index === this.activeCommandIndex) {
        item.classList.add("active");
        item.scrollIntoView({ block: "nearest" });
      } else {
        item.classList.remove("active");
      }
    });
  }

  selectCommand(name) {
    const input = this.shadowRoot.getElementById("chat-input");
    if (input) {
      const val = input.value;
      const matchInfo = this.lastCommandMatch;
      if (matchInfo) {
        const before = val.substring(0, matchInfo.index);
        const after = val.substring(matchInfo.index + matchInfo.length);
        const inserted = `${matchInfo.triggerChar}${name} `;
        input.value = before + inserted + after;
        const newCursorPos = matchInfo.index + inserted.length;
        input.setSelectionRange(newCursorPos, newCursorPos);
      } else {
        input.value = `/${name} `;
      }
      input.focus();
      this.hideAutocompleteDropdown();
      this.updateSendButtonState();
    }
  }

  hideAutocompleteDropdown() {
    const dropdown = this.shadowRoot.getElementById("command-dropdown");
    if (dropdown) {
      dropdown.style.display = "none";
    }
    this.activeCommandIndex = -1;
  }

  // --- Real-time voice tools routing ---
  async handleLiveToolCall(name, args, callId = null, isBackend = false) {
    console.log(`[LiveToolCall] name=${name}, callId=${callId}, isBackend=${isBackend}`, args);
    
    const area = this.shadowRoot.getElementById("live-transcript");
    const div = document.createElement("div");
    div.className = "live-transcript-msg live-msg-tool";
    
    const domId = callId ? `tool-call-${callId}` : `tool-call-${name}-${Date.now()}`;
    
    div.innerHTML = `
      <div class="live-msg-avatar">${icons.refresh}</div>
      <div class="live-msg-bubble tool-bubble" id="${domId}" data-name="${name}">
        <span>Executing <strong>${name}</strong>...</span>
      </div>
    `;
    area.appendChild(div);
    area.scrollTop = area.scrollHeight;
    
    if (isBackend) {
      console.log(`[LiveToolCall] Backend tool detected: ${name}. Waiting for server result.`);
      return;
    }

    let result = null;
    const handler = this.toolHandlers.get(name);
    
    if (handler) {
      try {
        result = await handler(args);
      } catch (err) {
        result = { error: err.message };
      }
    } else {
      const effectiveCallId = callId || `live-${name}-${Date.now()}`;
      const resultPromise = new Promise((resolve) => {
        const onResult = (e) => {
          if (e.detail.callId === effectiveCallId || e.detail.callId === name) {
            this.removeEventListener('tool-result-submitted', onResult);
            resolve(e.detail.result);
          }
        };
        this.addEventListener('tool-result-submitted', onResult);
        
        setTimeout(() => {
          this.removeEventListener('tool-result-submitted', onResult);
          resolve({ error: "Live tool execution timed out" });
        }, 30000);
      });

      const event = new CustomEvent("tool-call", {
        detail: { name, args, live: true, callId: effectiveCallId },
        bubbles: true,
        composed: true
      });
      this.dispatchEvent(event);
      
      result = await resultPromise;
    }

    if (this.liveConnection) {
      try {
        await this.liveConnection.invoke("SendToolResult", callId, JSON.stringify(result));
      } catch (err) {
        console.error("Failed to send tool result:", err);
        div.querySelector(".live-msg-bubble").innerHTML = `<span style="color:var(--danger-color)">Failed to execute <strong>${name}</strong></span>`;
      }
    }
  }

  async handleLiveToolResult(callId, name, result) {
    console.log(`[LiveToolResult] id=${callId}, name=${name}`, result);
    
    const area = this.shadowRoot.getElementById("live-transcript");
    let bubble = this.shadowRoot.getElementById(`tool-call-${callId}`);
    if (!bubble) {
      const bubbles = this.shadowRoot.querySelectorAll(`.tool-bubble[data-name="${name}"]`);
      bubble = bubbles[bubbles.length - 1]; 
    }

    if (bubble) {
      bubble.classList.add("tool-completed");
      
      const wrapper = bubble.closest(".live-transcript-msg");
      if (wrapper) {
        wrapper.classList.add("tool-done");
        const avatar = wrapper.querySelector(".live-msg-avatar");
        if (avatar) {
          avatar.innerHTML = icons.awesome;
          avatar.style.animation = "none";
        }
      }

      bubble.innerHTML = `<div class="tool-result-header">
        ${icons.check} <span>Completed <strong>${name}</strong></span>
      </div>
      <div class="live-widget-container" style="margin-top:10px"></div>`;
      
      const container = bubble.querySelector(".live-widget-container");
      let data = result;
      
      if (data && (data.content !== undefined || data.Content !== undefined)) {
        data = data.content !== undefined ? data.content : data.Content;
      }
      if (data && (data.result !== undefined || data.Result !== undefined)) {
        data = data.result !== undefined ? data.result : data.Result;
      }
      if (typeof data === 'string' && data.trim().startsWith('[')) {
        try { data = JSON.parse(data); } catch(e) {}
      }

      console.log(`[LiveToolResult] Final data for ${name}:`, data);

      const isData = data && (
        (data.rows && data.rows.length > 0) || 
        (data.data && data.data.length > 0) || 
        (Array.isArray(data) && data.length > 0 && typeof data[0] === 'object' && data[0] !== null)
      );
      
      if (isData) {
         this.renderDataResult(data, container);
      } else if (data !== null && data !== undefined) {
         const pre = document.createElement("pre");
         pre.className = "live-simple-result";
         pre.textContent = typeof data === 'string' ? data : JSON.stringify(data, null, 2);
         bubble.appendChild(pre);
      }
      
      this.scrollToBottom();
    }
  }

  addMessage(role, htmlContent, fileId = null, fileName = null, messageId = null) {
    const container = this.shadowRoot.getElementById("messages-container");
    const wrapper = document.createElement("div");
    const isUser = role === "user";
    const isAgent = role === "agent";
    wrapper.className = `message-wrapper ${isUser ? "user-side" : "ai-side"} ${isAgent ? "agent-side" : ""} message-appear`;
    if (messageId) {
      wrapper.dataset.messageId = messageId;
    }
    
    const time = new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
    const avatar = isUser ? "" : `<div class="message-avatar ${isAgent ? 'agent-avatar' : 'ai-avatar'}">${isAgent ? icons.person : icons.awesome}</div>`;
    const userAvatar = isUser ? `<div class="message-avatar user-avatar">${icons.user}</div>` : "";
    
    if (!isUser && !isAgent) {
      container.querySelectorAll("[data-action='regenerate']").forEach(btn => btn.remove());
    }

    let actionsHtml = "";
    if (!isUser) {
      actionsHtml = `
        <div class="message-actions">
          <button class="msg-action-btn" data-action="copy" title="Copy">${icons.copy}</button>
          <button class="msg-action-btn" data-action="speak" title="Listen">${icons.voice}</button>
          <button class="msg-action-btn" data-action="regenerate" title="Regenerate">${icons.refresh}</button>
          <span class="feedback-divider"></span>
          <button class="msg-action-btn feedback-btn" data-action="thumbsup" title="Good response">👍</button>
          <button class="msg-action-btn feedback-btn" data-action="thumbsdown" title="Bad response">👎</button>
        </div>
      `;
    }

    wrapper.innerHTML = `
      ${avatar}
      <div class="message ${isUser ? "user" : "ai"}-message">
        <div class="message-bubble">${htmlContent}</div>
        <div class="message-footer">
          <span class="message-time">${time}</span>
        </div>
        ${actionsHtml}
      </div>
      ${userAvatar}
    `;
    container.appendChild(wrapper);

    // Bind UI actions
    if (!isUser) {
      wrapper.querySelectorAll(".msg-action-btn").forEach(btn => {
        btn.onclick = () => {
          const action = btn.getAttribute("data-action");
          const bubble = wrapper.querySelector(".message-bubble");
          const text = bubble.innerText;
          
          if (action === "copy") {
            navigator.clipboard.writeText(text);
            const originalIcon = btn.innerHTML;
            btn.innerHTML = icons.check;
            btn.style.color = "var(--success-color, #2ecc71)";
            setTimeout(() => {
              btn.innerHTML = originalIcon;
              btn.style.color = "";
            }, 2000);
          } else if (action === "speak") {
            const originalIcon = btn.innerHTML;
            btn.innerHTML = icons.loading;
            
            const utterance = new SpeechSynthesisUtterance(text);
            utterance.onend = () => {
              btn.innerHTML = originalIcon;
              btn.classList.remove("speaking-active");
            };
            btn.classList.add("speaking-active");
            window.speechSynthesis.speak(utterance);
          } else if (action === "regenerate") {
            this.regenerateLastResponse();
          } else if (action === "thumbsup" || action === "thumbsdown") {
            const msgId = wrapper.dataset.messageId;
            if (msgId) {
              const feedbackValue = action === "thumbsup" ? 1 : -1;
              this.submitFeedback(msgId, feedbackValue, btn, wrapper);
            }
          }
        };
      });
    }

    this.scrollToBottom();
    return wrapper;
  }

  async submitFeedback(messageId, feedback, btn, wrapper) {
    try {
      const response = await fetch(`${this.apiUrl}/api/chat/messages/${messageId}/feedback`, {
        method: "POST",
        headers: { ...this.getHeaders(), "Content-Type": "application/json" },
        body: JSON.stringify({ feedback }),
      });
      if (response.ok) {
        wrapper.querySelectorAll(".feedback-btn").forEach(b => {
          b.classList.remove("feedback-active");
          b.style.opacity = "0.4";
        });
        btn.classList.add("feedback-active");
        btn.style.opacity = "1";
      }
    } catch (e) {
      console.error("Feedback submission failed:", e);
    }
  }

  async regenerateLastResponse() {
    const messages = this.shadowRoot.getElementById("messages-container").querySelectorAll(".message-wrapper.user-side");
    if (messages.length === 0) return;
    
    const lastUserMsg = messages[messages.length - 1];
    const text = lastUserMsg.querySelector(".message-text").innerText;
    
    const allMsgs = this.shadowRoot.getElementById("messages-container").children;
    if (allMsgs.length > 0 && !allMsgs[allMsgs.length - 1].classList.contains("user-side")) {
      allMsgs[allMsgs.length - 1].remove();
    }

    this.shadowRoot.getElementById("chat-input").value = text;
    this.sendMessage();
  }

  startNewChat() {
    if (this.abortController) {
      this.abortController.abort();
    }
    if (this.isLive) {
      this.stopLiveSession();
    }
    this.stopGeneration();
    this.stopHandoffConnection();
    this.handoffStatus = "ai";
    this.currentSessionId = null;
    localStorage.removeItem(this.getSessionStorageKey());
    this.shadowRoot.getElementById("messages-container").innerHTML = "";

    const transcriptArea = this.shadowRoot.getElementById("live-transcript");
    if (transcriptArea) transcriptArea.innerHTML = "";

    this.renderEmptyState();
    if (this.isHistoryOpen) this.toggleHistory();

    this.isTyping = false;
    this.updateInputButtons();
  }

  toggleChat() {
    this.isOpen = !this.isOpen;
    const container = this.shadowRoot.getElementById("main-container");
    const fab = this.shadowRoot.getElementById("fab-toggle");
    const mini = this.shadowRoot.getElementById("mini-live");

    if (this.isOpen) {
      container.classList.add("open");
      fab.classList.add("toggle-hidden");
      mini.classList.remove("active");
      if (this.isLive) {
        this.visualizer.init(this.shadowRoot.getElementById("live-orb-canvas"));
        this.miniVisualizer.destroy();
      }
    } else {
      container.classList.remove("open");
      if (this.isLive) {
        mini.classList.add("active");
        this.miniVisualizer.init(this.shadowRoot.getElementById("live-orb-canvas-mini"));
        this.visualizer.destroy();
      } else {
        fab.classList.remove("toggle-hidden");
      }
    }
  }

  async loadSessions() {
    try {
      const isArchived = this.shadowRoot.getElementById("tab-archived").classList.contains("history-tab-active");
      const endpoint = isArchived ? "archived" : "sessions";
      let url = `${this.apiUrl}/api/chat/${endpoint}`;
      const projectId = this.projectId || this.getAttribute("project-id");
      if (projectId) {
        url += `?projectId=${encodeURIComponent(projectId)}`;
      }
      const response = await fetch(url, {
        headers: this.getHeaders(),
      });
      if (!response.ok) throw new Error("Failed to load history");
      this.sessions = await this.safeJson(response);
      this.renderHistoryList(isArchived);
    } catch (err) {
      console.error("Failed to load sessions", err);
      this.shadowRoot.getElementById("history-list").innerHTML = `<div class="history-error">Error loading history</div>`;
    }
  }

  async loadSessionMessages(sessionId) {
    this.stopHandoffConnection();
    this.currentSessionId = sessionId;
    localStorage.setItem(this.getSessionStorageKey(), sessionId);
    const projectId = this.projectId || this.getAttribute("project-id") || localStorage.getItem("ai_chat_project_id");
    if (projectId) localStorage.setItem("ai_chat_project_id", projectId);
    
    const list = this.shadowRoot.getElementById("messages-container");
    list.innerHTML = `<div class="history-loading">Loading messages...</div>`;
    try {
      let url = `${this.apiUrl}/api/chat/sessions/${sessionId}`;
      if (projectId) {
        url += `?projectId=${encodeURIComponent(projectId)}`;
      }
      const response = await fetch(url, {
        headers: this.getHeaders(),
      });
      if (!response.ok) throw new Error("Session not found");
      const messages = await this.safeJson(response);
      list.innerHTML = "";
      messages.forEach((m) => {
        const role = (m.Role || m.role || "").toLowerCase();
        const isAi = role === "ai" || role === "assistant" || role === "model" || role === "bot";
        const isTool = role === "tool";
        const rawContent = m.Content || m.content || "";
        
        let isRichResponse = false;
        let richType = null;
        let richPayload = null;
        let cleanContent = rawContent;

        if (isAi || role === "agent") {
          const regex = /\[([A-Z0-9_-]+)\s+RESPONSE\]/i;
          const match = rawContent.match(regex);
          if (match) {
            const fullMatch = match[0];
            const typeStr = match[1].toLowerCase();
            const index = rawContent.indexOf(fullMatch);
            const textBefore = rawContent.substring(0, index).trim();
            const jsonPart = rawContent.substring(index + fullMatch.length).trim();
            try {
              richPayload = JSON.parse(jsonPart);
              richType = typeStr;
              isRichResponse = true;
              cleanContent = textBefore;
            } catch (e) {
              console.error("Failed to parse historic rich response JSON:", e);
            }
          }
        }

        if (isRichResponse) {
          const displayRole = role === "agent" ? "agent" : (isAi ? "ai" : "user");
          const formattedText = cleanContent ? this.formatMarkdown(cleanContent) : "";
          const msgWrap = this.addMessage(displayRole, `<div class="message-text-content">${formattedText}</div><div class="message-widget-container"></div>`, null, null, m.Id || m.id);
          this.renderRichResponse({
            responseType: richType,
            responsePayload: richPayload
          }, msgWrap.querySelector(".message-bubble"), true);
          return;
        }

        let toolData = m.ToolResult || m.toolResult;
        let wasParsedAsTool = false;
        let isToolCalling = false;

        if (rawContent.trim().startsWith('{')) {
          try {
            const parsed = JSON.parse(rawContent);
            if (parsed.toolName || parsed.ToolName) {
              toolData = parsed;
              wasParsedAsTool = true;
            }
            if (parsed.toolCalls || parsed.ToolCalls || parsed.toolCall || parsed.ToolCall) {
              isToolCalling = true;
            }
          } catch(e) {}
        }

        if (isTool || wasParsedAsTool || isToolCalling) {
           const toolName = toolData?.toolName || toolData?.ToolName;
           const isDbTool = toolName === 'query_project_database' || toolName === 'query_database' || toolName === 'query_data';
           const hasResult = toolData?.result || toolData?.Result;
           
           if (isDbTool && hasResult) {
              const msgWrap = this.addMessage("ai", `<div class="message-text-content"></div><div class="message-widget-container"></div>`, null, null, m.Id || m.id);
              const widgetContainer = msgWrap.querySelector(".message-widget-container");
              this.renderDataResult(toolData.result || toolData.Result, widgetContainer);
           }
           return;
        }

        const content = this.formatMarkdown(rawContent);
        const fileId = m.AttachedFileId || m.attachedFileId;
        const fileName = m.AttachedFileName || m.attachedFileName;
        const img = m.ImageDataUrl || m.imageDataUrl;
        
        let displayHtml = img ? `<div class="message-image-container"><img src="${img}" class="message-image"></div>` + content : content;
        if (fileId && fileName) {
          displayHtml += `<div class="message-attachment-pill">${icons.attach} <span>${fileName}</span></div>`;
        }
        const displayRole = role === "agent" ? "agent" : (isAi ? "ai" : "user");
        const msgWrap = this.addMessage(displayRole, isAi || displayRole === "agent" ? `<div class="message-text-content">${displayHtml}</div><div class="message-widget-container"></div>` : displayHtml, fileId, fileName, m.Id || m.id);
        
        if (toolData && isAi) {
          const widgetContainer = msgWrap.querySelector(".message-widget-container");
          this.renderDataResult(toolData.result || toolData.Result, widgetContainer);
        }
      });
      this.scrollToBottom();
      this.startHandoffConnection();
    } catch (err) {
      list.innerHTML = "";
      this.addMessage("ai", "Error loading chat history.");
    }
  }

  toggleHistory() {
    this.isHistoryOpen = !this.isHistoryOpen;
    this.shadowRoot.getElementById("history-drawer").classList.toggle("history-open", this.isHistoryOpen);
    if (this.isHistoryOpen) this.loadSessions();
  }

  handleFileSelection(e) {
    const files = Array.from(e.target.files);
    if (files.length === 0) return;
    
    const file = files[0];
    this.attachments = [];
    this.pastedImage = null;

    if (file.type.startsWith('image/')) {
      const reader = new FileReader();
      reader.onload = (event) => {
        this.pastedImage = {
          data: event.target.result,
          type: file.type
        };
        this.renderAttachments();
      };
      reader.readAsDataURL(file);
      return;
    }

    const attachment = { name: file.name, isUploading: true, id: null };
    this.attachments.push(attachment);
    this.renderAttachments();
    
    this.uploadFile(file).then(uploaded => {
      attachment.id = uploaded.id;
      attachment.isUploading = false;
      this.renderAttachments();
    }).catch(err => {
      attachment.error = true;
      attachment.isUploading = false;
      this.renderAttachments();
    });
  }

  renderAttachments() {
    const container = this.shadowRoot.getElementById("attachments-container");
    if (this.attachments.length === 0 && !this.pastedImage) {
      container.style.display = "none";
      return;
    }
    container.style.display = "flex";
    container.innerHTML = "";
    this.attachments.forEach((att, i) => {
      let icon = icons.attach;
      if (att.name) {
        const ext = att.name.split('.').pop().toLowerCase();
        if (['pdf'].includes(ext)) icon = icons.pdf || icons.attach;
        else if (['xls', 'xlsx', 'csv'].includes(ext)) icon = icons.excel || icons.attach;
        else if (['doc', 'docx', 'txt', 'rtf', 'md'].includes(ext)) icon = icons.list || icons.attach;
      }

      const pill = document.createElement("div");
      pill.className = `attached-file-pill ${att.error ? "error" : ""}`;
      pill.innerHTML = `
        ${icon}
        <span class="attached-file-name">${att.name}</span>
        <button class="remove-attachment-btn" data-idx="${i}">${icons.close}</button>
      `;
      pill.querySelector("button").onclick = () => {
        this.attachments.splice(i, 1);
        this.renderAttachments();
      };
      container.appendChild(pill);
    });

    if (this.pastedImage) {
      const pill = document.createElement("div");
      pill.className = "attached-file-pill";
      pill.innerHTML = `
        <img src="${this.pastedImage.data}" class="pasted-image-thumb" alt="Pasted Image">
        <span class="attached-file-name">Pasted Image</span>
        <button class="remove-attachment-btn">${icons.close}</button>
      `;
      pill.querySelector("button").onclick = () => {
        this.pastedImage = null;
        this.renderAttachments();
      };
      container.appendChild(pill);
    }

    this.updateSendButtonState();
  }







  switchHistoryTab(tab) {
    const isArchived = tab === "archived";
    this.shadowRoot.getElementById("tab-chats").classList.toggle("history-tab-active", !isArchived);
    this.shadowRoot.getElementById("tab-archived").classList.toggle("history-tab-active", isArchived);
    this.loadSessions();
  }

  handleMessagesScroll() {
    const m = this.shadowRoot.getElementById("messages-container");
    const btn = this.shadowRoot.getElementById("scroll-down-btn");
    const isAtBottom = m.scrollHeight - m.scrollTop - m.clientHeight < 100;
    btn.style.display = isAtBottom ? "none" : "flex";
  }

  renderHistoryList(isArchived) {
    const list = this.shadowRoot.getElementById("history-list");
    list.innerHTML = "";
    this.sessions.forEach((s) => {
      const item = document.createElement("div");
      item.className = `history-item ${s.id === this.currentSessionId ? "active" : ""}`;
      item.innerHTML = `
        <div class="history-item-icon">${icons.history}</div>
        <div class="history-item-content">
          <div class="history-item-title">${s.title || "Untitled Chat"}</div>
          <div class="history-item-date">${new Date(s.lastMessageAt).toLocaleDateString()}</div>
        </div>
      `;
      item.onclick = () => this.loadSessionMessages(s.id);
      list.appendChild(item);
    });
  }

  renderEmptyState() {
    const container = this.shadowRoot.getElementById("messages-container");
    container.innerHTML = `
      <div class="chatbox-empty-state">
        <div class="empty-state-icon">${icons.awesome}</div>
        <h3>${this.getAttribute("welcome-message") || "How can I help you today?"}</h3>
        <div class="suggestion-chips">
          ${this.suggestions.map(s => `<button class="suggestion-chip">${s}</button>`).join("")}
        </div>
      </div>
    `;
    container.querySelectorAll(".suggestion-chip").forEach((btn, i) => {
      btn.onclick = () => {
        this.shadowRoot.getElementById("chat-input").value = this.suggestions[i];
        this.sendMessage();
      };
    });
  }

  stopGeneration() {
    if (this.abortController) {
      this.abortController.abort();
      this.isTyping = false;
      this.updateInputButtons();
    }
  }

  adjustTextAreaHeight(el) { el.style.height = "auto"; el.style.height = Math.min(el.scrollHeight, 120) + "px"; }
  
  updateSendButtonState() {
    const input = this.shadowRoot.getElementById("chat-input");
    const btn = this.shadowRoot.getElementById("btn-send");
    const hasContent = input.value.trim().length > 0 || this.attachments.length > 0 || this.pastedImage;
    btn.disabled = !hasContent;
    btn.classList.toggle("send-btn-active", hasContent);
  }

  updateInputButtons() {
    this.shadowRoot.getElementById("btn-send").style.display = this.isTyping ? "none" : "flex";
    this.shadowRoot.getElementById("btn-mic").style.display = this.isTyping ? "none" : "flex";
    this.shadowRoot.getElementById("btn-stop").style.display = this.isTyping ? "flex" : "none";
  }

  toggleFullscreen() {
    this.isFullscreen = !this.isFullscreen;
    this.shadowRoot.getElementById("main-container").classList.toggle("chatbox-fullscreen", this.isFullscreen);
  }

  async loadExternalScripts() {
    // Only Chart.js needs dynamic load now. Highlight.js and Marked are fully bundled!
    const scripts = [
      { id: 'chart-js', url: 'https://cdn.jsdelivr.net/npm/chart.js' }
    ];

    const promises = scripts.map(s => {
      if (document.getElementById(s.id)) return Promise.resolve();
      return new Promise((resolve) => {
        const script = document.createElement('script');
        script.id = s.id;
        script.src = s.url;
        script.async = true;
        script.onload = resolve;
        script.onerror = resolve;
        document.head.appendChild(script);
      });
    });

    await Promise.all(promises);
  }

  scrollToBottom() {
    const m = this.shadowRoot.getElementById("messages-container");
    const l = this.shadowRoot.getElementById("live-transcript");
    const target = (this.isLive && l) ? l : m;
    
    if (target) {
      target.scrollTop = target.scrollHeight;
      
      target.querySelectorAll(".copy-code-btn").forEach(btn => {
        if (btn.dataset.listener) return;
        btn.dataset.listener = "true";
        btn.onclick = () => {
          const codeId = btn.getAttribute("data-code-id");
          const code = this.shadowRoot.getElementById(codeId).innerText;
          navigator.clipboard.writeText(code);
          const original = btn.innerHTML;
          btn.innerHTML = `${icons.check} Copied`;
          setTimeout(() => btn.innerHTML = original, 2000);
        };
      });
    }
  }

  async sendMessage() {
    const input = this.shadowRoot.getElementById("chat-input");
    let text = input.value.trim();
    if (!text && !this.attachments.length && !this.pastedImage) return;

    const emptyState = this.shadowRoot.querySelector(".chatbox-empty-state");
    if (emptyState) emptyState.remove();

    input.value = "";
    input.style.height = "auto";
    this.updateSendButtonState();

    let displayHtml = `<div class="message-text">${text}</div>`;
    if (this.pastedImage) {
      displayHtml = `<div class="message-image-container"><img src="${this.pastedImage.data}" class="message-image"></div>` + displayHtml;
    }
    this.attachments.forEach(att => {
      if (att.id) displayHtml += `<div class="message-attachment-pill">${icons.attach} <span>${att.name}</span></div>`;
    });

    this.addMessage("user", displayHtml);

    const attachedFileId = this.attachments.length > 0 ? this.attachments[0].id : null;
    const imageDataUrl = this.pastedImage ? this.pastedImage.data : null;
    this.attachments = [];
    this.pastedImage = null;
    this.renderAttachments();

    if (this.chatService.userTypingTimeout) clearTimeout(this.chatService.userTypingTimeout);
    this.chatService.isUserTypingSignalSent = false;
    if (this.chatService.connection && this.currentSessionId && this.handoffStatus !== "ai") {
      this.chatService.connection.invoke("SendUserTyping", this.currentSessionId, false, this.apiKey).catch(console.error);
    }

    if (this.handoffStatus === "active" || this.handoffStatus === "queued") {
      if (this.chatService.connection && this.chatService.connection.state === "Connected") {
        try {
          await this.chatService.connection.invoke("SendUserMessage", this.currentSessionId, text, this.apiKey);
        } catch (err) {
          console.error("Failed to send message via SignalR:", err);
          await this.sendHandoffMessageViaHttp(text, attachedFileId, imageDataUrl);
        }
      } else {
        await this.sendHandoffMessageViaHttp(text, attachedFileId, imageDataUrl);
      }
      return;
    }

    this.isTyping = true;
    this.updateInputButtons();

    const aiWrapper = this.addMessage("ai", '<div class="typing-indicator"><div class="typing-dot"></div><div class="typing-dot"></div><div class="typing-dot"></div></div>');
    const bubble = aiWrapper.querySelector(".message-bubble");

    try {
      this.abortController = new AbortController();
      const { modelName: selectedModel, provider: selectedProvider } = this.getSelectedModel();
      const response = await fetch(`${this.apiUrl}/api/chat`, {
        method: "POST",
        headers: { ...this.getHeaders(), "Content-Type": "application/json" },
        signal: this.abortController.signal,
        body: JSON.stringify({
          message: text,
          sessionId: this.currentSessionId,
          projectId: this.projectId,
          configurationId: this.configurationId,
          provider: selectedProvider,
          modelName: selectedModel,
          attachedFileId,
          imageDataUrl,
          context: this.getPageContext(),
          sessionContext: this.getAttribute("session-context") || null
        }),
      });

      if (!response.ok) throw new Error(`API Error: ${response.status}`);

      const reader = response.body.getReader();
      const decoder = new TextDecoder();
      let fullText = "", hasStarted = false;

      bubble.innerHTML = '<div class="message-text-content"><div class="typing-indicator"><div class="typing-dot"></div><div class="typing-dot"></div><div class="typing-dot"></div></div></div><div class="message-widget-container"></div>';
      const textContent = bubble.querySelector(".message-text-content");
      const widgetContainer = bubble.querySelector(".message-widget-container");

      while (true) {
        const { done, value } = await reader.read();
        if (done) break;
        const chunk = decoder.decode(value);
        const lines = chunk.split("\n");

        for (const line of lines) {
          if (line.startsWith("data: ")) {
            try {
              const data = JSON.parse(line.substring(6));
              const toolCalls = data.ToolCalls || data.toolCalls;
              const toolCall = data.ToolCall || data.toolCall;
              const toolResult = data.ToolResult || data.toolResult;
              const sid = data.SessionId || data.sessionId || data.sid;
              const textChunk = data.Text || data.text;
              const errorChunk = data.Error || data.error;

              if (sid && this.currentSessionId !== sid) {
                this.currentSessionId = sid;
                localStorage.setItem(this.getSessionStorageKey(), sid);
              }

              const isDone = data.Done || data.done;
              const msgId = data.MessageId || data.messageId;
              if (isDone && msgId) {
                aiWrapper.dataset.messageId = msgId;
              }

              if (errorChunk) {
                textContent.innerHTML = `<span style="color:var(--danger-color)">${errorChunk}</span>`;
                hasStarted = true;
                continue;
              }

              if (toolCalls && toolCalls.length > 0) {
                hasStarted = true;
                this.handleToolCalls(toolCalls, bubble);
                continue;
              }

              if (toolCall) {
                hasStarted = true;
                this.handleToolCalls([toolCall], bubble);
                continue; 
              }

              if (toolResult) {
                const isDbTool = (toolResult.toolName === 'query_project_database' || toolResult.toolName === 'query_database' || toolResult.toolName === 'query_data');
                if (isDbTool) {
                  if (!hasStarted) { textContent.innerHTML = ""; hasStarted = true; }
                  this.renderDataResult(toolResult.result, widgetContainer);
                  continue;
                }
              }

              const ruleResponse = data.RuleResponse || data.ruleResponse;
              if (ruleResponse) {
                if (!hasStarted) { textContent.innerHTML = ""; hasStarted = true; }
                this.renderRichResponse(ruleResponse, bubble);
                this.scrollToBottom();
              }

              if (textChunk) {
                if (!hasStarted) { textContent.innerHTML = ""; hasStarted = true; }
                fullText += textChunk;
                textContent.innerHTML = this.formatMarkdown(fullText);
                this.scrollToBottom();
              }
            } catch(e) { console.error("Stream parse error:", e); }
          }
        }
      }
      
      if (!hasStarted) {
        bubble.innerHTML = '<span style="opacity:0.6; font-style:italic">No response from AI</span>';
      }
    } catch (err) {
      bubble.innerHTML = `<span style="color:var(--danger-color)">Error: ${err.message}</span>`;
    } finally {
      this.isTyping = false;
      this.updateInputButtons();
      this.startHandoffConnection();
    }
  }

  async sendUserActionMessage(displayText, apiValue) {
    if (this.isTyping) return;

    const emptyState = this.shadowRoot.querySelector(".chatbox-empty-state");
    if (emptyState) emptyState.remove();

    const input = this.shadowRoot.getElementById("chat-input");
    if (input) {
      input.value = "";
      input.style.height = "auto";
    }
    this.updateSendButtonState();

    this.addMessage("user", `<div class="message-text">${displayText}</div>`);

    if (this.chatService.userTypingTimeout) clearTimeout(this.chatService.userTypingTimeout);
    this.chatService.isUserTypingSignalSent = false;

    if (this.handoffStatus === "active" || this.handoffStatus === "queued") {
      const textToSend = apiValue || displayText;
      if (this.chatService.connection && this.chatService.connection.state === "Connected") {
        try {
          await this.chatService.connection.invoke("SendUserMessage", this.currentSessionId, textToSend, this.apiKey);
        } catch (err) {
          console.error("Failed to send action message via SignalR:", err);
          await this.sendHandoffMessageViaHttp(textToSend);
        }
      } else {
        await this.sendHandoffMessageViaHttp(textToSend);
      }
      return;
    }

    this.isTyping = true;
    this.updateInputButtons();

    const aiWrapper = this.addMessage("ai", '<div class="typing-indicator"><div class="typing-dot"></div><div class="typing-dot"></div><div class="typing-dot"></div></div>');
    const bubble = aiWrapper.querySelector(".message-bubble");

    try {
      this.abortController = new AbortController();
      const { modelName: selectedModel, provider: selectedProvider } = this.getSelectedModel();
      const response = await fetch(`${this.apiUrl}/api/chat`, {
        method: "POST",
        headers: { ...this.getHeaders(), "Content-Type": "application/json" },
        signal: this.abortController.signal,
        body: JSON.stringify({
          message: apiValue || displayText,
          sessionId: this.currentSessionId,
          projectId: this.projectId,
          configurationId: this.configurationId,
          provider: selectedProvider,
          modelName: selectedModel,
          attachedFileId: null,
          imageDataUrl: null,
          context: this.getPageContext(),
          sessionContext: this.getAttribute("session-context") || null
        }),
      });

      if (!response.ok) throw new Error(`API Error: ${response.status}`);

      const reader = response.body.getReader();
      const decoder = new TextDecoder();
      let fullText = "", hasStarted = false;

      bubble.innerHTML = '<div class="message-text-content"><div class="typing-indicator"><div class="typing-dot"></div><div class="typing-dot"></div><div class="typing-dot"></div></div></div><div class="message-widget-container"></div>';
      const textContent = bubble.querySelector(".message-text-content");
      const widgetContainer = bubble.querySelector(".message-widget-container");

      while (true) {
        const { done, value } = await reader.read();
        if (done) break;
        const chunk = decoder.decode(value);
        const lines = chunk.split("\n");

        for (const line of lines) {
          if (line.startsWith("data: ")) {
            try {
              const data = JSON.parse(line.substring(6));
              const toolCalls = data.ToolCalls || data.toolCalls;
              const toolCall = data.ToolCall || data.toolCall;
              const toolResult = data.ToolResult || data.toolResult;
              const sid = data.SessionId || data.sessionId || data.sid;
              const textChunk = data.Text || data.text;
              const errorChunk = data.Error || data.error;

              if (sid && this.currentSessionId !== sid) {
                this.currentSessionId = sid;
                localStorage.setItem(this.getSessionStorageKey(), sid);
              }

              const isDone = data.Done || data.done;
              const msgId = data.MessageId || data.messageId;
              if (isDone && msgId) {
                aiWrapper.dataset.messageId = msgId;
              }

              if (errorChunk) {
                textContent.innerHTML = `<span style="color:var(--danger-color)">${errorChunk}</span>`;
                hasStarted = true;
                continue;
              }

              if (toolCalls && toolCalls.length > 0) {
                hasStarted = true;
                this.handleToolCalls(toolCalls, bubble);
                continue;
              }

              if (toolCall) {
                hasStarted = true;
                this.handleToolCalls([toolCall], bubble);
                continue; 
              }

              if (toolResult) {
                const isDbTool = (toolResult.toolName === 'query_project_database' || toolResult.toolName === 'query_database' || toolResult.toolName === 'query_data');
                if (isDbTool) {
                  if (!hasStarted) { textContent.innerHTML = ""; hasStarted = true; }
                  this.renderDataResult(toolResult.result, widgetContainer);
                  continue;
                }
              }

              const ruleResponse = data.RuleResponse || data.ruleResponse;
              if (ruleResponse) {
                if (!hasStarted) { textContent.innerHTML = ""; hasStarted = true; }
                this.renderRichResponse(ruleResponse, bubble);
                this.scrollToBottom();
              }

              if (textChunk) {
                if (!hasStarted) { textContent.innerHTML = ""; hasStarted = true; }
                fullText += textChunk;
                textContent.innerHTML = this.formatMarkdown(fullText);
                this.scrollToBottom();
              }
            } catch(e) { console.error("Stream parse error:", e); }
          }
        }
      }
      
      if (!hasStarted) {
        bubble.innerHTML = '<span style="opacity:0.6; font-style:italic">No response from AI</span>';
      }
    } catch (err) {
      bubble.innerHTML = `<span style="color:var(--danger-color)">Error: ${err.message}</span>`;
    } finally {
      this.isTyping = false;
      this.updateInputButtons();
      this.startHandoffConnection();
    }
  }

  async handleToolCalls(toolCalls, bubble) {
    const results = await Promise.all(toolCalls.map(async (tc) => {
      const toolName = tc.name || tc.Name;
      const toolArgs = tc.arguments || tc.Arguments;
      const callId = tc.id || tc.Id || Math.random().toString(36).substring(7);
      const thoughtSignature = tc.thoughtSignature || tc.ThoughtSignature || tc.thought_signature;

      console.log('Executing tool:', toolName, toolArgs);
      
      const textContent = bubble.querySelector(".message-text-content");
      if (textContent) {
        textContent.innerHTML = `<div class="tool-calling-indicator">
          <span class="spin-animation">${icons.refresh}</span>
          <span>Executing ${toolCalls.length > 1 ? toolCalls.length + ' parallel tasks' : 'tool ' + toolName}...</span>
        </div>`;
      }

      let result = null;
      const handler = this.toolHandlers.get(toolName);
      
      if (handler) {
        try {
          const args = typeof toolArgs === 'string' ? JSON.parse(toolArgs) : toolArgs;
          result = await handler(args);
        } catch (err) {
          result = { error: `Handler error: ${err.message}` };
        }
      } else {
        const resultPromise = new Promise((resolve) => {
          const onResult = (e) => {
            if (e.detail.callId === callId) {
              this.removeEventListener('tool-result-submitted', onResult);
              resolve(e.detail.result);
            }
          };
          this.addEventListener('tool-result-submitted', onResult);
          setTimeout(() => {
            this.removeEventListener('tool-result-submitted', onResult);
            resolve({ error: "Tool execution timed out" });
          }, 30000);
        });

        this.dispatchEvent(new CustomEvent("tool-call", {
          detail: { 
            name: toolName, 
            args: typeof toolArgs === 'string' ? JSON.parse(toolArgs) : toolArgs,
            callId: callId
          },
          bubbles: true,
          composed: true
        }));

        result = await resultPromise;
      }

      return {
        toolCallId: callId,
        toolName: toolName,
        result: result,
        thoughtSignature: thoughtSignature
      };
    }));

    const { modelName: toolModel, provider: toolProvider } = this.getSelectedModel();
    const response = await fetch(`${this.apiUrl}/api/chat`, {
      method: "POST",
      headers: { ...this.getHeaders(), "Content-Type": "application/json" },
      body: JSON.stringify({
        message: "",
        sessionId: this.currentSessionId,
        provider: toolProvider,
        modelName: toolModel,
        toolResults: results,
        context: this.getPageContext(),
        sessionContext: this.getAttribute("session-context") || null
      }),
    });

    const reader = response.body.getReader();
    const decoder = new TextDecoder();
    let fullText = "", hasStarted = false;
    const textContent = bubble.querySelector(".message-text-content");
    const widgetContainer = bubble.querySelector(".message-widget-container");

    while (true) {
      const { done, value } = await reader.read();
      if (done) break;
      const chunk = decoder.decode(value);
      const lines = chunk.split("\n");
      for (const line of lines) {
        if (line.startsWith("data: ")) {
          try {
            const data = JSON.parse(line.substring(6));
            const nextToolCalls = data.ToolCalls || data.toolCalls;
            const nextToolCall = data.ToolCall || data.toolCall;
            const toolResult = data.ToolResult || data.toolResult;
            const textChunk = data.Text || data.text;

            if (nextToolCalls && nextToolCalls.length > 0) {
              return this.handleToolCalls(nextToolCalls, bubble);
            }
            if (nextToolCall) {
              return this.handleToolCalls([nextToolCall], bubble);
            }

            if (toolResult) {
              const isDbTool = (toolResult.toolName === 'query_project_database' || toolResult.toolName === 'query_database' || toolResult.toolName === 'query_data');
              if (isDbTool) {
                if (!hasStarted) { textContent.innerHTML = ""; hasStarted = true; }
                this.renderDataResult(toolResult.result, widgetContainer);
                continue;
              }
            }

            const ruleResponse = data.RuleResponse || data.ruleResponse;
            if (ruleResponse) {
              if (!hasStarted) { textContent.innerHTML = ""; hasStarted = true; }
              this.renderRichResponse(ruleResponse, bubble);
              this.scrollToBottom();
            }

            if (textChunk) {
              if (!hasStarted) { textContent.innerHTML = ""; hasStarted = true; }
              fullText += textChunk;
              textContent.innerHTML = this.formatMarkdown(fullText);
              this.scrollToBottom();
            }
          } catch(e) { console.error("Stream parse error:", e); }
        }
      }
    }
    this.isTyping = false;
    this.updateInputButtons();
    this.startHandoffConnection();
  }
}

customElements.define("ai-chatbox", AiChatBox);

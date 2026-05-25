import * as signalR from '@microsoft/signalr';

export class LiveVoiceService {
  constructor(chatbox) {
    this.chatbox = chatbox;
    this.connection = null;
    this.audioContext = null;
    this.playbackContext = null;
    this.playbackAnalyser = null;
    this.micAnalyser = null;
    this.micStream = null;
    this.nextPlayTime = 0;
    this.liveStartTime = null;
    this.liveTimerInterval = null;
    this.visibilityHandler = null;
  }

  async toggleLiveMode() {
    const cb = this.chatbox;
    if (cb.handoffStatus === "active" || cb.handoffStatus === "queued") {
      alert("Live voice is not available during an active support support session.");
      return;
    }
    if (cb.isLive) {
      this.stopLiveSession();
    } else {
      this.startLiveSession();
    }
  }

  async startLiveSession() {
    const cb = this.chatbox;
    cb.isLive = true;
    cb.shadowRoot.getElementById("live-overlay").classList.add("active");
    
    const transcriptArea = cb.shadowRoot.getElementById("live-transcript");
    if (transcriptArea) transcriptArea.innerHTML = "";
    cb.shadowRoot.getElementById("btn-live").classList.add("pulse-animation");
    cb.visualizer.init(cb.shadowRoot.getElementById("live-orb-canvas"));
    
    this.liveStartTime = Date.now();
    this.liveTimerInterval = setInterval(() => this.updateLiveTimer(), 1000);
    this.updateLiveStatus("connecting", "Connecting...");

    try {
      if (!this.audioContext) this.audioContext = new (window.AudioContext || window.webkitAudioContext)({ sampleRate: 16000 });
      if (this.audioContext.state === "suspended") await this.audioContext.resume();

      this.connection = new signalR.HubConnectionBuilder()
        .withUrl(`${cb.apiUrl}/liveAudioHub`, {
          accessTokenFactory: () => cb.authToken
        })
        .withAutomaticReconnect()
        .build();

      this.connection.on("ReceiveAudioChunk", data => this.playAudioChunk(data));
      this.connection.on("ReceiveTextChunk", (text, isThought) => {
        if (isThought) {
          this.updateThought(text);
        } else {
          this.addLiveMessage("ai", text);
        }
      });
      this.connection.on("ReceiveInputTranscription", text => this.addLiveMessage("user", text));
      this.connection.on("ReceiveToolCall", (id, name, args, isBackend) => cb.handleLiveToolCall(name, args, id, isBackend));
      this.connection.on("ReceiveToolResult", (id, name, result) => cb.handleLiveToolResult(id, name, result));
      this.connection.on("ReceiveError", (msg) => this.showLiveError(msg));
      this.connection.on("ReceiveDisconnected", (reason) => {
        this.updateLiveStatus("error", "Disconnected");
        this.showLiveError("Connection lost: " + reason);
      });
      
      await this.connection.start();
      const voice = cb.shadowRoot.getElementById("voice-select").value;
      
      // Use separate hub methods for widget (API key) vs dashboard (JWT) auth
      if (cb.apiKey) {
        await this.connection.invoke("StartLive", cb.userId, voice, cb.apiKey, cb.currentSessionId);
      } else if (cb.authToken) {
        await this.connection.invoke("StartLiveDashboard", cb.userId, voice, cb.projectId || "", cb.configurationId || "", cb.currentSessionId);
      } else {
        throw new Error("No API key or auth token configured");
      }

      const workletUrl = `${cb.apiUrl}/widget/audio-processor.js`;
      console.log("Loading audio worklet from:", workletUrl);
      await this.audioContext.audioWorklet.addModule(workletUrl);
      this.micStream = await navigator.mediaDevices.getUserMedia({ audio: true });
      const source = this.audioContext.createMediaStreamSource(this.micStream);
      
      this.micAnalyser = this.audioContext.createAnalyser();
      this.micAnalyser.fftSize = 512;
      source.connect(this.micAnalyser);

      const processor = new AudioWorkletNode(this.audioContext, "audio-processor");
      source.connect(processor);

      cb.visualizer.setAnalysers(this.micAnalyser, this.playbackAnalyser);
      cb.miniVisualizer.setAnalysers(this.micAnalyser, this.playbackAnalyser);

      processor.port.onmessage = (e) => {
        if (!cb.isLive || cb.isLiveMuted) return;
        const base64 = btoa(String.fromCharCode(...new Uint8Array(e.data.pcm16.buffer)));
        this.connection.invoke("SendAudio", base64).catch(console.error);
      };

      processor.connect(this.audioContext.destination);
      this.updateLiveStatus("listening", "Listening");
      cb.shadowRoot.getElementById("live-error-bar").style.display = "none";
      
      this.visibilityHandler = async () => {
        if (document.visibilityState === 'visible' && cb.isLive) {
          if (this.audioContext && this.audioContext.state === 'suspended') {
            await this.audioContext.resume();
          }
          if (this.playbackContext && this.playbackContext.state === 'suspended') {
            await this.playbackContext.resume();
          }
          this.updateLiveStatus("listening", "Listening");
          cb.shadowRoot.getElementById("live-error-bar").style.display = "none";
        } else if (document.visibilityState === 'hidden' && cb.isLive) {
          this.updateLiveStatus("warning", "Backgrounded");
          this.showLiveError("Tab is backgrounded. Audio may be delayed.");
        }
      };
      document.addEventListener('visibilitychange', this.visibilityHandler);
    } catch (err) {
      console.error("Live session startup failed:", err);
      this.updateLiveStatus("error", "Error Occurred");
      this.showLiveError(err.message || "Failed to connect");
    }
  }

  reconnectLiveSession() {
    this.stopLiveSession();
    setTimeout(() => this.startLiveSession(), 500);
  }

  showLiveError(msg) {
    const cb = this.chatbox;
    const bar = cb.shadowRoot.getElementById("live-error-bar");
    if (bar) bar.style.display = "flex";
    const txt = cb.shadowRoot.getElementById("live-error-text");
    if (txt) txt.textContent = msg;
  }

  stopLiveSession() {
    const cb = this.chatbox;
    cb.isLive = false;
    cb.shadowRoot.getElementById("live-overlay").classList.remove("active");
    cb.shadowRoot.getElementById("mini-live").classList.remove("active");
    cb.shadowRoot.getElementById("btn-live").classList.remove("pulse-animation");
    cb.shadowRoot.getElementById("fab-toggle").classList.remove("toggle-hidden");
    
    clearInterval(this.liveTimerInterval);
    cb.visualizer.destroy();
    cb.miniVisualizer.destroy();

    if (this.connection) this.connection.stop();
    if (this.micStream) this.micStream.getTracks().forEach(t => t.stop());
    if (this.audioContext) this.audioContext.close();
    if (this.playbackContext) {
      this.playbackContext.close();
      this.playbackContext = null;
    }
    this.playbackAnalyser = null;
    this.nextPlayTime = 0;
    this.audioContext = null;
    this.connection = null;

    if (this.visibilityHandler) {
      document.removeEventListener('visibilitychange', this.visibilityHandler);
      this.visibilityHandler = null;
    }
  }

  async playAudioChunk(data) {
    const cb = this.chatbox;
    if (!this.playbackContext) {
      this.playbackContext = new (window.AudioContext || window.webkitAudioContext)({ sampleRate: 24000 });
      this.playbackAnalyser = this.playbackContext.createAnalyser();
      this.playbackAnalyser.fftSize = 512;
      this.playbackAnalyser.connect(this.playbackContext.destination);
      this.nextPlayTime = 0;
      
      if (cb.visualizer) cb.visualizer.setAnalysers(this.micAnalyser, this.playbackAnalyser);
      if (cb.miniVisualizer) cb.miniVisualizer.setAnalysers(this.micAnalyser, this.playbackAnalyser);
    }
    const binary = atob(data);
    const pcm16 = new Int16Array(binary.length / 2);
    for (let i = 0; i < pcm16.length; i++) pcm16[i] = binary.charCodeAt(i*2) | (binary.charCodeAt(i*2+1) << 8);
    
    const float32 = new Float32Array(pcm16.length);
    for (let i = 0; i < pcm16.length; i++) float32[i] = pcm16[i] / 32768;

    const buffer = this.playbackContext.createBuffer(1, float32.length, 24000);
    buffer.copyToChannel(float32, 0);
    const source = this.playbackContext.createBufferSource();
    source.buffer = buffer;
    source.connect(this.playbackAnalyser);

    const now = this.playbackContext.currentTime;
    if (this.nextPlayTime < now) this.nextPlayTime = now + 0.05;
    
    source.start(this.nextPlayTime);
    this.nextPlayTime += buffer.duration;
    this.updateLiveStatus("speaking", "AI Speaking");
    
    source.onended = () => {
      if (this.playbackContext.currentTime >= this.nextPlayTime - 0.1) {
        this.updateLiveStatus("listening", "Listening");
        this.updateThought(null); 
      }
    };
  }

  updateThought(text) {
    const cb = this.chatbox;
    const bar = cb.shadowRoot.getElementById("live-thinking-bar");
    const el = cb.shadowRoot.getElementById("live-thought-text");
    if (!text) {
      if (bar) bar.style.display = "none";
    } else {
      if (bar) bar.style.display = "flex";
      if (el) el.textContent = text;
      this.updateLiveStatus("thinking", "Thinking");
    }
  }

  updateLiveTimer() {
    const cb = this.chatbox;
    const sec = Math.floor((Date.now() - this.liveStartTime) / 1000);
    const min = Math.floor(sec / 60);
    const time = `${min.toString().padStart(2,'0')}:${(sec%60).toString().padStart(2,'0')}`;
    cb.shadowRoot.getElementById("live-timer-text").textContent = time;
    cb.shadowRoot.getElementById("mini-timer-text").textContent = time;
  }

  updateLiveStatus(status, text) {
    const cb = this.chatbox;
    const badge = cb.shadowRoot.getElementById("live-badge");
    const miniBadge = cb.shadowRoot.getElementById("mini-status-dot");
    if (badge) badge.className = `live-status-badge badge-${status}`;
    if (miniBadge) miniBadge.className = `mini-status-dot badge-${status}`;
    cb.shadowRoot.getElementById("live-status-text").textContent = text;
    
    const thinkingBar = cb.shadowRoot.getElementById("live-thinking-bar");
    if (status === "thinking") {
      if (thinkingBar) thinkingBar.style.display = "flex";
    } else if (status !== "error") {
      if (thinkingBar) thinkingBar.style.display = "none";
    }

    cb.visualizer.setState(status);
    cb.miniVisualizer.setState(status);
  }

  addLiveMessage(role, text) {
    const cb = this.chatbox;
    const area = cb.shadowRoot.getElementById("live-transcript");
    const empty = area.querySelector(".live-transcript-empty");
    if (empty) empty.remove();

    const last = area.lastElementChild;
    const isUser = role === 'user';
    const roleClass = isUser ? 'live-msg-user' : 'live-msg-ai';
    const avatarIcon = isUser ? cb.icons.person : cb.icons.awesome;

    if (last && last.classList.contains(roleClass)) {
      const textSpan = last.querySelector(".live-msg-text");
      const currentText = (textSpan.dataset.raw || textSpan.textContent) + text;
      textSpan.dataset.raw = currentText;
      textSpan.innerHTML = cb.formatMarkdown(currentText);
    } else {
      const div = document.createElement("div");
      div.className = `live-transcript-msg ${roleClass} message-appear`;
      div.innerHTML = `
        <div class="live-msg-avatar">${avatarIcon}</div>
        <div class="live-msg-bubble">
          <span class="live-msg-text" data-raw="${text}">${cb.formatMarkdown(text)}</span>
          <span class="live-msg-time">${new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}</span>
        </div>
      `;
      area.appendChild(div);
    }
    area.scrollTop = area.scrollHeight;
  }

  toggleLiveMute() {
    const cb = this.chatbox;
    cb.isLiveMuted = !cb.isLiveMuted;
    const btn = cb.shadowRoot.getElementById("live-mute-btn");
    const miniBtn = cb.shadowRoot.getElementById("mini-mute-btn");
    const text = cb.shadowRoot.getElementById("live-mute-text");
    
    if (cb.isLiveMuted) {
      btn.classList.add("muted");
      miniBtn.classList.add("muted");
      if (text) text.textContent = "Unmute";
    } else {
      btn.classList.remove("muted");
      miniBtn.classList.remove("muted");
      if (text) text.textContent = "Mute";
    }
  }

  sendLiveTextMessage() {
    const cb = this.chatbox;
    const input = cb.shadowRoot.getElementById("live-text-input");
    const text = input.value.trim();
    if (text && this.connection) {
      this.connection.invoke("SendText", text);
      this.addLiveMessage("user", text);
      input.value = "";
      input.style.height = "auto";
    }
  }
}

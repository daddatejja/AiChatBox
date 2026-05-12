(function () {
  class LiveVisualizer {
    constructor() {
      this.canvas = null;
      this.ctx = null;
      this.animFrame = null;
      this.isRunning = false;
      this.state = "idle";
      this.BUF = 256;
      this.dataArr = new Uint8Array(this.BUF);
      this.smoothBands = new Float32Array(8).fill(0);
      this._bands = new Float32Array(8).fill(0);
      this.smoothVol = 0;
      this.orbRadius = 0;
      this.phase = 0;
      this._ringVol = new Float32Array(4);
      this._stops = [
        [22, 138, 173],
        [26, 117, 159],
        [30, 96, 145],
        [118, 200, 147],
        [82, 182, 154],
        [52, 160, 164]
      ];
      this._resizeHandler = () => this.resize();
    }

    init(canvasEl) {
      if (!canvasEl) return;
      this.canvas = canvasEl;
      this.ctx = this.canvas.getContext("2d");
      this.isRunning = true;
      this.resize();
      window.addEventListener("resize", this._resizeHandler);
      this.tick();
    }

    resize() {
      if (!this.canvas) return;
      const dpr = window.devicePixelRatio || 1;
      const rect = this.canvas.getBoundingClientRect();
      this.canvas.width = rect.width * dpr;
      this.canvas.height = rect.height * dpr;
      this.ctx.scale(dpr, dpr);
      this.W = rect.width;
      this.H = rect.height;
    }

    setState(s) {
      this.state = s;
    }

    setAnalysers(mic, play) {
      this.micAnalyser = mic;
      this.playAnalyser = play;
    }

    destroy() {
      this.isRunning = false;
      if (this.animFrame) cancelAnimationFrame(this.animFrame);
      window.removeEventListener("resize", this._resizeHandler);
      this.canvas = null;
      this.ctx = null;
    }

    updateFreqBands() {
      const step = this.BUF >> 3;
      const d = this.dataArr,
        b = this._bands;
      const inv = 1 / (step * 255);
      for (let i = 0; i < 8; i++) {
        let sum = 0,
          base = i * step;
        for (let j = 0; j < step; j++) sum += d[base + j];
        b[i] = sum * inv;
      }
    }

    getColor(i, total, alpha) {
      if (this.state === "error") return `rgba(239,68,68,${alpha})`;
      if (this.state === "thinking") return `rgba(245,158,11,${alpha})`;
      const stops = this._stops;
      const t = (i / total) * (stops.length - 1);
      const lo = t | 0;
      const hi = lo + 1 < stops.length ? lo + 1 : lo;
      const f = t - lo;
      const slo = stops[lo],
        shi = stops[hi];
      const r = slo[0] + (shi[0] - slo[0]) * f;
      const g = slo[1] + (shi[1] - slo[1]) * f;
      const b = slo[2] + (shi[2] - slo[2]) * f;
      return `rgba(${(r + 0.5) | 0},${(g + 0.5) | 0},${(b + 0.5) | 0},${alpha})`;
    }

    tick() {
      if (!this.isRunning || !this.ctx) return;

      if (this.playAnalyser) {
        this.playAnalyser.getByteFrequencyData(this.dataArr);
      } else if (this.micAnalyser) {
        this.micAnalyser.getByteFrequencyData(this.dataArr);
      }

      this.updateFreqBands();
      const sb = this.smoothBands,
        rb = this._bands;
      for (let i = 0; i < 8; i++) sb[i] = sb[i] + (rb[i] - sb[i]) * 0.12;

      let vol = (sb[0] + sb[1] + sb[2] + sb[3]) * 0.25;
      
      // Breathing effect when silent
      if (vol < 0.02) {
        vol = 0.02 + Math.sin(Date.now() * 0.002) * 0.015;
      }
      this.smoothVol = this.smoothVol + (vol - this.smoothVol) * 0.1;

      const W = this.W,
        H = this.H;
      const cx = W * 0.5,
        cy = H * 0.5;
      const minDim = W < H ? W : H;

      const targetR = minDim * 0.22 + this.smoothVol * minDim * 0.4;
      this.orbRadius = this.orbRadius + (targetR - this.orbRadius) * 0.08;

      this.ctx.clearRect(0, 0, W, H);

      const sv = this.smoothVol,
        rv = this._ringVol;
      for (let i = 0; i < 4; i++) rv[i] = sb[i] * 0.5 + sv * 0.5;

      const orbR = this.orbRadius,
        phase = this.phase;
      const noiseA = minDim * 0.048,
        noiseB = minDim * 0.03;
      const N = 200,
        TAU = Math.PI * 2,
        step = TAU / N;

      for (let ring = 3; ring >= 0; ring--) {
        const ringVol = rv[ring];
        const baseR = orbR * (0.4 + ring * 0.2);
        const pA = phase * (1 + ring * 0.3),
          pB = phase * 0.7;
        const freqA = 3 + ring,
          freqB = 5 - ring;
        const nA = ringVol * noiseA,
          nB = ringVol * noiseB;

        this.ctx.beginPath();
        for (let i = 0; i <= N; i++) {
          const a = i * step;
          const r =
            baseR +
            Math.sin(a * freqA + pA) * nA +
            Math.cos(a * freqB + pB) * nB;
          const x = cx + Math.cos(a) * r;
          const y = cy + Math.sin(a) * r;
          i === 0 ? this.ctx.moveTo(x, y) : this.ctx.lineTo(x, y);
        }
        this.ctx.closePath();
        this.ctx.fillStyle = this.getColor(ring, 4, 0.08 + ring * 0.04);
        this.ctx.fill();
        this.ctx.strokeStyle = this.getColor(ring, 4, 0.5 + sb[ring] * 0.4);
        this.ctx.lineWidth = 1.2 - ring * 0.2;
        this.ctx.stroke();
      }

      const glowR = orbR * 0.6;
      const grd = this.ctx.createRadialGradient(cx, cy, 0, cx, cy, glowR);
      grd.addColorStop(0, `rgba(255,255,255,${0.04 + sv * 0.08})`);
      grd.addColorStop(1, "rgba(255,255,255,0)");
      this.ctx.beginPath();
      this.ctx.arc(cx, cy, glowR, 0, TAU);
      this.ctx.fillStyle = grd;
      this.ctx.fill();

      this.phase += 0.008;
      this.animFrame = requestAnimationFrame(() => this.tick());
    }
  }

  // ---- Main Web Component ----
  class AiChatBox extends HTMLElement {
    constructor() {
      super();
      this.attachShadow({ mode: "open" });

      // State
      this.isOpen = false;
      this.isFullscreen = false;
      this.isHistoryOpen = false;
      this.isLive = false;
      this.isLiveMuted = false;
      this.isRecording = false;
      this.isTyping = false;
      this.sessions = [];
      this.attachments = [];
      this.pastedImage = null;
      this.currentSessionId = localStorage.getItem("ai_chat_session_id") || null;
      this.liveTimerInterval = null;
      this.liveStartTime = null;

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

      // Tools
      this.toolHandlers = new Map();

      // Audio & Live
      this.visualizer = new LiveVisualizer();
      this.miniVisualizer = new LiveVisualizer();
      this.audioContext = null;
      this.liveConnection = null;
      this.playbackContext = null;
      this.nextPlayTime = 0;

      // SVG Icons
      this.icons = {
        awesome: '<svg width="24" height="24" viewBox="0 0 24 24" fill="currentColor"><path d="M19 9l1.25-2.75L23 5l-2.75-1.25L19 1l-1.25 2.75L15 5l2.75 1.25L19 9zm-7.5.5L9 4 6.5 9.5 1 12l5.5 2.5L9 20l2.5-5.5L17 12l-5.5-2.5zM19 15l-1.25 2.75L15 19l2.75 1.25L19 23l1.25-2.75L23 19l-2.75-1.25L19 15z"/></svg>',
        voice: '<svg width="24" height="24" viewBox="0 0 24 24" fill="currentColor"><path d="M9,5A4,4 0 0,1 13,9A4,4 0 0,1 9,13A4,4 0 0,1 5,9A4,4 0 0,1 9,5M9,15C11.67,15 17,16.34 17,19V21H1V19C1,16.34 6.33,15 9,15M16.76,5.36C18.78,7.56 18.78,10.61 16.76,12.63L15.08,10.94C15.92,9.76 15.92,8.23 15.08,7.05L16.76,5.36M20.07,2C24,6.05 23.97,12.11 20.07,16L18.44,14.37C21.21,11.19 21.21,6.65 18.44,3.63L20.07,2Z" /></svg>',
        history: '<svg width="20" height="20" viewBox="0 0 24 24" fill="currentColor"><path d="M13 3c-4.97 0-9 4.03-9 9H1l3.89 3.89.07.14L9 12H6c0-3.87 3.13-7 7-7s7 3.13 7 7-3.13 7-7 7c-1.93 0-3.68-.79-4.94-2.06l-1.42 1.42C8.27 19.99 10.51 21 13 21c4.97 0 9-4.03 9-9s-4.03-9-9-9zm-1 5v5l4.28 2.54.72-1.21-3.5-2.08V8H12z"/></svg>',
        newChat: '<svg width="20" height="20" viewBox="0 0 24 24" fill="currentColor"><path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2zM12 14v-3h3V9h-3V6H9v3H6v2h3v3h3z"/></svg>',
        fullscreen: '<svg width="20" height="20" viewBox="0 0 24 24" fill="currentColor"><path d="M7 14H5v5h5v-2H7v-3zm-2-4h2V7h3V5H5v5zm12 7h-3v2h5v-5h-2v3zM14 5v2h3v3h2V5h-5z"/></svg>',
        close: '<svg width="20" height="20" viewBox="0 0 24 24" fill="currentColor"><path d="M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z"/></svg>',
        send: '<svg width="20" height="20" viewBox="0 0 24 24" fill="currentColor"><path d="M2.01 21L23 12 2.01 3 2 10l15 2-15 2z"/></svg>',
        mic: '<svg width="20" height="20" viewBox="0 0 24 24" fill="currentColor"><path d="M12 14c1.66 0 2.99-1.34 2.99-3L15 5c0-1.66-1.34-3-3-3S9 3.34 9 5v6c0 1.66 1.34 3 3 3zm5.3-3c0 3-2.54 5.1-5.3 5.1S6.7 14 6.7 11H5c0 3.41 2.72 6.23 6 6.72V21h2v-3.28c3.28-.48 6-3.3 6-6.72h-1.7z"/></svg>',
        micOff: '<svg width="20" height="20" viewBox="0 0 24 24" fill="currentColor"><path d="M19 11h-1.7c0 .74-.16 1.43-.43 2.05l1.23 1.23c.56-.98.9-2.09.9-3.28zm-4.02.17l1.42 1.42C15.89 12.18 16 11.6 16 11V5c0-2.21-1.79-4-4-4S8 2.79 8 5v.28l7 7v-.11zm-10.71-9.3l-1.27 1.27 3.89 3.89C6.35 7.61 6 8.76 6 10v1c0 3.31 2.69 6 6 6 .91 0 1.76-.2 2.53-.55L18.73 21l1.27-1.27L4.27 1.87zM11 19.98V22h2v-2.02c3.07-.35 5.5-2.92 5.5-6.03h-2c0 2.48-2.02 4.5-4.5 4.5s-4.5-2.02-4.5-4.5H6c0 3.11 2.43 5.68 5.5 6.03z"/></svg>',
        attach: '<svg width="20" height="20" viewBox="0 0 24 24" fill="currentColor"><path d="M16.5 6v11.5c0 2.21-1.79 4-4 4s-4-1.79-4-4V5a2.5 2.5 0 0 1 5 0v10.5c0 .55-.45 1-1 1s-1-.45-1-1V6H10v9.5a2.5 2.5 0 0 0 5 0V5c0-2.21-1.79-4-4-4S7 2.79 7 5v12.5c0 3.04 2.46 5.5 5.5 5.5s5.5-2.46 5.5-5.5V6h-1.5z"/></svg>',
        stop: '<svg width="20" height="20" viewBox="0 0 24 24" fill="currentColor"><path d="M6 6h12v12H6z"/></svg>',
        user: '<svg width="18" height="18" viewBox="0 0 24 24" fill="currentColor"><path d="M12 12c2.21 0 4-1.79 4-4s-1.79-4-4-4-4 1.79-4 4 1.79 4 4 4zm0 2c-2.67 0-8 1.34-8 4v2h16v-2c0-2.66-5.33-4-8-4z"/></svg>',
        timer: '<svg width="16" height="16" viewBox="0 0 24 24" fill="currentColor"><path d="M11.99 2C6.47 2 2 6.48 2 12s4.47 10 9.99 10C17.52 22 22 17.52 22 12S17.52 2 11.99 2zM12 20c-4.42 0-8-3.58-8-8s3.58-8 8-8 8 3.58 8 8-3.58 8-8 8zm.5-13H11v6l5.25 3.15.75-1.23-4.5-2.67z"/></svg>',
        drag: '<svg width="16" height="16" viewBox="0 0 24 24" fill="currentColor"><path d="M11 18c0 1.1-.9 2-2 2s-2-.9-2-2 .9-2 2-2 2 .9 2 2zm-2-8c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2zm0-6c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2zm6 4c1.1 0 2-.9 2-2s-.9-2-2-2-2 .9-2 2 .9 2 2 2zm0 2c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2zm0 6c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2z"/></svg>',
        callEnd: '<svg width="20" height="20" viewBox="0 0 24 24" fill="currentColor"><path d="M12 9c-1.6 0-3.15.25-4.6.72v3.1c0 .39-.23.74-.56.9-.98.49-1.87 1.12-2.66 1.85-.18.18-.43.28-.7.28-.28 0-.53-.11-.71-.29L.29 13.08c-.18-.17-.29-.42-.29-.7 0-.28.11-.53.29-.71C3.34 8.78 7.46 7 12 7s8.66 1.78 11.71 4.67c.18.18.29.43.29.71 0 .28-.11.53-.29.71l-2.48 2.48c-.18.18-.43.29-.71.29-.27 0-.52-.11-.7-.28-.79-.74-1.69-1.36-2.67-1.85-.33-.16-.56-.5-.56-.9v-3.1C15.15 9.25 13.6 9 12 9z"/></svg>',
        copy: '<svg width="18" height="18" viewBox="0 0 24 24" fill="currentColor"><path d="M16 1H4c-1.1 0-2 .9-2 2v14h2V3h12V1zm3 4H8c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h11c1.1 0 2-.9 2-2V7c0-1.1-.9-2-2-2zm0 16H8V7h11v14z"/></svg>',
        check: '<svg width="18" height="18" viewBox="0 0 24 24" fill="currentColor"><path d="M9 16.17L4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41z"/></svg>',
        refresh: '<svg width="18" height="18" viewBox="0 0 24 24" fill="currentColor"><path d="M17.65 6.35C16.2 4.9 14.21 4 12 4c-4.42 0-7.99 3.58-7.99 8s3.57 8 7.99 8c3.73 0 6.84-2.55 7.73-6h-2.08c-.82 2.33-3.04 4-5.65 4-3.31 0-6-2.69-6-6s2.69-6 6-6c1.66 0 3.14.69 4.22 1.78L13 11h7V4l-2.35 2.35z"/></svg>',
        loading: '<svg width="18" height="18" viewBox="0 0 24 24" fill="currentColor" class="spin-animation"><path d="M12 4V2C6.48 2 2 6.48 2 12h2c0-4.41 3.59-8 8-8zm0 16v2c5.52 0 10-4.48 10-10h-2c0 4.41-3.59 8-8 8zm8-8h2c0-5.52-4.48-10-10-10v2c4.41 0 8 3.59 8 8z"/></svg>',
        lightbulb: '<svg width="18" height="18" viewBox="0 0 24 24" fill="currentColor"><path d="M9 21c0 .55.45 1 1 1h4c.55 0 1-.45 1-1v-1H9v1zm3-19C8.14 2 5 5.14 5 9c0 2.38 1.19 4.47 3 5.74V17c0 .55.45 1 1 1h6c.55 0 1-.45 1-1v-2.26c1.81-1.27 3-3.36 3-5.74 0-3.86-3.14-7-7-7zm2.85 11.1l-.85.6V16h-4v-2.3l-.85-.6A4.997 4.997 0 0 1 7 9c0-2.76 2.24-5 5-5s5 2.24 5 5c0 1.63-.8 3.06-2.15 3.9z"/></svg>',
        error: '<svg width="18" height="18" viewBox="0 0 24 24" fill="currentColor"><path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-2h2v2zm0-4h-2V7h2v6z"/></svg>',
        person: '<svg width="18" height="18" viewBox="0 0 24 24" fill="currentColor"><path d="M12 12c2.21 0 4-1.79 4-4s-1.79-4-4-4-4 1.79-4 4 1.79 4 4 4zm0 2c-2.67 0-8 1.34-8 4v2h16v-2c0-2.66-5.33-4-8-4z"/></svg>',
        minimize: '<svg width="20" height="20" viewBox="0 0 24 24" fill="currentColor"><path d="M19 13H5v-2h14v2z"/></svg>',
      };

      this.config = null;

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

    getHeaders() {
      const headers = { "X-User-Id": this.userId };
      if (this.apiKey) headers["X-Api-Key"] = this.apiKey;
      if (this.authToken) headers["Authorization"] = `Bearer ${this.authToken}`;
      return headers;
    }

    getSelectedModel() {
      const select = this.shadowRoot.getElementById("model-select");
      const option = select?.selectedOptions?.[0];
      return {
        modelName: select?.value || this.modelName,
        provider: option?.dataset?.provider || this.provider
      };
    }

    async safeJson(response) {
      const contentType = response.headers.get("content-type");
      if (contentType && contentType.includes("application/json")) {
        return await response.json();
      }
      const text = await response.text();
      throw new Error(`Expected JSON but got ${contentType || 'unknown'}. Content: ${text.substring(0, 100)}...`);
    }

    async fetchConfig() {
      if (!this.apiKey) return;
      try {
        const response = await fetch(`${this.apiUrl}/api/chat/config`, {
          headers: this.getHeaders()
        });
        if (response.ok) {
          this.config = await this.safeJson(response);
          if (this.config.defaultModel) this.modelName = this.config.defaultModel;
          if (this.config.defaultProvider) this.provider = this.config.defaultProvider;
          if (this.config.suggestionsJson) {
            try { this.suggestions = JSON.parse(this.config.suggestionsJson); } catch(e) {}
          }
        }
      } catch (err) {
        console.error("Failed to fetch widget config:", err);
      }
    }

    async connectedCallback() {
      // Initialize attributes in connectedCallback as they may not be ready in constructor
      this.apiUrl = this.getAttribute("api-base") || this.getAttribute("api-url") || window.location.origin;
      this.apiKey = this.getAttribute("api-key") || null;
      this.authToken = this.getAttribute("auth-token") || null;
      this.projectId = this.getAttribute("project-id") || null;
      this.configurationId = this.getAttribute("configuration-id") || null;
      this.userId = this.getAttribute("user-id") || "standalone-user";
      this.provider = this.getAttribute("provider") || "gemini";
      this.modelName = this.getAttribute("model") || "gemini-3.1-flash-lite-preview";
      this.suggestions = JSON.parse(
        this.getAttribute("suggestions") ||
          '["Record a new entry", "Show my last session", "What can you do?", "Help with my budget"]',
      );

      await this.fetchConfig();
      this.render();
      this.setupEventListeners();
      this.setupDraggable();
      if (this.currentSessionId) this.loadSessionMessages(this.currentSessionId);
      this.loadSessions();
    }

    render() {
      const stylePath = this.getAttribute("css-path") || `${this.apiUrl}/widget/ai-chatbox.css`;
      this.shadowRoot.innerHTML = `
                <link rel="stylesheet" href="${stylePath}">
                
                <button class="chatbox-toggle-btn" id="fab-toggle" title="Open AI Assistant">
                    ${this.icons.awesome}
                    <span class="toggle-pulse"></span>
                </button>

                <div class="chatbox-container" id="main-container">
                    <div class="chatbox-header" id="drag-header">
                        <div class="chatbox-title">
                            ${this.icons.awesome}
                            <span>${this.getAttribute("title") || this.config?.projectName || "AI Assistant"}</span>
                        </div>
                        <div class="chatbox-header-actions">
                            ${this.config?.liveVoiceEnabled !== false ? `<button class="header-action-btn" id="btn-live" title="Live Voice Mode">${this.icons.voice}</button>` : ''}
                            <button class="header-action-btn" id="btn-history" title="Chat history">${this.icons.history}</button>
                            <button class="header-action-btn" id="btn-new" title="New chat">${this.icons.newChat}</button>
                            <button class="header-action-btn" id="btn-full" title="Fullscreen">${this.icons.fullscreen}</button>
                            <button class="header-action-btn" id="btn-minimize" title="Minimize">${this.icons.minimize}</button>
                            <button class="header-action-btn" id="btn-close" title="Close">${this.icons.close}</button>
                        </div>
                    </div>

                    <div class="chatbox-live-view" id="live-overlay">
                        <div class="live-status-bar">
                            <div class="live-status-left">
                                <div class="live-status-badge badge-connecting" id="live-badge">
                                    <span class="live-status-dot"></span>
                                    <span id="live-status-text">Connecting...</span>
                                </div>
                            </div>
                            <div class="live-timer">
                                ${this.icons.timer}
                                <span id="live-timer-text">00:00</span>
                            </div>
                        </div>
                        
                        <div class="live-orb-section">
                            <canvas id="live-orb-canvas"></canvas>
                        </div>

                        <div class="live-thinking-bar" id="live-thinking-bar" style="display:none">
                            <div class="thinking-icon-anim">${this.icons.lightbulb}</div>
                            <span class="thinking-text" id="live-thought-text">Thinking...</span>
                        </div>

                        <div class="live-error-bar" id="live-error-bar" style="display:none">
                            ${this.icons.error}
                            <span id="live-error-text" style="flex:1">An error occurred</span>
                            <button class="live-reconnect-btn" id="live-reconnect-btn">Reconnect</button>
                        </div>

                        <div class="live-transcript-area" id="live-transcript">
                            <div class="live-transcript-empty">
                                ${this.icons.voice}
                                <p>Speak or type to start the conversation</p>
                            </div>
                        </div>

                        <div class="live-controls-bar glass-controls">
                            <div class="live-text-input-row">
                                <textarea class="live-text-field" id="live-text-input" placeholder="Type a message..." rows="1"></textarea>
                                <button class="modern-action-btn live-send-btn" id="live-send-btn">${this.icons.send}</button>
                            </div>
                            <div class="live-action-buttons">
                                <button class="live-ctrl-btn live-pill-btn live-mute-btn" id="live-mute-btn">
                                    ${this.icons.mic}
                                    <span id="live-mute-text">Mute</span>
                                </button>
                                <button class="live-ctrl-btn live-pill-btn live-end-btn" id="live-end-btn">
                                    ${this.icons.callEnd}
                                    <span>End Session</span>
                                </button>
                            </div>
                        </div>
                    </div>

                    <div class="chatbox-history-drawer" id="history-drawer">
                        <div class="history-header">
                            <h3 id="history-header-title">Chat History</h3>
                            <button class="header-action-btn" id="btn-history-close" style="color:var(--secondary-text)">${this.icons.close}</button>
                        </div>
                        <div class="history-tabs">
                            <button class="history-tab history-tab-active" id="tab-chats">Chats</button>
                            <button class="history-tab" id="tab-archived">Archived</button>
                        </div>
                        <div class="history-list" id="history-list">
                            <!-- Sessions will be loaded here -->
                        </div>
                    </div>

                    <div class="chatbox-messages" id="messages-container">
                        <!-- Messages or Empty State will be loaded here -->
                    </div>

                    <button class="chatbox-scroll-down-btn" id="scroll-down-btn" title="Scroll to bottom" style="display:none">
                        <svg width="20" height="20" viewBox="0 0 24 24" fill="currentColor"><path d="M7.41 8.59L12 13.17l4.59-4.58L18 10l-6 6-6-6 1.41-1.41z"/></svg>
                    </button>

                    <div class="chatbox-input-area">
                        <div class="modern-input-wrapper">
                            <div class="attachments-row" id="attachments-container" style="display:none"></div>
                            
                            <div class="input-row">
                                <button class="modern-action-btn" id="btn-attach" title="Attach file">${this.icons.attach}</button>
                                <input type="file" id="file-input" style="display:none" multiple>
                                
                                <textarea class="modern-chat-input" id="chat-input" placeholder="Message AI Assistant..." rows="1"></textarea>
                                
                                <button class="modern-send-btn" id="btn-mic" title="Hold to talk">${this.icons.mic}</button>
                                <button class="modern-send-btn" id="btn-send" title="Send message" disabled>${this.icons.send}</button>
                                <button class="modern-send-btn stop-btn" id="btn-stop" style="display:none" title="Stop generation">${this.icons.stop}</button>
                            </div>

                            <div class="input-footer">
                                <div class="model-selector-wrapper">
                                    <select class="modern-model-select" id="model-select">
                                        ${(this.config?.enabledModels?.length > 0 ? this.config.enabledModels : [{model: "gemini-3.1-flash-lite-preview", provider: "gemini"}, {model: "gemini-3-flash", provider: "gemini"}, {model: "gemini-2.5-flash-lite", provider: "gemini"}]).map(item => {
                                            const name = typeof item === 'string' ? item : item.model;
                                            const prov = typeof item === 'string' ? this.provider : item.provider;
                                            return `<option value="${name}" data-provider="${prov}" ${name === this.modelName ? 'selected' : ''}>${name.split('-').map(w => w.charAt(0).toUpperCase() + w.slice(1)).join(' ')}</option>`;
                                        }).join('')}
                                    </select>
                                    <select class="modern-model-select" id="voice-select">
                                        <option value="Puck">Puck</option>
                                        <option value="Charon">Charon</option>
                                        <option value="Kore">Kore</option>
                                        <option value="Fenrir">Fenrir</option>
                                    </select>
                                </div>
                            </div>
                        </div>
                        <div class="chatbox-resize-handle" id="chat-resize-handle"></div>
                    </div>
                </div>

                <div class="chatbox-minimized-live" id="mini-live">
                    <div class="mini-drag-handle" title="Drag to move" id="pill-drag">${this.icons.drag}</div>
                    <div class="mini-orb-container" id="mini-orb-expand" title="Expand Assistant">
                        <canvas id="live-orb-canvas-mini" class="mini-orb-canvas"></canvas>
                        <div class="mini-status-dot badge-connecting" id="mini-status-dot"></div>
                    </div>
                    <div class="mini-controls">
                        <span class="mini-timer" id="mini-timer-text">00:00</span>
                        <div class="mini-actions">
                            <button class="mini-action-btn" id="mini-mute-btn" title="Mute">${this.icons.mic}</button>
                            <button class="mini-action-btn" id="mini-end-btn" title="End Session" style="color:var(--danger-color)">${this.icons.callEnd}</button>
                        </div>
                    </div>
                </div>
            `;
    }

    setupEventListeners() {
      const root = this.shadowRoot;

      // Toggle Chatbox
      root.getElementById("fab-toggle").onclick = () => this.toggleChat();
      root.getElementById("btn-close").onclick = () => this.toggleChat();

      // Header Actions
      root.getElementById("btn-full").onclick = () => this.toggleFullscreen();
      root.getElementById("btn-minimize").onclick = () => this.toggleChat();
      root.getElementById("btn-history").onclick = () => this.toggleHistory();
      root.getElementById("btn-history-close").onclick = () => this.toggleHistory();
      root.getElementById("btn-new").onclick = () => this.startNewChat();
      const liveBtn = root.getElementById("btn-live");
      if (liveBtn) liveBtn.onclick = () => this.toggleLiveMode();

      // Input Actions
      root.getElementById("btn-send").onclick = () => this.sendMessage();
      root.getElementById("btn-stop").onclick = () => this.stopGeneration();
      
      const chatInput = root.getElementById("chat-input");
      chatInput.onkeydown = (e) => {
        if (e.key === "Enter" && !e.shiftKey) {
          e.preventDefault();
          this.sendMessage();
        }
      };
      chatInput.oninput = (e) => {
        this.adjustTextAreaHeight(e.target);
        this.updateSendButtonState();
      };

      // Attachment Actions
      root.getElementById("btn-attach").onclick = () => root.getElementById("file-input").click();
      root.getElementById("file-input").onchange = (e) => this.handleFileSelection(e);

      // Mic Button (Hold to talk)
      const micBtn = root.getElementById("btn-mic");
      micBtn.onmousedown = () => this.startVoiceRecording();
      micBtn.onmouseup = () => this.stopVoiceRecording();
      micBtn.onmouseleave = () => this.cancelVoiceRecording();

      // Live View Actions
      root.getElementById("live-send-btn").onclick = () => this.sendLiveTextMessage();
      const liveTextInput = root.getElementById("live-text-input");
      liveTextInput.onkeydown = (e) => {
        if (e.key === "Enter" && !e.shiftKey) {
          e.preventDefault();
          this.sendLiveTextMessage();
        }
      };
      liveTextInput.oninput = (e) => this.adjustTextAreaHeight(e.target);

      root.getElementById("live-mute-btn").onclick = () => this.toggleLiveMute();
      root.getElementById("live-end-btn").onclick = () => this.stopLiveSession();
      root.getElementById("live-reconnect-btn").onclick = () => this.reconnectLiveSession();

      // Minimized Live Actions
      root.getElementById("mini-orb-expand").onclick = () => this.toggleChat();
      root.getElementById("mini-mute-btn").onclick = () => this.toggleLiveMute();
      root.getElementById("mini-end-btn").onclick = () => this.stopLiveSession();

      // History Tabs
      root.getElementById("tab-chats").onclick = () => this.switchHistoryTab("chats");
      root.getElementById("tab-archived").onclick = () => this.switchHistoryTab("archived");

      // Scroll Down Button
      const messagesContainer = root.getElementById("messages-container");
      messagesContainer.onscroll = () => this.handleMessagesScroll();
      root.getElementById("scroll-down-btn").onclick = () => this.scrollToBottom();
    }



    setupDraggable() {
      const root = this.shadowRoot;
      const container = root.getElementById("main-container");
      const header = root.getElementById("drag-header");
      const miniPill = root.getElementById("mini-live");
      const miniHandle = root.getElementById("pill-drag");

      let isDragging = false, startX, startY, initTop, initLeft;

      header.onmousedown = (e) => {
        if (e.target.closest("button") || this.isFullscreen) return;
        isDragging = true;
        const rect = container.getBoundingClientRect();
        container.style.bottom = "auto";
        container.style.right = "auto";
        container.style.top = rect.top + "px";
        container.style.left = rect.left + "px";
        startX = e.clientX;
        startY = e.clientY;
        initTop = rect.top;
        initLeft = rect.left;
        e.preventDefault();
      };

      miniHandle.onmousedown = (e) => {
        isDragging = "mini";
        const rect = miniPill.getBoundingClientRect();
        miniPill.style.bottom = "auto";
        miniPill.style.right = "auto";
        miniPill.style.top = rect.top + "px";
        miniPill.style.left = rect.left + "px";
        startX = e.clientX;
        startY = e.clientY;
        initTop = rect.top;
        initLeft = rect.left;
        e.preventDefault();
      };

      document.addEventListener("mousemove", (e) => {
        if (!isDragging) return;
        const dx = e.clientX - startX;
        const dy = e.clientY - startY;

        if (isDragging === true) {
          container.style.left = Math.max(0, Math.min(initLeft + dx, window.innerWidth - container.offsetWidth)) + "px";
          container.style.top = Math.max(0, Math.min(initTop + dy, window.innerHeight - container.offsetHeight)) + "px";
        } else if (isDragging === "mini") {
          miniPill.style.left = Math.max(0, Math.min(initLeft + dx, window.innerWidth - miniPill.offsetWidth)) + "px";
          miniPill.style.top = Math.max(0, Math.min(initTop + dy, window.innerHeight - miniPill.offsetHeight)) + "px";
        }
      });

      document.addEventListener("mouseup", () => (isDragging = false));

      // Resize Logic
      const resizeHandle = root.getElementById("chat-resize-handle");
      let isResizing = false;
      resizeHandle.onmousedown = (e) => {
        isResizing = true;
        startX = e.clientX;
        startY = e.clientY;
        const rect = container.getBoundingClientRect();
        initWidth = rect.width;
        initHeight = rect.height;
        e.preventDefault();
      };

      let initWidth, initHeight;
      document.addEventListener("mousemove", (e) => {
        if (isResizing) {
          const dx = startX - e.clientX; // Resizing from bottom-left
          const dy = startY - e.clientY;
          container.style.width = Math.max(320, initWidth + dx) + "px";
          container.style.height = Math.max(400, initHeight + dy) + "px";
        }
      });
      document.addEventListener("mouseup", () => (isResizing = false));
    }

    // ---- Messaging Logic ----
    async sendMessage() {
      const input = this.shadowRoot.getElementById("chat-input");
      const text = input.value.trim();
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
        if (att.id) displayHtml += `<div class="message-attachment-pill">${this.icons.attach} <span>${att.name}</span></div>`;
      });

      this.addMessage("user", displayHtml);

      const attachedFileId = this.attachments.length > 0 ? this.attachments[0].id : null;
      const imageDataUrl = this.pastedImage ? this.pastedImage.data : null;
      this.attachments = [];
      this.pastedImage = null;
      this.renderAttachments();

      this.isTyping = true;
      this.updateInputButtons();

      const aiWrapper = this.addMessage("ai", '<div class="typing-indicator"><span class="typing-dot"></span><span class="typing-dot"></span><span class="typing-dot"></span></div>');
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
            imageDataUrl
          }),
        });

        if (!response.ok) throw new Error(`API Error: ${response.status}`);

        const reader = response.body.getReader();
        const decoder = new TextDecoder();
        let fullText = "", hasStarted = false;

        while (true) {
          const { done, value } = await reader.read();
          if (done) break;
          const chunk = decoder.decode(value);
          const lines = chunk.split("\n");

          for (const line of lines) {
            if (line.startsWith("data: ")) {
              try {
                const data = JSON.parse(line.substring(6));
                const toolCall = data.ToolCall || data.toolCall;
                const sid = data.SessionId || data.sessionId || data.sid;
                const textChunk = data.Text || data.text;
                const errorChunk = data.Error || data.error;

                if (sid && !this.currentSessionId) {
                  this.currentSessionId = sid;
                  localStorage.setItem("ai_chat_session_id", sid);
                }

                if (errorChunk) {
                  bubble.innerHTML = `<span style="color:var(--danger-color)">${errorChunk}</span>`;
                  hasStarted = true;
                  break;
                }

                if (toolCall) {
                  this.handleToolCall(toolCall, bubble);
                  return; // Stop processing this stream, handleToolCall will continue
                }

                if (textChunk) {
                  if (!hasStarted) { bubble.innerHTML = ""; hasStarted = true; }
                  fullText += textChunk;
                  bubble.innerHTML = this.formatMarkdown(fullText);
                  this.scrollToBottom();
                }
              } catch(e) {}
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
      }
    }

    async handleToolCall(toolCall, bubble) {
      const toolName = toolCall.name || toolCall.Name;
      const toolArgs = toolCall.arguments || toolCall.Arguments;
      const callId = toolCall.id || toolCall.Id || Date.now().toString();

      console.log('Executing tool:', toolName, toolArgs);
      
      // Update UI to show thinking/calling tool
      bubble.innerHTML = `<div class="tool-calling-indicator">
        <span class="spin-animation">${this.icons.refresh}</span>
        <span>Calling tool: <strong>${toolName}</strong>...</span>
      </div>`;

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
        // Create result promise before dispatching event to avoid race condition
        const resultPromise = new Promise((resolve) => {
          const onResult = (e) => {
            if (e.detail.callId === callId) {
              this.removeEventListener('tool-result-submitted', onResult);
              resolve(e.detail.result);
            }
          };
          this.addEventListener('tool-result-submitted', onResult);
          
          // Timeout after 30 seconds
          setTimeout(() => {
            this.removeEventListener('tool-result-submitted', onResult);
            resolve({ error: "Tool execution timed out" });
          }, 30000);
        });

        // Dispatch event for external handling
        const event = new CustomEvent("tool-call", {
          detail: { 
            name: toolName, 
            args: typeof toolArgs === 'string' ? JSON.parse(toolArgs) : toolArgs,
            callId: callId
          },
          bubbles: true,
          composed: true
        });
        this.dispatchEvent(event);

        // Wait for submitToolResult to be called
        result = await resultPromise;
      }

      // Send result back to API to continue conversation
      const { modelName: toolModel, provider: toolProvider } = this.getSelectedModel();
      const response = await fetch(`${this.apiUrl}/api/chat`, {
        method: "POST",
        headers: { ...this.getHeaders(), "Content-Type": "application/json" },
        body: JSON.stringify({
          message: "",
          sessionId: this.currentSessionId,
          provider: toolProvider,
          modelName: toolModel,
          toolResult: {
            toolName: toolName,
            result: result
          }
        }),
      });

      // Restart the streaming UI for the new response
      const reader = response.body.getReader();
      const decoder = new TextDecoder();
      let fullText = "", hasStarted = false;

      while (true) {
        const { done, value } = await reader.read();
        if (done) break;
        const chunk = decoder.decode(value);
        const lines = chunk.split("\n");
        for (const line of lines) {
          if (line.startsWith("data: ")) {
            try {
              const data = JSON.parse(line.substring(6));
              const textChunk = data.text || data.Text;
              if (textChunk) {
                if (!hasStarted) { bubble.innerHTML = ""; hasStarted = true; }
                fullText += textChunk;
                bubble.innerHTML = this.formatMarkdown(fullText);
                this.scrollToBottom();
              }
            } catch(e) {}
          }
        }
      }
      
      this.isTyping = false;
      this.updateInputButtons();
    }

    async handleLiveToolCall(name, args, id = null) {
      console.log("Live Tool Call:", name, args, id);
      
      // Add a special tool message to live transcript
      const area = this.shadowRoot.getElementById("live-transcript");
      const div = document.createElement("div");
      div.className = "live-transcript-msg live-msg-tool";
      div.innerHTML = `
        <div class="live-msg-avatar">${this.icons.refresh}</div>
        <div class="live-msg-bubble tool-bubble">
          <span>Executing <strong>${name}</strong>...</span>
        </div>
      `;
      area.appendChild(div);
      area.scrollTop = area.scrollHeight;

      let result = null;
      const handler = this.toolHandlers.get(name);
      
      if (handler) {
        try {
          result = await handler(args);
        } catch (err) {
          result = { error: err.message };
        }
      } else {
        // Support external handling via submitToolResult
        const callId = id || `live-${name}-${Date.now()}`;
        const resultPromise = new Promise((resolve) => {
          const onResult = (e) => {
            if (e.detail.callId === callId || e.detail.callId === name) {
              this.removeEventListener('tool-result-submitted', onResult);
              resolve(e.detail.result);
            }
          };
          this.addEventListener('tool-result-submitted', onResult);
          
          // Timeout after 30 seconds for live tools
          setTimeout(() => {
            this.removeEventListener('tool-result-submitted', onResult);
            resolve({ error: "Live tool execution timed out" });
          }, 30000);
        });

        // Dispatch event for external handling
        const event = new CustomEvent("tool-call", {
          detail: { name, args, live: true, callId },
          bubbles: true,
          composed: true
        });
        this.dispatchEvent(event);
        
        result = await resultPromise;
      }

      // Send result back to hub
      if (this.liveConnection) {
        try {
          await this.liveConnection.invoke("SendToolResult", name, JSON.stringify(result));
          div.querySelector(".live-msg-bubble").innerHTML = `<span>Tool <strong>${name}</strong> executed successfully.</span>`;
        } catch (err) {
          console.error("Failed to send tool result:", err);
        }
      }
    }


    addMessage(role, htmlContent, fileId = null, fileName = null) {
      const container = this.shadowRoot.getElementById("messages-container");
      const wrapper = document.createElement("div");
      const isUser = role === "user";
      wrapper.className = `message-wrapper ${isUser ? "user-side" : ""} message-appear`;
      
      const time = new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
      const avatar = isUser ? "" : `<div class="message-avatar ai-avatar">${this.icons.awesome}</div>`;
      const userAvatar = isUser ? `<div class="message-avatar user-avatar">${this.icons.user}</div>` : "";
      
      // Remove previous regenerate buttons
      if (!isUser) {
        container.querySelectorAll("[data-action='regenerate']").forEach(btn => btn.remove());
      }

      let actionsHtml = "";
      if (!isUser) {
        actionsHtml = `
          <div class="message-actions">
            <button class="msg-action-btn" data-action="copy" title="Copy">${this.icons.copy}</button>
            <button class="msg-action-btn" data-action="speak" title="Listen">${this.icons.voice}</button>
            <button class="msg-action-btn" data-action="regenerate" title="Regenerate">${this.icons.refresh}</button>
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

      // Add action listeners
      if (!isUser) {
        wrapper.querySelectorAll(".msg-action-btn").forEach(btn => {
          btn.onclick = () => {
            const action = btn.getAttribute("data-action");
            const bubble = wrapper.querySelector(".message-bubble");
            const text = bubble.innerText;
            
            if (action === "copy") {
              navigator.clipboard.writeText(text);
              const originalIcon = btn.innerHTML;
              btn.innerHTML = this.icons.check;
              btn.style.color = "var(--success-color, #2ecc71)";
              setTimeout(() => {
                btn.innerHTML = originalIcon;
                btn.style.color = "";
              }, 2000);
            } else if (action === "speak") {
              const originalIcon = btn.innerHTML;
              btn.innerHTML = this.icons.loading;
              
              const utterance = new SpeechSynthesisUtterance(text);
              utterance.onend = () => {
                btn.innerHTML = originalIcon;
                btn.classList.remove("speaking-active");
              };
              btn.classList.add("speaking-active");
              window.speechSynthesis.speak(utterance);
            } else if (action === "regenerate") {
              this.regenerateLastResponse();
            }
          };
        });
      }

      this.scrollToBottom();
      return wrapper;
    }

    async regenerateLastResponse() {
      const messages = this.shadowRoot.getElementById("messages-container").querySelectorAll(".message-wrapper.user-side");
      if (messages.length === 0) return;
      
      const lastUserMsg = messages[messages.length - 1];
      const text = lastUserMsg.querySelector(".message-text").innerText;
      
      // Clear last AI message if it exists
      const allMsgs = this.shadowRoot.getElementById("messages-container").children;
      if (allMsgs.length > 0 && !allMsgs[allMsgs.length - 1].classList.contains("user-side")) {
        allMsgs[allMsgs.length - 1].remove();
      }

      this.shadowRoot.getElementById("chat-input").value = text;
      this.sendMessage();
    }

    // ---- Live Mode Logic ----
    async toggleLiveMode() {
      if (this.isLive) {
        this.stopLiveSession();
      } else {
        this.startLiveSession();
      }
    }

    async startLiveSession() {
      this.isLive = true;
      this.shadowRoot.getElementById("live-overlay").classList.add("active");
      this.shadowRoot.getElementById("btn-live").classList.add("pulse-animation");
      this.visualizer.init(this.shadowRoot.getElementById("live-orb-canvas"));
      
      this.liveStartTime = Date.now();
      this.liveTimerInterval = setInterval(() => this.updateLiveTimer(), 1000);
      this.updateLiveStatus("connecting", "Connecting...");

      try {
        if (!this.audioContext) this.audioContext = new (window.AudioContext || window.webkitAudioContext)({ sampleRate: 16000 });
        if (this.audioContext.state === "suspended") await this.audioContext.resume();

        if (typeof window.signalR === "undefined") {
          await new Promise((res, rej) => {
            const s = document.createElement("script");
            s.src = "https://cdnjs.cloudflare.com/ajax/libs/microsoft-signalr/8.0.0/signalr.min.js";
            s.onload = res; s.onerror = rej; document.head.appendChild(s);
          });
        }

        this.liveConnection = new window.signalR.HubConnectionBuilder()
          .withUrl(`${this.apiUrl}/liveAudioHub`, {
            accessTokenFactory: () => this.authToken
          })
          .withAutomaticReconnect()
          .build();

        this.liveConnection.on("ReceiveAudioChunk", data => this.playAudioChunk(data));
        this.liveConnection.on("ReceiveTextChunk", (text, isThought) => {
          if (isThought) {
            this.updateThought(text);
          } else {
            this.addLiveMessage("ai", text);
          }
        });
        this.liveConnection.on("ReceiveInputTranscription", text => this.addLiveMessage("user", text));
        this.liveConnection.on("ReceiveToolCall", (id, name, args) => this.handleLiveToolCall(name, args, id));
        
        await this.liveConnection.start();
        const voice = this.shadowRoot.getElementById("voice-select").value;
        
        // Use separate hub methods for widget (API key) vs dashboard (JWT) auth
        if (this.apiKey) {
          await this.liveConnection.invoke("StartLive", this.userId, voice, this.apiKey);
        } else if (this.authToken) {
          await this.liveConnection.invoke("StartLiveDashboard", this.userId, voice, this.projectId || "", this.configurationId || "");
        } else {
          throw new Error("No API key or auth token configured");
        }

        const workletUrl = `${this.apiUrl}/widget/audio-processor.js`;
        console.log("Loading audio worklet from:", workletUrl);
        await this.audioContext.audioWorklet.addModule(workletUrl);
        this.micStream = await navigator.mediaDevices.getUserMedia({ audio: true });
        const source = this.audioContext.createMediaStreamSource(this.micStream);
        
        this.micAnalyser = this.audioContext.createAnalyser();
        this.micAnalyser.fftSize = 512;
        source.connect(this.micAnalyser);

        const processor = new AudioWorkletNode(this.audioContext, "audio-processor");
        source.connect(processor);

        this.visualizer.setAnalysers(this.micAnalyser, this.playbackAnalyser);
        this.miniVisualizer.setAnalysers(this.micAnalyser, this.playbackAnalyser);

        processor.port.onmessage = (e) => {
          if (!this.isLive || this.isLiveMuted) return;
          const base64 = btoa(String.fromCharCode(...new Uint8Array(e.data.pcm16.buffer)));
          this.liveConnection.invoke("SendAudio", base64).catch(console.error);
        };

        processor.connect(this.audioContext.destination);
        this.updateLiveStatus("listening", "Listening");
        this.shadowRoot.getElementById("live-error-bar").style.display = "none";
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
      const bar = this.shadowRoot.getElementById("live-error-bar");
      bar.style.display = "flex";
      this.shadowRoot.getElementById("live-error-text").textContent = msg;
    }

    stopLiveSession() {
      this.isLive = false;
      this.shadowRoot.getElementById("live-overlay").classList.remove("active");
      this.shadowRoot.getElementById("mini-live").classList.remove("active");
      this.shadowRoot.getElementById("btn-live").classList.remove("pulse-animation");
      this.shadowRoot.getElementById("fab-toggle").classList.remove("toggle-hidden");
      
      clearInterval(this.liveTimerInterval);
      this.visualizer.destroy();
      this.miniVisualizer.destroy();

      if (this.liveConnection) this.liveConnection.stop();
      if (this.micStream) this.micStream.getTracks().forEach(t => t.stop());
      if (this.audioContext) this.audioContext.close();
      this.audioContext = null;
      this.liveConnection = null;
    }

    async playAudioChunk(data) {
      if (!this.playbackContext) {
        this.playbackContext = new (window.AudioContext || window.webkitAudioContext)({ sampleRate: 24000 });
        this.playbackAnalyser = this.playbackContext.createAnalyser();
        this.playbackAnalyser.fftSize = 512;
        this.playbackAnalyser.connect(this.playbackContext.destination);
        this.nextPlayTime = 0;
        
        if (this.visualizer) this.visualizer.setAnalysers(this.micAnalyser, this.playbackAnalyser);
        if (this.miniVisualizer) this.miniVisualizer.setAnalysers(this.micAnalyser, this.playbackAnalyser);
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
      const bar = this.shadowRoot.getElementById("live-thinking-bar");
      const el = this.shadowRoot.getElementById("live-thought-text");
      if (!text) {
        bar.style.display = "none";
      } else {
        bar.style.display = "flex";
        el.textContent = text;
        this.updateLiveStatus("thinking", "Thinking");
      }
    }

    // ---- UI Helpers ----
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

    updateLiveTimer() {
      const sec = Math.floor((Date.now() - this.liveStartTime) / 1000);
      const min = Math.floor(sec / 60);
      const time = `${min.toString().padStart(2,'0')}:${(sec%60).toString().padStart(2,'0')}`;
      this.shadowRoot.getElementById("live-timer-text").textContent = time;
      this.shadowRoot.getElementById("mini-timer-text").textContent = time;
    }

    updateLiveStatus(status, text) {
      const badge = this.shadowRoot.getElementById("live-badge");
      const miniBadge = this.shadowRoot.getElementById("mini-status-dot");
      badge.className = `live-status-badge badge-${status}`;
      miniBadge.className = `mini-status-dot badge-${status}`;
      this.shadowRoot.getElementById("live-status-text").textContent = text;
      
      const thinkingBar = this.shadowRoot.getElementById("live-thinking-bar");
      if (status === "thinking") {
        thinkingBar.style.display = "flex";
      } else if (status !== "error") {
        thinkingBar.style.display = "none";
      }

      this.visualizer.setState(status);
      this.miniVisualizer.setState(status);
    }

    addLiveMessage(role, text) {
      const area = this.shadowRoot.getElementById("live-transcript");
      const empty = area.querySelector(".live-transcript-empty");
      if (empty) empty.remove();

      const last = area.lastElementChild;
      const isUser = role === 'user';
      const roleClass = isUser ? 'live-msg-user' : 'live-msg-ai';
      const avatarIcon = isUser ? this.icons.person : this.icons.awesome;

      if (last && last.classList.contains(roleClass)) {
        last.querySelector(".live-msg-text").textContent += text;
      } else {
        const div = document.createElement("div");
        div.className = `live-transcript-msg ${roleClass}`;
        div.innerHTML = `
          <div class="live-msg-avatar">${avatarIcon}</div>
          <div class="live-msg-bubble">
            <span class="live-msg-text">${text}</span>
            <span class="live-msg-time">${new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}</span>
          </div>
        `;
        area.appendChild(div);
      }
      area.scrollTop = area.scrollHeight;
    }

    // ---- Missing Methods Implementation ----
    async loadSessions() {
      try {
        const isArchived = this.shadowRoot.getElementById("tab-archived").classList.contains("history-tab-active");
        const endpoint = isArchived ? "archived" : "sessions";
        const response = await fetch(`${this.apiUrl}/api/chat/${endpoint}`, {
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
      this.currentSessionId = sessionId;
      localStorage.setItem("ai_chat_session_id", sessionId);
      const list = this.shadowRoot.getElementById("messages-container");
      list.innerHTML = `<div class="history-loading">Loading messages...</div>`;
      try {
        const response = await fetch(`${this.apiUrl}/api/chat/sessions/${sessionId}`, {
          headers: this.getHeaders(),
        });
        if (!response.ok) throw new Error("Session not found");
        const messages = await this.safeJson(response);
        list.innerHTML = "";
        messages.forEach((m) => {
          const role = m.Role || m.role;
          const content = this.formatMarkdown(m.Content || m.content);
          const fileId = m.AttachedFileId || m.attachedFileId;
          const fileName = m.AttachedFileName || m.attachedFileName;
          const img = m.ImageDataUrl || m.imageDataUrl;
          let displayHtml = content;
          if (img) displayHtml = `<div class="message-image-container"><img src="${img}" class="message-image"></div>` + displayHtml;
          this.addMessage(role, displayHtml, fileId, fileName);
        });
        this.scrollToBottom();
      } catch (err) {
        list.innerHTML = "";
        this.addMessage("ai", "Error loading chat history.");
      }
    }

    startNewChat() {
      this.currentSessionId = null;
      localStorage.removeItem("ai_chat_session_id");
      this.shadowRoot.getElementById("messages-container").innerHTML = "";
      this.renderEmptyState();
      if (this.isHistoryOpen) this.toggleHistory();
    }

    toggleHistory() {
      this.isHistoryOpen = !this.isHistoryOpen;
      this.shadowRoot.getElementById("history-drawer").classList.toggle("history-open", this.isHistoryOpen);
      if (this.isHistoryOpen) this.loadSessions();
    }

    toggleLiveMute() {
      this.isLiveMuted = !this.isLiveMuted;
      const btn = this.shadowRoot.getElementById("live-mute-btn");
      const miniBtn = this.shadowRoot.getElementById("mini-mute-btn");
      const text = this.shadowRoot.getElementById("live-mute-text");
      
      if (this.isLiveMuted) {
        btn.classList.add("muted");
        miniBtn.classList.add("muted");
        text.textContent = "Unmute";
      } else {
        btn.classList.remove("muted");
        miniBtn.classList.remove("muted");
        text.textContent = "Mute";
      }
    }

    handleFileSelection(e) {
      const files = Array.from(e.target.files);
      files.forEach(async (file) => {
        const attachment = { name: file.name, isUploading: true, id: null };
        this.attachments.push(attachment);
        this.renderAttachments();
        try {
          const uploaded = await this.uploadFile(file);
          attachment.id = uploaded.id;
          attachment.isUploading = false;
        } catch (err) {
          attachment.error = true;
          attachment.isUploading = false;
        }
        this.renderAttachments();
      });
    }

    async uploadFile(file) {
      const formData = new FormData();
      formData.append("file", file);
      const resp = await fetch(`${this.apiUrl}/api/File/upload`, {
        method: "POST",
        headers: this.getHeaders(),
        body: formData,
      });
      if (!resp.ok) throw new Error("Upload failed");
      return await this.safeJson(resp);
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
        const pill = document.createElement("div");
        pill.className = `attached-file-pill ${att.error ? "error" : ""}`;
        pill.innerHTML = `
          ${this.icons.attach}
          <span>${att.name}</span>
          <button class="remove-attachment" data-idx="${i}">${this.icons.close}</button>
        `;
        pill.querySelector("button").onclick = () => {
          this.attachments.splice(i, 1);
          this.renderAttachments();
        };
        container.appendChild(pill);
      });
      this.updateSendButtonState();
    }

    startVoiceRecording() {
      this.isRecording = true;
      this.shadowRoot.getElementById("btn-mic").classList.add("recording");

      navigator.mediaDevices.getUserMedia({ audio: true }).then((stream) => {
        this._mediaRecorder = new MediaRecorder(stream, { mimeType: "audio/webm;codecs=opus" });
        this._audioChunks = [];
        this._mediaRecorder.ondataavailable = (e) => {
          if (e.data.size > 0) this._audioChunks.push(e.data);
        };
        this._mediaRecorder.start();
      }).catch((err) => {
        console.error("Microphone access denied:", err);
        this.cancelVoiceRecording();
      });
    }

    async stopVoiceRecording() {
      if (!this.isRecording) return;
      this.isRecording = false;
      this.shadowRoot.getElementById("btn-mic").classList.remove("recording");

      if (!this._mediaRecorder || this._audioChunks.length === 0) return;

      this._mediaRecorder.onstop = async () => {
        this._mediaRecorder.stream.getTracks().forEach((t) => t.stop());

        const blob = new Blob(this._audioChunks, { type: "audio/webm" });
        const formData = new FormData();
        formData.append("audio", blob, "recording.webm");
        formData.append("language", "auto");

        try {
          const res = await fetch(`${this.apiUrl}/api/audio/transcribe`, {
            method: "POST",
            headers: this.getHeaders(),
            body: formData
          });
          if (res.ok) {
            const data = await this.safeJson(res);
            const input = this.shadowRoot.getElementById("chat-input");
            input.value = data.text || "";
            this.updateSendButtonState();
            if (data.text) this.sendMessage();
          }
        } catch (err) {
          console.error("Transcription failed:", err);
        }
      };

      this._mediaRecorder.stop();
    }

    cancelVoiceRecording() {
      this.isRecording = false;
      this.shadowRoot.getElementById("btn-mic").classList.remove("recording");
      if (this._mediaRecorder && this._mediaRecorder.state !== "inactive") {
        this._mediaRecorder.stream.getTracks().forEach((t) => t.stop());
      }
    }

    sendLiveTextMessage() {
      const input = this.shadowRoot.getElementById("live-text-input");
      const text = input.value.trim();
      if (text && this.liveConnection) {
        this.liveConnection.invoke("SendText", text);
        this.addLiveMessage("user", text);
        input.value = "";
        input.style.height = "auto";
      }
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
          <div class="history-item-icon">${this.icons.history}</div>
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
          <div class="empty-state-icon">${this.icons.awesome}</div>
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

    // Standard helpers
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
    formatMarkdown(text) {
      if (!text) return "";
      let html = text.replace(/\*\*(.*?)\*\*/g, "<strong>$1</strong>")
                     .replace(/`([^`]+)`/g, "<code>$1</code>")
                     .replace(/\n/g, "<br>");
      
      // Handle code blocks with copy button
      html = html.replace(/<pre><code>([\s\S]*?)<\/code><\/pre>|```([\s\S]*?)```/g, (match, p1, p2) => {
        const code = (p1 || p2 || "").replace(/<br>/g, "\n");
        const id = 'code-' + Math.random().toString(36).substr(2, 9);
        return `
          <div class="code-block-wrapper">
            <div class="code-header">
              <span>Code</span>
              <button class="copy-code-btn" data-code-id="${id}">${this.icons.copy} Copy</button>
            </div>
            <pre><code id="${id}">${code}</code></pre>
          </div>
        `;
      });

      return html;
    }
    scrollToBottom() {
      const m = this.shadowRoot.getElementById("messages-container");
      if (m) {
        m.scrollTop = m.scrollHeight;
        
        // Attach copy listeners to new code blocks
        m.querySelectorAll(".copy-code-btn").forEach(btn => {
          if (btn.dataset.listener) return;
          btn.dataset.listener = "true";
          btn.onclick = () => {
            const codeId = btn.getAttribute("data-code-id");
            const code = this.shadowRoot.getElementById(codeId).innerText;
            navigator.clipboard.writeText(code);
            const original = btn.innerHTML;
            btn.innerHTML = `${this.icons.check} Copied`;
            setTimeout(() => btn.innerHTML = original, 2000);
          };
        });
      }
    }
  }

  customElements.define("ai-chatbox", AiChatBox);
})();

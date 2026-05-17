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
      
      this.handoffPoller = null;
      this.handoffConnection = null;
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
        list: '<svg width="18" height="18" viewBox="0 0 24 24" fill="currentColor"><path d="M3 13h2v-2H3v2zm0 4h2v-2H3v2zm0-8h2V7H3v2zm4 4h14v-2H7v2zm0 4h14v-2H7v2zM7 7v2h14V7H7z"/></svg>',
        chart: '<svg width="18" height="18" viewBox="0 0 24 24" fill="currentColor"><path d="M5 9.2h3V19H5zM10.6 5h2.8v14h-2.8zm5.6 8H19v6h-2.8z"/></svg>',
        expand: '<svg width="18" height="18" viewBox="0 0 24 24" fill="currentColor"><path d="M7 14H5v5h5v-2H7v-3zm-2-4h2V7h3V5H5v5zm12 7h-3v2h5v-5h-2v3zM14 5v2h3v3h2V5h-5z"/></svg>',
        collapse: '<svg width="18" height="18" viewBox="0 0 24 24" fill="currentColor"><path d="M5 16h3v3h2v-5H5v2zm3-8H5v2h5V5H8v3zm6 11h2v-3h3v-2h-5v5zm2-11V5h-2v5h5V8h-3z"/></svg>',
        excel: '<svg width="18" height="18" viewBox="0 0 24 24" fill="currentColor"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8l-6-6zm2 16H8v-2h8v2zm0-4H8v-2h8v2zm-3-5V3.5L18.5 9H13z"/></svg>',
        pdf: '<svg width="18" height="18" viewBox="0 0 24 24" fill="currentColor"><path d="M20 2H8a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V4a2 2 0 0 0-2-2zm-8.5 7.5c0 .83-.67 1.5-1.5 1.5H9v2H7.5V7H10c.83 0 1.5.67 1.5 1.5v1zm5 2c0 .83-.67 1.5-1.5 1.5h-2.5V7H15c.83 0 1.5.67 1.5 1.5v3zm4-3H19v1h1.5V11H19v2h-1.5V7h3v1.5zM9 8.5V10h1V8.5H9zm5 0V12h1V8.5h-1zM4 6H2v14a2 2 0 0 0 2 2h14v-2H4V6z"/></svg>',
        download: '<svg width="18" height="18" viewBox="0 0 24 24" fill="currentColor"><path d="M19 9h-4V3H9v6H5l7 7 7-7zM5 18v2h14v-2H5z"/></svg>',
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

    getPageContext() {
      return {
        url: window.location.href,
        title: document.title,
        path: window.location.pathname
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
          if (this.config.suggestions && Array.isArray(this.config.suggestions) && this.config.suggestions.length > 0) {
            this.suggestions = this.config.suggestions;
          }
          if (this.config.theme) {
            this.applyTheme(this.config.theme);
          }
        }
      } catch (err) {
        console.error("Failed to fetch widget config:", err);
      }
    }

    applyTheme(theme) {
      if (!theme) return;
      if (theme.primaryColor) {
        this.style.setProperty("--primary-color", theme.primaryColor);
        // Create a simple gradient based on primary color
        this.style.setProperty("--primary-gradient", `linear-gradient(135deg, ${theme.primaryColor} 0%, ${this.adjustColor(theme.primaryColor, -20)} 100%)`);
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
      // Layout positions will be handled via a host class or inline style
      if (theme.position === 'bottom-left') {
        this.style.setProperty("--widget-right", "auto");
        this.style.setProperty("--widget-left", "24px");
      } else {
        this.style.setProperty("--widget-right", "24px");
        this.style.setProperty("--widget-left", "auto");
      }
    }

    // Helper to darken/lighten hex colors slightly for gradients
    adjustColor(color, amount) {
        return '#' + color.replace(/^#/, '').replace(/../g, color => 
            ('0'+Math.min(255, Math.max(0, parseInt(color, 16) + amount)).toString(16)).substr(-2));
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
        this.getAttribute("suggestions") || '[]',
      );
      if (this.suggestions.length === 0) {
          this.suggestions = ["Good morning", "How can you help me?", "Tell me a joke"];
      }

      await this.loadExternalScripts();

      await this.fetchConfig();
      this.render();
      this.setupEventListeners();
      this.setupDraggable();
      if (this.currentSessionId) {
        this.loadSessionMessages(this.currentSessionId);
      } else {
        this.renderEmptyState();
      }
      this.loadSessions();
    }

    render() {
      const stylePath = this.getAttribute("css-path") || `${this.apiUrl}/widget/ai-chatbox.css`;
      this.shadowRoot.innerHTML = `
                <link rel="stylesheet" href="${stylePath}">
                <style>
                  .message-text-content:empty, .message-widget-container:empty { display: none; }
                  .message-text-content { margin-bottom: 8px; line-height: 1.5; }
                  .message-widget-container { width: 100%; overflow: hidden; margin-top: 10px; transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1); }
                  .message-widget-container.expanded { width: calc(100% + 40px); margin-left: -20px; }
                  
                  .data-result-widget { 
                    background: var(--bg-glass, rgba(255, 255, 255, 0.03)); 
                    backdrop-filter: blur(12px);
                    border-radius: 16px; 
                    border: 1px solid var(--border-color, rgba(255, 255, 255, 0.1)); 
                    overflow: hidden; 
                    box-shadow: 0 8px 32px rgba(0,0,0,0.2);
                    display: flex;
                    flex-direction: column;
                  }

                  .data-tabs {
                    display: flex;
                    padding: 6px;
                    background: rgba(0,0,0,0.2);
                    gap: 4px;
                    border-bottom: 1px solid rgba(255,255,255,0.05);
                  }

                  .data-tab {
                    flex: 1;
                    padding: 8px 12px;
                    border: none;
                    background: transparent;
                    color: var(--text-muted, #94a3b8);
                    font-size: 13px;
                    font-weight: 500;
                    cursor: pointer;
                    border-radius: 8px;
                    transition: all 0.2s ease;
                    display: flex;
                    align-items: center;
                    justify-content: center;
                    gap: 6px;
                  }

                  .data-tab svg { width: 14px; height: 14px; }
                  .data-tab:hover { background: rgba(255,255,255,0.05); color: #fff; }
                  .data-tab.active { background: var(--primary-color, #6366f1); color: #fff; box-shadow: 0 2px 8px rgba(99, 102, 241, 0.4); }

                  .data-actions { display: flex; gap: 4px; padding-left: 8px; border-left: 1px solid rgba(255,255,255,0.1); }
                  .data-action-btn {
                    padding: 6px;
                    background: transparent;
                    border: none;
                    color: var(--text-muted);
                    cursor: pointer;
                    border-radius: 6px;
                    transition: all 0.2s;
                    display: flex;
                    align-items: center;
                    justify-content: center;
                  }
                  .data-action-btn:hover { background: rgba(255,255,255,0.1); color: #fff; }
                  .data-action-btn svg { width: 14px; height: 14px; }

                  .data-content { padding: 12px; position: relative; min-height: 200px; }
                  .data-panel { display: none; }
                  .data-panel.active { display: block; animation: fadeIn 0.3s ease; }
                  
                  @keyframes fadeIn { from { opacity: 0; transform: translateY(5px); } to { opacity: 1; transform: translateY(0); } }

                  .table-container { overflow-x: auto; border-radius: 8px; border: 1px solid rgba(255,255,255,0.05); }
                  .data-result-widget table { width: 100%; border-collapse: collapse; font-size: 12px; color: var(--text-main); }
                  .data-result-widget th { background: rgba(0,0,0,0.3); padding: 10px 12px; text-align: left; font-weight: 600; color: #fff; }
                  .data-result-widget td { padding: 8px 12px; border-bottom: 1px solid rgba(255,255,255,0.05); white-space: nowrap; }
                  .data-result-widget tr:nth-child(even) { background: rgba(255,255,255,0.02); }
                  .data-result-widget tr:hover { background: rgba(99, 102, 241, 0.1); }

                  .chart-controls { display: flex; justify-content: flex-end; margin-bottom: 10px; gap: 8px; }
                  .chart-type-select {
                    background: rgba(0,0,0,0.4);
                    color: #fff;
                    border: 1px solid rgba(255,255,255,0.1);
                    border-radius: 6px;
                    padding: 4px 8px;
                    font-size: 12px;
                    outline: none;
                  }

                  .data-chart-canvas { width: 100% !important; height: 220px !important; }
                  
                  /* Typing Indicator */
                  .typing-indicator { display: flex; gap: 5px; padding: 8px 12px; }
                  .typing-dot { width: 7px; height: 7px; border-radius: 50%; background: #94a3b8; animation: typingBounce 1.4s ease-in-out infinite; }
                  .typing-dot:nth-child(2) { animation-delay: 0.15s; }
                  .typing-dot:nth-child(3) { animation-delay: 0.3s; }
                  @keyframes typingBounce {
                    0%, 60%, 100% { transform: translateY(0); opacity: 0.4; }
                    30% { transform: translateY(-6px); opacity: 1; }
                  }

                  .tool-calling-indicator {
                    display: flex;
                    align-items: center;
                    gap: 10px;
                    padding: 10px 14px;
                    background: rgba(57, 167, 185, 0.05);
                    border-radius: 10px;
                    color: var(--primary-color);
                    font-size: 13px;
                    border: 1px dashed var(--primary-color);
                    margin: 4px 0;
                  }
                  .spin-animation { animation: spin 1s linear infinite; display: inline-block; }
                  @keyframes spin { 100% { transform: rotate(360deg); } }
                  .live-widget-container { width: 100%; overflow: hidden; }
                  .live-simple-result {
                    font-size: 11px;
                    opacity: 0.8;
                    margin-top: 8px;
                    overflow-x: auto;
                    background: rgba(0,0,0,0.2);
                    padding: 8px;
                    border-radius: 8px;
                    border: 1px solid rgba(255,255,255,0.1);
                    color: #cbd5e1;
                    max-height: 150px;
                    white-space: pre-wrap;
                  }
                  .agent-side .message-bubble { background: var(--primary-color, #6366f1); color: white; border-bottom-left-radius: 4px; border-bottom-right-radius: 12px; }
                  .agent-avatar { background: var(--primary-color, #6366f1); color: white; }
                </style>
                
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
        this.handleUserTyping();
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

      // Clear user typing status when sending
      if (this.userTypingTimeout) clearTimeout(this.userTypingTimeout);
      this.isUserTypingSignalSent = false;
      if (this.handoffConnection && this.currentSessionId && this.handoffStatus !== "ai") {
        this.handoffConnection.invoke("SendUserTyping", this.currentSessionId, false, this.apiKey).catch(console.error);
      }

      // Bypass streaming UI completely if human handoff is active or queued
      if (this.handoffStatus === "active" || this.handoffStatus === "queued") {
        if (this.handoffConnection && this.handoffConnection.state === "Connected") {
          try {
            await this.handoffConnection.invoke("SendUserMessage", this.currentSessionId, text, this.apiKey);
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
            context: this.getPageContext()
          }),
        });

        if (!response.ok) throw new Error(`API Error: ${response.status}`);

        const reader = response.body.getReader();
        const decoder = new TextDecoder();
        let fullText = "", hasStarted = false;

        // Initialize containers for text and widgets
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

                if (sid && !this.currentSessionId) {
                  this.currentSessionId = sid;
                  localStorage.setItem("ai_chat_session_id", sid);
                }

                // Capture message ID from Done chunk for feedback
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

    async sendHandoffMessageViaHttp(text, attachedFileId, imageDataUrl) {
      try {
        const { modelName: selectedModel, provider: selectedProvider } = this.getSelectedModel();
        const response = await fetch(`${this.apiUrl}/api/chat`, {
          method: "POST",
          headers: { ...this.getHeaders(), "Content-Type": "application/json" },
          body: JSON.stringify({
            message: text,
            sessionId: this.currentSessionId,
            projectId: this.projectId,
            configurationId: this.configurationId,
            provider: selectedProvider,
            modelName: selectedModel,
            attachedFileId,
            imageDataUrl,
            context: this.getPageContext()
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
                  if (sid && !this.currentSessionId) {
                    this.currentSessionId = sid;
                    localStorage.setItem("ai_chat_session_id", sid);
                  }
                } catch (e) {}
              }
            }
          }
        }
      } catch (err) {
        console.error("Failed to send HTTP handoff message:", err);
      } finally {
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
            <span class="spin-animation">${this.icons.refresh}</span>
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

      // Send results back to API
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
          context: this.getPageContext()
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
      
      // Ensure we are connected for human handoff agent messages
      this.startHandoffConnection();
    }

    async handleLiveToolCall(name, args, callId = null, isBackend = false) {
      console.log(`[LiveToolCall] name=${name}, callId=${callId}, isBackend=${isBackend}`, args);
      
      const area = this.shadowRoot.getElementById("live-transcript");
      const div = document.createElement("div");
      div.className = "live-transcript-msg live-msg-tool";
      
      // Use name as fallback if callId is not provided
      const domId = callId ? `tool-call-${callId}` : `tool-call-${name}-${Date.now()}`;
      
      div.innerHTML = `
        <div class="live-msg-avatar">${this.icons.refresh}</div>
        <div class="live-msg-bubble tool-bubble" id="${domId}" data-name="${name}">
          <span>Executing <strong>${name}</strong>...</span>
        </div>
      `;
      area.appendChild(div);
      area.scrollTop = area.scrollHeight;
      
      // If it's a backend tool, the server handles it; we return and wait for ReceiveToolResult
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
          await this.liveConnection.invoke("SendToolResult", callId, JSON.stringify(result));
          // Don't remove the div, handleLiveToolResult will update it
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
        
        // Update the message wrapper to show AI avatar instead of loading spinner when done
        const wrapper = bubble.closest(".live-transcript-msg");
        if (wrapper) {
          wrapper.classList.add("tool-done");
          const avatar = wrapper.querySelector(".live-msg-avatar");
          if (avatar) {
            avatar.innerHTML = this.icons.awesome;
            avatar.style.animation = "none";
          }
        }

        bubble.innerHTML = `<div class="tool-result-header">
          ${this.icons.check} <span>Completed <strong>${name}</strong></span>
        </div>
        <div class="live-widget-container" style="margin-top:10px"></div>`;
        
        const container = bubble.querySelector(".live-widget-container");
        
        let data = result;
        
        // Handle various wrapping formats
        if (data && (data.content !== undefined || data.Content !== undefined)) {
          data = data.content !== undefined ? data.content : data.Content;
        }
        
        if (data && (data.result !== undefined || data.Result !== undefined)) {
          data = data.result !== undefined ? data.result : data.Result;
        }

        // Sometimes the result is a JSON string that needs parsing
        if (typeof data === 'string' && data.trim().startsWith('[')) {
          try { data = JSON.parse(data); } catch(e) {}
        }

        console.log(`[LiveToolResult] Final data for ${name}:`, data);

        const isData = data && (data.rows || data.data || Array.isArray(data));
        
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
      const avatar = isUser ? "" : `<div class="message-avatar ${isAgent ? 'agent-avatar' : 'ai-avatar'}">${isAgent ? this.icons.person : this.icons.awesome}</div>`;
      const userAvatar = isUser ? `<div class="message-avatar user-avatar">${this.icons.user}</div>` : "";
      
      // Remove previous regenerate buttons
      if (!isUser && !isAgent) {
        container.querySelectorAll("[data-action='regenerate']").forEach(btn => btn.remove());
      }

      let actionsHtml = "";
      if (!isUser) {
        actionsHtml = `
          <div class="message-actions">
            <button class="msg-action-btn" data-action="copy" title="Copy">${this.icons.copy}</button>
            <button class="msg-action-btn" data-action="speak" title="Listen">${this.icons.voice}</button>
            <button class="msg-action-btn" data-action="regenerate" title="Regenerate">${this.icons.refresh}</button>
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
          // Highlight the selected button
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
      
      // Clear last AI message if it exists
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
      this.stopGeneration();
      this.stopHandoffConnection();
      this.handoffStatus = "ai";
      this.currentSessionId = null;
      localStorage.removeItem("ai_chat_session_id");
      this.shadowRoot.getElementById("messages-container").innerHTML = "";

      const transcriptArea = this.shadowRoot.getElementById("live-transcript");
      if (transcriptArea) transcriptArea.innerHTML = "";

      this.renderEmptyState();
      if (this.isHistoryOpen) this.toggleHistory();

      // Select the generic AI mode if an agent session ended
      this.isTyping = false;
      this.updateInputButtons();
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
      
      const transcriptArea = this.shadowRoot.getElementById("live-transcript");
      if (transcriptArea) transcriptArea.innerHTML = "";
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
        this.liveConnection.on("ReceiveToolCall", (id, name, args, isBackend) => this.handleLiveToolCall(name, args, id, isBackend));
        this.liveConnection.on("ReceiveToolResult", (id, name, result) => this.handleLiveToolResult(id, name, result));
        
        await this.liveConnection.start();
        const voice = this.shadowRoot.getElementById("voice-select").value;
        
        // Use separate hub methods for widget (API key) vs dashboard (JWT) auth
        if (this.apiKey) {
          await this.liveConnection.invoke("StartLive", this.userId, voice, this.apiKey, this.currentSessionId);
        } else if (this.authToken) {
          await this.liveConnection.invoke("StartLiveDashboard", this.userId, voice, this.projectId || "", this.configurationId || "", this.currentSessionId);
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
        const textSpan = last.querySelector(".live-msg-text");
        // Store raw text in a data attribute to handle cumulative markdown rendering
        const currentText = (textSpan.dataset.raw || textSpan.textContent) + text;
        textSpan.dataset.raw = currentText;
        textSpan.innerHTML = this.formatMarkdown(currentText);
      } else {
        const div = document.createElement("div");
        div.className = `live-transcript-msg ${roleClass} message-appear`;
        div.innerHTML = `
          <div class="live-msg-avatar">${avatarIcon}</div>
          <div class="live-msg-bubble">
            <span class="live-msg-text" data-raw="${text}">${this.formatMarkdown(text)}</span>
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

    async startHandoffConnection() {
      if (!this.config?.handoffEnabled || !this.currentSessionId || this.currentSessionId === "null" || this.currentSessionId === "undefined") return;
      if (!this.apiKey) {
        this.startHandoffPoller();
        return;
      }
      
      if (this.handoffConnection) return; // already connected or connecting
      
      try {
        if (typeof window.signalR === "undefined") {
          await new Promise((res, rej) => {
            const s = document.createElement("script");
            s.src = "https://cdnjs.cloudflare.com/ajax/libs/microsoft-signalr/8.0.0/signalr.min.js";
            s.onload = res; s.onerror = rej; document.head.appendChild(s);
          });
        }

        this.handoffConnection = new window.signalR.HubConnectionBuilder()
          .withUrl(`${this.apiUrl}/liveChatHub`)
          .withAutomaticReconnect()
          .build();

        this.handoffConnection.on("HandoffStatus", (data) => {
          console.log("[HandoffConnection] Status update:", data);
          if (data.status) {
            this.handoffStatus = data.status;
          }
        });

        this.handoffConnection.on("ReceiveAgentMessage", (msg) => {
          console.log("[HandoffConnection] Received agent message:", msg);
          const existingMsg = this.shadowRoot.querySelector(`[data-message-id="${msg.id}"]`);
          if (!existingMsg) {
            this.toggleAgentTypingIndicator(false);
            const aiWrapper = this.addMessage("agent", "", null, null, msg.id);
            const bubble = aiWrapper.querySelector(".message-bubble");
            bubble.innerHTML = `<div class="message-text-content">${this.formatMarkdown(msg.content)}</div>`;
            this.scrollToBottom();
          }
        });

        this.handoffConnection.on("ReceiveAgentTyping", (data) => {
          if (data.sessionId === this.currentSessionId) {
            this.toggleAgentTypingIndicator(data.isTyping);
          }
        });

        this.handoffConnection.on("AgentJoined", (data) => {
          console.log("[HandoffConnection] Agent joined:", data);
          this.handoffStatus = "active";
          this.addSystemNotice(data.message || "A support agent has joined the conversation.");
        });

        this.handoffConnection.on("SessionResolved", (data) => {
          console.log("[HandoffConnection] Session resolved:", data);
          this.handoffStatus = "ai";
          this.addSystemNotice(data.message || "The support session has ended. You're now chatting with AI again.");
          this.stopHandoffConnection();
        });

        this.handoffConnection.on("ReturnedToAi", (data) => {
          console.log("[HandoffConnection] Returned to AI:", data);
          this.handoffStatus = "ai";
          this.addSystemNotice(data.message || "You've been returned to the AI assistant.");
          this.stopHandoffConnection();
        });

        this.handoffConnection.on("ReceiveError", (msg) => {
          console.error("[HandoffConnection] Error:", msg);
        });

        this.handoffConnection.onclose(() => {
          console.log("[HandoffConnection] Connection closed.");
        });

        await this.handoffConnection.start();
        console.log("[HandoffConnection] Connection started successfully.");
        await this.handoffConnection.invoke("JoinSession", this.currentSessionId, this.apiKey);

      } catch (err) {
        console.error("[HandoffConnection] Start failed, falling back to polling:", err);
        this.handoffConnection = null;
        this.startHandoffPoller();
      }
    }

    stopHandoffConnection() {
      if (this.handoffConnection) {
        this.handoffConnection.stop().catch(console.error);
        this.handoffConnection = null;
      }
      this.stopHandoffPoller();
    }

    toggleAgentTypingIndicator(isTyping) {
      const container = this.shadowRoot.getElementById("messages-container");
      let indicator = this.shadowRoot.getElementById("agent-typing-indicator");
      
      if (isTyping) {
        if (!indicator) {
          indicator = document.createElement("div");
          indicator.id = "agent-typing-indicator";
          indicator.className = "message-wrapper ai-side message-appear agent-side";
          indicator.innerHTML = `
            <div class="message-avatar agent-avatar">${this.icons.user}</div>
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
          this.scrollToBottom();
        }
      } else {
        if (indicator) {
          indicator.remove();
        }
      }
    }

    addSystemNotice(text) {
      const container = this.shadowRoot.getElementById("messages-container");
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
      this.scrollToBottom();
      return wrapper;
    }

    handleUserTyping() {
      if (!this.config?.handoffEnabled || !this.currentSessionId || !this.handoffConnection || this.handoffStatus === "ai") return;
      
      if (!this.isUserTypingSignalSent) {
        this.isUserTypingSignalSent = true;
        this.handoffConnection.invoke("SendUserTyping", this.currentSessionId, true, this.apiKey).catch(console.error);
      }
      
      if (this.userTypingTimeout) clearTimeout(this.userTypingTimeout);
      this.userTypingTimeout = setTimeout(() => {
        this.isUserTypingSignalSent = false;
        if (this.handoffConnection && this.currentSessionId) {
          this.handoffConnection.invoke("SendUserTyping", this.currentSessionId, false, this.apiKey).catch(console.error);
        }
      }, 2000);
    }

    startHandoffPoller() {
      if (!this.config?.handoffEnabled || !this.currentSessionId || this.currentSessionId === "null" || this.currentSessionId === "undefined") return;
      if (this.handoffPoller) return;

      const poll = async () => {
        try {
          const url = new URL(`${this.apiUrl}/api/chat/${this.currentSessionId}/poll`);
          url.searchParams.append("since", this.lastHandoffPollTime);
          
          const response = await fetch(url.toString(), {
            headers: this.getHeaders()
          });
          
          if (!response.ok) return;
          const data = await this.safeJson(response);
          
          if (data.serverTime) {
            this.lastHandoffPollTime = data.serverTime;
          } else {
            this.lastHandoffPollTime = new Date().toISOString();
          }

          if (data.handoffStatus) {
            this.handoffStatus = data.handoffStatus;
          }

          if (data.messages && data.messages.length > 0) {
            data.messages.forEach(msg => {
               if (msg.role === "agent") {
                   const existingMsg = this.shadowRoot.querySelector(`[data-message-id="${msg.id}"]`);
                   if (!existingMsg) {
                       const aiWrapper = this.addMessage("agent", "", null, null, msg.id);
                       const bubble = aiWrapper.querySelector(".message-bubble");
                       bubble.innerHTML = `<div class="message-text-content">${this.formatMarkdown(msg.content)}</div>`;
                       this.scrollToBottom();
                   }
               }
            });
          }

          if (data.handoffStatus === "ai" || data.handoffStatus === "resolved") {
            this.handoffStatus = "ai";
            this.stopHandoffPoller();
          }

        } catch(e) {
          console.error("Handoff polling error:", e);
        }
      };

      poll();
      this.handoffPoller = setInterval(poll, 3000);
    }

    stopHandoffPoller() {
      if (this.handoffPoller) {
        clearInterval(this.handoffPoller);
        this.handoffPoller = null;
      }
    }

    async loadSessionMessages(sessionId) {
      this.stopHandoffConnection();
      this.currentSessionId = sessionId;
      localStorage.setItem("ai_chat_session_id", sessionId);
      const projectId = this.getAttribute("project-id") || localStorage.getItem("ai_chat_project_id");
      if (projectId) localStorage.setItem("ai_chat_project_id", projectId);
      
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
          const role = (m.Role || m.role || "").toLowerCase();
          const isAi = role === "ai" || role === "assistant";
          const isTool = role === "tool";
          const rawContent = m.Content || m.content || "";
          
          // Try to detect if the content is a JSON tool result
          let toolData = m.ToolResult || m.toolResult;
          let wasParsedAsTool = false;

          if (!toolData && rawContent.trim().startsWith('{')) {
            try {
              const parsed = JSON.parse(rawContent);
              if (parsed.toolName || parsed.ToolName) {
                toolData = parsed;
                wasParsedAsTool = true;
              }
            } catch(e) {}
          }

          if (isTool || wasParsedAsTool) {
             const toolName = toolData?.toolName || toolData?.ToolName;
             const isDbTool = toolName === 'query_project_database' || toolName === 'query_database' || toolName === 'query_data';
             const hasResult = toolData?.result || toolData?.Result;
             
             if (isDbTool) {
                if (!hasResult) return; // Skip empty/null database results in history
                
                const msgWrap = this.addMessage("ai", `<div class="message-text-content"></div><div class="message-widget-container"></div>`, null, null, m.Id || m.id);
                const widgetContainer = msgWrap.querySelector(".message-widget-container");
                this.renderDataResult(toolData.result || toolData.Result, widgetContainer);
                return; 
             }
             
             if (isTool && !wasParsedAsTool) return; // Skip non-DB raw tool messages if they aren't parsed
          }

          const content = this.formatMarkdown(rawContent);
          const fileId = m.AttachedFileId || m.attachedFileId;
          const fileName = m.AttachedFileName || m.attachedFileName;
          const img = m.ImageDataUrl || m.imageDataUrl;
          
          const displayHtml = img ? `<div class="message-image-container"><img src="${img}" class="message-image"></div>` + content : content;
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
    async loadExternalScripts() {
      const scripts = [
        { id: 'marked-js', url: 'https://cdn.jsdelivr.net/npm/marked/marked.min.js' },
        { id: 'hljs-js', url: 'https://cdnjs.cloudflare.com/ajax/libs/highlight.js/11.9.0/highlight.min.js' },
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
          script.onerror = resolve; // Continue even if one fails
          document.head.appendChild(script);
        });
      });

      await Promise.all(promises);

      // Add highlight.js theme if not present
      if (!document.getElementById('hljs-theme')) {
        const link = document.createElement('link');
        link.id = 'hljs-theme';
        link.rel = 'stylesheet';
        link.href = 'https://cdnjs.cloudflare.com/ajax/libs/highlight.js/11.9.0/styles/github-dark.min.css';
        document.head.appendChild(link);
      }
    }

    formatMarkdown(text) {
      if (!text) return "";
      
      try {
        if (window.marked) {
          // Configure marked on every call to ensure options are set (or once if you prefer)
          window.marked.setOptions({
            highlight: (code, lang) => {
              if (window.hljs) {
                if (lang && window.hljs.getLanguage(lang)) {
                  return window.hljs.highlight(code, { language: lang }).value;
                }
                return window.hljs.highlightAuto(code).value;
              }
              return code;
            },
            breaks: true,
            gfm: true,
            headerIds: false,
            mangle: false
          });
          
          let html = window.marked.parse(text);
          
          // Re-apply the code block wrapper logic for the copy button
          // Marked generates <pre><code class="language-x">...</code></pre>
          const tempDiv = document.createElement('div');
          tempDiv.innerHTML = html;
          
          tempDiv.querySelectorAll('pre code').forEach(codeEl => {
            const pre = codeEl.parentElement;
            const code = codeEl.innerText;
            const id = 'code-' + Math.random().toString(36).substr(2, 9);
            const langMatch = codeEl.className.match(/language-(\w+)/);
            const lang = langMatch ? langMatch[1] : 'code';
            
            const wrapper = document.createElement('div');
            wrapper.className = 'code-block-wrapper';
            wrapper.innerHTML = `
              <div class="code-header">
                <span>${lang.toUpperCase()}</span>
                <button class="copy-code-btn" data-code-id="${id}">${this.icons.copy} Copy</button>
              </div>
              <pre><code id="${id}" class="${codeEl.className}">${codeEl.innerHTML}</code></pre>
            `;
            pre.parentNode.replaceChild(wrapper, pre);
          });
          
          return tempDiv.innerHTML;
        }
      } catch (e) {
        console.warn("Markdown parsing failed, falling back to basic rendering", e);
      }

      // Fallback to basic rendering if marked is not available or fails
      let html = text.replace(/\*\*(.*?)\*\*/g, "<strong>$1</strong>")
                     .replace(/`([^`]+)`/g, "<code>$1</code>")
                     .replace(/\n/g, "<br>");
      
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
      const l = this.shadowRoot.getElementById("live-transcript");
      
      const target = (this.isLive && l) ? l : m;
      
      if (target) {
        target.scrollTop = target.scrollHeight;
        
        // Attach copy listeners to new code blocks
        target.querySelectorAll(".copy-code-btn").forEach(btn => {
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

    renderDataResult(result, container) {
      console.log("[renderDataResult] rendering to:", container, "data:", result);
      if (!result) return;
      
      // Handle both formats: {columns, rows} or {data: []} or raw array
      let rows = result.rows || result.data || (Array.isArray(result) ? result : []);
      let columns = result.columns || [];
      
      if (columns.length === 0 && rows.length > 0) {
        columns = Object.keys(rows[0]);
      }

      if (columns.length === 0) {
        container.innerHTML = '<div class="data-empty">No data returned.</div>';
        return;
      }

      const id = 'data-' + Math.random().toString(36).substr(2, 9);
      container.innerHTML = `
        <div class="data-result-widget" id="${id}">
          <div class="data-tabs">
            <button class="data-tab active" data-tab="table" title="View as Table">${this.icons.list} Table</button>
            <button class="data-tab" data-tab="chart" title="View as Chart">${this.icons.chart} Chart</button>
            <div style="flex:1"></div>
            <div class="data-actions">
              <button class="data-action-btn" data-action="expand" title="Expand View">${this.icons.expand}</button>
              <button class="data-action-btn" data-action="copy" title="Copy to CSV">${this.icons.copy}</button>
              <button class="data-action-btn" data-action="excel" title="Export Excel">${this.icons.excel}</button>
              <button class="data-action-btn" data-action="pdf" title="Export PDF">${this.icons.pdf}</button>
            </div>
          </div>
          <div class="data-content">
            <div class="data-panel active" data-panel="table">
              <div class="table-container">
                <table>
                  <thead>
                    <tr>${columns.map(c => `<th>${c}</th>`).join('')}</tr>
                  </thead>
                  <tbody>
                    ${rows.slice(0, 15).map(row => `
                      <tr>${columns.map(c => `<td>${row[c] !== null ? row[c] : ''}</td>`).join('')}</tr>
                    `).join('')}
                    ${rows.length > 15 ? `<tr><td colspan="${columns.length}" style="text-align:center; font-style:italic; padding: 12px; background: rgba(0,0,0,0.1)">Showing first 15 of ${rows.length} rows</td></tr>` : ''}
                  </tbody>
                </table>
              </div>
            </div>
            <div class="data-panel" data-panel="chart">
              <div class="chart-controls">
                <button class="data-action-btn" data-action="download-chart" title="Download Chart">${this.icons.download || this.icons.save || ''}</button>
                <select class="chart-type-select">
                  <option value="bar">Bar Chart</option>
                  <option value="line">Line Chart</option>
                  <option value="pie">Pie Chart</option>
                  <option value="doughnut">Doughnut</option>
                </select>
              </div>
              <div class="chart-wrapper">
                <canvas class="data-chart-canvas"></canvas>
              </div>
            </div>
          </div>
        </div>
      `;

      const widget = container.querySelector(`#${id}`);
      const tabs = widget.querySelectorAll('.data-tab');
      const panels = widget.querySelectorAll('.data-panel');
      
      tabs.forEach(tab => {
        tab.onclick = () => {
          tabs.forEach(t => t.classList.remove('active'));
          panels.forEach(p => p.classList.remove('active'));
          tab.classList.add('active');
          widget.querySelector(`[data-panel="${tab.dataset.tab}"]`).classList.add('active');
          if (tab.dataset.tab === 'chart') this.initChart(widget, columns, rows);
        };
      });

      widget.querySelectorAll('.data-action-btn').forEach(btn => {
        btn.onclick = () => {
          const action = btn.dataset.action;
          if (action === 'expand') {
            container.classList.toggle('expanded');
            btn.innerHTML = container.classList.contains('expanded') ? this.icons.collapse : this.icons.expand;
            if (widget.querySelector('[data-panel="chart"]').classList.contains('active')) {
              setTimeout(() => this.initChart(widget, columns, rows), 300);
            }
          } else if (action === 'copy') {
            const csv = [columns.join(','), ...rows.map(r => columns.map(c => r[c]).join(','))].join('\n');
            navigator.clipboard.writeText(csv);
            const original = btn.innerHTML;
            btn.innerHTML = this.icons.check;
            setTimeout(() => btn.innerHTML = original, 2000);
          } else if (action === 'download-chart') {
            const canvas = widget.querySelector('.data-chart-canvas');
            const link = document.createElement('a');
            link.download = 'chart.png';
            link.href = canvas.toDataURL('image/png');
            link.click();
          } else {
            this.exportData(action, result);
          }
        };
      });

      widget.querySelector('.chart-type-select').onchange = () => this.initChart(widget, columns, rows);
      this.scrollToBottom();
    }

    initChart(widget, columns, rows) {
      const canvas = widget.querySelector('.data-chart-canvas');
      const type = widget.querySelector('.chart-type-select').value;
      if (!window.Chart) return;

      if (canvas._chart) canvas._chart.destroy();

      const labels = rows.map(r => r[columns[0]]?.toString() || '');
      const datasets = columns.slice(1).filter(c => typeof rows[0][c] === 'number').map((c, i) => ({
        label: c,
        data: rows.map(r => r[c]),
        backgroundColor: `hsla(${(i * 60) % 360}, 70%, 60%, 0.6)`,
        borderColor: `hsla(${(i * 60) % 360}, 70%, 50%, 1)`,
        borderWidth: 1
      }));

      const gridColor = 'rgba(255, 255, 255, 0.1)';
      const textColor = '#94a3b8';

      canvas._chart = new window.Chart(canvas, {
        type: type,
        data: { labels, datasets },
        options: { 
          responsive: true, 
          maintainAspectRatio: false,
          plugins: {
            legend: {
              display: type !== 'pie' && type !== 'doughnut',
              position: 'top',
              labels: { color: textColor, boxWidth: 12, padding: 10, font: { size: 11 } }
            },
            tooltip: {
              backgroundColor: 'rgba(15, 23, 42, 0.9)',
              titleColor: '#fff',
              bodyColor: '#cbd5e1',
              borderColor: 'rgba(99, 102, 241, 0.5)',
              borderWidth: 1,
              padding: 10,
              displayColors: true
            }
          },
          scales: (type === 'pie' || type === 'doughnut') ? {} : {
            x: {
              grid: { display: false },
              ticks: { 
                color: textColor, 
                font: { size: 10 },
                maxRotation: 45,
                minRotation: 45,
                callback: function(value) {
                  const label = this.getLabelForValue(value);
                  return label && label.length > 10 ? label.substr(0, 10) + '...' : label;
                }
              }
            },
            y: {
              grid: { color: gridColor },
              ticks: { color: textColor, font: { size: 10 } }
            }
          }
        }
      });
    }

    async exportData(format, data) {
      try {
        // Extract the actual rows for the backend
        const rows = data.data || data.rows || (Array.isArray(data) ? data : []);
        
        const response = await fetch(`${this.apiUrl}/api/export/${format}`, {
          method: 'POST',
          headers: { ...this.getHeaders(), 'Content-Type': 'application/json' },
          body: JSON.stringify({
            data: rows, // Send as a real array, not a stringified array
            title: "Data Report",
            fileName: `report_${new Date().getTime()}`
          })
        });
        if (response.ok) {
          const blob = await response.blob();
          const url = window.URL.createObjectURL(blob);
          const a = document.createElement('a');
          a.href = url;
          a.download = `report.${format === 'excel' ? 'xlsx' : 'pdf'}`;
          document.body.appendChild(a);
          a.click();
          a.remove();
        } else {
          const error = await response.text();
          console.error('Export failed:', error);
        }
      } catch (err) {
        console.error('Export failed:', err);
      }
    }
  }

  customElements.define("ai-chatbox", AiChatBox);
})();

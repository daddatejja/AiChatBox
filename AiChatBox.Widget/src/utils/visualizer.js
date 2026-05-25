export class LiveVisualizer {
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
    if (!W || !H) {
      // In case element is not loaded yet or has 0 dimensions
      this.animFrame = requestAnimationFrame(() => this.tick());
      return;
    }
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

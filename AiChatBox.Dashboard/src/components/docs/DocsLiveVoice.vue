<template>
    <section id="live-voice" class="doc-section">
        <h2 class="section-title"><i class="pi pi-microphone"></i> Live Voice Mode</h2>
        <p class="section-intro">AiChatBox supports real-time voice conversations powered by Gemini Live and SignalR WebSockets. Users can speak naturally and hear AI responses aloud.</p>

        <h3 class="sub-heading">How It Works</h3>
        <div class="flow-cards">
            <div class="flow-card">
                <i class="pi pi-microphone"></i>
                <strong>Capture</strong>
                <span>Browser captures mic audio via Web Audio API</span>
            </div>
            <div class="flow-arrow"><i class="pi pi-arrow-right"></i></div>
            <div class="flow-card">
                <i class="pi pi-wifi"></i>
                <strong>Stream</strong>
                <span>Audio chunks sent via SignalR WebSocket</span>
            </div>
            <div class="flow-arrow"><i class="pi pi-arrow-right"></i></div>
            <div class="flow-card">
                <i class="pi pi-sparkles"></i>
                <strong>Process</strong>
                <span>Server proxies to Gemini Live API</span>
            </div>
            <div class="flow-arrow"><i class="pi pi-arrow-right"></i></div>
            <div class="flow-card">
                <i class="pi pi-volume-up"></i>
                <strong>Playback</strong>
                <span>Audio response streamed back to browser</span>
            </div>
        </div>

        <h3 class="sub-heading">Enable Live Voice</h3>
        <p class="desc">To enable Live Voice mode for a configuration:</p>
        <ol class="steps-list">
            <li>Go to your <strong>Project → Configuration → Edit</strong></li>
            <li>Enable <strong>"Live Voice"</strong> toggle</li>
            <li>Set your Gemini API key in the configuration's provider settings</li>
            <li>The 🎤 Live button will appear in the widget header automatically</li>
        </ol>

        <h3 class="sub-heading">SignalR Hub</h3>
        <p class="desc">The live audio connection uses a SignalR hub at:</p>
        <div class="code-block">
            <div class="code-header">WebSocket Endpoint</div>
            <pre><code>wss://your-api.com/liveAudioHub</code></pre>
        </div>
        <p class="desc">The hub supports these methods:</p>
        <div class="attr-table">
            <div class="attr-row header"><span>Method</span><span>Direction</span><span>Description</span></div>
            <div class="attr-row"><code>StartSession</code><span>Client → Server</span><span>Initialize a live session with model and voice config</span></div>
            <div class="attr-row"><code>SendAudio</code><span>Client → Server</span><span>Send a chunk of PCM audio data</span></div>
            <div class="attr-row"><code>SendText</code><span>Client → Server</span><span>Send a text message during live session</span></div>
            <div class="attr-row"><code>EndSession</code><span>Client → Server</span><span>End the live session</span></div>
            <div class="attr-row"><code>ReceiveAudio</code><span>Server → Client</span><span>Receive audio response chunk</span></div>
            <div class="attr-row"><code>ReceiveTranscript</code><span>Server → Client</span><span>Receive text transcript</span></div>
        </div>

        <h3 class="sub-heading">Available Voices</h3>
        <div class="voice-grid">
            <div class="voice-card"><strong>Puck</strong><span>Friendly, conversational</span></div>
            <div class="voice-card"><strong>Charon</strong><span>Deep, authoritative</span></div>
            <div class="voice-card"><strong>Kore</strong><span>Warm, professional</span></div>
            <div class="voice-card"><strong>Fenrir</strong><span>Energetic, upbeat</span></div>
        </div>
    </section>
</template>

<style scoped>
.doc-section { margin-bottom: 64px; }
.section-title { display: flex; align-items: center; gap: 10px; font-size: 1.6rem; font-weight: 700; color: var(--p-surface-900); margin-bottom: 12px; }
.section-title .pi { color: var(--p-primary-500); font-size: 1.3rem; }
.section-intro { color: var(--p-surface-500); font-size: 1.05rem; line-height: 1.7; margin-bottom: 32px; max-width: 720px; }
.sub-heading { font-size: 1.15rem; font-weight: 700; color: var(--p-surface-900); margin: 32px 0 8px 0; padding-top: 16px; border-top: 1px solid var(--p-surface-100); }
.desc { color: var(--p-surface-600); font-size: 0.92rem; line-height: 1.6; margin-bottom: 16px; }

.code-block { border: 1px solid var(--p-surface-200); border-radius: 10px; overflow: hidden; margin-bottom: 16px; }
.code-header { padding: 6px 14px; background: var(--p-surface-100); font-size: 0.72rem; font-weight: 600; color: var(--p-surface-500); text-transform: uppercase; letter-spacing: 0.04em; border-bottom: 1px solid var(--p-surface-200); }
.code-block pre { margin: 0; padding: 14px; background: var(--p-surface-900); overflow-x: auto; }
.code-block code { color: var(--p-primary-300); font-size: 0.82rem; }

.flow-cards { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; margin-bottom: 32px; }
.flow-card { flex: 1; min-width: 120px; display: flex; flex-direction: column; align-items: center; gap: 6px; padding: 18px 12px; background: var(--p-surface-50); border: 1px solid var(--p-surface-200); border-radius: 12px; text-align: center; }
.flow-card .pi { font-size: 1.4rem; color: var(--p-primary-500); }
.flow-card strong { font-size: 0.85rem; color: var(--p-surface-900); }
.flow-card span { font-size: 0.75rem; color: var(--p-surface-500); }
.flow-arrow { color: var(--p-surface-300); }

.steps-list { padding-left: 20px; margin-bottom: 24px; }
.steps-list li { color: var(--p-surface-700); font-size: 0.92rem; line-height: 1.8; }

.attr-table { border: 1px solid var(--p-surface-200); border-radius: 10px; overflow: hidden; margin-bottom: 24px; }
.attr-row { display: grid; grid-template-columns: 180px 140px 1fr; padding: 10px 16px; border-bottom: 1px solid var(--p-surface-100); font-size: 0.85rem; color: var(--p-surface-700); align-items: center; }
.attr-row:last-child { border-bottom: none; }
.attr-row.header { background: var(--p-surface-50); font-weight: 700; font-size: 0.75rem; text-transform: uppercase; color: var(--p-surface-500); }
.attr-row code { background: var(--p-surface-100); padding: 2px 6px; border-radius: 4px; font-size: 0.8rem; color: var(--p-primary-600); }

.voice-grid { display: grid; grid-template-columns: repeat(4, 1fr); gap: 12px; }
.voice-card { padding: 16px; background: var(--p-surface-50); border: 1px solid var(--p-surface-200); border-radius: 10px; text-align: center; display: flex; flex-direction: column; gap: 4px; }
.voice-card strong { font-size: 0.95rem; color: var(--p-surface-900); }
.voice-card span { font-size: 0.78rem; color: var(--p-surface-500); }

@media (max-width: 700px) { .voice-grid { grid-template-columns: repeat(2, 1fr); } }
</style>

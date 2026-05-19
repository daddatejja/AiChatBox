<template>
    <section id="changelog" class="doc-section">
        <h2 class="section-title"><i class="pi pi-history"></i> Release Changelog</h2>
        <p class="section-intro">Follow the latest updates, feature releases, and improvements to the AiChatBox platform.</p>

        <div class="changelog-timeline">
            <!-- v1.3.0 -->
            <div class="timeline-item">
                <div class="timeline-badge current">v1.3.0</div>
                <div class="timeline-content">
                    <div class="timeline-header">
                        <h3>Widget Appearance & Theming Engine</h3>
                        <span class="timeline-date">May 2026</span>
                    </div>
                    <p class="timeline-desc">Added dynamic branding and configuration capability allowing developers to customize visual widget setups without touching custom stylesheets.</p>
                    <ul class="timeline-features">
                        <li><span class="badge feat">Feature</span> <strong>Dashboard Theming Panel</strong>: Interactive hex color pickers, font family selectors, and layout alignment toggles in Config Details.</li>
                        <li><span class="badge feat">Feature</span> <strong>Real-time Live Preview</strong>: Visual mock bubble showing changes instantly as custom CSS variables are set.</li>
                        <li><span class="badge perf">Performance</span> <strong>Runtime Element Injections</strong>: Web component handles async theme fetching and dynamic host style updates.</li>
                    </ul>
                </div>
            </div>

            <!-- v1.2.0 -->
            <div class="timeline-item">
                <div class="timeline-badge">v1.2.0</div>
                <div class="timeline-content">
                    <div class="timeline-header">
                        <h3>Visual Conversation Flow Builder</h3>
                        <span class="timeline-date">May 2026</span>
                    </div>
                    <p class="timeline-desc">Introduced a powerful node-based canvas editor to orchestrate structured, deterministic conversation paths alongside dynamic LLM processing.</p>
                    <ul class="timeline-features">
                        <li><span class="badge feat">Feature</span> <strong>Vue Flow Integration</strong>: Drag-and-drop node graph canvas with custom interactive states, triggers, messages, webhooks, and AI prompts.</li>
                        <li><span class="badge feat">Feature</span> <strong>Branching & Conditionals</strong>: Route conversations down separate paths using matches like `equals`, `contains`, and custom `regex`.</li>
                        <li><span class="badge feat">Feature</span> <strong>Context Variables</strong>: Capture user inputs into keys like <code v-pre>{{user_name}}</code> and interpolate them in downstream messages or webhooks.</li>
                    </ul>
                </div>
            </div>

            <!-- v1.1.0 -->
            <div class="timeline-item">
                <div class="timeline-badge">v1.1.0</div>
                <div class="timeline-content">
                    <div class="timeline-header">
                        <h3>Gemini Live Real-time Audio</h3>
                        <span class="timeline-date">April 2026</span>
                    </div>
                    <p class="timeline-desc">Enabled fluid, real-time voice conversations using high-speed audio buffer streaming over WebSockets.</p>
                    <ul class="timeline-features">
                        <li><span class="badge feat">Feature</span> <strong>SignalR Audio Hub</strong>: Ultra-low latency binary voice stream transfer for direct full-duplex conversations.</li>
                        <li><span class="badge feat">Feature</span> <strong>Orb UI Animation</strong>: Fluid glassmorphic orb rendering that pulses, vibrates, and breathes reactively based on audio input signals.</li>
                        <li><span class="badge fix">Fix</span> <strong>Audio Session Locks</strong>: Corrected `ObjectDisposedException` and DbContext scope leaks during live background queries.</li>
                    </ul>
                </div>
            </div>

            <!-- v1.0.0 -->
            <div class="timeline-item">
                <div class="timeline-badge">v1.0.0</div>
                <div class="timeline-content">
                    <div class="timeline-header">
                        <h3>Core Platform Release</h3>
                        <span class="timeline-date">March 2026</span>
                    </div>
                    <p class="timeline-desc">Initial public release of the enterprise-ready conversational AI orchestration engine.</p>
                    <ul class="timeline-features">
                        <li><span class="badge feat">Feature</span> <strong>Configuration Profiles</strong>: Segment model choices (Gemini, Groq), system prompts, and custom API access keys.</li>
                        <li><span class="badge feat">Feature</span> <strong>Dynamic Analytics</strong>: Multi-metric sales grids, performance comparison charts, and user thumbs-up/down message logging.</li>
                        <li><span class="badge feat">Feature</span> <strong>Custom Extensible Tools</strong>: Create JSON schemas for database and HTTP API triggers that LLM models execute automatically.</li>
                    </ul>
                </div>
            </div>
        </div>
    </section>
</template>

<style scoped>
.doc-section { margin-bottom: 64px; }
.section-title { display: flex; align-items: center; gap: 10px; font-size: 1.6rem; font-weight: 700; color: var(--p-surface-900); margin-bottom: 12px; }
.section-title .pi { color: var(--p-primary-500); font-size: 1.3rem; }
.section-intro { color: var(--p-surface-500); font-size: 1.05rem; line-height: 1.7; margin-bottom: 32px; max-width: 720px; }

.changelog-timeline {
    position: relative;
    padding-left: 32px;
    border-left: 2px solid var(--p-surface-200);
    display: flex;
    flex-direction: column;
    gap: 40px;
}

.timeline-item {
    position: relative;
}

.timeline-badge {
    position: absolute;
    left: -48px;
    top: 0;
    width: 32px;
    height: 32px;
    border-radius: 50%;
    background: var(--p-surface-100);
    border: 2px solid var(--p-surface-300);
    display: flex;
    align-items: center;
    justify-content: center;
    font-size: 0.65rem;
    font-weight: 800;
    color: var(--p-surface-600);
    transition: all 0.2s ease;
}

.timeline-badge.current {
    background: linear-gradient(135deg, var(--p-primary-500), var(--p-primary-600));
    border-color: var(--p-primary-500);
    color: white;
    box-shadow: 0 0 0 4px var(--p-primary-50);
}

.timeline-content {
    background: var(--p-surface-50);
    border: 1px solid var(--p-surface-200);
    border-radius: 12px;
    padding: 24px;
    transition: transform 0.2s ease, border-color 0.2s ease;
}

.timeline-content:hover {
    border-color: var(--p-primary-300);
    transform: translateY(-2px);
}

.timeline-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 12px;
    flex-wrap: wrap;
    gap: 8px;
}

.timeline-header h3 {
    margin: 0;
    font-size: 1.2rem;
    font-weight: 700;
    color: var(--p-surface-900);
}

.timeline-date {
    font-size: 0.8rem;
    font-weight: 600;
    color: var(--p-surface-400);
    text-transform: uppercase;
    letter-spacing: 0.05em;
}

.timeline-desc {
    margin: 0 0 16px 0;
    font-size: 0.92rem;
    color: var(--p-surface-600);
    line-height: 1.6;
}

.timeline-features {
    list-style: none;
    padding: 0;
    margin: 0;
    display: flex;
    flex-direction: column;
    gap: 10px;
}

.timeline-features li {
    font-size: 0.85rem;
    color: var(--p-surface-700);
    display: flex;
    align-items: flex-start;
    gap: 10px;
    line-height: 1.5;
}

.badge {
    padding: 2px 6px;
    border-radius: 4px;
    font-size: 0.65rem;
    font-weight: 800;
    text-transform: uppercase;
    letter-spacing: 0.05em;
    flex-shrink: 0;
    margin-top: 1px;
}

.badge.feat { background: #dbeafe; color: #1e40af; }
.badge.fix { background: #fee2e2; color: #991b1b; }
.badge.perf { background: #e0f2fe; color: #0369a1; }
</style>

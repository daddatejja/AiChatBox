<template>
    <section id="getting-started" class="doc-section">
        <h2 class="section-title">
            <i class="pi pi-bolt"></i> Getting Started
        </h2>
        <p class="section-intro">Get up and running with AiChatBox in under 5 minutes. Follow these steps to create your first AI-powered assistant.</p>

        <div class="steps-grid">
            <div class="step-card">
                <div class="step-number">1</div>
                <h3>Create a Project</h3>
                <p>Navigate to the <strong>Projects</strong> page and click <strong>"Create Project"</strong>. Each project acts as a container for your bot configurations, API keys, and custom tools.</p>
                <div class="step-tip">
                    <i class="pi pi-info-circle"></i>
                    <span>Projects allow you to manage multiple AI assistants from one dashboard — e.g. a support bot and an internal tool.</span>
                </div>
            </div>

            <div class="step-card">
                <div class="step-number">2</div>
                <h3>Add a Configuration</h3>
                <p>Inside your project, create a <strong>Configuration</strong>. This defines the AI provider (Gemini/Groq), model, and system prompt. Think of configurations as environments — you might have "Production" and "Staging".</p>
                <div class="code-block">
                    <div class="code-header">Example System Prompt</div>
                    <pre><code>You are a helpful customer support assistant for Acme Corp.
You can help users with orders, returns, and product questions.
Always be friendly and concise.</code></pre>
                </div>
            </div>

            <div class="step-card">
                <div class="step-number">3</div>
                <h3>Generate an API Key</h3>
                <p>In the <strong>API Access Keys</strong> section, generate a key linked to your configuration. You'll use this key to authenticate the widget or API calls.</p>
                <div class="warning-box">
                    <i class="pi pi-exclamation-triangle"></i>
                    <div>
                        <strong>Security Note:</strong> Copy the key immediately — it won't be shown again. Restrict usage via <strong>Allowed Domains</strong> in your project settings.
                    </div>
                </div>
            </div>

            <div class="step-card">
                <div class="step-number">4</div>
                <h3>Embed the Widget</h3>
                <p>Add two lines to your HTML to deploy the chat widget. See the <a href="#widget">Widget Integration</a> section for full details.</p>
                <div class="code-block">
                    <div class="code-header">Quick Start — HTML</div>
                    <pre><code>&lt;script src="https://your-api-domain.com/widget/ai-chatbox.js"&gt;&lt;/script&gt;
&lt;ai-chatbox api-key="acb_your_key_here" api-url="https://your-api-domain.com"&gt;&lt;/ai-chatbox&gt;</code></pre>
                </div>
            </div>
        </div>

        <div class="arch-overview">
            <h3>Architecture Overview</h3>
            <div class="arch-cards">
                <div class="arch-card">
                    <i class="pi pi-globe"></i>
                    <strong>Widget</strong>
                    <span>Web Component embedded in your site</span>
                </div>
                <div class="arch-arrow"><i class="pi pi-arrow-right"></i></div>
                <div class="arch-card">
                    <i class="pi pi-server"></i>
                    <strong>API Server</strong>
                    <span>Handles auth, routing, tool orchestration</span>
                </div>
                <div class="arch-arrow"><i class="pi pi-arrow-right"></i></div>
                <div class="arch-card">
                    <i class="pi pi-sparkles"></i>
                    <strong>AI Provider</strong>
                    <span>Gemini or Groq for inference</span>
                </div>
            </div>
        </div>
    </section>
</template>

<style scoped>
.doc-section { margin-bottom: 64px; }

.section-title {
    display: flex;
    align-items: center;
    gap: 10px;
    font-size: 1.6rem;
    font-weight: 700;
    color: var(--p-surface-900);
    margin-bottom: 12px;
}

.section-title .pi { color: var(--p-primary-500); font-size: 1.3rem; }

.section-intro {
    color: var(--p-surface-500);
    font-size: 1.05rem;
    line-height: 1.7;
    margin-bottom: 32px;
    max-width: 720px;
}

.steps-grid {
    display: grid;
    grid-template-columns: 1fr;
    gap: 20px;
    margin-bottom: 40px;
}

@media (max-width: 900px) {
    .steps-grid { grid-template-columns: 1fr; }
}

.step-card {
    background: var(--p-surface-50);
    border: 1px solid var(--p-surface-200);
    border-radius: 14px;
    padding: 24px;
    position: relative;
}

.step-number {
    width: 28px;
    height: 28px;
    background: linear-gradient(135deg, var(--p-primary-500), var(--p-primary-600));
    color: white;
    border-radius: 50%;
    display: flex;
    align-items: center;
    justify-content: center;
    font-size: 0.8rem;
    font-weight: 700;
    margin-bottom: 12px;
}

.step-card h3 {
    font-size: 1.05rem;
    font-weight: 700;
    color: var(--p-surface-900);
    margin-bottom: 8px;
}

.step-card p {
    color: var(--p-surface-600);
    font-size: 0.9rem;
    line-height: 1.6;
    margin: 0 0 12px 0;
}

.step-tip {
    display: flex;
    gap: 8px;
    padding: 10px 14px;
    background: var(--p-primary-50);
    border-radius: 8px;
    font-size: 0.82rem;
    color: var(--p-primary-700);
    line-height: 1.5;
}

.step-tip .pi { margin-top: 2px; font-size: 0.85rem; }

.warning-box {
    display: flex;
    gap: 10px;
    padding: 12px 14px;
    background: #fef3cd;
    border-radius: 8px;
    font-size: 0.82rem;
    color: #856404;
    line-height: 1.5;
}

.warning-box .pi { margin-top: 2px; }

.code-block {
    border: 1px solid var(--p-surface-200);
    border-radius: 10px;
    overflow: hidden;
    margin-top: 8px;
}

.code-header {
    padding: 6px 14px;
    background: var(--p-surface-100);
    font-size: 0.72rem;
    font-weight: 600;
    color: var(--p-surface-500);
    text-transform: uppercase;
    letter-spacing: 0.04em;
    border-bottom: 1px solid var(--p-surface-200);
}

.code-block pre {
    margin: 0;
    padding: 14px;
    background: var(--p-surface-900);
    overflow-x: auto;
}

.code-block code {
    color: var(--p-primary-300);
    font-size: 0.82rem;
}

/* Architecture */
.arch-overview h3 {
    font-size: 1.1rem;
    margin-bottom: 16px;
    color: var(--p-surface-900);
}

.arch-cards {
    display: flex;
    align-items: center;
    gap: 12px;
    flex-wrap: wrap;
}

.arch-card {
    flex: 1;
    min-width: 140px;
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 6px;
    padding: 20px;
    background: var(--p-surface-50);
    border: 1px solid var(--p-surface-200);
    border-radius: 12px;
    text-align: center;
}

.arch-card .pi { font-size: 1.5rem; color: var(--p-primary-500); }
.arch-card strong { font-size: 0.9rem; color: var(--p-surface-900); }
.arch-card span { font-size: 0.78rem; color: var(--p-surface-500); }
.arch-arrow { color: var(--p-surface-300); font-size: 1.2rem; }
</style>

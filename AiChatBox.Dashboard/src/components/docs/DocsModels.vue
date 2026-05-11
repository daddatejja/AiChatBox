<template>
    <section id="models" class="doc-section">
        <h2 class="section-title"><i class="pi pi-cog"></i> Models & Configuration</h2>
        <p class="section-intro">AiChatBox supports multiple AI providers and models. Choose the best model for your use case — from fast lightweight responses to advanced reasoning.</p>

        <h3 class="sub-heading">Available Providers</h3>
        <div class="provider-grid">
            <div class="provider-card">
                <div class="provider-header">
                    <i class="pi pi-google"></i>
                    <strong>Google Gemini</strong>
                </div>
                <p>Google's multimodal AI models. Supports text, images, code, and live voice. Best for most use cases.</p>
                <div class="model-list">
                    <div class="model-item">
                        <code>gemini-2.5-flash</code>
                        <span class="model-tag fast">Fast</span>
                        <span>Best balance of speed and quality</span>
                    </div>
                    <div class="model-item">
                        <code>gemini-2.5-pro</code>
                        <span class="model-tag pro">Pro</span>
                        <span>Advanced reasoning, longer context</span>
                    </div>
                    <div class="model-item">
                        <code>gemini-2.5-flash-lite</code>
                        <span class="model-tag lite">Lite</span>
                        <span>Fastest, most cost-effective</span>
                    </div>
                    <div class="model-item">
                        <code>gemini-3-flash</code>
                        <span class="model-tag new">New</span>
                        <span>Latest generation, improved capabilities</span>
                    </div>
                    <div class="model-item">
                        <code>gemini-3.1-flash-lite-preview</code>
                        <span class="model-tag new">Preview</span>
                        <span>Cutting-edge preview model</span>
                    </div>
                </div>
            </div>

            <div class="provider-card">
                <div class="provider-header">
                    <i class="pi pi-bolt"></i>
                    <strong>Groq</strong>
                </div>
                <p>Ultra-fast inference with open-source models. Great for high-throughput, latency-sensitive applications.</p>
                <div class="model-list">
                    <div class="model-item">
                        <code>llama-3.3-70b-versatile</code>
                        <span class="model-tag pro">70B</span>
                        <span>Powerful general-purpose model</span>
                    </div>
                    <div class="model-item">
                        <code>llama-3.1-8b-instant</code>
                        <span class="model-tag fast">8B</span>
                        <span>Ultra-fast responses</span>
                    </div>
                    <div class="model-item">
                        <code>mixtral-8x7b-32768</code>
                        <span class="model-tag fast">MoE</span>
                        <span>Mixture of Experts, 32k context</span>
                    </div>
                </div>
            </div>
        </div>

        <h3 class="sub-heading">Configuration Environments</h3>
        <p class="desc">Configurations let you manage different environments or personas within the same project. Common patterns:</p>
        <div class="env-examples">
            <div class="env-card">
                <strong>Production</strong>
                <span>Gemini 2.5 Flash — balanced speed & quality with strict system prompt</span>
            </div>
            <div class="env-card">
                <strong>Staging</strong>
                <span>Gemini 3 Flash — test latest models before production rollout</span>
            </div>
            <div class="env-card">
                <strong>Internal</strong>
                <span>Gemini 2.5 Pro — advanced reasoning for internal team tools</span>
            </div>
        </div>

        <h3 class="sub-heading">Allowed Domains</h3>
        <p class="desc">Restrict which domains can use your API key. Set in <strong>Project Settings → Allowed Domains</strong>:</p>
        <div class="code-block">
            <div class="code-header">Examples</div>
            <pre><code># Single domain
example.com

# Multiple domains (comma-separated)
example.com, app.example.com, staging.example.com

# Wildcard (all subdomains)
*.example.com

# Allow all (development only — NOT recommended for production)
*</code></pre>
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

.provider-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 20px; margin-bottom: 24px; }
.provider-card { padding: 24px; background: var(--p-surface-50); border: 1px solid var(--p-surface-200); border-radius: 14px; }
.provider-header { display: flex; align-items: center; gap: 10px; margin-bottom: 8px; }
.provider-header .pi { font-size: 1.2rem; color: var(--p-primary-500); }
.provider-header strong { font-size: 1.1rem; color: var(--p-surface-900); }
.provider-card > p { font-size: 0.88rem; color: var(--p-surface-600); margin: 0 0 16px 0; line-height: 1.5; }

.model-list { display: flex; flex-direction: column; gap: 8px; }
.model-item { display: flex; align-items: center; gap: 8px; padding: 8px 12px; background: var(--p-surface-0); border: 1px solid var(--p-surface-100); border-radius: 8px; font-size: 0.82rem; }
.model-item code { background: var(--p-surface-100); padding: 2px 6px; border-radius: 4px; color: var(--p-primary-600); font-size: 0.78rem; white-space: nowrap; }
.model-item span:last-child { color: var(--p-surface-500); flex: 1; }
.model-tag { padding: 2px 6px; border-radius: 4px; font-size: 0.65rem; font-weight: 700; text-transform: uppercase; white-space: nowrap; }
.model-tag.fast { background: #d1fae5; color: #065f46; }
.model-tag.pro { background: #dbeafe; color: #1e40af; }
.model-tag.lite { background: #fef3c7; color: #92400e; }
.model-tag.new { background: #ede9fe; color: #5b21b6; }

.env-examples { display: flex; flex-direction: column; gap: 10px; margin-bottom: 24px; }
.env-card { padding: 14px 18px; background: var(--p-surface-50); border: 1px solid var(--p-surface-200); border-radius: 10px; display: flex; gap: 12px; align-items: baseline; }
.env-card strong { font-size: 0.9rem; color: var(--p-surface-900); min-width: 100px; }
.env-card span { font-size: 0.85rem; color: var(--p-surface-600); }

@media (max-width: 800px) { .provider-grid { grid-template-columns: 1fr; } }
</style>

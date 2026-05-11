<template>
    <section id="widget" class="doc-section">
        <h2 class="section-title"><i class="pi pi-code"></i> Widget Integration</h2>
        <p class="section-intro">The AiChatBox widget is a Web Component that you drop into any HTML page. It handles chat UI, voice input, file uploads, live mode, and tool execution out of the box.</p>

        <h3 class="sub-heading">Basic Integration</h3>
        <p class="desc">Add these two lines to your HTML — that's all you need for a fully functional AI chat:</p>
        <div class="code-block">
            <div class="code-header">HTML</div>
            <pre><code>&lt;!-- 1. Load the widget script --&gt;
&lt;script src="https://your-api.com/widget/ai-chatbox.js"&gt;&lt;/script&gt;

&lt;!-- 2. Place the component --&gt;
&lt;ai-chatbox
    api-key="acb_your_api_key"
    api-url="https://your-api.com"
    user-id="user_123"&gt;
&lt;/ai-chatbox&gt;</code></pre>
        </div>

        <h3 class="sub-heading">Widget Attributes</h3>
        <p class="desc">Configure the widget behavior via HTML attributes:</p>
        <div class="attr-table">
            <div class="attr-row header">
                <span>Attribute</span><span>Type</span><span>Description</span>
            </div>
            <div class="attr-row"><code>api-key</code><span>string</span><span>Your project API key (starts with <code>acb_</code>)</span></div>
            <div class="attr-row"><code>api-url</code><span>string</span><span>Base URL of your AiChatBox API server</span></div>
            <div class="attr-row"><code>user-id</code><span>string</span><span>Unique identifier for the end user (for session tracking)</span></div>
            <div class="attr-row"><code>provider</code><span>string</span><span>AI provider: <code>"gemini"</code> or <code>"groq"</code></span></div>
            <div class="attr-row"><code>model</code><span>string</span><span>Model name, e.g. <code>"gemini-2.5-flash"</code></span></div>
            <div class="attr-row"><code>suggestions</code><span>JSON array</span><span>Quick-reply suggestions shown on empty state</span></div>
            <div class="attr-row"><code>css-path</code><span>string</span><span>Custom CSS file URL to override widget styles</span></div>
            <div class="attr-row"><code>title</code><span>string</span><span>Custom title shown in the widget header</span></div>
            <div class="attr-row"><code>auth-token</code><span>string</span><span>JWT token (alternative to api-key for dashboard use)</span></div>
            <div class="attr-row"><code>project-id</code><span>string</span><span>Project UUID (used with auth-token)</span></div>
            <div class="attr-row"><code>configuration-id</code><span>string</span><span>Configuration UUID (used with auth-token)</span></div>
        </div>

        <h3 class="sub-heading">Full Example with Suggestions</h3>
        <div class="code-block">
            <div class="code-header">HTML — Complete Setup</div>
            <pre><code>&lt;!DOCTYPE html&gt;
&lt;html lang="en"&gt;
&lt;head&gt;
    &lt;meta charset="UTF-8"&gt;
    &lt;title&gt;My App&lt;/title&gt;
&lt;/head&gt;
&lt;body&gt;
    &lt;h1&gt;Welcome to My App&lt;/h1&gt;

    &lt;!-- AiChatBox Widget --&gt;
    &lt;ai-chatbox
        api-key="acb_aMz8LIH1lqc0jRZs97oafzMPXr1ci0sR"
        api-url="https://api.yoursite.com"
        user-id="visitor_001"
        provider="gemini"
        model="gemini-2.5-flash"
        title="Acme Support"
        suggestions='["Track my order", "Return policy", "Talk to an agent"]'&gt;
    &lt;/ai-chatbox&gt;

    &lt;script src="https://api.yoursite.com/widget/ai-chatbox.js"&gt;&lt;/script&gt;
&lt;/body&gt;
&lt;/html&gt;</code></pre>
        </div>

        <h3 class="sub-heading">Widget Events</h3>
        <p class="desc">The widget emits custom DOM events you can listen to:</p>
        <div class="code-block">
            <div class="code-header">JavaScript — Event Handling</div>
            <pre><code>const widget = document.querySelector('ai-chatbox');

// Listen for tool calls (when no handler is registered)
widget.addEventListener('tool-call', (e) => {
    console.log('Tool called:', e.detail.name);
    console.log('Arguments:', e.detail.args);
    console.log('Call ID:', e.detail.callId);

    // Process the tool call and return result
    const result = { status: "success", data: "..." };
    widget.submitToolResult(e.detail.callId, result);
});</code></pre>
        </div>
    </section>
</template>

<style scoped>
.doc-section { margin-bottom: 64px; }

.section-title {
    display: flex; align-items: center; gap: 10px;
    font-size: 1.6rem; font-weight: 700; color: var(--p-surface-900); margin-bottom: 12px;
}
.section-title .pi { color: var(--p-primary-500); font-size: 1.3rem; }

.section-intro {
    color: var(--p-surface-500); font-size: 1.05rem; line-height: 1.7; margin-bottom: 32px; max-width: 720px;
}

.sub-heading {
    font-size: 1.15rem; font-weight: 700; color: var(--p-surface-900);
    margin: 32px 0 8px 0; padding-top: 16px; border-top: 1px solid var(--p-surface-100);
}

.desc { color: var(--p-surface-600); font-size: 0.92rem; line-height: 1.6; margin-bottom: 16px; }

.code-block {
    border: 1px solid var(--p-surface-200); border-radius: 10px; overflow: hidden; margin-bottom: 24px;
}
.code-header {
    padding: 6px 14px; background: var(--p-surface-100); font-size: 0.72rem; font-weight: 600;
    color: var(--p-surface-500); text-transform: uppercase; letter-spacing: 0.04em;
    border-bottom: 1px solid var(--p-surface-200);
}
.code-block pre { margin: 0; padding: 14px; background: var(--p-surface-900); overflow-x: auto; }
.code-block code { color: var(--p-primary-300); font-size: 0.82rem; }

.attr-table {
    border: 1px solid var(--p-surface-200); border-radius: 10px; overflow: hidden; margin-bottom: 24px;
}
.attr-row {
    display: grid; grid-template-columns: 180px 100px 1fr; padding: 10px 16px;
    border-bottom: 1px solid var(--p-surface-100); font-size: 0.85rem; color: var(--p-surface-700);
    align-items: center; gap: 8px;
}
.attr-row:last-child { border-bottom: none; }
.attr-row.header {
    background: var(--p-surface-50); font-weight: 700; font-size: 0.75rem;
    text-transform: uppercase; color: var(--p-surface-500); letter-spacing: 0.04em;
}
.attr-row code {
    background: var(--p-surface-100); padding: 2px 6px; border-radius: 4px;
    font-size: 0.8rem; color: var(--p-primary-600);
}
</style>

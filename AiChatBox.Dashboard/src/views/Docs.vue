<script setup lang="ts">
import { ref, computed } from 'vue';
import Button from 'primevue/button';
import Card from 'primevue/card';

const activeSection = ref('quickstart');

const sections = [
    { id: 'quickstart', label: 'Quickstart' },
    { id: 'widget', label: 'Widget Integration' },
    { id: 'tools', label: 'Tool Calls' },
    { id: 'rest-api', label: 'REST API Reference' },
    { id: 'models', label: 'Available Models' }
];

const scrollTo = (id: string) => {
    activeSection.value = id;
    document.getElementById(id)?.scrollIntoView({ behavior: 'smooth' });
};
</script>

<template>
    <div class="docs-layout">
        <!-- Sidebar -->
        <aside class="docs-sidebar">
            <div class="logo">
                <div class="logo-icon"></div>
                <span>AiChatBox Docs</span>
            </div>
            <nav class="sidebar-nav">
                <div v-for="s in sections" :key="s.id" 
                     class="nav-item" 
                     :class="{ active: activeSection === s.id }"
                     @click="scrollTo(s.id)">
                    {{ s.label }}
                </div>
            </nav>
            <div class="sidebar-footer">
                <router-link to="/">Back to Dashboard</router-link>
            </div>
        </aside>

        <!-- Main Content -->
        <main class="docs-content">
            <div id="quickstart" class="content-section">
                <h1>Quickstart</h1>
                <p>Get up and running with AiChatBox in minutes. AiChatBox provides both a ready-to-use UI widget and a powerful REST API.</p>
                
                <div class="info-card">
                    <h3>1. Create a Project</h3>
                    <p>Head to the dashboard and create a new project. Each project can have multiple configurations (persona, model, etc.).</p>
                </div>

                <div class="info-card">
                    <h3>2. Generate an API Key</h3>
                    <p>Within your project, generate an API key. **Important:** Keep this key secure and restrict it via domain whitelisting in the project settings.</p>
                </div>
            </div>

            <div id="widget" class="content-section">
                <h2>Widget Integration</h2>
                <p>The easiest way to add AI to your site is via our Web Component. It supports real-time voice, file uploads, and custom tools.</p>
                
                <div class="code-container">
                    <div class="code-header">HTML Integration</div>
                    <pre class="code-block"><code>&lt;!-- Load assets from your hosted API --&gt;
&lt;link rel="stylesheet" href="https://api.yoursite.com/ai-chatbox.css"&gt;
&lt;script type="module" src="https://api.yoursite.com/ai-chatbox.js"&gt;&lt;/script&gt;

&lt;!-- Initialize --&gt;
&lt;ai-chatbox 
    api-key="acb_your_key"
    user-id="unique_user_id"
    suggestions='["Order Status", "Pricing"]'&gt;
&lt;/ai-chatbox&gt;</code></pre>
                </div>
            </div>

            <div id="tools" class="content-section">
                <h2>Tool Calls (Function Calling)</h2>
                <p>AiChatBox can call functions in your own application. Define the tool schema in the dashboard, and handle the execution in your frontend.</p>
                
                <div class="code-container">
                    <div class="code-header">JavaScript Tool Handling</div>
                    <pre class="code-block"><code>const widget = document.querySelector('ai-chatbox');

// Register a handler for 'get_inventory' tool
widget.registerTool('get_inventory', async (args) => {
    const res = await fetch(`/api/inventory/${args.productId}`);
    return await res.json();
});</code></pre>
                </div>
            </div>

            <div id="rest-api" class="content-section">
                <h2>REST API Reference</h2>
                <p>Use the REST API for custom integrations or backend-to-backend communication. Our API is OpenAI-compatible.</p>
                
                <Card class="endpoint-card">
                    <template #content>
                        <div class="endpoint">
                            <span class="badge post">POST</span>
                            <code>/api/chat/completions</code>
                        </div>
                        <p class="description">Send a message to the AI and get a completion response.</p>
                    </template>
                </Card>
            </div>
        </main>

        <!-- Code/Reference Column -->
        <aside class="docs-reference">
            <div class="sticky-ref">
                <h3>Try it with Curl</h3>
                <pre class="ref-code"><code>curl https://api.yoursite.com/api/chat/completions \
  -H "Authorization: Bearer YOUR_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "messages": [
      {"role": "user", "content": "Hello!"}
    ]
  }'</code></pre>
            </div>
        </aside>
    </div>
</template>

<style scoped>
.docs-layout {
    display: flex;
    min-height: 100vh;
    background-color: var(--p-surface-950);
    color: var(--p-surface-0);
}

/* Sidebar */
.docs-sidebar {
    width: 280px;
    border-right: 1px solid var(--p-surface-800);
    display: flex;
    flex-direction: column;
    padding: 24px;
    position: fixed;
    height: 100vh;
}

.logo {
    display: flex;
    align-items: center;
    gap: 12px;
    margin-bottom: 48px;
    font-weight: 700;
    font-size: 1.1rem;
}

.logo-icon {
    width: 28px;
    height: 28px;
    background: linear-gradient(135deg, var(--p-primary-500), var(--p-primary-700));
    border-radius: 8px;
}

.sidebar-nav {
    flex: 1;
}

.nav-item {
    padding: 10px 16px;
    border-radius: 8px;
    cursor: pointer;
    color: var(--p-surface-400);
    transition: all 0.2s;
    font-size: 0.95rem;
}

.nav-item:hover {
    color: var(--p-surface-0);
    background-color: var(--p-surface-900);
}

.nav-item.active {
    color: var(--p-primary-400);
    background-color: var(--p-primary-950);
    font-weight: 600;
}

.sidebar-footer {
    padding-top: 24px;
    border-top: 1px solid var(--p-surface-800);
}

.sidebar-footer a {
    color: var(--p-surface-400);
    text-decoration: none;
    font-size: 0.85rem;
}

/* Content */
.docs-content {
    margin-left: 280px;
    flex: 1;
    padding: 64px 48px;
    max-width: 800px;
}

.content-section {
    margin-bottom: 80px;
}

h1 { font-size: 2.5rem; margin-bottom: 24px; }
h2 { font-size: 1.75rem; margin-top: 48px; margin-bottom: 16px; }

p {
    color: var(--p-surface-300);
    line-height: 1.7;
    margin-bottom: 24px;
}

.info-card {
    background-color: var(--p-surface-900);
    border: 1px solid var(--p-surface-800);
    border-radius: 12px;
    padding: 20px;
    margin-bottom: 16px;
}

.info-card h3 { margin-top: 0; font-size: 1.1rem; color: var(--p-primary-400); }
.info-card p { margin-bottom: 0; font-size: 0.95rem; }

/* Code Blocks */
.code-container {
    margin: 24px 0;
    border: 1px solid var(--p-surface-800);
    border-radius: 12px;
    overflow: hidden;
}

.code-header {
    background-color: var(--p-surface-900);
    padding: 8px 16px;
    font-size: 0.8rem;
    color: var(--p-surface-400);
    border-bottom: 1px solid var(--p-surface-800);
    font-family: monospace;
}

.code-block {
    margin: 0;
    padding: 24px;
    background-color: var(--p-surface-950);
    overflow-x: auto;
    font-family: 'JetBrains Mono', monospace;
    font-size: 0.9rem;
}

.code-block code { color: var(--p-primary-300); }

/* Endpoint Cards */
.endpoint-card {
    background-color: var(--p-surface-900);
    border: 1px solid var(--p-surface-800);
}

.endpoint {
    display: flex;
    align-items: center;
    gap: 12px;
    margin-bottom: 12px;
}

.badge {
    padding: 4px 8px;
    border-radius: 4px;
    font-size: 0.75rem;
    font-weight: 700;
}

.badge.post { background-color: var(--p-primary-900); color: var(--p-primary-300); }

.description { font-size: 0.9rem; margin: 0; }

/* Reference Column */
.docs-reference {
    width: 400px;
    border-left: 1px solid var(--p-surface-800);
    padding: 48px 24px;
    display: none; /* Hidden on mobile/small screens */
}

@media (min-width: 1400px) {
    .docs-reference { display: block; }
}

.sticky-ref {
    position: sticky;
    top: 48px;
}

.ref-code {
    background-color: var(--p-surface-900);
    padding: 24px;
    border-radius: 12px;
    font-family: 'JetBrains Mono', monospace;
    font-size: 0.85rem;
    color: var(--p-surface-200);
    border: 1px solid var(--p-surface-800);
}
</style>


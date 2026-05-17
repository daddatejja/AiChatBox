<script setup lang="ts">
import { ref } from 'vue';

const activeFramework = ref('vanilla');

const frameworks = [
    { id: 'vanilla', label: 'Vanilla JS', icon: 'pi-code' },
    { id: 'react', label: 'React', icon: 'pi-atom' },
    { id: 'vue', label: 'Vue 3', icon: 'pi-sparkles' },
    { id: 'wordpress', label: 'WordPress', icon: 'pi-globe' }
];
</script>

<template>
    <section id="widget" class="doc-section">
        <h2 class="section-title"><i class="pi pi-code"></i> Widget Integration</h2>
        <p class="section-intro">The AiChatBox widget is a high-performance Web Component. Embedding it handles the visual user interface, streaming interactions, live handoffs, and voice hubs entirely out of the box.</p>

        <!-- Framework selector tabs -->
        <h3 class="sub-heading">Multi-Framework Quickstarts</h3>
        <p class="desc">Select your frontend framework to view the quickstart integration guides:</p>
        
        <div class="framework-tabs">
            <button 
                v-for="fw in frameworks" 
                :key="fw.id" 
                :class="['tab-btn', { active: activeFramework === fw.id }]"
                @click="activeFramework = fw.id"
            >
                <i :class="['pi', fw.icon]"></i>
                <span>{{ fw.label }}</span>
            </button>
        </div>

        <!-- Tab contents -->
        <div class="tab-content-container">
            <!-- Vanilla JS -->
            <div v-if="activeFramework === 'vanilla'" class="tab-pane">
                <p class="desc">Add the custom script and floating element anywhere in your target HTML file:</p>
                <div class="code-block">
                    <div class="code-header">HTML Setup</div>
                    <pre><code>&lt;!-- 1. Load the widget script from your API --&gt;
&lt;script src="https://your-api.com/widget/ai-chatbox.js"&gt;&lt;/script&gt;

&lt;!-- 2. Inject the custom element --&gt;
&lt;ai-chatbox
    api-key="acb_your_api_key"
    api-url="https://your-api.com"
    user-id="visitor_123"&gt;
&lt;/ai-chatbox&gt;</code></pre>
                </div>
            </div>

            <!-- React -->
            <div v-if="activeFramework === 'react'" class="tab-pane">
                <p class="desc">Load the widget dynamically in React using a standard lifecycle `useEffect` hook:</p>
                <div class="code-block">
                    <div class="code-header">React Component (JSX)</div>
                    <pre><code>import React, { useEffect } from 'react';

export default function AiChatWidget() {
  useEffect(() => {
    // Avoid duplicate script insertion
    if (!document.getElementById('ai-chatbox-script')) {
      const script = document.createElement('script');
      script.id = 'ai-chatbox-script';
      script.src = 'https://your-api.com/widget/ai-chatbox.js';
      document.head.appendChild(script);
    }
  }, []);

  return (
    &lt;ai-chatbox
      api-key="acb_your_api_key"
      api-url="https://your-api.com"
      user-id="react_visitor"
    /&gt;
  );
}</code></pre>
                </div>
            </div>

            <!-- Vue 3 -->
            <div v-if="activeFramework === 'vue'" class="tab-pane">
                <p class="desc">Configure Vue 3 to support custom elements, and register the script during lifecycle setup:</p>
                <div class="code-block">
                    <div class="code-header">Vue 3 Component (SFC)</div>
                    <pre><code>&lt;template&gt;
  &lt;!-- Render the custom widget tag directly --&gt;
  &lt;ai-chatbox
    api-key="acb_your_api_key"
    api-url="https://your-api.com"
    :user-id="userId"
  /&gt;
&lt;/template&gt;

&lt;script setup&gt;
import { onMounted } from 'vue';

const userId = "vue_visitor_456";

onMounted(() => {
  if (!document.getElementById('ai-chatbox-script')) {
    const script = document.createElement('script');
    script.id = 'ai-chatbox-script';
    script.src = 'https://your-api.com/widget/ai-chatbox.js';
    document.head.appendChild(script);
  }
});
&lt;/script&gt;</code></pre>
                </div>
            </div>

            <!-- WordPress -->
            <div v-if="activeFramework === 'wordpress'" class="tab-pane">
                <p class="desc">Deploy the widget across your entire WordPress site using a visual block editor:</p>
                <ol class="wp-instructions">
                    <li>Open your WordPress Admin Dashboard and edit the page or template.</li>
                    <li>Add a new <strong>Custom HTML</strong> block inside the block editor.</li>
                    <li>Paste the integration code below into the Custom HTML editor box:</li>
                </ol>
                <div class="code-block">
                    <div class="code-header">WordPress Custom HTML Code</div>
                    <pre><code>&lt;script src="https://your-api.com/widget/ai-chatbox.js"&gt;&lt;/script&gt;
&lt;ai-chatbox
    api-key="acb_your_api_key"
    api-url="https://your-api.com"
    user-id="wp_user"&gt;
&lt;/ai-chatbox&gt;</code></pre>
                </div>
            </div>
        </div>

        <!-- Custom styling variables -->
        <h3 class="sub-heading">Styling & CSS Custom Properties</h3>
        <p class="desc">You can easily override widget visual styles by mapping custom values directly to our native CSS variables:</p>
        
        <div class="attr-table">
            <div class="attr-row header">
                <span>CSS Variable</span><span>Default</span><span>Description</span>
            </div>
            <div class="attr-row">
                <code>--primary-color</code>
                <span>#39a7b9</span>
                <span>Active accent color for headers, toggles, and buttons</span>
            </div>
            <div class="attr-row">
                <code>--bg-color</code>
                <span>#ffffff</span>
                <span>Background base color of the conversational window frame</span>
            </div>
            <div class="attr-row">
                <code>--font-family</code>
                <span>system-ui</span>
                <span>Typography layout family applied to all text components</span>
            </div>
            <div class="attr-row">
                <code>--widget-left</code>
                <span>auto</span>
                <span>Left offset coordinate (set when using Left position setting)</span>
            </div>
            <div class="attr-row">
                <code>--widget-right</code>
                <span>24px</span>
                <span>Right offset coordinate (set when using Right position setting)</span>
            </div>
        </div>

        <div class="code-block">
            <div class="code-header">Custom Theme CSS Example</div>
            <pre><code>/* Add style attributes directly to override the component values */
&lt;ai-chatbox
    api-key="acb_key"
    api-url="https://your-api.com"
    style="--primary-color: #e11d48; --font-family: 'Inter', sans-serif;"&gt;
&lt;/ai-chatbox&gt;</code></pre>
        </div>

        <!-- Attributes -->
        <h3 class="sub-heading">Widget HTML Attributes</h3>
        <p class="desc">Customize the widget behaviors and configurations using standard attributes:</p>
        <div class="attr-table">
            <div class="attr-row header">
                <span>Attribute</span><span>Type</span><span>Description</span>
            </div>
            <div class="attr-row"><code>api-key</code><span>string</span><span>Your secure project configuration API Key (starts with <code>acb_</code>)</span></div>
            <div class="attr-row"><code>api-url</code><span>string</span><span>Base URL targeting your deployed API server</span></div>
            <div class="attr-row"><code>user-id</code><span>string</span><span>Unique identifier for the end user to track conversations</span></div>
            <div class="attr-row"><code>title</code><span>string</span><span>Branded title shown at the top of the chat panel</span></div>
            <div class="attr-row"><code>suggestions</code><span>JSON array</span><span>Quick-reply buttons to display on start (e.g. <code>'["Hello", "Help"]'</code>)</span></div>
        </div>

        <h3 class="sub-heading">Widget Events Reference</h3>
        <p class="desc">Listen for native custom events emitted from the Web Component in your host script:</p>
        <div class="code-block">
            <div class="code-header">JavaScript — Custom Event Handling</div>
            <pre><code>const chatbox = document.querySelector('ai-chatbox');

// Listen for dynamic tool executions
chatbox.addEventListener('tool-call', (event) => {
    const { name, args, callId } = event.detail;
    console.log(`Executing client tool: ${name} with args:`, args);
    
    // Resolve tool call asynchronously and submit result
    const mockResult = { data: "Project processed successfully" };
    chatbox.submitToolResult(callId, mockResult);
});

// Listen for message thumbs-up/down ratings
chatbox.addEventListener('feedback', (event) => {
    const { messageId, value } = event.detail;
    console.log(`User left feedback score: ${value} on message ID: ${messageId}`);
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

.framework-tabs {
    display: flex;
    gap: 8px;
    margin-bottom: 16px;
    border-bottom: 1px solid var(--p-surface-200);
    padding-bottom: 8px;
}

.tab-btn {
    display: flex;
    align-items: center;
    gap: 8px;
    padding: 8px 16px;
    border: 1px solid transparent;
    background: transparent;
    color: var(--p-surface-500);
    font-size: 0.85rem;
    font-weight: 600;
    cursor: pointer;
    border-radius: 6px;
    transition: all 0.15s ease;
}

.tab-btn:hover {
    color: var(--p-surface-800);
    background: var(--p-surface-100);
}

.tab-btn.active {
    background: var(--p-primary-50);
    color: var(--p-primary-600);
    border-color: var(--p-primary-100);
}

.tab-content-container {
    margin-bottom: 24px;
}

.wp-instructions {
    margin: 0 0 16px 20px;
    padding: 0;
    font-size: 0.88rem;
    color: var(--p-surface-600);
    display: flex;
    flex-direction: column;
    gap: 8px;
}

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
    display: grid; grid-template-columns: 180px 140px 1fr; padding: 10px 16px;
    border-bottom: 1px solid var(--p-surface-100); font-size: 0.85rem; color: var(--p-surface-700);
    align-items: center; gap: 8px;
}
.attr-row:last-child { border-bottom: none; }
.attr-row.header {
    background: var(--p-surface-55); font-weight: 700; font-size: 0.75rem;
    text-transform: uppercase; color: var(--p-surface-500); letter-spacing: 0.04em;
}
.attr-row code {
    background: var(--p-surface-100); padding: 2px 6px; border-radius: 4px;
    font-size: 0.8rem; color: var(--p-primary-600);
}
</style>

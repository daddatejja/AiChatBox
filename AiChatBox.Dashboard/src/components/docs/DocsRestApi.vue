<script setup lang="ts">
import { ref } from 'vue';
import { useApi } from '../../composables/useApi';

const { getToken, API_BASE } = useApi();

const authToken = ref(getToken() || '');
const apiKey = ref(localStorage.getItem('acb_api_key') || '');
const selectedProjectId = ref('edd9d3c2-c863-408d-830b-68b94ca5e560'); // fallback/default

// State for active interactive operations
const consoleResponse = ref('');
const consoleStatus = ref<number | null>(null);
const consoleLatency = ref<number | null>(null);
const isSending = ref(false);
const activeTab = ref<'docs' | 'runner'>('docs');

interface Endpoint {
    id: string;
    method: 'GET' | 'POST' | 'PUT' | 'DELETE';
    path: string;
    description: string;
    requiresAuth: boolean;
    defaultBody?: string;
    routeParamName?: string;
}

const endpoints: Endpoint[] = [
    {
        id: 'list-projects',
        method: 'GET',
        path: '/api/project',
        description: 'Retrieve all projects owned by the authenticated developer account.',
        requiresAuth: true
    },
    {
        id: 'send-chat',
        method: 'POST',
        path: '/api/chat',
        description: 'Send a message and receive a completion response. Integrates LLM orchestration and vector search internally.',
        requiresAuth: false,
        defaultBody: JSON.stringify({
            message: "Hello assistant! What capabilities do you support?",
            provider: "gemini",
            modelName: "gemini-3.1-flash"
        }, null, 2)
    },
    {
        id: 'list-sessions',
        method: 'GET',
        path: '/api/chat/sessions',
        description: 'Get all dynamic conversational sessions in progress for the active account.',
        requiresAuth: true
    },
    {
        id: 'list-tools',
        method: 'GET',
        path: '/api/tool/project/{projectId}',
        description: 'List all custom defined tool functions associated with the project.',
        requiresAuth: true,
        routeParamName: 'projectId'
    }
];

const activeEndpoint = ref<Endpoint>(endpoints[0]);
const requestBody = ref(activeEndpoint.value.defaultBody || '');
const routeParamValue = ref(selectedProjectId.value);

function selectEndpoint(ep: Endpoint) {
    activeEndpoint.value = ep;
    requestBody.value = ep.defaultBody || '';
    routeParamValue.value = ep.routeParamName === 'projectId' ? selectedProjectId.value : '';
    consoleResponse.value = '';
    consoleStatus.value = null;
    consoleLatency.value = null;
}

async function executeRequest() {
    isSending.value = true;
    consoleResponse.value = 'Connecting to server and sending request payload...';
    consoleStatus.value = null;
    consoleLatency.value = null;

    const startTime = performance.now();
    let url = `${API_BASE}${activeEndpoint.value.path}`;
    
    // Interpolate route params if any
    if (activeEndpoint.value.routeParamName && routeParamValue.value) {
        url = url.replace(`{${activeEndpoint.value.routeParamName}}`, routeParamValue.value);
    }

    const headers: Record<string, string> = {
        'Content-Type': 'application/json'
    };

    if (activeEndpoint.value.requiresAuth) {
        headers['Authorization'] = `Bearer ${authToken.value}`;
    } else if (apiKey.value) {
        headers['X-Api-Key'] = apiKey.value;
    }

    try {
        const options: RequestInit = {
            method: activeEndpoint.value.method,
            headers
        };

        if (activeEndpoint.value.method !== 'GET' && requestBody.value) {
            options.body = requestBody.value;
        }

        const response = await fetch(url, options);
        const endTime = performance.now();
        consoleLatency.value = Math.round(endTime - startTime);
        consoleStatus.value = response.status;

        const contentType = response.headers.get('content-type');
        if (contentType && contentType.includes('application/json')) {
            const data = await response.json();
            consoleResponse.value = JSON.stringify(data, null, 2);
        } else {
            consoleResponse.value = await response.text() || '(No Content)';
        }
    } catch (e: any) {
        const endTime = performance.now();
        consoleLatency.value = Math.round(endTime - startTime);
        consoleStatus.value = 500;
        consoleResponse.value = `Client connection failed:\n${e.message}`;
    } finally {
        isSending.value = false;
    }
}
</script>

<template>
    <section id="rest-api" class="doc-section">
        <h2 class="section-title"><i class="pi pi-server"></i> REST API & Live Test Console</h2>
        <p class="section-intro">Build custom visual widgets or trigger chats programmatically. Switch to the **Interactive API Runner** tab on any endpoint card to trigger requests and view live responses instantly.</p>

        <!-- Dynamic Live API Console Grid -->
        <div class="api-console-grid">
            
            <!-- Sidebar: Endpoints selector -->
            <div class="console-sidebar">
                <h4 class="sidebar-title">Endpoints Directory</h4>
                <div class="endpoint-list">
                    <button 
                        v-for="ep in endpoints" 
                        :key="ep.id" 
                        :class="['ep-select-btn', { active: activeEndpoint.id === ep.id }]"
                        @click="selectEndpoint(ep)"
                    >
                        <span :class="['method-badge', ep.method.toLowerCase()]">{{ ep.method }}</span>
                        <span class="path-text">{{ ep.path }}</span>
                    </button>
                </div>

                <!-- Global headers editor -->
                <div class="auth-config-card">
                    <h4><i class="pi pi-shield"></i> Headers Setup</h4>
                    <div class="input-group">
                        <label>Authorization (Bearer JWT)</label>
                        <input 
                            v-model="authToken" 
                            type="text" 
                            placeholder="Paste JWT token here..." 
                            class="console-input font-mono"
                        />
                    </div>
                    <div class="input-group mt-2">
                        <label>X-Api-Key (Optional)</label>
                        <input 
                            v-model="apiKey" 
                            type="text" 
                            placeholder="acb_your_key_here..." 
                            class="console-input font-mono"
                        />
                    </div>
                </div>
            </div>

            <!-- Main: Terminal or Docs details -->
            <div class="console-main">
                <div class="card-header-bar">
                    <div class="endpoint-meta">
                        <span :class="['method-badge large', activeEndpoint.method.toLowerCase()]">{{ activeEndpoint.method }}</span>
                        <code class="path-code">{{ activeEndpoint.path }}</code>
                    </div>
                    
                    <div class="console-tabs">
                        <button 
                            :class="['console-tab-btn', { active: activeTab === 'docs' }]" 
                            @click="activeTab = 'docs'"
                        >
                            <i class="pi pi-book"></i> Docs Reference
                        </button>
                        <button 
                            :class="['console-tab-btn', { active: activeTab === 'runner' }]" 
                            @click="activeTab = 'runner'"
                        >
                            <i class="pi pi-play"></i> Interactive Runner
                        </button>
                    </div>
                </div>

                <!-- Tab: Static Docs -->
                <div v-if="activeTab === 'docs'" class="console-card-body">
                    <h3 class="ep-header-title">Endpoint Description</h3>
                    <p class="ep-desc-text">{{ activeEndpoint.description }}</p>

                    <div class="auth-badge-row">
                        <span v-if="activeEndpoint.requiresAuth" class="auth-pill secure">
                            <i class="pi pi-lock"></i> Requires Authentication (JWT)
                        </span>
                        <span v-else class="auth-pill public">
                            <i class="pi pi-unlock"></i> Public Access (API Key)
                        </span>
                    </div>

                    <!-- Static request structure -->
                    <div class="code-block mt-4">
                        <div class="code-header">HTTP Request Mapping</div>
                        <pre class="bg-dark"><code>Headers:
{{ activeEndpoint.requiresAuth ? 'Authorization: Bearer <your_jwt_token>' : 'X-Api-Key: <your_api_key>' }}
Content-Type: application/json

Path Parameters:
{{ activeEndpoint.routeParamName ? `- ${activeEndpoint.routeParamName}: Required UUID string` : 'None required' }}</code></pre>
                    </div>

                    <!-- Default body template -->
                    <div v-if="activeEndpoint.defaultBody" class="code-block">
                        <div class="code-header">Example Request Body</div>
                        <pre class="bg-dark"><code>{{ activeEndpoint.defaultBody }}</code></pre>
                    </div>
                </div>

                <!-- Tab: Live Request Runner -->
                <div v-if="activeTab === 'runner'" class="console-card-body runner-view">
                    
                    <div class="runner-inputs">
                        <h4 class="section-label">Request Parameters</h4>
                        
                        <!-- Route Parameter -->
                        <div v-if="activeEndpoint.routeParamName" class="input-group">
                            <label>Route Parameter: <code>{{ activeEndpoint.routeParamName }}</code></label>
                            <input 
                                v-model="routeParamValue" 
                                type="text" 
                                class="console-input font-mono"
                            />
                        </div>

                        <!-- JSON Request Body -->
                        <div v-if="activeEndpoint.method !== 'GET'" class="input-group mt-2">
                            <label>JSON Body Editor</label>
                            <textarea 
                                v-model="requestBody" 
                                rows="6" 
                                class="console-textarea font-mono"
                            ></textarea>
                        </div>

                        <button 
                            :disabled="isSending" 
                            class="run-trigger-btn mt-3"
                            @click="executeRequest"
                        >
                            <i class="pi pi-send"></i> 
                            {{ isSending ? 'Sending Live Query...' : 'Run API Request' }}
                        </button>
                    </div>

                    <!-- Live Terminal Output Screen -->
                    <div class="terminal-container">
                        <div class="terminal-header">
                            <div class="terminal-circles">
                                <span class="circle red"></span>
                                <span class="circle yellow"></span>
                                <span class="circle green"></span>
                            </div>
                            <span class="terminal-title">Interactive Terminal Response</span>
                            
                            <!-- Timing and status -->
                            <div v-if="consoleStatus !== null" class="terminal-stats">
                                <span :class="['status-pill', consoleStatus < 300 ? 'ok' : 'error']">
                                    {{ consoleStatus }}
                                </span>
                                <span class="latency-pill"><i class="pi pi-clock"></i> {{ consoleLatency }}ms</span>
                            </div>
                        </div>

                        <!-- Code display frame -->
                        <div class="terminal-screen font-mono">
                            <pre v-if="consoleResponse">{{ consoleResponse }}</pre>
                            <span v-else class="placeholder-msg">Terminal ready. Click "Run API Request" to execute live.</span>
                        </div>
                    </div>

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

/* Grid Layout */
.api-console-grid {
    display: grid;
    grid-template-columns: 300px 1fr;
    gap: 24px;
    background: var(--p-surface-0);
    border: 1px solid var(--p-surface-200);
    border-radius: 16px;
    overflow: hidden;
}

@media (max-width: 1024px) {
    .api-console-grid {
        grid-template-columns: 1fr;
    }
}

/* Sidebar Styling */
.console-sidebar {
    background: var(--p-surface-50);
    border-right: 1px solid var(--p-surface-200);
    padding: 20px;
    display: flex;
    flex-direction: column;
    gap: 20px;
}

.sidebar-title {
    margin: 0;
    font-size: 0.72rem;
    font-weight: 700;
    text-transform: uppercase;
    letter-spacing: 0.08em;
    color: var(--p-surface-400);
}

.endpoint-list {
    display: flex;
    flex-direction: column;
    gap: 6px;
}

.ep-select-btn {
    display: flex;
    align-items: center;
    gap: 10px;
    padding: 10px 12px;
    border: 1px solid transparent;
    background: transparent;
    border-radius: 8px;
    cursor: pointer;
    text-align: left;
    transition: all 0.2s ease;
    width: 100%;
}

.ep-select-btn:hover {
    background: var(--p-surface-100);
}

.ep-select-btn.active {
    background: var(--p-surface-200);
    border-color: var(--p-surface-300);
}

.path-text {
    font-size: 0.8rem;
    font-family: monospace;
    font-weight: 600;
    color: var(--p-surface-800);
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
}

.method-badge {
    padding: 2px 6px;
    border-radius: 4px;
    font-size: 0.65rem;
    font-weight: 800;
    text-transform: uppercase;
    flex-shrink: 0;
}

.method-badge.large {
    padding: 4px 10px;
    font-size: 0.8rem;
    border-radius: 6px;
}

.method-badge.get { background: #d1fae5; color: #065f46; }
.method-badge.post { background: #dbeafe; color: #1e40af; }
.method-badge.put { background: #fef3c7; color: #92400e; }
.method-badge.delete { background: #fee2e2; color: #991b1b; }

/* Auth config card */
.auth-config-card {
    background: var(--p-surface-0);
    border: 1px solid var(--p-surface-200);
    border-radius: 12px;
    padding: 16px;
}

.auth-config-card h4 {
    margin: 0 0 12px 0;
    font-size: 0.82rem;
    font-weight: 700;
    color: var(--p-surface-800);
    display: flex;
    align-items: center;
    gap: 6px;
}

.auth-config-card h4 .pi {
    color: var(--p-primary-500);
}

.input-group {
    display: flex;
    flex-direction: column;
    gap: 6px;
}

.input-group label {
    font-size: 0.72rem;
    font-weight: 600;
    color: var(--p-surface-500);
}

.console-input {
    background: var(--p-surface-50);
    border: 1px solid var(--p-surface-300);
    border-radius: 6px;
    padding: 6px 10px;
    font-size: 0.8rem;
    color: var(--p-surface-800);
    width: 100%;
}

.console-input:focus, .console-textarea:focus {
    border-color: var(--p-primary-500);
    outline: none;
}

/* Main Console styling */
.console-main {
    display: flex;
    flex-direction: column;
    padding: 24px;
    min-width: 0;
}

.card-header-bar {
    display: flex;
    justify-content: space-between;
    align-items: center;
    border-bottom: 1px solid var(--p-surface-200);
    padding-bottom: 16px;
    margin-bottom: 20px;
    flex-wrap: wrap;
    gap: 12px;
}

.endpoint-meta {
    display: flex;
    align-items: center;
    gap: 12px;
}

.path-code {
    font-size: 1.1rem;
    font-weight: 700;
    color: var(--p-surface-900);
}

.console-tabs {
    display: flex;
    background: var(--p-surface-100);
    padding: 4px;
    border-radius: 8px;
    gap: 4px;
}

.console-tab-btn {
    display: flex;
    align-items: center;
    gap: 6px;
    padding: 6px 12px;
    border: none;
    background: transparent;
    color: var(--p-surface-600);
    font-size: 0.8rem;
    font-weight: 600;
    cursor: pointer;
    border-radius: 6px;
    transition: all 0.15s ease;
}

.console-tab-btn.active {
    background: var(--p-surface-0);
    color: var(--p-primary-600);
    box-shadow: 0 1px 3px rgba(0,0,0,0.05);
}

.console-card-body {
    flex: 1;
    display: flex;
    flex-direction: column;
}

.ep-header-title {
    font-size: 0.95rem;
    font-weight: 700;
    color: var(--p-surface-800);
    margin: 0 0 6px 0;
}

.ep-desc-text {
    font-size: 0.88rem;
    color: var(--p-surface-600);
    line-height: 1.6;
    margin: 0 0 16px 0;
}

.auth-badge-row {
    margin-bottom: 20px;
}

.auth-pill {
    display: inline-flex;
    align-items: center;
    gap: 6px;
    padding: 4px 10px;
    border-radius: 20px;
    font-size: 0.72rem;
    font-weight: 600;
}

.auth-pill.secure { background: #fee2e2; color: #991b1b; }
.auth-pill.public { background: #d1fae5; color: #065f46; }

.code-block {
    border: 1px solid var(--p-surface-200); border-radius: 10px; overflow: hidden; margin-bottom: 16px;
}
.code-header {
    padding: 6px 14px; background: var(--p-surface-100); font-size: 0.72rem; font-weight: 600;
    color: var(--p-surface-500); text-transform: uppercase; letter-spacing: 0.04em;
    border-bottom: 1px solid var(--p-surface-200);
}
.code-block pre.bg-dark { margin: 0; padding: 14px; background: var(--p-surface-900); overflow-x: auto; color: var(--p-primary-300); font-size: 0.82rem; }

/* Interactive view styling */
.runner-view {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 24px;
}

@media (max-width: 900px) {
    .runner-view {
        grid-template-columns: 1fr;
    }
}

.runner-inputs {
    display: flex;
    flex-direction: column;
    gap: 16px;
}

.section-label {
    margin: 0;
    font-size: 0.8rem;
    font-weight: 700;
    text-transform: uppercase;
    letter-spacing: 0.05em;
    color: var(--p-surface-400);
}

.console-textarea {
    background: var(--p-surface-50);
    border: 1px solid var(--p-surface-300);
    border-radius: 6px;
    padding: 10px;
    font-size: 0.8rem;
    color: var(--p-surface-800);
    width: 100%;
    resize: vertical;
}

.run-trigger-btn {
    display: flex;
    align-items: center;
    justify-content: center;
    gap: 8px;
    padding: 10px 20px;
    background: linear-gradient(135deg, var(--p-primary-500), var(--p-primary-600));
    color: white;
    font-size: 0.85rem;
    font-weight: 700;
    border: none;
    border-radius: 8px;
    cursor: pointer;
    transition: filter 0.15s ease;
}

.run-trigger-btn:hover {
    filter: brightness(1.05);
}

.run-trigger-btn:disabled {
    opacity: 0.7;
    cursor: not-allowed;
}

/* Terminal Styling */
.terminal-container {
    background: var(--p-surface-900);
    border-radius: 12px;
    border: 1px solid var(--p-surface-800);
    overflow: hidden;
    display: flex;
    flex-direction: column;
    min-height: 280px;
}

.terminal-header {
    background: var(--p-surface-950);
    padding: 10px 16px;
    display: flex;
    align-items: center;
    justify-content: space-between;
    border-bottom: 1px solid var(--p-surface-800);
}

.terminal-circles {
    display: flex;
    gap: 6px;
}

.terminal-circles .circle {
    width: 10px;
    height: 10px;
    border-radius: 50%;
    display: block;
}

.terminal-circles .circle.red { background: #ff5f56; }
.terminal-circles .circle.yellow { background: #ffbd2e; }
.terminal-circles .circle.green { background: #27c93f; }

.terminal-title {
    font-size: 0.72rem;
    font-weight: 600;
    color: var(--p-surface-500);
    text-transform: uppercase;
    letter-spacing: 0.05em;
}

.terminal-stats {
    display: flex;
    align-items: center;
    gap: 8px;
}

.status-pill {
    padding: 2px 6px;
    border-radius: 4px;
    font-size: 0.65rem;
    font-weight: 700;
}

.status-pill.ok { background: #065f46; color: #d1fae5; }
.status-pill.error { background: #991b1b; color: #fee2e2; }

.latency-pill {
    font-size: 0.65rem;
    color: var(--p-surface-400);
    font-weight: 600;
}

.terminal-screen {
    padding: 16px;
    flex: 1;
    overflow: auto;
    font-size: 0.8rem;
    color: var(--p-primary-300);
    line-height: 1.5;
}

.terminal-screen pre {
    margin: 0;
    white-space: pre-wrap;
    word-break: break-all;
}

.placeholder-msg {
    color: var(--p-surface-600);
    display: flex;
    align-items: center;
    justify-content: center;
    height: 100%;
    italic: true;
}
</style>

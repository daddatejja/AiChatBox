<template>
    <section id="rest-api" class="doc-section">
        <h2 class="section-title"><i class="pi pi-server"></i> REST API Reference</h2>
        <p class="section-intro">Use the REST API for server-to-server integrations, custom frontends, or mobile apps. All endpoints use JSON and support streaming via SSE.</p>

        <h3 class="sub-heading">Authentication</h3>
        <p class="desc">Two authentication methods are supported:</p>
        <div class="auth-methods">
            <div class="auth-card">
                <h4><i class="pi pi-key"></i> API Key</h4>
                <p>For widget and public integrations. Pass via header:</p>
                <code>X-Api-Key: acb_your_key_here</code>
            </div>
            <div class="auth-card">
                <h4><i class="pi pi-lock"></i> JWT Bearer Token</h4>
                <p>For dashboard and authenticated sessions. Pass via header:</p>
                <code>Authorization: Bearer eyJhbG...</code>
            </div>
        </div>

        <h3 class="sub-heading">Chat Endpoints</h3>

        <div class="endpoint-card">
            <div class="endpoint-header">
                <span class="method post">POST</span>
                <code>/api/chat</code>
            </div>
            <p class="endpoint-desc">Send a message and receive a streamed AI response (Server-Sent Events).</p>
            <div class="code-block">
                <div class="code-header">Request Body</div>
                <pre><code>{
  "message": "What's the weather in London?",
  "sessionId": null,
  "projectId": "uuid-optional",
  "configurationId": "uuid-optional",
  "provider": "gemini",
  "modelName": "gemini-2.5-flash",
  "attachedFileId": null,
  "imageDataUrl": null
}</code></pre>
            </div>
            <div class="code-block">
                <div class="code-header">Response (SSE Stream)</div>
                <pre><code>data: {"sessionId":"abc-123","text":"The weather "}
data: {"text":"in London is "}
data: {"text":"currently 18°C and cloudy."}
data: {"done":true}

// If a tool call is triggered:
data: {"toolCall":{"name":"get_weather","arguments":"{\"city\":\"London\"}","id":"call_1"}}</code></pre>
            </div>
            <div class="code-block">
                <div class="code-header">cURL Example</div>
                <pre><code>curl -X POST https://your-api.com/api/chat \
  -H "X-Api-Key: acb_your_key" \
  -H "Content-Type: application/json" \
  -H "X-User-Id: user_123" \
  -d '{
    "message": "Hello, how are you?",
    "provider": "gemini",
    "modelName": "gemini-2.5-flash"
  }'</code></pre>
            </div>
        </div>

        <div class="endpoint-card">
            <div class="endpoint-header">
                <span class="method get">GET</span>
                <code>/api/chat/sessions</code>
            </div>
            <p class="endpoint-desc">List all chat sessions for the authenticated user.</p>
            <div class="code-block">
                <div class="code-header">Response</div>
                <pre><code>[
  {
    "id": "session-uuid",
    "title": "Weather inquiry",
    "createdAt": "2026-05-10T14:30:00Z",
    "messageCount": 4
  }
]</code></pre>
            </div>
        </div>

        <div class="endpoint-card">
            <div class="endpoint-header">
                <span class="method get">GET</span>
                <code>/api/chat/sessions/{sessionId}</code>
            </div>
            <p class="endpoint-desc">Get all messages for a specific session.</p>
            <div class="code-block">
                <div class="code-header">Response</div>
                <pre><code>[
  { "role": "user", "content": "Hello!" },
  { "role": "assistant", "content": "Hi! How can I help you today?" }
]</code></pre>
            </div>
        </div>

        <div class="endpoint-card">
            <div class="endpoint-header">
                <span class="method get">GET</span>
                <code>/api/chat/config</code>
            </div>
            <p class="endpoint-desc">Get the widget configuration for the current API key (project name, models, system prompt).</p>
        </div>

        <h3 class="sub-heading">Project Endpoints</h3>

        <div class="endpoint-card">
            <div class="endpoint-header"><span class="method get">GET</span><code>/api/project</code></div>
            <p class="endpoint-desc">List all projects. <em>Requires JWT auth.</em></p>
        </div>
        <div class="endpoint-card">
            <div class="endpoint-header"><span class="method post">POST</span><code>/api/project</code></div>
            <p class="endpoint-desc">Create a new project. Body: <code>{ "name": "My Bot" }</code></p>
        </div>
        <div class="endpoint-card">
            <div class="endpoint-header"><span class="method get">GET</span><code>/api/project/{id}/configurations</code></div>
            <p class="endpoint-desc">List configurations for a project.</p>
        </div>
        <div class="endpoint-card">
            <div class="endpoint-header"><span class="method post">POST</span><code>/api/project/{id}/keys</code></div>
            <p class="endpoint-desc">Generate an API key. Body: <code>{ "label": "Prod Key", "configurationId": "uuid" }</code></p>
        </div>

        <h3 class="sub-heading">Tool Endpoints</h3>

        <div class="endpoint-card">
            <div class="endpoint-header"><span class="method get">GET</span><code>/api/tool/project/{projectId}</code></div>
            <p class="endpoint-desc">List all custom tools for a project.</p>
        </div>
        <div class="endpoint-card">
            <div class="endpoint-header"><span class="method post">POST</span><code>/api/tool/project/{projectId}</code></div>
            <p class="endpoint-desc">Create a new tool.</p>
            <div class="code-block">
                <div class="code-header">Request Body</div>
                <pre><code>{
  "name": "check_inventory",
  "description": "Check product stock levels",
  "parametersJsonSchema": "{\"type\":\"object\",\"properties\":{\"productId\":{\"type\":\"string\"}}}",
  "isActive": true
}</code></pre>
            </div>
        </div>
        <div class="endpoint-card">
            <div class="endpoint-header"><span class="method put">PUT</span><code>/api/tool/{id}</code></div>
            <p class="endpoint-desc">Update an existing tool definition.</p>
        </div>
        <div class="endpoint-card">
            <div class="endpoint-header"><span class="method delete">DELETE</span><code>/api/tool/{id}</code></div>
            <p class="endpoint-desc">Delete a tool.</p>
        </div>

        <h3 class="sub-heading">File Upload</h3>
        <div class="endpoint-card">
            <div class="endpoint-header"><span class="method post">POST</span><code>/api/file/upload</code></div>
            <p class="endpoint-desc">Upload a file for context in chat. Use <code>multipart/form-data</code>.</p>
            <div class="code-block">
                <div class="code-header">cURL Example</div>
                <pre><code>curl -X POST https://your-api.com/api/file/upload \
  -H "X-Api-Key: acb_your_key" \
  -F "file=@document.pdf"</code></pre>
            </div>
        </div>
    </section>
</template>

<style scoped>
.doc-section { margin-bottom: 64px; }
.section-title { display: flex; align-items: center; gap: 10px; font-size: 1.6rem; font-weight: 700; color: var(--p-surface-900); margin-bottom: 12px; }
.section-title .pi { color: var(--p-primary-500); font-size: 1.3rem; }
.section-intro { color: var(--p-surface-500); font-size: 1.05rem; line-height: 1.7; margin-bottom: 32px; max-width: 720px; }
.sub-heading { font-size: 1.15rem; font-weight: 700; color: var(--p-surface-900); margin: 32px 0 12px 0; padding-top: 16px; border-top: 1px solid var(--p-surface-100); }
.desc { color: var(--p-surface-600); font-size: 0.92rem; line-height: 1.6; margin-bottom: 16px; }

.code-block { border: 1px solid var(--p-surface-200); border-radius: 10px; overflow: hidden; margin-bottom: 16px; }
.code-header { padding: 6px 14px; background: var(--p-surface-100); font-size: 0.72rem; font-weight: 600; color: var(--p-surface-500); text-transform: uppercase; letter-spacing: 0.04em; border-bottom: 1px solid var(--p-surface-200); }
.code-block pre { margin: 0; padding: 14px; background: var(--p-surface-900); overflow-x: auto; }
.code-block code { color: var(--p-primary-300); font-size: 0.82rem; }

.auth-methods { display: grid; grid-template-columns: 1fr 1fr; gap: 16px; margin-bottom: 24px; }
.auth-card { padding: 20px; background: var(--p-surface-50); border: 1px solid var(--p-surface-200); border-radius: 10px; }
.auth-card h4 { display: flex; align-items: center; gap: 8px; margin: 0 0 8px 0; font-size: 0.95rem; color: var(--p-surface-900); }
.auth-card h4 .pi { color: var(--p-primary-500); }
.auth-card p { margin: 0 0 8px 0; font-size: 0.85rem; color: var(--p-surface-600); }
.auth-card > code { display: block; padding: 8px 12px; background: var(--p-surface-900); color: var(--p-primary-300); border-radius: 6px; font-size: 0.8rem; }

.endpoint-card { padding: 16px 20px; background: var(--p-surface-50); border: 1px solid var(--p-surface-200); border-radius: 10px; margin-bottom: 12px; }
.endpoint-header { display: flex; align-items: center; gap: 12px; margin-bottom: 6px; }
.endpoint-header code { font-size: 0.9rem; color: var(--p-surface-800); font-weight: 600; }
.endpoint-desc { margin: 0 0 12px 0; font-size: 0.88rem; color: var(--p-surface-600); }
.endpoint-desc:last-child { margin-bottom: 0; }
.endpoint-desc code { background: var(--p-surface-100); padding: 2px 5px; border-radius: 4px; font-size: 0.8rem; color: var(--p-primary-600); }

.method { padding: 3px 8px; border-radius: 4px; font-size: 0.7rem; font-weight: 800; text-transform: uppercase; }
.method.get { background: #d1fae5; color: #065f46; }
.method.post { background: #dbeafe; color: #1e40af; }
.method.put { background: #fef3c7; color: #92400e; }
.method.delete { background: #fee2e2; color: #991b1b; }

@media (max-width: 700px) { .auth-methods { grid-template-columns: 1fr; } }
</style>

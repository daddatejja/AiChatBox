<script setup lang="ts">
import { ref, onMounted, watch, nextTick } from 'vue';
import { useApi } from '../composables/useApi';
import Select from 'primevue/select';
import Button from 'primevue/button';
import Card from 'primevue/card';
import ScrollPanel from 'primevue/scrollpanel';
import InputText from 'primevue/inputtext';
import ProgressSpinner from 'primevue/progressspinner';
import Tag from 'primevue/tag';

const { apiFetch, API_BASE, getToken } = useApi();
const showWidget = ref(false);

const projects = ref<any[]>([]);
const configurations = ref<any[]>([]);
const selectedProject = ref<any>(null);
const selectedConfig = ref<any>(null);

const messages = ref<any[]>([]);
const inputText = ref('');
const isStreaming = ref(false);
const scrollPanel = ref<any>(null);

const sessionId = ref<string | null>(null);

async function loadProjects() {
    try {
        const res = await apiFetch('/api/project');
        if (res.ok) {
            projects.value = await res.json();
            if (projects.value.length > 0) {
                selectedProject.value = projects.value[0];
            }
        }
    } catch (e) {
        console.error('Failed to load projects', e);
    }
}

async function loadConfigurations(projectId: string) {
    try {
        const res = await apiFetch(`/api/project/${projectId}/configurations`);
        if (res.ok) {
            configurations.value = await res.json();
            if (configurations.value.length > 0) {
                selectedConfig.value = configurations.value[0];
            } else {
                selectedConfig.value = null;
            }
        }
    } catch (e) {
        console.error('Failed to load configs', e);
    }
}

const sessions = ref<any[]>([]);

async function loadSessions() {
    try {
        const res = await apiFetch('/api/chat/sessions');
        if (res.ok) {
            sessions.value = await res.json();
        }
    } catch (e) {
        console.error('Failed to load sessions', e);
    }
}

async function loadSessionMessages(id: string) {
    try {
        sessionId.value = id;
        const res = await apiFetch(`/api/chat/sessions/${id}`);
        if (res.ok) {
            const data = await res.json();
            messages.value = data.map((m: any) => ({
                role: m.role,
                content: m.content
            }));
            scrollToBottom();
        }
    } catch (e) {
        console.error('Failed to load session messages', e);
    }
}

watch(selectedProject, (newVal) => {
    if (newVal) {
        loadConfigurations(newVal.id);
        messages.value = [];
        sessionId.value = null;
        loadSessions();
    }
});

onMounted(() => {
    loadProjects();
    loadSessions();
});

function scrollToBottom() {
    nextTick(() => {
        if (scrollPanel.value) {
            const container = scrollPanel.value.$el.querySelector('.p-scrollpanel-content');
            if (container) {
                container.scrollTop = container.scrollHeight;
            }
        }
    });
}

async function sendMessage() {
    if (!inputText.value.trim() || isStreaming.value) return;

    const userMessage = { role: 'user', content: inputText.value };
    messages.value.push(userMessage);
    const textToSubmit = inputText.value;
    inputText.value = '';
    isStreaming.value = true;
    scrollToBottom();

    try {
        const response = await apiFetch('/api/chat', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                message: textToSubmit,
                sessionId: sessionId.value,
                projectId: selectedProject.value?.id,
                provider: selectedConfig.value?.defaultProvider,
                modelName: selectedConfig.value?.defaultModel,
                systemPrompt: selectedConfig.value?.systemPrompt
            })
        });

        if (!response.ok) throw new Error('Failed to send message');

        const reader = response.body?.getReader();
        if (!reader) return;

        const assistantMessage = { role: 'assistant', content: '' };
        messages.value.push(assistantMessage);

        const decoder = new TextDecoder();
        let buffer = '';

        while (true) {
            const { done, value } = await reader.read();
            if (done) break;

            buffer += decoder.decode(value, { stream: true });
            const lines = buffer.split('\n');
            buffer = lines.pop() || '';

            for (const line of lines) {
                if (line.trim().startsWith('data: ')) {
                    try {
                        const dataStr = line.trim().slice(6);
                        if (!dataStr) continue;
                        
                        const chunk = JSON.parse(dataStr);
                        if (chunk.sessionId) sessionId.value = chunk.sessionId;
                        
                        if (chunk.text) {
                            assistantMessage.content += chunk.text;
                            scrollToBottom();
                        }

                        if (chunk.toolCall) {
                            messages.value.push({
                                role: 'tool-call',
                                content: `Tool Call: ${chunk.toolCall.name}(${chunk.toolCall.arguments})`,
                                toolName: chunk.toolCall.name,
                                args: chunk.toolCall.arguments
                            });
                            scrollToBottom();
                        }

                        if (chunk.done) {
                            isStreaming.value = false;
                        }
                        
                        if (chunk.error) {
                            assistantMessage.content = 'Error: ' + chunk.error;
                            isStreaming.value = false;
                        }
                    } catch (e) {
                        console.error('Error parsing chunk', e);
                    }
                }
            }
        }
    } catch (e: any) {
        messages.value.push({ role: 'error', content: e.message });
    } finally {
        isStreaming.value = false;
        scrollToBottom();
        loadSessions();
    }
}
async function openWidgetPreview() {
    if (!selectedProject.value) return;
    
    // Ensure widget script is loaded from API
    if (!document.getElementById('ai-chatbox-script')) {
        const script = document.createElement('script');
        script.id = 'ai-chatbox-script';
        script.src = `${API_BASE}/widget/ai-chatbox.js`;
        document.head.appendChild(script);
        
        // Wait for script to load
        await new Promise((resolve) => {
            script.onload = resolve;
        });
    }
    
    showWidget.value = true;
}

</script>

<template>
    <div class="playground-layout">
        <header class="playground-header">
            <div class="header-left">
                <h1 class="text-3xl font-bold m-0">Playground</h1>
                <p class="text-surface-400 m-0">Test your AI configurations in real-time</p>
            </div>
            <div class="header-controls">
                <div class="control-group">
                    <label>Project</label>
                    <Select v-model="selectedProject" :options="projects" optionLabel="name" placeholder="Select Project" class="w-64" />
                </div>
                <div class="control-group">
                    <label>Configuration</label>
                    <Select v-model="selectedConfig" :options="configurations" optionLabel="name" placeholder="Select Config" class="w-64" />
                </div>
                <Button label="Test with Widget" icon="pi pi-external-link" severity="help" class="ml-2" @click="openWidgetPreview" :disabled="!selectedProject" />
                <Button icon="pi pi-refresh" severity="secondary" rounded text v-tooltip.bottom="'Reset Chat'" @click="messages = []; sessionId = null;" />
            </div>
        </header>

        <div class="playground-content">
            <aside class="sessions-sidebar">
                <div class="sidebar-header">
                    <span class="text-sm font-bold text-surface-400">HISTORY</span>
                    <Button icon="pi pi-plus" size="small" text rounded v-tooltip.right="'New Chat'" @click="messages = []; sessionId = null;" />
                </div>
                <ScrollPanel class="sessions-list">
                    <div 
                        v-for="s in sessions" 
                        :key="s.id" 
                        :class="['session-item', { active: sessionId === s.id }]"
                        @click="loadSessionMessages(s.id)"
                    >
                        <i class="pi pi-comment text-xs"></i>
                        <span class="session-title">{{ s.title }}</span>
                    </div>
                    <div v-if="sessions.length === 0" class="text-xs text-surface-500 text-center mt-4">
                        No previous sessions
                    </div>
                </ScrollPanel>
            </aside>

            <div class="chat-container">
                <ScrollPanel ref="scrollPanel" class="chat-messages">
                    <div class="message-list">
                        <div v-if="messages.length === 0" class="empty-state">
                            <i class="pi pi-comments text-6xl text-surface-600 mb-4"></i>
                            <h3 class="text-xl">No messages yet</h3>
                            <p class="text-surface-400">Select a project and configuration, then start typing below.</p>
                        </div>
                        
                        <div v-for="(msg, index) in messages" :key="index" :class="['message-wrapper', msg.role]">
                            <div class="message-avatar">
                                <i :class="msg.role === 'user' ? 'pi pi-user' : 'pi pi-android'"></i>
                            </div>
                            <div class="message-bubble">
                                <div v-if="msg.role === 'tool-call'" class="tool-call-info">
                                    <Tag value="TOOL CALL" severity="info" class="mb-2" />
                                    <pre>{{ msg.content }}</pre>
                                </div>
                                <div v-else class="message-text">
                                    {{ msg.content }}
                                    <ProgressSpinner v-if="isStreaming && index === messages.length - 1 && msg.role === 'assistant' && !msg.content" style="width: 20px; height: 20px" strokeWidth="4" />
                                </div>
                            </div>
                        </div>
                    </div>
                </ScrollPanel>

                <div class="chat-input-area">
                    <div class="input-container">
                        <InputText 
                            v-model="inputText" 
                            placeholder="Type your message..." 
                            class="flex-1" 
                            @keyup.enter="sendMessage"
                            :disabled="isStreaming || !selectedProject"
                        />
                        <Button 
                            icon="pi pi-send" 
                            @click="sendMessage" 
                            :loading="isStreaming"
                            :disabled="!inputText.trim() || !selectedProject"
                        />
                    </div>
                    <div class="input-footer">
                        <span class="text-xs text-surface-500">
                            {{ selectedConfig ? `${selectedConfig.defaultProvider} / ${selectedConfig.defaultModel}` : 'Select a configuration to start' }}
                        </span>
                    </div>
                </div>
            </div>

            <aside class="config-sidebar">
                <Card class="h-full border-none shadow-none bg-surface-800">
                    <template #title>
                        <span class="text-lg font-semibold">Active Configuration</span>
                    </template>
                    <template #content>
                        <div v-if="selectedConfig" class="config-details">
                            <div class="detail-item">
                                <label>Provider</label>
                                <Tag :value="selectedConfig.defaultProvider" severity="contrast" />
                            </div>
                            <div class="detail-item">
                                <label>Model</label>
                                <span class="text-surface-200">{{ selectedConfig.defaultModel }}</span>
                            </div>
                            <div class="detail-item">
                                <label>System Prompt</label>
                                <div class="prompt-box">
                                    {{ selectedConfig.systemPrompt || 'No system prompt set' }}
                                </div>
                            </div>
                            <div class="detail-item mt-4">
                                <label>Session ID</label>
                                <code class="text-xs text-primary">{{ sessionId || 'New Session' }}</code>
                            </div>
                        </div>
                        <div v-else class="text-surface-400 italic">
                            Select a configuration to view details.
                        </div>
                    </template>
                </Card>
            </aside>
        </div>
        
        <!-- Widget Preview Overlay -->
        <ai-chatbox 
            v-if="showWidget" 
            :project-id="selectedProject?.id" 
            :api-base="API_BASE" 
            :auth-token="getToken()"
            @close="showWidget = false"
        ></ai-chatbox>
    </div>
</template>

<style scoped>
.playground-layout {
    display: flex;
    flex-direction: column;
    height: calc(100vh - 96px);
    gap: 24px;
}

.playground-header {
    display: flex;
    justify-content: space-between;
    align-items: flex-end;
    padding-bottom: 24px;
    border-bottom: 1px solid var(--p-surface-700);
}

.header-controls {
    display: flex;
    gap: 16px;
    align-items: flex-end;
}

.control-group {
    display: flex;
    flex-direction: column;
    gap: 8px;
}

.control-group label {
    font-size: 0.75rem;
    font-weight: 600;
    color: var(--p-surface-400);
    text-transform: uppercase;
}

.playground-content {
    display: flex;
    flex: 1;
    gap: 24px;
    min-height: 0;
}

.chat-container {
    flex: 1;
    display: flex;
    flex-direction: column;
    background-color: var(--p-surface-0);
    border-radius: 12px;
    border: 1px solid var(--p-surface-200);
    overflow: hidden;
}

.chat-messages {
    flex: 1;
    padding: 24px;
}

.message-list {
    display: flex;
    flex-direction: column;
    gap: 24px;
}

.empty-state {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    height: 300px;
    text-align: center;
    color: var(--p-surface-400);
}

.message-wrapper {
    display: flex;
    gap: 16px;
}

.message-avatar {
    width: 36px;
    height: 36px;
    border-radius: 8px;
    background-color: var(--p-surface-100);
    display: flex;
    align-items: center;
    justify-content: center;
    color: var(--p-surface-400);
    flex-shrink: 0;
}

.user .message-avatar {
    background-color: var(--p-primary-500);
    color: var(--p-primary-0);
}

.message-bubble {
    flex: 1;
    min-width: 0;
}

.message-text {
    background-color: var(--p-surface-100);
    padding: 12px 16px;
    border-radius: 0 12px 12px 12px;
    color: var(--p-surface-900);
    line-height: 1.5;
    white-space: pre-wrap;
    word-break: break-word;
}

.user .message-text {
    background-color: transparent;
    padding: 0;
    font-size: 1.1rem;
    font-weight: 500;
    color: var(--p-surface-900);
}

.tool-call-info {
    background-color: var(--p-surface-50);
    padding: 12px;
    border-radius: 8px;
    border-left: 4px solid var(--p-info-500);
}

.tool-call-info pre {
    margin: 0;
    font-family: monospace;
    font-size: 0.85rem;
    color: var(--p-info-600);
}

.chat-input-area {
    padding: 24px;
    border-top: 1px solid var(--p-surface-200);
    background-color: var(--p-surface-0);
}

.input-container {
    display: flex;
    gap: 12px;
}

.input-footer {
    margin-top: 12px;
    display: flex;
    justify-content: center;
}

.config-sidebar {
    width: 300px;
}

.sessions-sidebar {
    width: 240px;
    display: flex;
    flex-direction: column;
    background-color: var(--p-surface-50);
    border-radius: 12px;
    border: 1px solid var(--p-surface-200);
    overflow: hidden;
}

.sidebar-header {
    padding: 12px 16px;
    display: flex;
    justify-content: space-between;
    align-items: center;
    border-bottom: 1px solid var(--p-surface-200);
    background-color: var(--p-surface-100);
}

.sessions-list {
    flex: 1;
}

.session-item {
    padding: 10px 16px;
    display: flex;
    align-items: center;
    gap: 12px;
    cursor: pointer;
    transition: all 0.2s;
    border-bottom: 1px solid var(--p-surface-100);
}

.session-item:hover {
    background-color: var(--p-surface-100);
}

.session-item.active {
    background-color: var(--p-primary-100);
    border-left: 3px solid var(--p-primary-500);
}

.session-title {
    font-size: 0.85rem;
    color: var(--p-surface-700);
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
}

.config-details {
    display: flex;
    flex-direction: column;
    gap: 20px;
}

.detail-item {
    display: flex;
    flex-direction: column;
    gap: 8px;
}

.detail-item label {
    font-size: 0.75rem;
    font-weight: 600;
    color: var(--p-surface-500);
    text-transform: uppercase;
}

.prompt-box {
    background-color: var(--p-surface-50);
    padding: 12px;
    border-radius: 8px;
    font-size: 0.9rem;
    color: var(--p-surface-700);
    max-height: 200px;
    overflow-y: auto;
    white-space: pre-wrap;
}
</style>

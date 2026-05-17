<script setup lang="ts">
import { ref, onMounted, onUnmounted, computed, nextTick, watch } from 'vue';
import { useApi } from '../composables/useApi';
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import Button from 'primevue/button';
import InputText from 'primevue/inputtext';
import Select from 'primevue/select';

const { apiFetch, getToken, API_BASE } = useApi();

// ─── State ───
interface HandoffSession {
    sessionId: string;
    userId: string;
    projectId: string | null;
    projectName: string;
    configurationName: string | null;
    handoffStatus: string;
    agentId: string | null;
    queuedAt: string | null;
    claimedAt: string | null;
    lastMessage: string;
    messageCount: number;
}

interface ChatMessage {
    id: string;
    role: string;
    content: string;
    createdAt: string;
    feedback?: number | null;
}

const queuedSessions = ref<HandoffSession[]>([]);
const activeSessions = ref<HandoffSession[]>([]);
const selectedSession = ref<HandoffSession | null>(null);
const messages = ref<ChatMessage[]>([]);
const messageInput = ref('');
const loadingMessages = ref(false);
const sending = ref(false);
const isUserTyping = ref(false);
const isAgentTypingSignalSent = ref(false);
let agentTypingTimeout: any = null;
const tab = ref<'queue' | 'active'>('queue');
const projects = ref<{ id: string; name: string }[]>([]);
const selectedProjectId = ref<string | null>(null);

// SignalR
let connection: any = null;
const connected = ref(false);

const messagesContainer = ref<HTMLElement | null>(null);

function scrollToBottom() {
    nextTick(() => {
        if (messagesContainer.value) {
            messagesContainer.value.scrollTop = messagesContainer.value.scrollHeight;
        }
    });
}

// ─── API Calls ───
async function loadSessions() {
    try {
        const url = selectedProjectId.value
            ? `/api/handoff/all?projectId=${selectedProjectId.value}`
            : '/api/handoff/all';
        const res = await apiFetch(url);
        if (res.ok) {
            const data = await res.json();
            queuedSessions.value = data.queued;
            activeSessions.value = data.active;
        }
    } catch (e) { console.error(e); }
}

async function loadProjects() {
    try {
        const res = await apiFetch('/api/project');
        if (res.ok) projects.value = await res.json();
    } catch (e) { console.error(e); }
}

async function selectSession(session: HandoffSession) {
    isUserTyping.value = false;
    isAgentTypingSignalSent.value = false;
    if (agentTypingTimeout) clearTimeout(agentTypingTimeout);

    selectedSession.value = session;
    loadingMessages.value = true;
    try {
        const res = await apiFetch(`/api/handoff/session/${session.sessionId}/messages`);
        if (res.ok) {
            const data = await res.json();
            messages.value = data.messages;
            scrollToBottom();
        }
    } catch (e) { console.error(e); }
    loadingMessages.value = false;
}

async function claimSession(session: HandoffSession) {
    if (!connection) return;
    await connection.invoke('ClaimSession', session.sessionId);
}

function handleAgentTyping() {
    if (!selectedSession.value || !connection) return;

    if (!isAgentTypingSignalSent.value) {
        isAgentTypingSignalSent.value = true;
        connection.invoke('SendAgentTyping', selectedSession.value.sessionId, true).catch(console.error);
    }

    if (agentTypingTimeout) clearTimeout(agentTypingTimeout);
    agentTypingTimeout = setTimeout(() => {
        isAgentTypingSignalSent.value = false;
        if (connection && selectedSession.value) {
            connection.invoke('SendAgentTyping', selectedSession.value.sessionId, false).catch(console.error);
        }
    }, 2000);
}

watch(messageInput, (newVal) => {
    if (newVal.trim().length > 0) {
        handleAgentTyping();
    }
});

async function sendMessage() {
    if (!selectedSession.value || !messageInput.value.trim() || !connection) return;

    sending.value = true;
    try {
        if (agentTypingTimeout) clearTimeout(agentTypingTimeout);
        isAgentTypingSignalSent.value = false;
        await connection.invoke('SendAgentTyping', selectedSession.value.sessionId, false).catch(console.error);

        await connection.invoke('SendAgentMessage', selectedSession.value.sessionId, messageInput.value.trim());
        messageInput.value = '';
    } catch (e) { console.error(e); }
    sending.value = false;
}

async function resolveSession() {
    if (!selectedSession.value || !connection) return;
    await connection.invoke('ResolveSession', selectedSession.value.sessionId);
    selectedSession.value = null;
    messages.value = [];
    await loadSessions();
}

async function returnToAi() {
    if (!selectedSession.value || !connection) return;
    await connection.invoke('ReturnToAi', selectedSession.value.sessionId);
    selectedSession.value = null;
    messages.value = [];
    await loadSessions();
}

// ─── SignalR Setup ───
async function setupSignalR() {
    const token = getToken();
    if (!token) return;

    connection = new HubConnectionBuilder()
        .withUrl(`${API_BASE}/liveChatHub`, {
            accessTokenFactory: () => token
        })
        .withAutomaticReconnect()
        .configureLogging(LogLevel.Information)
        .build();

    // Agent pool events
    connection.on('NewSessionQueued', () => {
        loadSessions();
    });

    connection.on('SessionClaimed', () => {
        loadSessions();
    });

    connection.on('SessionClaimResult', (data: any) => {
        if (data.success) {
            loadSessions();
            // Select the newly claimed session
            const claimed = queuedSessions.value.find(s => s.sessionId === data.sessionId)
                || activeSessions.value.find(s => s.sessionId === data.sessionId);
            if (claimed) {
                claimed.handoffStatus = 'active';
                selectSession(claimed);
                tab.value = 'active';
            }
        }
    });

    // Real-time messages
    connection.on('ReceiveUserMessage', (msg: any) => {
        if (selectedSession.value && msg.sessionId === selectedSession.value.sessionId) {
            messages.value.push({
                id: msg.id,
                role: msg.role,
                content: msg.content,
                createdAt: msg.createdAt
            });
            scrollToBottom();
        }
    });

    connection.on('ReceiveAgentMessage', (msg: any) => {
        if (selectedSession.value && selectedSession.value.sessionId) {
            messages.value.push({
                id: msg.id,
                role: msg.role,
                content: msg.content,
                createdAt: msg.createdAt
            });
            scrollToBottom();
        }
    });

    connection.on('ReceiveUserTyping', (data: any) => {
        if (selectedSession.value && data.sessionId === selectedSession.value.sessionId) {
            isUserTyping.value = data.isTyping;
            scrollToBottom();
        }
    });

    try {
        await connection.start();
        connected.value = true;

        // Join agent pool for all projects
        for (const p of projects.value) {
            await connection.invoke('JoinAgentPool', p.id);
        }
    } catch (e) {
        console.error('SignalR connection failed:', e);
    }
}

// ─── Helpers ───
function formatTime(iso: string | null): string {
    if (!iso) return '';
    return new Date(iso).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
}

function waitTime(iso: string | null): string {
    if (!iso) return '';
    const diff = Math.floor((Date.now() - new Date(iso).getTime()) / 1000);
    if (diff < 60) return `${diff}s`;
    if (diff < 3600) return `${Math.floor(diff / 60)}m`;
    return `${Math.floor(diff / 3600)}h ${Math.floor((diff % 3600) / 60)}m`;
}

function roleLabel(role: string): string {
    switch (role) {
        case 'user': return 'User';
        case 'model': return 'AI';
        case 'agent': return 'You';
        default: return role;
    }
}

function roleClass(role: string): string {
    switch (role) {
        case 'user': return 'msg-user';
        case 'model': return 'msg-ai';
        case 'agent': return 'msg-agent';
        default: return 'msg-system';
    }
}

const displayedSessions = computed(() => tab.value === 'queue' ? queuedSessions.value : activeSessions.value);

// Watch project filter
watch(selectedProjectId, () => loadSessions());

// ─── Lifecycle ───
onMounted(async () => {
    await loadProjects();
    await loadSessions();
    await setupSignalR();
});

onUnmounted(() => {
    connection?.stop();
});
</script>

<template>
    <div class="live-chat">
        <header class="page-header">
            <div>
                <h1>Live Chat</h1>
                <p class="subtitle">Manage human handoff conversations in real-time.</p>
            </div>
            <div class="header-controls">
                <span :class="['connection-indicator', { online: connected }]">
                    <i :class="['pi', connected ? 'pi-circle-fill' : 'pi-circle']"></i>
                    {{ connected ? 'Connected' : 'Disconnected' }}
                </span>
                <Select
                    v-model="selectedProjectId"
                    :options="[{ id: null, name: 'All Projects' }, ...projects]"
                    optionLabel="name"
                    optionValue="id"
                    placeholder="Filter by project"
                    style="min-width: 200px"
                />
            </div>
        </header>

        <div class="chat-layout">
            <!-- Left Panel: Session List -->
            <aside class="session-panel">
                <div class="tab-bar">
                    <button :class="['tab-btn', { active: tab === 'queue' }]" @click="tab = 'queue'">
                        Queue
                        <span v-if="queuedSessions.length" class="tab-badge">{{ queuedSessions.length }}</span>
                    </button>
                    <button :class="['tab-btn', { active: tab === 'active' }]" @click="tab = 'active'">
                        My Active
                        <span v-if="activeSessions.length" class="tab-badge tab-badge-active">{{ activeSessions.length }}</span>
                    </button>
                </div>

                <div class="session-list">
                    <div
                        v-for="s in displayedSessions"
                        :key="s.sessionId"
                        :class="['session-card', { selected: selectedSession?.sessionId === s.sessionId }]"
                        @click="selectSession(s)"
                    >
                        <div class="session-card-header">
                            <span class="session-user">{{ s.userId }}</span>
                            <span class="session-time">{{ waitTime(s.queuedAt) }}</span>
                        </div>
                        <div class="session-project">{{ s.projectName }}</div>
                        <p class="session-preview">{{ s.lastMessage.substring(0, 80) }}{{ s.lastMessage.length > 80 ? '...' : '' }}</p>
                        <div class="session-card-footer">
                            <span :class="['status-badge', 'status-' + s.handoffStatus]">{{ s.handoffStatus }}</span>
                            <Button
                                v-if="s.handoffStatus === 'queued'"
                                label="Claim"
                                icon="pi pi-check"
                                size="small"
                                @click.stop="claimSession(s)"
                            />
                        </div>
                    </div>
                    <div v-if="displayedSessions.length === 0" class="empty-state">
                        <i class="pi pi-inbox"></i>
                        <p>{{ tab === 'queue' ? 'No sessions in queue' : 'No active sessions' }}</p>
                    </div>
                </div>
            </aside>

            <!-- Right Panel: Chat -->
            <main class="chat-panel">
                <template v-if="selectedSession">
                    <!-- Chat Header -->
                    <div class="chat-header">
                        <div class="chat-header-info">
                            <h3>{{ selectedSession.userId }}</h3>
                            <span class="chat-header-project">{{ selectedSession.projectName }}</span>
                            <span :class="['status-badge', 'status-' + selectedSession.handoffStatus]">
                                {{ selectedSession.handoffStatus }}
                            </span>
                        </div>
                        <div class="chat-header-actions" v-if="selectedSession.handoffStatus === 'active'">
                            <Button label="Return to AI" icon="pi pi-replay" severity="secondary" outlined size="small" @click="returnToAi" />
                            <Button label="Resolve" icon="pi pi-check-circle" severity="success" size="small" @click="resolveSession" />
                        </div>
                    </div>

                    <!-- Messages -->
                    <div ref="messagesContainer" class="messages-area">
                        <div v-if="loadingMessages" class="loading-messages">
                            <i class="pi pi-spin pi-spinner"></i> Loading messages...
                        </div>
                        <template v-else>
                            <div
                                v-for="msg in messages"
                                :key="msg.id"
                                :class="['message', roleClass(msg.role)]"
                            >
                                <div class="message-header">
                                    <span class="message-role">{{ roleLabel(msg.role) }}</span>
                                    <span class="message-time">{{ formatTime(msg.createdAt) }}</span>
                                </div>
                                <div class="message-content">{{ msg.content }}</div>
                            </div>
                            <div v-if="isUserTyping" class="message message-user message-typing">
                                <div class="message-header">
                                    <span class="message-role">User is typing...</span>
                                </div>
                                <div class="message-content">
                                    <div class="typing-indicator">
                                        <div class="typing-dot"></div>
                                        <div class="typing-dot"></div>
                                        <div class="typing-dot"></div>
                                    </div>
                                </div>
                            </div>
                        </template>
                    </div>

                    <!-- Input -->
                    <div v-if="selectedSession.handoffStatus === 'active'" class="chat-input">
                        <InputText
                            v-model="messageInput"
                            placeholder="Type your message..."
                            fluid
                            @keyup.enter="sendMessage"
                        />
                        <Button
                            icon="pi pi-send"
                            :loading="sending"
                            @click="sendMessage"
                            :disabled="!messageInput.trim()"
                        />
                    </div>
                    <div v-else-if="selectedSession.handoffStatus === 'queued'" class="chat-input-disabled">
                        <i class="pi pi-lock"></i> Claim this session to start chatting
                    </div>
                </template>

                <!-- Empty State -->
                <div v-else class="chat-empty">
                    <div class="chat-empty-content">
                        <i class="pi pi-comments"></i>
                        <h3>Select a conversation</h3>
                        <p>Choose a session from the queue or your active conversations to start chatting.</p>
                    </div>
                </div>
            </main>
        </div>
    </div>
</template>

<style scoped>
.live-chat {
    height: calc(100vh - 40px);
    display: flex;
    flex-direction: column;
}

.page-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 24px;
    flex-shrink: 0;
}
.page-header h1 { margin: 0; }
.subtitle {
    color: var(--p-surface-500);
    font-size: 0.85rem;
    margin: 4px 0 0 0;
}

.header-controls {
    display: flex;
    align-items: center;
    gap: 16px;
}

.connection-indicator {
    display: flex;
    align-items: center;
    gap: 6px;
    font-size: 0.8rem;
    color: var(--p-surface-400);
    padding: 6px 12px;
    border-radius: 20px;
    background: var(--p-surface-100);
}
.connection-indicator.online {
    color: var(--p-green-600);
    background: var(--p-green-50);
}

/* ─── Layout ─── */
.chat-layout {
    display: flex;
    flex: 1;
    gap: 0;
    border: 1px solid var(--p-surface-200);
    border-radius: 12px;
    overflow: hidden;
    min-height: 0;
}

/* ─── Session Panel ─── */
.session-panel {
    width: 340px;
    min-width: 340px;
    border-right: 1px solid var(--p-surface-200);
    display: flex;
    flex-direction: column;
    background: var(--p-surface-50);
}

.tab-bar {
    display: flex;
    border-bottom: 1px solid var(--p-surface-200);
    flex-shrink: 0;
}
.tab-btn {
    flex: 1;
    padding: 12px 16px;
    border: none;
    background: none;
    cursor: pointer;
    font-weight: 500;
    font-size: 0.85rem;
    color: var(--p-surface-500);
    border-bottom: 2px solid transparent;
    transition: all 0.2s ease;
    display: flex;
    align-items: center;
    justify-content: center;
    gap: 8px;
}
.tab-btn:hover { color: var(--p-surface-700); }
.tab-btn.active {
    color: var(--p-primary-600);
    border-bottom-color: var(--p-primary-500);
}
.tab-badge {
    font-size: 0.7rem;
    padding: 2px 7px;
    border-radius: 10px;
    background: var(--p-red-500);
    color: white;
    font-weight: 700;
}
.tab-badge-active {
    background: var(--p-green-500);
}

.session-list {
    flex: 1;
    overflow-y: auto;
    padding: 8px;
}

.session-card {
    padding: 12px;
    border-radius: 8px;
    cursor: pointer;
    transition: all 0.15s ease;
    margin-bottom: 4px;
    border: 1px solid transparent;
}
.session-card:hover {
    background: var(--p-surface-100);
}
.session-card.selected {
    background: var(--p-primary-50);
    border-color: var(--p-primary-200);
}

.session-card-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 4px;
}
.session-user {
    font-weight: 600;
    font-size: 0.85rem;
    color: var(--p-surface-800);
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    max-width: 180px;
}
.session-time {
    font-size: 0.75rem;
    color: var(--p-surface-400);
}
.session-project {
    font-size: 0.75rem;
    color: var(--p-primary-500);
    margin-bottom: 4px;
}
.session-preview {
    font-size: 0.8rem;
    color: var(--p-surface-600);
    margin: 0 0 8px 0;
    line-height: 1.4;
}
.session-card-footer {
    display: flex;
    justify-content: space-between;
    align-items: center;
}

.status-badge {
    font-size: 0.7rem;
    padding: 2px 8px;
    border-radius: 10px;
    font-weight: 600;
    text-transform: uppercase;
}
.status-queued {
    background: var(--p-orange-100);
    color: var(--p-orange-700);
}
.status-active {
    background: var(--p-green-100);
    color: var(--p-green-700);
}
.status-resolved {
    background: var(--p-surface-100);
    color: var(--p-surface-600);
}
.status-ai {
    background: var(--p-blue-100);
    color: var(--p-blue-700);
}

.empty-state {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    padding: 48px 16px;
    color: var(--p-surface-400);
    text-align: center;
}
.empty-state i { font-size: 2rem; margin-bottom: 8px; }
.empty-state p { margin: 0; font-size: 0.85rem; }

/* ─── Chat Panel ─── */
.chat-panel {
    flex: 1;
    display: flex;
    flex-direction: column;
    background: var(--p-surface-0);
    min-width: 0;
}

.chat-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding: 16px 20px;
    border-bottom: 1px solid var(--p-surface-200);
    flex-shrink: 0;
}
.chat-header-info {
    display: flex;
    align-items: center;
    gap: 12px;
}
.chat-header-info h3 {
    margin: 0;
    font-size: 1rem;
}
.chat-header-project {
    font-size: 0.8rem;
    color: var(--p-primary-500);
}
.chat-header-actions {
    display: flex;
    gap: 8px;
}

/* ─── Messages ─── */
.messages-area {
    flex: 1;
    overflow-y: auto;
    padding: 20px;
    display: flex;
    flex-direction: column;
    gap: 12px;
}

.loading-messages {
    text-align: center;
    padding: 48px;
    color: var(--p-surface-400);
}

.message {
    max-width: 80%;
    padding: 10px 14px;
    border-radius: 12px;
    font-size: 0.88rem;
    line-height: 1.5;
}
.msg-user {
    align-self: flex-start;
    background: var(--p-surface-100);
    color: var(--p-surface-800);
    border-bottom-left-radius: 4px;
}
.msg-ai {
    align-self: flex-start;
    background: var(--p-blue-50);
    color: var(--p-blue-800);
    border-bottom-left-radius: 4px;
    border-left: 3px solid var(--p-blue-300);
}
.msg-agent {
    align-self: flex-end;
    background: var(--p-primary-500);
    color: white;
    border-bottom-right-radius: 4px;
}
.msg-system {
    align-self: center;
    background: var(--p-surface-50);
    color: var(--p-surface-500);
    font-style: italic;
    font-size: 0.82rem;
}

.message-header {
    display: flex;
    justify-content: space-between;
    gap: 16px;
    margin-bottom: 4px;
}
.message-role {
    font-weight: 600;
    font-size: 0.75rem;
    text-transform: uppercase;
    opacity: 0.7;
}
.message-time {
    font-size: 0.7rem;
    opacity: 0.5;
}
.message-content {
    white-space: pre-wrap;
    word-break: break-word;
}

/* ─── Input ─── */
.chat-input {
    display: flex;
    gap: 8px;
    padding: 16px 20px;
    border-top: 1px solid var(--p-surface-200);
    flex-shrink: 0;
}

.chat-input-disabled {
    display: flex;
    align-items: center;
    justify-content: center;
    gap: 8px;
    padding: 16px 20px;
    border-top: 1px solid var(--p-surface-200);
    color: var(--p-surface-400);
    font-size: 0.85rem;
    flex-shrink: 0;
}

/* ─── Empty Chat ─── */
.chat-empty {
    flex: 1;
    display: flex;
    align-items: center;
    justify-content: center;
}
.chat-empty-content {
    text-align: center;
    color: var(--p-surface-400);
}
.chat-empty-content i {
    font-size: 3rem;
    margin-bottom: 16px;
}
.chat-empty-content h3 {
    margin: 0 0 8px 0;
    color: var(--p-surface-600);
}
.chat-empty-content p {
    margin: 0;
    font-size: 0.85rem;
}

/* ─── Responsive ─── */
@media (max-width: 768px) {
    .chat-layout {
        flex-direction: column;
    }
    .session-panel {
        width: 100%;
        min-width: auto;
        max-height: 300px;
        border-right: none;
        border-bottom: 1px solid var(--p-surface-200);
    }
    .page-header {
        flex-direction: column;
        align-items: flex-start;
        gap: 12px;
    }
    .header-controls {
        flex-wrap: wrap;
    }
}

/* ─── Typing Indicator ─── */
.message-typing {
    opacity: 0.85;
}
.typing-indicator {
    display: flex;
    align-items: center;
    gap: 4px;
    padding: 4px 0;
    min-height: 16px;
}
.typing-dot {
    width: 6px;
    height: 6px;
    background-color: var(--p-surface-600);
    border-radius: 50%;
    animation: bounce 1.4s infinite ease-in-out both;
}
.typing-dot:nth-child(1) {
    animation-delay: -0.32s;
}
.typing-dot:nth-child(2) {
    animation-delay: -0.16s;
}

@keyframes bounce {
    0%, 80%, 100% { 
        transform: scale(0);
    } 40% { 
        transform: scale(1.0);
    }
}
</style>

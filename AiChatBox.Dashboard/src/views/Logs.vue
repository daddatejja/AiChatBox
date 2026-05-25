<script setup lang="ts">
import { ref, onMounted, watch } from 'vue';
import { useRouter } from 'vue-router';
import { useApi } from '../composables/useApi';
import DataTable from 'primevue/datatable';
import Column from 'primevue/column';
import Button from 'primevue/button';
import Select from 'primevue/select';
import Dialog from 'primevue/dialog';
import InputText from 'primevue/inputtext';
import Tag from 'primevue/tag';
import Tabs from 'primevue/tabs';
import TabList from 'primevue/tablist';
import Tab from 'primevue/tab';
import TabPanels from 'primevue/tabpanels';
import TabPanel from 'primevue/tabpanel';
import Timeline from 'primevue/timeline';
import ScrollPanel from 'primevue/scrollpanel';

const { apiFetch } = useApi();
const router = useRouter();

const logs = ref<any[]>([]);
const totalLogs = ref(0);
const offset = ref(0);
const limit = ref(20);
const loading = ref(true);

const flowLogs = ref<any[]>([]);
const totalFlowLogs = ref(0);
const flowOffset = ref(0);
const flowLimit = ref(20);
const flowLoading = ref(true);
const activeTab = ref('request-logs');

const projects = ref<any[]>([]);
const selectedProject = ref<any>(null);
const searchQuery = ref('');
const sortField = ref('createdAt');
const sortOrder = ref(-1);

async function loadProjects() {
    try {
        const res = await apiFetch('/api/project');
        if (res.ok) {
            projects.value = await res.json();
        }
    } catch(e) { console.error(e); }
}

async function load() {
    loading.value = true;
    try {
        let url = `/api/logs?offset=${offset.value}&limit=${limit.value}&sortField=${sortField.value}&sortOrder=${sortOrder.value}`;
        
        if (selectedProject.value) {
            url += `&projectId=${selectedProject.value.id}`;
        }
        
        if (searchQuery.value) {
            url += `&search=${encodeURIComponent(searchQuery.value)}`;
        }

        const res = await apiFetch(url);
        if (res.ok) {
            const data = await res.json();
            logs.value = data.items || [];
            totalLogs.value = data.total || 0;
        }
    } catch(e) {
        console.error(e);
    }
    loading.value = false;
}

async function loadFlowLogs() {
    if (!selectedProject.value) {
        flowLogs.value = [];
        totalFlowLogs.value = 0;
        return;
    }
    flowLoading.value = true;
    try {
        const res = await apiFetch(`/api/projects/${selectedProject.value.id}/flows/execution-logs?offset=${flowOffset.value}&limit=${flowLimit.value}`);
        if (res.ok) {
            const data = await res.json();
            flowLogs.value = data.items || [];
            totalFlowLogs.value = data.total || 0;
        }
    } catch(e) {
        console.error(e);
    }
    flowLoading.value = false;
}

const onPage = (event: any) => {
    offset.value = event.first;
    limit.value = event.rows;
    load();
};

const onFlowPage = (event: any) => {
    flowOffset.value = event.first;
    flowLimit.value = event.rows;
    loadFlowLogs();
};

const onSort = (event: any) => {
    sortField.value = event.sortField;
    sortOrder.value = event.sortOrder;
    load();
};

let searchTimeout: any = null;
watch(searchQuery, () => {
    if (searchTimeout) clearTimeout(searchTimeout);
    searchTimeout = setTimeout(() => {
        if (activeTab.value === 'request-logs') {
            offset.value = 0;
            load();
        }
    }, 500);
});

watch(selectedProject, () => {
    offset.value = 0;
    flowOffset.value = 0;
    load();
    loadFlowLogs();
});

watch(activeTab, (newTab) => {
    if (newTab === 'flow-logs') {
        loadFlowLogs();
    } else {
        load();
    }
});

const formatDate = (value: string) => {
    return new Date(value).toLocaleString();
};

const getErrorSummary = (msg: string) => {
    if (!msg) return '';
    // Look for status code or status name
    const match = msg.match(/status code (\w+)/i) || msg.match(/(\d{3})/);
    return match ? match[1] : 'Error';
};

const visibleDetail = ref(false);
const selectedLog = ref<any>(null);
const traceLogs = ref<any[]>([]);
const loadingTrace = ref(false);

const getLogColor = (endpoint: string) => {
    if (!endpoint) return '#64748b';
    if (endpoint.includes('User')) return '#3b82f6';
    if (endpoint.includes('Model')) return '#8b5cf6';
    if (endpoint.includes('Tool')) return '#f59e0b';
    if (endpoint.includes('Embedding')) return '#10b981';
    if (endpoint.includes('Generate')) return '#8b5cf6';
    if (endpoint.includes('Error')) return '#ef4444';
    return '#64748b';
};

const getLogIcon = (endpoint: string) => {
    if (!endpoint) return 'pi pi-server';
    if (endpoint.includes('User')) return 'pi pi-user';
    if (endpoint.includes('Model')) return 'pi pi-sparkles';
    if (endpoint.includes('Tool')) return 'pi pi-cog';
    if (endpoint.includes('Embedding')) return 'pi pi-database';
    if (endpoint.includes('Generate')) return 'pi pi-bolt';
    return 'pi pi-server';
};

const formatPreview = (raw: string) => {
    if (!raw) return '-';
    
    if (raw.includes('[Transcription]') || raw.includes('[Model]')) {
        const lines = raw.split('\n').filter(l => l.trim());
        const lastLine = lines[lines.length - 1] || '';
        return lastLine.replace(/\[(Transcription|Model)\]:?\s*/g, '');
    }

    try {
        const parsed = JSON.parse(raw);
        if (parsed.message) return parsed.message;
        if (parsed.text) return parsed.text;
        if (parsed.toolName) return `Executing: ${parsed.toolName}`;
        if (typeof parsed === 'object') {
            const firstKey = Object.keys(parsed)[0];
            return `${firstKey}: ${JSON.stringify(parsed[firstKey])}`;
        }
    } catch {
        return raw.length > 80 ? raw.substring(0, 80) + '...' : raw;
    }
    return raw;
};

async function showDetail(log: any) {
    selectedLog.value = log;
    visibleDetail.value = true;
    traceLogs.value = [];
    
    const sid = log.sessionId || log.SessionId;
    if (sid) {
        loadingTrace.value = true;
        try {
            const res = await apiFetch(`/api/logs/trace/${sid}`);
            if (res.ok) {
                const logs = await res.json();
                let combined: any[] = [];
                for (const l of logs) {
                    if (l.endpoint === 'GeminiLive/Session') {
                        const parsedEvents = parseLiveTimeline(l.rawResponse);
                        for (const evt of parsedEvents) {
                            combined.push({
                                id: evt.id || Math.random().toString(),
                                isLiveEvent: true,
                                createdAt: evt.timestamp,
                                endpoint: evt.type,
                                type: evt.type,
                                meta: evt.meta,
                                content: evt.content,
                                transcription: evt.transcription,
                                audioSrc: evt.audioSrc,
                                rawResponse: l.rawResponse,
                            });
                        }
                    } else {
                        combined.push(l);
                    }
                }
                combined.sort((a, b) => new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime());
                traceLogs.value = combined;
            }
        } catch(e) { console.error(e); }
        loadingTrace.value = false;
    }
}

async function togglePin(log: any) {
    try {
        const res = await apiFetch(`/api/logs/${log.id}/pin`, { method: 'POST' });
        if (res.ok) {
            const data = await res.json();
            log.isPinned = data.isPinned;
        }
    } catch(e) { console.error(e); }
}

const visibleFlowDetail = ref(false);
const selectedFlowLog = ref<any>(null);
const flowLogSteps = ref<any[]>([]);

async function showFlowDetail(log: any) {
    selectedFlowLog.value = log;
    visibleFlowDetail.value = true;
    flowLogSteps.value = [];
    
    try {
        const res = await apiFetch(`/api/projects/${selectedProject.value.id}/flows/execution-logs/${log.id}`);
        if (res.ok) {
            const data = await res.json();
            selectedFlowLog.value = data;
            flowLogSteps.value = JSON.parse(data.stepsJson || '[]');
        }
    } catch(e) {
        console.error(e);
    }
}

function replayRun(log: any) {
    visibleFlowDetail.value = false;
    router.push(`/project/${selectedProject.value.id}/flow/${log.flowId}?replayLogId=${log.id}`);
}

const pcmBase64ArrayToWavDataUrl = (base64Chunks: string[], sampleRate: number = 24000) => {
    let totalLen = 0;
    const binaryChunks = base64Chunks.map(b => {
        const bin = window.atob(b);
        totalLen += bin.length;
        return bin;
    });
    
    if (totalLen === 0) return '';
    
    const bytes = new Uint8Array(totalLen);
    let offset = 0;
    for (const bin of binaryChunks) {
        for (let i = 0; i < bin.length; i++) {
            bytes[offset++] = bin.charCodeAt(i);
        }
    }
    
    const numChannels = 1;
    const bitsPerSample = 16;
    const byteRate = sampleRate * numChannels * (bitsPerSample / 8);
    const blockAlign = numChannels * (bitsPerSample / 8);
    
    const buffer = new ArrayBuffer(44 + bytes.length);
    const view = new DataView(buffer);
    
    const writeString = (v: DataView, off: number, str: string) => {
        for (let i = 0; i < str.length; i++) {
            v.setUint8(off + i, str.charCodeAt(i));
        }
    };
    
    writeString(view, 0, 'RIFF');
    view.setUint32(4, 36 + bytes.length, true);
    writeString(view, 8, 'WAVE');
    writeString(view, 12, 'fmt ');
    view.setUint32(16, 16, true);
    view.setUint16(20, 1, true);
    view.setUint16(22, numChannels, true);
    view.setUint32(24, sampleRate, true);
    view.setUint32(28, byteRate, true);
    view.setUint16(32, blockAlign, true);
    view.setUint16(34, bitsPerSample, true);
    writeString(view, 36, 'data');
    view.setUint32(40, bytes.length, true);
    
    const data = new Uint8Array(buffer, 44);
    data.set(bytes);
    
    const outBytes = new Uint8Array(buffer);
    let binary = '';
    const chunkSize = 8192;
    for (let i = 0; i < outBytes.length; i += chunkSize) {
        const chunk = outBytes.subarray(i, i + chunkSize);
        binary += String.fromCharCode.apply(null, chunk as any);
    }
    const wavBase64 = window.btoa(binary);
    return `data:audio/wav;base64,${wavBase64}`;
};

const formatJsonDetail = (raw: string) => {
    if (!raw) return '';
    try {
        const parsed = JSON.parse(raw);
        const truncateBase64 = (obj: any) => {
            if (Array.isArray(obj)) {
                obj.forEach(truncateBase64);
            } else if (typeof obj === 'object' && obj !== null) {
                for (const key in obj) {
                    if (typeof obj[key] === 'string' && obj[key].length > 1000) {
                        obj[key] = `[Binary Data Omitted - Length: ${obj[key].length}]`;
                    } else {
                        truncateBase64(obj[key]);
                    }
                }
            }
        };
        truncateBase64(parsed);
        return JSON.stringify(parsed, null, 2);
    } catch {
        return raw;
    }
};

const parseLiveTimeline = (rawResponse: string) => {
    if (!rawResponse) return [];
    try {
        const parsed = JSON.parse(rawResponse);
        if (Array.isArray(parsed)) {
            const merged: any[] = [];
            for (const evt of parsed) {
                const last = merged[merged.length - 1];
                if (last && last.type === evt.type && evt.type.includes('Audio')) {
                    last.base64Chunks.push(evt.content);
                    if (evt.transcription && evt.transcription !== "(untranscribed)") {
                         last.transcription = (last.transcription && last.transcription !== "(untranscribed)" ? last.transcription + " " : "") + evt.transcription;
                    }
                } else {
                    evt.base64Chunks = evt.content ? [evt.content] : [];
                    merged.push({ ...evt });
                }
            }
            merged.forEach(evt => {
                 if (evt.type.includes('Audio') && evt.base64Chunks.length > 0) {
                     try {
                         const rate = evt.type === 'UserAudio' ? 16000 : 24000;
                         evt.audioSrc = pcmBase64ArrayToWavDataUrl(evt.base64Chunks, rate);
                     } catch(e) { console.error("Audio conversion failed", e); }
                 }
            });
            return merged;
        }
    } catch { }
    return [];
};

const getEventTypeClass = (type: string) => {
    if (type.includes('User')) return 'request';
    if (type.includes('Model')) return 'response';
    if (type.includes('Tool')) return 'tool-block';
    return 'system-block';
};

onMounted(() => {
    loadProjects();
    load();
});
</script>

<template>
    <div>
        <header class="header">
            <div class="header-main">
                <h1>Execution Telemetry & Logs</h1>
                <p class="subtitle">Inspect API request telemetry and interactive conversation flows</p>
            </div>
            <div class="header-actions">
                <div class="search-container" v-if="activeTab === 'request-logs'">
                    <i class="pi pi-search search-icon"></i>
                    <InputText v-model="searchQuery" placeholder="Search logs..." class="w-64 search-input" />
                </div>
                <Select v-model="selectedProject" :options="projects" optionLabel="name" placeholder="Filter by Project" showClear class="w-64" />
            </div>
        </header>

        <Tabs v-model:value="activeTab" class="w-full">
            <TabList class="mb-4">
                <Tab value="request-logs">API Request Logs</Tab>
                <Tab value="flow-logs">Flow Runs & Playback</Tab>
            </TabList>
            <TabPanels>
                <TabPanel value="request-logs">
                    <div class="logs-container">
                        <DataTable 
                            :value="logs" 
                            lazy 
                            paginator 
                            :rows="limit" 
                            :totalRecords="totalLogs"
                            :loading="loading"
                            @page="onPage" 
                            @sort="onSort"
                            sortField="createdAt"
                            :sortOrder="-1"
                            class="p-datatable-sm logs-table" 
                            responsiveLayout="scroll"
                            scrollable 
                            scrollHeight="calc(100vh - 280px)"
                        >
                            <Column field="createdAt" header="Time" sortable style="min-width: 160px">
                                <template #body="slotProps">
                                    <span class="text-secondary">{{ formatDate(slotProps.data.createdAt) }}</span>
                                </template>
                            </Column>
                            <Column field="endpoint" header="Endpoint" sortable style="min-width: 120px">
                                <template #body="slotProps">
                                    <span class="mono-text">{{ slotProps.data.endpoint || '-' }}</span>
                                </template>
                            </Column>
                            <Column field="inputTokens" header="In" sortable style="width: 80px"></Column>
                            <Column field="outputTokens" header="Out" sortable style="width: 80px"></Column>
                            <Column field="durationMs" header="Duration" sortable style="width: 100px">
                                <template #body="slotProps">
                                    {{ slotProps.data.durationMs }}ms
                                </template>
                            </Column>
                            <Column field="errorMessage" header="Status" style="width: 120px">
                                <template #body="slotProps">
                                    <Tag v-if="slotProps.data.errorMessage" severity="danger" :value="getErrorSummary(slotProps.data.errorMessage)" :title="slotProps.data.errorMessage" />
                                    <Tag v-else severity="success" value="Success" />
                                </template>
                            </Column>
                            <Column header="Pin" style="width: 60px">
                                <template #body="slotProps">
                                    <Button :icon="slotProps.data.isPinned ? 'pi pi-star-fill' : 'pi pi-star'" 
                                            :severity="slotProps.data.isPinned ? 'warn' : 'secondary'" 
                                            :text="!slotProps.data.isPinned" 
                                            rounded 
                                            @click="togglePin(slotProps.data)" />
                                </template>
                            </Column>
                            <Column header="Details" style="width: 80px">
                                <template #body="slotProps">
                                    <Button icon="pi pi-eye" severity="secondary" text rounded @click="showDetail(slotProps.data)" />
                                </template>
                            </Column>
                        </DataTable>
                        
                        <p v-if="!loading && !logs.length" class="empty-text">No logs found matching your criteria.</p>
                    </div>
                </TabPanel>

                <TabPanel value="flow-logs">
                    <div class="logs-container">
                        <DataTable 
                            :value="flowLogs" 
                            lazy 
                            paginator 
                            :rows="flowLimit" 
                            :totalRecords="totalFlowLogs"
                            :loading="flowLoading"
                            @page="onFlowPage"
                            class="p-datatable-sm logs-table"
                            responsiveLayout="scroll"
                            scrollable
                            scrollHeight="calc(100vh - 280px)"
                        >
                            <Column field="startedAt" header="Start Time" style="min-width: 160px">
                                <template #body="slotProps">
                                    <span class="text-secondary">{{ formatDate(slotProps.data.startedAt) }}</span>
                                </template>
                            </Column>
                            <Column field="flowName" header="Conversation Flow" style="min-width: 140px">
                                <template #body="slotProps">
                                    <span class="font-bold text-primary">{{ slotProps.data.flowName }}</span>
                                </template>
                            </Column>
                            <Column field="sessionTitle" header="Client Session" style="min-width: 160px">
                                <template #body="slotProps">
                                    <span class="mono-text text-sm">{{ slotProps.data.sessionTitle }}</span>
                                </template>
                            </Column>
                            <Column field="stepsCount" header="Steps" style="width: 80px">
                                <template #body="slotProps">
                                    <Tag severity="info" :value="slotProps.data.stepsCount + ' steps'" rounded />
                                </template>
                            </Column>
                            <Column field="totalDurationMs" header="Execution Time" style="width: 120px">
                                <template #body="slotProps">
                                    <span class="mono-text">{{ slotProps.data.totalDurationMs.toFixed(1) }}ms</span>
                                </template>
                            </Column>
                            <Column field="completedAt" header="Status" style="width: 120px">
                                <template #body="slotProps">
                                    <Tag v-if="slotProps.data.completedAt" severity="success" value="Completed" />
                                    <Tag v-else severity="warn" value="Active / Paused" />
                                </template>
                            </Column>
                            <Column header="Trace Playback" style="width: 100px">
                                <template #body="slotProps">
                                    <Button icon="pi pi-play" severity="primary" text rounded @click="showFlowDetail(slotProps.data)" label="Replay" class="p-button-sm" />
                                </template>
                            </Column>
                        </DataTable>
                        <p v-if="!flowLoading && !flowLogs.length" class="empty-text">No active flow logs found for this project.</p>
                    </div>
                </TabPanel>
            </TabPanels>
        </Tabs>

        <Dialog v-model:visible="visibleFlowDetail" modal header="Flow Execution Playback" :style="{ width: '55vw' }">
            <div v-if="selectedFlowLog" class="flow-log-playback-detail" style="margin: -1rem; padding: 1.5rem;">
                <div class="playback-header flex items-center justify-between mb-4 p-4 bg-surface-50 border border-surface-200 rounded-lg">
                    <div>
                        <h3 class="text-lg font-bold text-primary mb-1">{{ selectedFlowLog.flowName }}</h3>
                        <p class="text-xs text-secondary">
                            Session: <code class="mono-text">{{ selectedFlowLog.sessionTitle }}</code> | Started at: {{ formatDate(selectedFlowLog.startedAt) }}
                        </p>
                    </div>
                    <Button label="Visual Replay in Canvas" icon="pi pi-sparkles" severity="success" @click="replayRun(selectedFlowLog)" />
                </div>
                
                <h4 class="text-sm font-semibold mb-3 flex items-center gap-1"><i class="pi pi-list text-primary"></i>Node Traversal Path Telemetry:</h4>
                <ScrollPanel style="width: 100%; height: 420px" class="trace-scroll">
                    <Timeline :value="flowLogSteps" align="left" class="custom-timeline">
                        <template #marker="slotProps">
                            <span class="custom-marker flex items-center justify-center rounded-full" 
                                  :style="{ 
                                      width: '2rem', 
                                      height: '2rem',
                                      backgroundColor: slotProps.item.NodeType === 'trigger' ? 'var(--p-emerald-500)' :
                                                       slotProps.item.NodeType === 'message' ? 'var(--p-blue-500)' :
                                                       slotProps.item.NodeType === 'ai' ? 'var(--p-purple-500)' :
                                                       slotProps.item.NodeType === 'richresponse' ? 'var(--p-orange-500)' :
                                                       slotProps.item.NodeType === 'condition' ? 'var(--p-pink-500)' :
                                                       slotProps.item.NodeType === 'webhook' ? 'var(--p-sky-500)' : 'var(--p-slate-500)'
                                  }">
                                <i :class="slotProps.item.NodeType === 'trigger' ? 'pi pi-bolt' :
                                           slotProps.item.NodeType === 'message' ? 'pi pi-comment' :
                                           slotProps.item.NodeType === 'input' ? 'pi pi-sign-in' :
                                           slotProps.item.NodeType === 'ai' ? 'pi pi-sparkles' :
                                           slotProps.item.NodeType === 'richresponse' ? 'pi pi-image' :
                                           slotProps.item.NodeType === 'condition' ? 'pi pi-sitemap' :
                                           slotProps.item.NodeType === 'webhook' ? 'pi pi-globe' : 'pi pi-circle'" 
                                   style="font-size: 0.9rem; color: #fff;"></i>
                            </span>
                        </template>
                        <template #content="slotProps">
                            <div class="timeline-item is-selected">
                                <div class="item-header flex items-center justify-between">
                                    <div class="header-left flex items-center gap-2">
                                        <span class="item-time text-xs">{{ new Date(slotProps.item.ExecutedAt).toLocaleTimeString() }}</span>
                                        <span class="font-bold text-sm text-surface-900">{{ slotProps.item.NodeLabel }}</span>
                                        <Tag size="small" severity="secondary" :value="slotProps.item.NodeType.toUpperCase()" />
                                    </div>
                                    <span class="text-xs text-secondary font-mono">{{ slotProps.item.DurationMs.toFixed(1) }}ms</span>
                                </div>
                                
                                <div class="trace-content mt-2 flex flex-col gap-2">
                                    <!-- Input/Output Message previews -->
                                    <div v-if="slotProps.item.InputMessage" class="content-block request">
                                        <span class="block-label text-xs">USER INPUT RECEIVED</span>
                                        <div class="block-text text-sm">"{{ slotProps.item.InputMessage }}"</div>
                                    </div>
                                    
                                    <div v-if="slotProps.item.OutputMessage" class="content-block response">
                                        <span class="block-label text-xs">OUTPUT RESPONDED</span>
                                        <div class="block-text text-sm">{{ slotProps.item.OutputMessage }}</div>
                                    </div>

                                    <!-- Variables snapshot Visualizer -->
                                    <div class="variables-panel mt-2 p-2 bg-surface-50 border border-surface-200 rounded" 
                                         v-if="slotProps.item.VariablesSnapshotJson && slotProps.item.VariablesSnapshotJson !== '{}'">
                                        <span class="text-xs font-semibold text-secondary flex items-center gap-1 mb-1">
                                            <i class="pi pi-database text-xs"></i> Session Variables Snapshot:
                                        </span>
                                        <div class="flex flex-wrap gap-2">
                                            <div v-for="(val, key) in JSON.parse(slotProps.item.VariablesSnapshotJson)" :key="key" 
                                                 class="flex items-center gap-1 bg-surface-100 border border-surface-200 px-2 py-0.5 rounded text-xs">
                                                <strong class="text-surface-700 font-mono">{{ key }}:</strong>
                                                <span class="text-primary font-mono">"{{ val }}"</span>
                                            </div>
                                        </div>
                                    </div>
                                </div>
                            </div>
                        </template>
                    </Timeline>
                </ScrollPanel>
            </div>
        </Dialog>

        <Dialog v-model:visible="visibleDetail" modal header="Request Details & Trace" :style="{ width: '70vw' }">
            <div v-if="selectedLog" class="log-detail">
                <Tabs value="0">
                    <TabList>
                        <Tab value="0">General Info</Tab>
                        <Tab value="1" v-if="selectedLog.sessionId || selectedLog.SessionId">Session Trace</Tab>
                    </TabList>
                    <TabPanels>
                        <TabPanel value="0">
                            <div class="detail-content">
                                <div class="detail-group">
                                    <label>Endpoint</label>
                                    <code class="endpoint-badge">{{ selectedLog.endpoint }}</code>
                                </div>
                                <div class="detail-group" v-if="selectedLog.sessionId || selectedLog.SessionId">
                                    <label>Session Trace ID</label>
                                    <code class="text-xs">{{ selectedLog.sessionId || selectedLog.SessionId }}</code>
                                </div>
                                <div class="detail-group" v-if="selectedLog.rawRequest">
                                    <label>Request Content</label>
                                    <pre class="json-block">{{ formatJsonDetail(selectedLog.rawRequest) }}</pre>
                                </div>
                                <div class="detail-group" v-if="selectedLog.rawResponse">
                                    <label>Response / Tool Content</label>
                                    <pre class="json-block">{{ formatJsonDetail(selectedLog.rawResponse) }}</pre>
                                </div>
                                <div class="detail-group" v-if="selectedLog.errorMessage">
                                    <label>Error</label>
                                    <div class="error-panel">
                                        <i class="pi pi-exclamation-circle"></i>
                                        <span>{{ selectedLog.errorMessage }}</span>
                                    </div>
                                </div>
                            </div>
                        </TabPanel>
                        <TabPanel value="1">
                            <div v-if="loadingTrace" class="loading-trace">
                                <i class="pi pi-spin pi-spinner" style="font-size: 2rem"></i>
                                <p>Loading session trace...</p>
                            </div>
                            <ScrollPanel v-else style="width: 100%; height: 500px" class="trace-scroll">
                                <Timeline :value="traceLogs" align="left" class="custom-timeline">
                                    <template #marker="slotProps">
                                        <span class="custom-marker" :style="{ backgroundColor: getLogColor(slotProps.item.endpoint) }">
                                            <i :class="getLogIcon(slotProps.item.endpoint)"></i>
                                        </span>
                                    </template>
                                    <template #content="slotProps">
                                        <div :class="['timeline-item', { 'is-selected': slotProps.item.id === selectedLog.id }]" @click="selectedLog = slotProps.item">
                                            <div class="item-header">
                                                <div class="header-left">
                                                    <span class="item-time">{{ new Date(slotProps.item.createdAt).toLocaleTimeString() }}</span>
                                                    <span class="item-endpoint" :style="{ color: getLogColor(slotProps.item.endpoint) }">
                                                        {{ slotProps.item.isLiveEvent ? `${slotProps.item.type} ${slotProps.item.meta ? '(' + slotProps.item.meta + ')' : ''}` : slotProps.item.endpoint }}
                                                    </span>
                                                </div>
                                                <div class="item-meta">
                                                    <Tag v-if="slotProps.item.errorMessage" severity="danger" value="Error" size="small" />
                                                    <span v-if="slotProps.item.durationMs" class="item-duration">{{ slotProps.item.durationMs }}ms</span>
                                                </div>
                                            </div>

                                            <div class="trace-content">
                                                <div v-if="slotProps.item.isLiveEvent" class="live-trace-events">
                                                    <div class="content-block" :class="getEventTypeClass(slotProps.item.type)">
                                                        <div v-if="slotProps.item.type.includes('Audio')" class="block-audio">
                                                            <audio v-if="slotProps.item.audioSrc" controls :src="slotProps.item.audioSrc" style="width: 100%; height: 32px;"></audio>
                                                            <div v-if="slotProps.item.transcription" class="block-text mt-2"><i>"{{ slotProps.item.transcription }}"</i></div>
                                                        </div>
                                                        <div v-else class="block-text">{{ slotProps.item.content }}</div>
                                                    </div>
                                                </div>
                                                <div v-else>
                                                    <div class="content-block request mb-2">
                                                        <span class="block-label">REQUEST</span>
                                                        <div class="block-text">{{ formatPreview(slotProps.item.rawRequest) }}</div>
                                                    </div>
                                                    <div v-if="slotProps.item.rawResponse" class="content-block response">
                                                        <span class="block-label">RESPONSE</span>
                                                        <div class="block-text">{{ formatPreview(slotProps.item.rawResponse) }}</div>
                                                    </div>
                                                </div>
                                            </div>
                                        </div>
                                    </template>
                                </Timeline>
                            </ScrollPanel>
                        </TabPanel>
                    </TabPanels>
                </Tabs>
            </div>
        </Dialog>
    </div>
</template>

<style scoped>
.header {
    margin-bottom: 32px;
    display: flex;
    justify-content: space-between;
    align-items: flex-end;
    gap: 24px;
    flex-wrap: wrap;
}
.header-actions {
    display: flex;
    gap: 16px;
    align-items: center;
}
.subtitle {
    color: var(--p-surface-400);
    margin-top: 4px;
}
.loading, .empty-text {
    text-align: center;
    padding: 64px;
    color: var(--p-surface-400);
}
.logs-container {
    background-color: var(--p-surface-0);
    border: 1px solid var(--p-surface-200);
    border-radius: 8px;
    padding: 16px;
}
:deep(.p-datatable .p-datatable-thead > tr > th) {
    background-color: var(--p-surface-0);
    color: var(--p-surface-500);
    font-weight: 500;
    border-bottom: 1px solid var(--p-surface-200);
}
:deep(.p-datatable .p-datatable-tbody > tr) {
    background-color: var(--p-surface-0);
    color: var(--p-surface-900);
}
:deep(.p-datatable .p-datatable-tbody > tr > td) {
    border-bottom: 1px solid var(--p-surface-200);
}
.text-secondary {
    color: var(--p-surface-400);
    font-size: 0.85rem;
}
.mono-text {
    font-family: 'JetBrains Mono', monospace;
    font-size: 0.8rem;
}
.text-danger {
    color: var(--p-red-400);
}
.text-truncate {
    display: block;
    max-width: 300px;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
}
.pagination {
    display: flex;
    justify-content: center;
    align-items: center;
    gap: 16px;
    margin-top: 24px;
}
.page-info {
    color: var(--p-surface-400);
    font-size: 0.9rem;
}

.log-detail {
    margin: -1rem;
}
.detail-content {
    display: flex;
    flex-direction: column;
    gap: 20px;
    padding: 1rem;
}
.detail-group label {
    display: block;
    font-weight: 600;
    margin-bottom: 8px;
    color: var(--p-surface-500);
    font-size: 0.75rem;
    text-transform: uppercase;
    letter-spacing: 0.5px;
}
.endpoint-badge {
    background-color: var(--p-surface-100);
    color: var(--p-surface-700);
    padding: 4px 12px;
    border-radius: 4px;
    font-family: 'JetBrains Mono', monospace;
}
.json-block {
    background-color: var(--p-surface-950);
    color: var(--p-primary-300);
    padding: 16px;
    border-radius: 8px;
    font-family: 'JetBrains Mono', monospace;
    font-size: 0.85rem;
    overflow-x: auto;
    margin: 0;
    border: 1px solid var(--p-surface-800);
    max-height: 300px;
}
.error-panel {
    background-color: color-mix(in srgb, var(--p-red-500), transparent 90%);
    border: 1px solid var(--p-red-500);
    color: var(--p-red-500);
    padding: 12px;
    border-radius: 6px;
    display: flex;
    align-items: center;
    gap: 12px;
}

/* Timeline Styles */
.loading-trace {
    display: flex;
    flex-direction: column;
    align-items: center;
    padding: 4rem;
    color: var(--p-surface-400);
}
.trace-scroll {
    padding: 1.5rem;
    background: var(--p-surface-50);
    border-radius: 12px;
}

/* Force Timeline to the left */
:deep(.p-timeline-event) {
    min-height: 80px;
}
:deep(.p-timeline-event-opposite) {
    display: none !important; /* Hide the empty left side */
}
:deep(.p-timeline-event-content) {
    padding-left: 1rem !important;
}
:deep(.p-timeline-event-marker) {
    border: none !important;
}

.custom-marker {
    display: flex;
    width: 2rem;
    height: 2rem;
    align-items: center;
    justify-content: center;
    color: #ffffff;
    border-radius: 50%;
    z-index: 1;
    box-shadow: 0 2px 4px rgba(0,0,0,0.1);
}
.custom-marker i {
    font-size: 0.9rem;
}
.timeline-item {
    background: var(--p-surface-0);
    border: 1px solid var(--p-surface-200);
    border-radius: 8px;
    padding: 16px;
    cursor: pointer;
    transition: all 0.2s cubic-bezier(0.4, 0, 0.2, 1);
    margin-bottom: 8px;
    position: relative;
    overflow: hidden;
}
.timeline-item::before {
    content: '';
    position: absolute;
    left: 0;
    top: 0;
    bottom: 0;
    width: 4px;
    background: transparent;
    transition: background 0.2s;
}
.timeline-item:hover {
    transform: translateX(4px);
    border-color: var(--p-primary-300);
}
.timeline-item.is-selected {
    border-color: var(--p-primary-500);
    background: color-mix(in srgb, var(--p-primary-500), transparent 98%);
    box-shadow: 0 4px 12px rgba(0,0,0,0.05);
}
.timeline-item.is-selected::before {
    background: var(--p-primary-500);
}
.item-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 12px;
}
.header-left {
    display: flex;
    align-items: center;
    gap: 12px;
}
.item-time {
    font-size: 0.75rem;
    color: var(--p-surface-400);
    font-weight: 500;
}
.item-meta {
    display: flex;
    align-items: center;
    gap: 8px;
}
.item-endpoint {
    font-family: 'JetBrains Mono', monospace;
    font-weight: 700;
    font-size: 0.8rem;
    text-transform: uppercase;
    letter-spacing: 0.5px;
}
.item-duration {
    font-size: 0.75rem;
    color: var(--p-surface-400);
    font-family: 'JetBrains Mono', monospace;
}
.trace-content {
    display: flex;
    flex-direction: column;
    gap: 8px;
}
.content-block {
    padding: 8px 12px;
    border-radius: 6px;
    font-size: 0.85rem;
    position: relative;
}
.content-block.request {
    background: var(--p-surface-50);
    border-left: 3px solid var(--p-surface-300);
}
.content-block.response {
    background: color-mix(in srgb, var(--p-primary-500), transparent 96%);
    border-left: 3px solid var(--p-primary-500);
}
.block-label {
    font-size: 0.65rem;
    font-weight: 800;
    color: var(--p-surface-400);
    display: block;
    margin-bottom: 2px;
    letter-spacing: 1px;
}
.block-text {
    color: var(--p-surface-700);
    line-height: 1.5;
    word-break: break-word;
}
.live-trace-events {
    display: flex;
    flex-direction: column;
    gap: 8px;
    width: 100%;
}
.content-block.tool-block {
    background: color-mix(in srgb, var(--p-orange-500), transparent 96%);
    border-left: 3px solid var(--p-orange-500);
}
.content-block.system-block {
    background: var(--p-surface-100);
    border-left: 3px solid var(--p-surface-400);
}
.mt-2 { margin-top: 8px; }
.mb-2 { margin-bottom: 8px; }
.time-small {
    font-size: 0.6rem;
    color: var(--p-surface-400);
    float: right;
    font-weight: normal;
}
/* ── Mobile Responsive ── */
@media (max-width: 768px) {
    .header {
        flex-direction: column;
        align-items: stretch;
        gap: 16px;
    }
    .header-actions {
        flex-direction: column;
        gap: 12px;
    }
    .header-actions :deep(.w-64) {
        width: 100% !important;
    }
    .header-actions :deep(.p-iconfield) {
        width: 100%;
    }
    .header-actions :deep(.p-inputtext) {
        width: 100% !important;
    }
    .search-container {
        width: 100%;
    }
    .search-input {
        width: 100% !important;
    }
    .header-actions :deep(.p-select) {
        width: 100% !important;
    }
    .logs-container {
        padding: 8px;
        border-radius: 0;
        border-left: none;
        border-right: none;
    }
}

.search-container {
    position: relative;
    display: inline-flex;
    align-items: center;
}
.search-icon {
    position: absolute;
    left: 12px;
    color: var(--p-surface-400);
    pointer-events: none;
    font-size: 0.9rem;
}
.search-input {
    padding-left: 36px !important;
}
</style>

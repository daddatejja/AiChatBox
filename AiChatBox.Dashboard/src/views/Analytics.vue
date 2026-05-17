<script setup lang="ts">
import { ref, onMounted, watch, computed } from 'vue';
import { useApi } from '../composables/useApi';
import Card from 'primevue/card';
import Select from 'primevue/select';
import DataTable from 'primevue/datatable';
import Column from 'primevue/column';
import Tag from 'primevue/tag';

const { apiFetch } = useApi();

const projects = ref<any[]>([]);
const selectedProject = ref<any>(null);
const selectedDays = ref(30);
const loading = ref(false);

const dayOptions = [
    { label: 'Last 7 days', value: 7 },
    { label: 'Last 14 days', value: 14 },
    { label: 'Last 30 days', value: 30 },
    { label: 'Last 90 days', value: 90 }
];

interface Overview {
    totalRequests: number;
    totalSessions: number;
    totalMessages: number;
    totalInputTokens: number;
    totalOutputTokens: number;
    avgResponseMs: number;
    errorCount: number;
    errorRate: number;
    thumbsUp: number;
    thumbsDown: number;
    feedbackScore: number;
    ruleMatches: number;
    days: number;
}

const overview = ref<Overview | null>(null);
const volume = ref<any[]>([]);
const providerBreakdown = ref<any[]>([]);
const modelBreakdown = ref<any[]>([]);
const feedbackItems = ref<any[]>([]);
const feedbackFilter = ref<string>('all');

const feedbackFilterOptions = [
    { label: 'All Feedback', value: 'all' },
    { label: 'Thumbs Up', value: '1' },
    { label: 'Thumbs Down', value: '-1' }
];

function buildParams() {
    const params = new URLSearchParams({ days: String(selectedDays.value) });
    if (selectedProject.value) params.set('projectId', selectedProject.value.id);
    return params.toString();
}

async function loadProjects() {
    const res = await apiFetch('/api/project');
    if (res.ok) projects.value = await res.json();
}

async function loadAll() {
    loading.value = true;
    const q = buildParams();
    const [ovRes, volRes, provRes, modRes, fbRes] = await Promise.all([
        apiFetch(`/api/analytics/overview?${q}`),
        apiFetch(`/api/analytics/volume?${q}`),
        apiFetch(`/api/analytics/providers?${q}`),
        apiFetch(`/api/analytics/models?${q}`),
        apiFetch(`/api/analytics/feedback?${q}${feedbackFilter.value !== 'all' ? '&feedbackFilter=' + feedbackFilter.value : ''}`)
    ]);
    if (ovRes.ok) overview.value = await ovRes.json();
    if (volRes.ok) volume.value = await volRes.json();
    if (provRes.ok) providerBreakdown.value = await provRes.json();
    if (modRes.ok) modelBreakdown.value = await modRes.json();
    if (fbRes.ok) feedbackItems.value = await fbRes.json();
    loading.value = false;
}

watch([selectedProject, selectedDays], () => loadAll());
watch(feedbackFilter, async () => {
    const q = buildParams();
    const res = await apiFetch(`/api/analytics/feedback?${q}${feedbackFilter.value !== 'all' ? '&feedbackFilter=' + feedbackFilter.value : ''}`);
    if (res.ok) feedbackItems.value = await res.json();
});

// Simple bar chart (pure CSS)
const maxVolumeRequests = computed(() => Math.max(...volume.value.map(v => v.requests), 1));

function formatNumber(n: number): string {
    if (n >= 1000000) return (n / 1000000).toFixed(1) + 'M';
    if (n >= 1000) return (n / 1000).toFixed(1) + 'K';
    return String(n);
}

function formatDate(d: string) {
    return new Date(d).toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
}

onMounted(() => { loadProjects(); loadAll(); });
</script>

<template>
    <div>
        <header class="header">
            <div class="header-main">
                <h1>Analytics</h1>
                <p class="subtitle">Usage metrics, performance, and quality insights across your projects.</p>
            </div>
            <div class="header-actions">
                <Select v-model="selectedProject" :options="projects" optionLabel="name" placeholder="All Projects" showClear class="w-48" />
                <Select v-model="selectedDays" :options="dayOptions" optionLabel="label" optionValue="value" class="w-44" />
            </div>
        </header>

        <!-- KPI Cards -->
        <div class="kpi-grid" v-if="overview">
            <div class="kpi-card">
                <div class="kpi-icon requests"><svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="22 12 18 12 15 21 9 3 6 12 2 12"/></svg></div>
                <div class="kpi-body">
                    <span class="kpi-value">{{ formatNumber(overview.totalRequests) }}</span>
                    <span class="kpi-label">API Requests</span>
                </div>
            </div>
            <div class="kpi-card">
                <div class="kpi-icon sessions"><svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"/></svg></div>
                <div class="kpi-body">
                    <span class="kpi-value">{{ formatNumber(overview.totalSessions) }}</span>
                    <span class="kpi-label">Sessions</span>
                </div>
            </div>
            <div class="kpi-card">
                <div class="kpi-icon tokens"><svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="2" y="7" width="20" height="14" rx="2" ry="2"/><path d="M16 21V5a2 2 0 0 0-2-2h-4a2 2 0 0 0-2 2v16"/></svg></div>
                <div class="kpi-body">
                    <span class="kpi-value">{{ formatNumber(overview.totalInputTokens + overview.totalOutputTokens) }}</span>
                    <span class="kpi-label">Total Tokens</span>
                </div>
            </div>
            <div class="kpi-card">
                <div class="kpi-icon speed"><svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></svg></div>
                <div class="kpi-body">
                    <span class="kpi-value">{{ overview.avgResponseMs }}ms</span>
                    <span class="kpi-label">Avg Response</span>
                </div>
            </div>
            <div class="kpi-card">
                <div class="kpi-icon errors"><svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><line x1="15" y1="9" x2="9" y2="15"/><line x1="9" y1="9" x2="15" y2="15"/></svg></div>
                <div class="kpi-body">
                    <span class="kpi-value">{{ overview.errorRate }}%</span>
                    <span class="kpi-label">Error Rate</span>
                </div>
            </div>
            <div class="kpi-card">
                <div class="kpi-icon feedback-score"><svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M14 9V5a3 3 0 0 0-3-3l-4 9v11h11.28a2 2 0 0 0 2-1.7l1.38-9a2 2 0 0 0-2-2.3H14z"/><path d="M7 22H4a2 2 0 0 1-2-2v-7a2 2 0 0 1 2-2h3"/></svg></div>
                <div class="kpi-body">
                    <span class="kpi-value">{{ overview.feedbackScore }}%</span>
                    <span class="kpi-label">Satisfaction ({{ overview.thumbsUp }}👍 {{ overview.thumbsDown }}👎)</span>
                </div>
            </div>
            <div class="kpi-card">
                <div class="kpi-icon rules"><svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="9 11 12 14 22 4"/><path d="M21 12v7a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11"/></svg></div>
                <div class="kpi-body">
                    <span class="kpi-value">{{ formatNumber(overview.ruleMatches) }}</span>
                    <span class="kpi-label">Rule Matches (0 cost)</span>
                </div>
            </div>
        </div>

        <!-- Volume Chart -->
        <Card class="chart-card">
            <template #title><span class="card-title">Daily Volume</span></template>
            <template #content>
                <div class="bar-chart" v-if="volume.length">
                    <div class="bar-row" v-for="v in volume" :key="v.date">
                        <span class="bar-date">{{ formatDate(v.date) }}</span>
                        <div class="bar-track">
                            <div class="bar-fill" :style="{ width: (v.requests / maxVolumeRequests * 100) + '%' }">
                                <span class="bar-label" v-if="v.requests > 0">{{ v.requests }}</span>
                            </div>
                        </div>
                        <span class="bar-sessions">{{ v.sessions }}s</span>
                    </div>
                </div>
                <div v-else class="empty-chart">No data in this time range.</div>
            </template>
        </Card>

        <!-- Provider & Model Breakdown -->
        <div class="grid-2">
            <Card class="chart-card">
                <template #title><span class="card-title">Provider Usage</span></template>
                <template #content>
                    <DataTable :value="providerBreakdown" class="p-datatable-sm" v-if="providerBreakdown.length">
                        <Column field="provider" header="Provider" />
                        <Column field="requests" header="Requests" sortable />
                        <Column header="Tokens">
                            <template #body="{ data }">{{ formatNumber(data.inputTokens + data.outputTokens) }}</template>
                        </Column>
                        <Column field="avgDurationMs" header="Avg ms" sortable />
                        <Column field="errors" header="Errors">
                            <template #body="{ data }">
                                <Tag v-if="data.errors > 0" severity="danger" :value="String(data.errors)" />
                                <span v-else class="text-muted">0</span>
                            </template>
                        </Column>
                    </DataTable>
                    <div v-else class="empty-chart">No provider data yet.</div>
                </template>
            </Card>

            <Card class="chart-card">
                <template #title><span class="card-title">Model Usage</span></template>
                <template #content>
                    <DataTable :value="modelBreakdown" class="p-datatable-sm" v-if="modelBreakdown.length">
                        <Column field="provider" header="Provider" style="width: 100px" />
                        <Column field="model" header="Model" />
                        <Column field="requests" header="Requests" sortable />
                        <Column field="avgDurationMs" header="Avg ms" sortable />
                    </DataTable>
                    <div v-else class="empty-chart">No model data yet.</div>
                </template>
            </Card>
        </div>

        <!-- Feedback -->
        <Card class="chart-card">
            <template #title>
                <div class="feedback-header">
                    <span class="card-title">User Feedback</span>
                    <Select v-model="feedbackFilter" :options="feedbackFilterOptions" optionLabel="label" optionValue="value" class="w-40" />
                </div>
            </template>
            <template #content>
                <DataTable :value="feedbackItems" class="p-datatable-sm" v-if="feedbackItems.length" :rows="10" paginator>
                    <Column field="createdAt" header="Time" style="width: 150px">
                        <template #body="{ data }">
                            <span class="text-muted">{{ new Date(data.createdAt).toLocaleString() }}</span>
                        </template>
                    </Column>
                    <Column field="role" header="Role" style="width: 80px">
                        <template #body="{ data }">
                            <Tag :value="data.role" :severity="data.role === 'model' ? 'info' : 'secondary'" />
                        </template>
                    </Column>
                    <Column field="content" header="Content" />
                    <Column field="feedback" header="Rating" style="width: 80px">
                        <template #body="{ data }">
                            <span v-if="data.feedback === 1" class="feedback-icon up">👍</span>
                            <span v-else-if="data.feedback === -1" class="feedback-icon down">👎</span>
                        </template>
                    </Column>
                </DataTable>
                <div v-else class="empty-chart">No feedback received yet. Feedback buttons will appear in the chat widget.</div>
            </template>
        </Card>
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
.header-actions { display: flex; gap: 12px; align-items: center; }
.subtitle { color: var(--p-surface-500); margin-top: 4px; font-size: 0.9rem; }

/* KPI Grid */
.kpi-grid {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
    gap: 16px;
    margin-bottom: 32px;
}
.kpi-card {
    background: var(--p-surface-0);
    border: 1px solid var(--p-surface-200);
    border-radius: 12px;
    padding: 20px;
    display: flex;
    align-items: center;
    gap: 16px;
    transition: transform 0.15s, box-shadow 0.15s;
}
.kpi-card:hover {
    transform: translateY(-2px);
    box-shadow: 0 4px 12px rgba(0,0,0,0.06);
}
.kpi-icon {
    width: 44px;
    height: 44px;
    border-radius: 10px;
    display: flex;
    align-items: center;
    justify-content: center;
    flex-shrink: 0;
}
.kpi-icon.requests { background: color-mix(in srgb, var(--p-blue-500) 12%, transparent); color: var(--p-blue-600); }
.kpi-icon.sessions { background: color-mix(in srgb, var(--p-green-500) 12%, transparent); color: var(--p-green-600); }
.kpi-icon.tokens { background: color-mix(in srgb, var(--p-purple-500) 12%, transparent); color: var(--p-purple-600); }
.kpi-icon.speed { background: color-mix(in srgb, var(--p-orange-500) 12%, transparent); color: var(--p-orange-600); }
.kpi-icon.errors { background: color-mix(in srgb, var(--p-red-500) 12%, transparent); color: var(--p-red-600); }
.kpi-icon.feedback-score { background: color-mix(in srgb, var(--p-teal-500) 12%, transparent); color: var(--p-teal-600); }
.kpi-icon.rules { background: color-mix(in srgb, var(--p-indigo-500) 12%, transparent); color: var(--p-indigo-600); }
.kpi-body { display: flex; flex-direction: column; min-width: 0; }
.kpi-value { font-size: 1.4rem; font-weight: 700; color: var(--p-surface-900); line-height: 1.2; }
.kpi-label { font-size: 0.75rem; color: var(--p-surface-500); margin-top: 2px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }

/* Bar Chart */
.chart-card {
    background: var(--p-surface-0);
    border: 1px solid var(--p-surface-200);
    margin-bottom: 24px;
}
.card-title { font-size: 1rem; font-weight: 600; }
.bar-chart { max-height: 400px; overflow-y: auto; }
.bar-row {
    display: flex;
    align-items: center;
    gap: 12px;
    padding: 3px 0;
}
.bar-date { font-size: 0.7rem; color: var(--p-surface-500); width: 50px; text-align: right; flex-shrink: 0; }
.bar-track { flex: 1; background: var(--p-surface-100); border-radius: 4px; height: 22px; overflow: hidden; }
.bar-fill {
    background: linear-gradient(90deg, var(--p-primary-400), var(--p-primary-600));
    height: 100%;
    border-radius: 4px;
    min-width: 0;
    display: flex;
    align-items: center;
    justify-content: flex-end;
    padding-right: 6px;
    transition: width 0.3s ease;
}
.bar-label { font-size: 0.65rem; color: white; font-weight: 600; }
.bar-sessions { font-size: 0.7rem; color: var(--p-surface-400); width: 30px; flex-shrink: 0; }
.empty-chart { text-align: center; padding: 32px; color: var(--p-surface-400); font-size: 0.9rem; }

/* Grid */
.grid-2 { display: grid; grid-template-columns: 1fr 1fr; gap: 24px; }

/* Feedback */
.feedback-header { display: flex; justify-content: space-between; align-items: center; width: 100%; }
.feedback-icon { font-size: 1.2rem; }
.feedback-icon.up { color: var(--p-green-600); }
.feedback-icon.down { color: var(--p-red-600); }
.text-muted { color: var(--p-surface-400); font-size: 0.85rem; }

/* Tables */
:deep(.p-datatable .p-datatable-thead > tr > th) {
    background: var(--p-surface-0);
    color: var(--p-surface-500);
    font-weight: 500;
    font-size: 0.8rem;
    border-bottom: 1px solid var(--p-surface-200);
}
:deep(.p-datatable .p-datatable-tbody > tr > td) {
    font-size: 0.85rem;
    border-bottom: 1px solid var(--p-surface-100);
}

@media (max-width: 768px) {
    .header { flex-direction: column; align-items: stretch; }
    .header-actions { flex-direction: column; }
    .kpi-grid { grid-template-columns: repeat(2, 1fr); }
    .grid-2 { grid-template-columns: 1fr; }
}
</style>

<script setup lang="ts">
import { ref, onMounted, watch } from 'vue';
import { useApi } from '../composables/useApi';
import DataTable from 'primevue/datatable';
import Column from 'primevue/column';
import Button from 'primevue/button';
import Select from 'primevue/select';
import Dialog from 'primevue/dialog';
import InputText from 'primevue/inputtext';
import IconField from 'primevue/iconfield';
import InputIcon from 'primevue/inputicon';
import Tag from 'primevue/tag';

const { apiFetch } = useApi();

const logs = ref<any[]>([]);
const totalLogs = ref(0);
const offset = ref(0);
const limit = ref(20);
const loading = ref(true);
const projects = ref<any[]>([]);
const selectedProject = ref<any>(null);
const searchQuery = ref('');
const sortField = ref('createdAt');
const sortOrder = ref(-1);

async function loadProjects() {
    try {
        const res = await apiFetch('/api/project');
        if (res.ok) projects.value = await res.json();
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

const onPage = (event: any) => {
    offset.value = event.first;
    limit.value = event.rows;
    load();
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
        offset.value = 0;
        load();
    }, 500);
});

watch(selectedProject, () => {
    offset.value = 0;
    load();
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

function showDetail(log: any) {
    selectedLog.value = log;
    visibleDetail.value = true;
}

onMounted(() => {
    loadProjects();
    load();
});
</script>

<template>
    <div>
        <header class="header">
            <div class="header-main">
                <h1>API Request Logs</h1>
                <p class="subtitle">Recent AI provider requests</p>
            </div>
            <div class="header-actions">
                <IconField iconPosition="left">
                    <InputIcon class="pi pi-search" />
                    <InputText v-model="searchQuery" placeholder="Search logs..." class="w-64" />
                </IconField>
                <Select v-model="selectedProject" :options="projects" optionLabel="name" placeholder="Filter by Project" showClear class="w-64" />
            </div>
        </header>

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
                <Column header="Details" style="width: 80px">
                    <template #body="slotProps">
                        <Button icon="pi pi-eye" severity="secondary" text rounded @click="showDetail(slotProps.data)" />
                    </template>
                </Column>
            </DataTable>
            
            <p v-if="!loading && !logs.length" class="empty-text">No logs found matching your criteria.</p>
        </div>

        <Dialog v-model:visible="visibleDetail" modal header="Log Details" :style="{ width: '50vw' }">
            <div v-if="selectedLog" class="log-detail">
                <div class="detail-group">
                    <label>Endpoint</label>
                    <code>{{ selectedLog.endpoint }}</code>
                </div>
                <div class="detail-group" v-if="selectedLog.rawRequest">
                    <label>Request Content</label>
                    <pre class="json-block">{{ selectedLog.rawRequest }}</pre>
                </div>
                <div class="detail-group" v-if="selectedLog.rawResponse">
                    <label>Response / Tool Call</label>
                    <pre class="json-block">{{ selectedLog.rawResponse }}</pre>
                </div>
                <div class="detail-group" v-if="selectedLog.errorMessage">
                    <label>Error</label>
                    <p class="text-danger">{{ selectedLog.errorMessage }}</p>
                </div>
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
    display: flex;
    flex-direction: column;
    gap: 20px;
}
.detail-group label {
    display: block;
    font-weight: 600;
    margin-bottom: 8px;
    color: var(--p-surface-500);
    font-size: 0.85rem;
    text-transform: uppercase;
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
</style>

<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
import { useRoute } from 'vue-router';
import { useApi } from '../composables/useApi';
import Button from 'primevue/button';
import Card from 'primevue/card';
import FileUpload from 'primevue/fileupload';
import ProgressBar from 'primevue/progressbar';
import Tag from 'primevue/tag';
import { useToast } from 'primevue/usetoast';
import { useConfirm } from 'primevue/useconfirm';
import Toast from 'primevue/toast';
import ConfirmDialog from 'primevue/confirmdialog';
import Dialog from 'primevue/dialog';
import DataTable from 'primevue/datatable';
import Column from 'primevue/column';
import InputText from 'primevue/inputtext';
import InputNumber from 'primevue/inputnumber';
import { FilterMatchMode } from '@primevue/core/api';
import { marked } from 'marked';
import Papa from 'papaparse';
import DOMPurify from 'dompurify';

const { apiFetch, getToken, API_BASE } = useApi();
const toast = useToast();
const confirm = useConfirm();

const route = useRoute();
const projectId = computed(() => route.params.projectId as string);
const documents = ref<any[]>([]);
const selectedDocuments = ref<any[]>([]);
const configurations = ref<any[]>([]);
const loading = ref(false);
const initialLoading = ref(true);
const crawling = ref(false);
const crawlUrl = ref('');
const maxPages = ref(10);
const showCrawlStarted = ref(false);
const crawlJobs = ref<any[]>([]);
let pollInterval: any = null;

const showAdvanced = ref(false);
const chunkSize = ref(1000);
const chunkOverlap = ref(200);
const chunkingStrategy = ref('recursive');

const filters = ref({
    global: { value: null, matchMode: FilterMatchMode.CONTAINS },
    fileName: { value: null, matchMode: FilterMatchMode.CONTAINS },
    status: { value: null, matchMode: FilterMatchMode.EQUALS }
});

const viewDialog = ref(false);
const viewLoading = ref(false);
const viewedDoc = ref<any>(null);
const docContent = ref('');
const pdfUrl = ref('');
const csvData = ref<{ data: any[], meta: any } | null>(null);

const renderedMarkdown = computed(() => {
    if (!docContent.value) return '';
    const rawHtml = marked.parse(docContent.value) as string;
    return DOMPurify.sanitize(rawHtml);
});

const hasConfigWithKey = computed(() => {
    return configurations.value.some(c => c.hasGeminiKey || c.hasOpenAiKey);
});

const uploadUrl = computed(() => {
    return `${API_BASE}/api/project/${projectId.value}/knowledge/upload?chunkSize=${chunkSize.value}&chunkOverlap=${chunkOverlap.value}&chunkingStrategy=${chunkingStrategy.value}`;
});

async function loadData() {
    initialLoading.value = true;
    try {
        await Promise.all([
            loadDocuments(),
            loadConfigurations(),
            loadCrawlJobs()
        ]);
    } finally {
        initialLoading.value = false;
    }
}

async function loadCrawlJobs() {
    try {
        const res = await apiFetch(`/api/project/${projectId.value}/knowledge/crawl`);
        if (res.ok) {
            crawlJobs.value = await res.json();
            const hasActiveJobs = crawlJobs.value.some(j => j.status === 'Processing' || j.status === 'Pending');
            if (hasActiveJobs) {
                startPolling();
            } else if (pollInterval) {
                clearInterval(pollInterval);
                pollInterval = null;
            }
        }
    } catch (e) {
        console.error('Failed to load crawl jobs', e);
    }
}

function startPolling() {
    if (pollInterval) return;
    pollInterval = setInterval(async () => {
        const oldFailures = documents.value.filter(d => d.status === 'Failed').map(d => d.id);
        
        await Promise.all([
            loadCrawlJobs(),
            loadDocuments(true)
        ]);

        const newFailures = documents.value.filter(d => d.status === 'Failed' && !oldFailures.includes(d.id));
        newFailures.forEach(f => {
            toast.add({ 
                severity: 'error', 
                summary: 'Processing Failed', 
                detail: `${f.fileName}: ${f.errorMessage || 'Unknown error'}`, 
                life: 10000 
            });
        });
    }, 5000);
}

async function loadConfigurations() {
    try {
        const res = await apiFetch(`/api/project/${projectId.value}/configurations`);
        if (res.ok) configurations.value = await res.json();
    } catch (e) {
        console.error('Failed to load configs', e);
    }
}

async function loadDocuments(silent = false) {
    if (!silent) loading.value = true;
    try {
        const res = await apiFetch(`/api/project/${projectId.value}/knowledge`);
        if (res.ok) documents.value = await res.json();
    } catch (e) {
        console.error(e);
    } finally {
        if (!silent) loading.value = false;
    }
}

function onBeforeSend(event: any) {
    const token = getToken();
    if (token) {
        event.xhr.setRequestHeader('Authorization', `Bearer ${token}`);
    }
}

function onUpload() {
    loadDocuments();
    toast.add({ severity: 'success', summary: 'Success', detail: 'Files uploaded and processing started.', life: 3000 });
}

function onError(event: any) {
    console.error('Upload error:', event);
    toast.add({ severity: 'error', summary: 'Upload Failed', detail: 'Please check the network tab for details.', life: 5000 });
}

async function deleteDoc(id: string) {
    confirm.require({
        message: 'Delete this document? It will be removed from the Knowledge Base and RAG system.',
        header: 'Confirm Deletion',
        icon: 'pi pi-exclamation-triangle',
        acceptProps: { label: 'Delete', severity: 'danger' },
        rejectProps: { label: 'Cancel', severity: 'secondary', outlined: true },
        accept: async () => {
            await apiFetch(`/api/project/${projectId.value}/knowledge/${id}`, { method: 'DELETE' });
            toast.add({ severity: 'success', summary: 'Deleted', detail: 'Document removed successfully.', life: 3000 });
            await loadDocuments();
        }
    });
}

async function batchDelete() {
    if (!selectedDocuments.value.length) return;
    confirm.require({
        message: `Delete ${selectedDocuments.value.length} selected documents?`,
        header: 'Confirm Batch Deletion',
        icon: 'pi pi-exclamation-triangle',
        acceptProps: { label: 'Delete All', severity: 'danger' },
        rejectProps: { label: 'Cancel', severity: 'secondary', outlined: true },
        accept: async () => {
            const ids = selectedDocuments.value.map(d => d.id);
            const res = await apiFetch(`/api/project/${projectId.value}/knowledge/batch/delete`, {
                method: 'POST',
                body: JSON.stringify(ids)
            });
            if (res.ok) {
                toast.add({ severity: 'success', summary: 'Deleted', detail: `${ids.length} documents removed.`, life: 3000 });
                selectedDocuments.value = [];
                await loadDocuments();
            }
        }
    });
}

async function batchRetry() {
    const failedDocs = selectedDocuments.value.filter(d => d.status === 'Failed');
    if (!failedDocs.length) {
        toast.add({ severity: 'info', summary: 'Nothing to retry', detail: 'Only failed documents can be retried.', life: 3000 });
        return;
    }
    const ids = failedDocs.map(d => d.id);
    const res = await apiFetch(`/api/project/${projectId.value}/knowledge/batch/retry`, {
        method: 'POST',
        body: JSON.stringify(ids)
    });
    if (res.ok) {
        toast.add({ severity: 'info', summary: 'Retry Started', detail: `Processing restarted for ${ids.length} documents.`, life: 3000 });
        selectedDocuments.value = [];
        await loadDocuments();
    }
}

function formatSize(bytes: number) {
    if (bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
}

function formatDate(date: string) {
    return new Date(date).toLocaleString();
}

async function retryDoc(id: string) {
    loading.value = true;
    try {
        const res = await apiFetch(`/api/project/${projectId.value}/knowledge/${id}/retry`, { method: 'POST' });
        if (res.ok) {
            await loadDocuments();
        } else {
            const err = await res.text();
            toast.add({ severity: 'error', summary: 'Retry Failed', detail: err, life: 5000 });
        }
    } catch (e) {
        console.error(e);
    } finally {
        loading.value = false;
    }
}

async function viewFile(doc: any) {
    viewedDoc.value = doc;
    viewDialog.value = true;
    viewLoading.value = true;
    docContent.value = '';
    csvData.value = null;
    
    if (pdfUrl.value) {
        window.URL.revokeObjectURL(pdfUrl.value);
        pdfUrl.value = '';
    }
    
    try {
        const res = await apiFetch(`/api/project/${projectId.value}/knowledge/${doc.id}/content`);
        if (res.ok) {
            if (doc.contentType.includes('pdf')) {
                const blob = await res.blob();
                pdfUrl.value = window.URL.createObjectURL(blob);
            } else if (doc.contentType.includes('csv')) {
                const data = await res.json();
                docContent.value = data.content;
                Papa.parse(data.content, {
                    header: true,
                    skipEmptyLines: true,
                    complete: (results: any) => {
                        csvData.value = results;
                    }
                });
            } else if (doc.contentType.includes('text') || doc.contentType.includes('json') || doc.contentType.includes('md') || doc.contentType.includes('markdown')) {
                const data = await res.json();
                docContent.value = data.content;
            } else {
                // For other types, offer download
                const blob = await res.blob();
                const url = window.URL.createObjectURL(blob);
                const a = document.createElement('a');
                a.href = url;
                a.download = doc.fileName;
                a.click();
                viewDialog.value = false;
            }
        }
    } catch (e) {
        console.error(e);
        toast.add({ severity: 'error', summary: 'Error', detail: 'Could not retrieve file content.', life: 3000 });
    } finally {
        viewLoading.value = false;
    }
}

function closeView() {
    viewDialog.value = false;
    csvData.value = null;
    if (pdfUrl.value) {
        window.URL.revokeObjectURL(pdfUrl.value);
        pdfUrl.value = '';
    }
}

async function startCrawl() {
    if (!crawlUrl.value) return;
    crawling.value = true;
    try {
        const res = await apiFetch(`/api/project/${projectId.value}/knowledge/crawl`, {
            method: 'POST',
            body: JSON.stringify({
                url: crawlUrl.value,
                maxPages: maxPages.value
            })
        });
        if (res.ok) {
            showCrawlStarted.value = true;
            crawlUrl.value = '';
            loadCrawlJobs();
            startPolling();
        } else {
            const err = await res.text();
            toast.add({ severity: 'error', summary: 'Crawl Failed', detail: err, life: 5000 });
        }
    } catch (e) {
        console.error(e);
        toast.add({ severity: 'error', summary: 'Error', detail: 'An error occurred while starting the crawl.', life: 5000 });
    } finally {
        crawling.value = false;
    }
}

onMounted(loadData);
</script>

<template>
    <div class="knowledge-base">
        <header class="header">
            <div>
                <router-link :to="'/project/' + projectId" class="back-link">
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <line x1="19" y1="12" x2="5" y2="12"></line>
                        <polyline points="12 19 5 12 12 5"></polyline>
                    </svg>
                    Back to Project
                </router-link>
                <h1>Knowledge Base (RAG)</h1>
                <p class="subtitle">Upload documents or import websites to provide context to your AI.</p>
            </div>
        </header>

        <section class="section">
            <div v-if="!hasConfigWithKey && !initialLoading" class="warning-banner mb-6">
                <div class="warning-icon">
                    <i class="pi pi-exclamation-triangle" style="font-size: 1.5rem"></i>
                </div>
                <div class="warning-content">
                    <h3>Missing API Configuration</h3>
                    <p>You must first configure a Gemini or OpenAI API key in Project Settings to generate embeddings.</p>
                </div>
            </div>

            <div class="grid grid-cols-1 lg:grid-cols-3 gap-6 mb-8">
                <!-- Upload Section -->
                <Card class="lg:col-span-2 shadow-sm border">
                    <template #title>
                        <div class="flex items-center gap-2 text-lg">
                            <i class="pi pi-upload text-primary"></i>
                            <span>Upload Documents</span>
                        </div>
                    </template>
                    <template #content>
                        <!-- Advanced Chunking Settings Accordion -->
                        <div class="mb-4 p-4 bg-surface-50 border rounded-lg flex flex-col gap-3">
                            <div class="flex items-center justify-between cursor-pointer" @click="showAdvanced = !showAdvanced">
                                <span class="font-semibold text-sm text-surface-700 flex items-center gap-2">
                                    <i class="pi pi-cog text-primary"></i>
                                    <span>Advanced RAG Splitter Options</span>
                                </span>
                                <i :class="showAdvanced ? 'pi pi-chevron-up' : 'pi pi-chevron-down'" class="text-xs text-surface-500"></i>
                            </div>
                            
                            <div v-show="showAdvanced" class="grid grid-cols-1 md:grid-cols-3 gap-4 pt-3 border-t">
                                <div class="flex flex-col gap-1">
                                    <label class="text-[10px] font-semibold text-surface-500 uppercase tracking-wider">Strategy</label>
                                    <select v-model="chunkingStrategy" class="p-inputtext w-full text-xs">
                                        <option value="character">Character Splitter</option>
                                        <option value="line">Line Splitter</option>
                                        <option value="recursive">Recursive Paragraph Splitter</option>
                                    </select>
                                </div>
                                <div class="flex flex-col gap-1">
                                    <label class="text-[10px] font-semibold text-surface-500 uppercase tracking-wider">Chunk Size</label>
                                    <InputNumber v-model="chunkSize" class="w-full text-xs" />
                                </div>
                                <div class="flex flex-col gap-1">
                                    <label class="text-[10px] font-semibold text-surface-500 uppercase tracking-wider">Overlap</label>
                                    <InputNumber v-model="chunkOverlap" class="w-full text-xs" />
                                </div>
                            </div>
                        </div>

                        <FileUpload name="file" :url="uploadUrl" @upload="onUpload" @error="onError"
                            @before-send="onBeforeSend" :multiple="true" accept=".pdf,.txt,.json,.md,.csv"
                            :maxFileSize="10000000" :withCredentials="true" :disabled="!hasConfigWithKey"
                            mode="advanced" class="custom-upload">
                            <template #empty>
                                <div class="py-4 text-center text-surface-500">
                                    <p>PDF, TXT, JSON, MD, CSV (Max 10MB)</p>
                                </div>
                            </template>
                        </FileUpload>
                    </template>
                </Card>

                <!-- Website Import Section -->
                <Card class="shadow-sm border">
                    <template #title>
                        <div class="flex items-center gap-2 text-lg">
                            <i class="pi pi-globe text-primary"></i>
                            <span>Website Import</span>
                        </div>
                    </template>
                    <template #content>
                        <div class="flex flex-col gap-4">
                            <div class="flex flex-col gap-2">
                                <label class="text-xs font-semibold text-surface-500 uppercase tracking-wider">Base URL</label>
                                <div class="search-container">
                                    <i class="pi pi-link search-icon"></i>
                                    <InputText v-model="crawlUrl" placeholder="https://example.com" class="w-full search-input" :disabled="!hasConfigWithKey || crawling" />
                                </div>
                            </div>
                            <div class="flex flex-col gap-2">
                                <label class="text-xs font-semibold text-surface-500 uppercase tracking-wider">Limit</label>
                                <select v-model="maxPages" class="p-inputtext w-full" :disabled="!hasConfigWithKey || crawling">
                                    <option :value="5">5 Pages</option>
                                    <option :value="10">10 Pages</option>
                                    <option :value="25">25 Pages</option>
                                    <option :value="50">50 Pages</option>
                                </select>
                            </div>
                            <Button label="Import Site" icon="pi pi-play" @click="startCrawl"
                                :loading="crawling" :disabled="!hasConfigWithKey || !crawlUrl" class="w-full" />
                        </div>
                    </template>
                </Card>
            </div>

            <!-- Active Crawls -->
            <div v-if="crawlJobs.length && crawlJobs.some(j => j.status === 'Processing' || j.status === 'Pending')" class="mb-8">
                <h3 class="mb-4 flex items-center gap-2 text-surface-700">
                    <i class="pi pi-sync pi-spin text-primary"></i>
                    Active Crawl Jobs
                </h3>
                <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
                    <Card v-for="job in crawlJobs.filter(j => j.status === 'Processing' || j.status === 'Pending')" :key="job.id" class="border shadow-none">
                        <template #content>
                            <div class="flex flex-col gap-2">
                                <div class="flex justify-between items-center">
                                    <span class="font-semibold text-sm truncate max-w-[200px]">{{ job.baseUrl }}</span>
                                    <Tag :value="job.status" severity="warn" />
                                </div>
                                <ProgressBar :value="(job.pagesCrawled / job.maxPages) * 100" style="height: 6px"></ProgressBar>
                                <div class="flex justify-between text-[10px] text-surface-500 uppercase">
                                    <span>Progress</span>
                                    <span>{{ job.pagesCrawled }} / {{ job.maxPages }} pages</span>
                                </div>
                            </div>
                        </template>
                    </Card>
                </div>
            </div>

            <!-- Documents Table -->
            <Card class="shadow-sm border">
                <template #title>
                    <div class="flex flex-col md:flex-row md:items-center justify-between gap-4">
                        <div class="flex items-center gap-2">
                            <i class="pi pi-database text-primary"></i>
                            <span>Ingested Documents</span>
                            <Tag v-if="documents.length" :value="documents.length" severity="secondary" class="ml-2" />
                        </div>
                        
                        <div class="flex items-center gap-2">
                            <div class="search-container">
                                <i class="pi pi-search search-icon"></i>
                                <InputText v-model="filters['global'].value" placeholder="Search files..." class="w-full md:w-64 search-input" />
                            </div>
                        </div>
                    </div>
                </template>
                <template #content>
                    <DataTable 
                        v-model:selection="selectedDocuments" 
                        :value="documents" 
                        :loading="loading"
                        :filters="filters"
                        dataKey="id" 
                        :paginator="true" 
                        :rows="10" 
                        responsiveLayout="scroll"
                        removableSort
                        class="p-datatable-sm"
                    >
                        <template #header>
                            <div class="flex items-center gap-2 py-1" v-if="selectedDocuments.length > 0">
                                <span class="text-sm font-medium mr-2">{{ selectedDocuments.length }} selected</span>
                                <Button icon="pi pi-refresh" label="Retry" severity="warn" text size="small" @click="batchRetry" />
                                <Button icon="pi pi-trash" label="Delete" severity="danger" text size="small" @click="batchDelete" />
                            </div>
                        </template>

                        <Column selectionMode="multiple" headerStyle="width: 3rem"></Column>
                        
                        <Column field="fileName" header="File Name" sortable>
                            <template #body="{ data }">
                                <div class="flex items-center gap-2 overflow-hidden">
                                    <i :class="data.contentType.includes('pdf') ? 'pi pi-file-pdf text-red-500' : 'pi pi-file text-primary'" style="font-size: 1.1rem"></i>
                                    <span class="font-medium text-sm truncate flex-1 min-w-0" style="max-width: 15rem;" v-tooltip="data.fileName">{{ data.fileName }}</span>
                                </div>
                            </template>
                        </Column>

                        <Column field="fileSize" header="Size" sortable>
                            <template #body="{ data }">
                                <span class="text-xs text-surface-500">{{ formatSize(data.fileSize) }}</span>
                            </template>
                        </Column>

                        <Column field="chunkCount" header="Chunks" sortable>
                            <template #body="{ data }">
                                <Tag v-if="data.chunkCount > 0" :value="data.chunkCount" severity="secondary" rounded />
                                <span v-else class="text-xs text-surface-400 italic">Pending</span>
                            </template>
                        </Column>

                        <Column field="status" header="Status" sortable>
                            <template #body="{ data }">
                                <Tag v-if="data.status === 'Completed'" severity="success" value="Ready" rounded />
                                <Tag v-else-if="data.status === 'Failed'" severity="danger" value="Failed" rounded v-tooltip="data.errorMessage" />
                                <Tag v-else severity="warn" value="Processing" rounded />
                            </template>
                        </Column>

                        <Column field="createdAt" header="Uploaded" sortable>
                            <template #body="{ data }">
                                <span class="text-xs text-surface-500">{{ formatDate(data.createdAt) }}</span>
                            </template>
                        </Column>

                        <Column header="Actions" alignFrozen="right" frozen>
                            <template #body="{ data }">
                                <div class="flex gap-1">
                                    <Button icon="pi pi-eye" text rounded severity="secondary" @click="viewFile(data)" v-tooltip.bottom="'View Content'" />
                                    <Button v-if="data.status === 'Failed'" icon="pi pi-refresh" text rounded severity="warn" @click="retryDoc(data.id)" v-tooltip.bottom="'Retry'" />
                                    <Button icon="pi pi-trash" text rounded severity="danger" @click="deleteDoc(data.id)" v-tooltip.bottom="'Delete'" />
                                </div>
                            </template>
                        </Column>
                        
                        <template #empty>
                            <div class="text-center py-8 text-surface-500">
                                No documents found.
                            </div>
                        </template>
                    </DataTable>
                </template>
            </Card>

            <Dialog v-model:visible="viewDialog" modal :header="viewedDoc?.fileName || 'Document View'" :style="{ width: '95vw', maxWidth: '1200px' }" :contentStyle="{ padding: '0' }" @hide="closeView">
                <div v-if="viewLoading" class="flex flex-col items-center justify-center py-12">
                    <i class="pi pi-spin pi-spinner text-3xl text-primary mb-4"></i>
                    <p>Loading document content...</p>
                </div>
                <div v-else-if="pdfUrl" class="pdf-viewer">
                    <iframe :src="pdfUrl" style="width: 100%; height: 80vh; border: none;"></iframe>
                </div>
                <div v-else-if="csvData" class="csv-viewer p-2 md:p-4">
                    <DataTable :value="csvData.data" scrollable scrollHeight="75vh" size="small" class="border rounded-lg overflow-hidden">
                        <Column v-for="col in csvData.meta.fields" :key="col" :field="col" :header="col" sortable></Column>
                    </DataTable>
                </div>
                <div v-else-if="docContent" class="doc-viewer-container p-2 md:p-6">
                    <div v-if="viewedDoc?.contentType.includes('text') || viewedDoc?.contentType.includes('md')" class="markdown-body" v-html="renderedMarkdown" style="max-height: 80vh; overflow-y: auto;"></div>
                    <pre v-else class="whitespace-pre-wrap font-sans text-sm p-4 bg-surface-50 rounded-lg border overflow-y-auto" style="max-height: 80vh;">{{ docContent }}</pre>
                </div>
                <div v-else class="text-center py-8">
                    <p>This file type cannot be previewed directly. It has been downloaded.</p>
                </div>
                <template #footer>
                    <div class="p-3 border-t bg-surface-50 flex justify-end">
                        <Button label="Close" icon="pi pi-times" @click="closeView" text />
                    </div>
                </template>
            </Dialog>

            <!-- Crawl Started Feedback -->
            <Dialog v-model:visible="showCrawlStarted" modal header="Crawl Job Started" :style="{ width: '450px' }">
                <div class="flex flex-col items-center text-center py-4">
                    <i class="pi pi-check-circle text-primary text-5xl mb-4"></i>
                    <h3>Website Crawl Initiated!</h3>
                    <p class="text-surface-500 mt-2 mb-6">
                        Firecrawl is now processing your request. 
                        The pages will appear in the list as "Processing" shortly.
                    </p>
                    <div class="bg-surface-50 p-4 border rounded-lg text-left w-full text-xs">
                        <strong>Note:</strong> Deep crawls can take several minutes. You can safely leave this page.
                    </div>
                </div>
                <template #footer>
                    <div class="flex justify-center w-full">
                        <Button label="Got it" severity="primary" @click="showCrawlStarted = false" class="px-8" />
                    </div>
                </template>
            </Dialog>
        </section>
        
        <ConfirmDialog />
        <Toast />
    </div>
</template>

<style scoped>
.header {
    margin-bottom: 32px;
}

.back-link {
    color: var(--p-primary-500);
    text-decoration: none;
    font-size: 0.85rem;
    font-weight: 600;
    display: flex;
    align-items: center;
    gap: 8px;
    margin-bottom: 8px;
}

.subtitle {
    color: var(--p-surface-500);
    font-size: 0.9rem;
    margin-top: 4px;
}

.section {
    margin-bottom: 48px;
}

.warning-banner {
    display: flex;
    gap: 16px;
    padding: 16px;
    background: color-mix(in srgb, var(--p-warning-500), transparent 92%);
    border: 1px solid var(--p-warning-200);
    border-radius: 8px;
    color: var(--p-warning-800);
}

.warning-content h3 {
    font-size: 0.95rem;
    margin-bottom: 2px;
}

.warning-content p {
    font-size: 0.85rem;
    margin: 0;
}

.custom-upload :deep(.p-fileupload-buttonbar) {
    background: transparent;
    border: none;
    padding: 0 0 1rem 0;
}

.custom-upload :deep(.p-fileupload-content) {
    border: 1px dashed var(--p-surface-300);
    border-radius: 8px;
}

.doc-viewer-container pre {
    scrollbar-width: thin;
    scrollbar-color: var(--p-surface-300) transparent;
}

.markdown-body {
    font-family: var(--p-font-family);
    line-height: 1.6;
    color: var(--p-surface-700);
    padding: 24px;
    background: var(--p-surface-0);
    border-radius: 8px;
    max-height: 80vh;
    overflow-y: auto;
    border: 1px solid var(--p-surface-200);
}

.markdown-body :deep(h1), 
.markdown-body :deep(h2), 
.markdown-body :deep(h3) {
    margin-top: 24px;
    margin-bottom: 16px;
    font-weight: 700;
    line-height: 1.25;
    color: var(--p-surface-900);
}

.markdown-body :deep(h1) { font-size: 1.75rem; border-bottom: 1px solid var(--p-surface-200); padding-bottom: 0.3em; }
.markdown-body :deep(h2) { font-size: 1.5rem; border-bottom: 1px solid var(--p-surface-200); padding-bottom: 0.3em; }
.markdown-body :deep(h3) { font-size: 1.25rem; }

.markdown-body :deep(p) {
    margin-top: 0;
    margin-bottom: 16px;
}

.markdown-body :deep(ul), 
.markdown-body :deep(ol) {
    padding-left: 2em;
    margin-bottom: 16px;
}

.markdown-body :deep(li) {
    margin-bottom: 4px;
}

.markdown-body :deep(code) {
    padding: 0.2em 0.4em;
    margin: 0;
    font-size: 85%;
    background-color: var(--p-surface-100);
    border-radius: 6px;
    font-family: monospace;
}

.markdown-body :deep(pre) {
    padding: 16px;
    overflow: auto;
    font-size: 85%;
    line-height: 1.45;
    background-color: var(--p-surface-50);
    border-radius: 8px;
    margin-bottom: 16px;
    border: 1px solid var(--p-surface-200);
}

.markdown-body :deep(pre code) {
    background: transparent;
    padding: 0;
    font-size: 100%;
}

.markdown-body :deep(blockquote) {
    padding: 0 1em;
    color: var(--p-surface-500);
    border-left: 0.25em solid var(--p-surface-300);
    margin: 0 0 16px 0;
}

.markdown-body :deep(img) {
    max-width: 100%;
    height: auto;
    border-radius: 8px;
}

.markdown-body :deep(a) {
    color: var(--p-primary-500);
    text-decoration: none;
}

.markdown-body :deep(a:hover) {
    text-decoration: underline;
}

.markdown-body :deep(table) {
    width: 100%;
    border-collapse: collapse;
    margin-bottom: 16px;
}

.markdown-body :deep(table th), 
.markdown-body :deep(table td) {
    padding: 8px 12px;
    border: 1px solid var(--p-surface-200);
}

.markdown-body :deep(table th) {
    background: var(--p-surface-50);
    font-weight: 600;
}

/* PrimeVue Overrides */
:deep(.p-datatable-header) {
    background: transparent;
    border-bottom: 1px solid var(--p-surface-200);
    padding: 1rem;
}

:deep(.p-datatable-thead > tr > th) {
    background: var(--p-surface-50);
    font-size: 0.75rem;
    text-transform: uppercase;
    letter-spacing: 0.05em;
    font-weight: 700;
    color: var(--p-surface-600);
}

:deep(.p-datatable-tbody > tr) {
    transition: background 0.2s;
}

:deep(.p-datatable-tbody > tr:hover) {
    background: var(--p-surface-50);
}

.mb-6 { margin-bottom: 1.5rem; }
.mb-8 { margin-bottom: 2rem; }
.mt-2 { margin-top: 0.5rem; }

.flex { display: flex; }
.flex-col { flex-direction: column; }
.items-center { align-items: center; }
.justify-between { justify-content: space-between; }
.gap-1 { gap: 0.25rem; }
.gap-2 { gap: 0.5rem; }
.gap-4 { gap: 1rem; }
.gap-6 { gap: 1.5rem; }

.w-full { width: 100%; }
.shadow-sm { box-shadow: 0 1px 2px 0 rgba(0, 0, 0, 0.05); }
.border { border: 1px solid var(--p-surface-200); }
.text-lg { font-size: 1.125rem; }
.text-sm { font-size: 0.875rem; }
.text-xs { font-size: 0.75rem; }
.font-semibold { font-weight: 600; }
.font-medium { font-weight: 500; }
.text-primary { color: var(--p-primary-500); }
.text-surface-500 { color: var(--p-surface-500); }
.text-surface-700 { color: var(--p-surface-700); }
.bg-surface-50 { background-color: var(--p-surface-50); }
.rounded-lg { border-radius: 0.5rem; }
.truncate { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.grid { display: grid; }
.grid-cols-1 { grid-template-columns: repeat(1, minmax(0, 1fr)); }

@media (min-width: 1024px) {
    .lg\:col-span-2 { grid-column: span 2 / span 2; }
    .lg\:grid-cols-3 { grid-template-columns: repeat(3, minmax(0, 1fr)); }
}

@media (min-width: 768px) {
    .md\:grid-cols-2 { grid-template-columns: repeat(2, minmax(0, 1fr)); }
    .md\:w-64 { width: 16rem; }
    .md\:flex-row { flex-direction: row; }
}

.search-container {
    position: relative;
    display: inline-flex;
    align-items: center;
    width: 100%;
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

<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
import { useRoute } from 'vue-router';
import { useApi } from '../composables/useApi';
import Button from 'primevue/button';
import Card from 'primevue/card';
import FileUpload from 'primevue/fileupload';
import ProgressBar from 'primevue/progressbar';
import Tag from 'primevue/tag';

const { apiFetch, getToken, API_BASE } = useApi();

const route = useRoute();
const projectId = computed(() => route.params.projectId as string);
const documents = ref<any[]>([]);
const configurations = ref<any[]>([]);
const loading = ref(false);
const initialLoading = ref(true);
const crawling = ref(false);
const crawlUrl = ref('');
const maxPages = ref(10);

const hasConfigWithKey = computed(() => {
    return configurations.value.some(c => c.hasGeminiKey || c.hasOpenAiKey);
});

const uploadUrl = computed(() => `${API_BASE}/api/project/${projectId.value}/knowledge/upload`);

async function loadData() {
    initialLoading.value = true;
    try {
        await Promise.all([
            loadDocuments(),
            loadConfigurations()
        ]);
    } finally {
        initialLoading.value = false;
    }
}

async function loadConfigurations() {
    try {
        const res = await apiFetch(`/api/project/${projectId.value}/configurations`);
        if (res.ok) configurations.value = await res.json();
    } catch (e) {
        console.error('Failed to load configs', e);
    }
}

async function loadDocuments() {
    loading.value = true;
    try {
        const res = await apiFetch(`/api/project/${projectId.value}/knowledge`);
        if (res.ok) documents.value = await res.json();
    } catch (e) {
        console.error(e);
    } finally {
        loading.value = false;
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
}

function onProgress(event: any) {
    // PrimeVue provides progress in event.progress
    console.log('Upload progress:', event.progress);
}

function onError(event: any) {
    console.error('Upload error:', event);
    alert('Upload failed. Please check the network tab for details.');
}

async function deleteDoc(id: string) {
    if (!confirm('Delete this document? It will be removed from the Knowledge Base and RAG system.')) return;
    await apiFetch(`/api/project/${projectId.value}/knowledge/${id}`, { method: 'DELETE' });
    await loadDocuments();
}

function formatSize(bytes: number) {
    if (bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
}

async function retryDoc(id: string) {
    loading.value = true;
    try {
        const res = await apiFetch(`/api/project/${projectId.value}/knowledge/${id}/retry`, { method: 'POST' });
        if (res.ok) {
            await loadDocuments();
        } else {
            const err = await res.text();
            alert(err);
        }
    } catch (e) {
        console.error(e);
    } finally {
        loading.value = false;
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
            alert('Crawl job started! It will run in the background. Refresh in a few minutes to see the pages.');
            crawlUrl.value = '';
            loadDocuments();
        } else {
            const err = await res.text();
            alert('Failed to start crawl: ' + err);
        }
    } catch (e) {
        console.error(e);
        alert('An error occurred while starting the crawl.');
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
                <p class="subtitle">Upload documents to provide extra context to your AI. Files are automatically
                    chunked and embedded.</p>
            </div>
        </header>

        <section class="section">
            <div v-if="!hasConfigWithKey && !initialLoading" class="warning-banner mb-6">
                <div class="warning-icon">
                    <i class="pi pi-exclamation-triangle" style="font-size: 1.5rem"></i>
                </div>
                <div class="warning-content">
                    <h3>Missing API Configuration</h3>
                    <p>To use the Knowledge Base, you must first create a project configuration with a valid Gemini or
                        OpenAI API key for generating embeddings.</p>
                    <router-link :to="'/project/' + projectId" class="p-button p-button-sm p-button-warning mt-2">
                        Configure Now
                    </router-link>
                </div>
            </div>

            <Card class="upload-card" :class="{ 'disabled-card': !hasConfigWithKey }">
                <template #content>
                    <FileUpload name="file" :url="uploadUrl" @upload="onUpload" @error="onError" @progress="onProgress"
                        @before-send="onBeforeSend" :multiple="true" accept=".pdf,.txt,.json,.md,.csv"
                        :maxFileSize="10000000" :withCredentials="true" :disabled="!hasConfigWithKey">
                        <template #content="{ files, progress, removeFileCallback }">
                            <div v-if="files.length > 0">
                                <div class="upload-progress-container mb-4" v-if="progress > 0">
                                    <div class="flex justify-between mb-1">
                                        <span class="text-xs font-medium">Uploading...</span>
                                        <span class="text-xs font-medium">{{ progress }}%</span>
                                    </div>
                                    <ProgressBar :value="progress" :showValue="false" style="height: 4px"></ProgressBar>
                                </div>

                                <div class="files-list">
                                    <div v-for="(file, index) of files" :key="file.name + file.size" class="file-item">
                                        <div class="file-header">
                                            <div class="file-details">
                                                <i class="pi pi-file text-primary"></i>
                                                <div>
                                                    <div class="file-name">{{ file.name }}</div>
                                                    <div class="file-size">{{ formatSize(file.size) }}</div>
                                                </div>
                                            </div>
                                            <Button icon="pi pi-times" @click="removeFileCallback(index)" text rounded
                                                severity="danger" />
                                        </div>
                                    </div>
                                </div>
                            </div>
                        </template>
                        <template #empty>
                            <div class="upload-placeholder" v-if="!loading">
                                <i class="pi pi-cloud-upload"
                                    style="font-size: 2.5rem; color: var(--p-surface-400)"></i>
                                <p>Drag and drop files here to upload (PDF, TXT, JSON, MD, CSV)</p>
                            </div>
                        </template>
                    </FileUpload>
                </template>
            </Card>

            <Card class="mt-8">
                <template #title>
                    <div class="flex items-center gap-2">
                        <i class="pi pi-globe text-primary"></i>
                        <span>Import from Website</span>
                    </div>
                </template>
                <template #content>
                    <p class="text-sm text-surface-500 mb-4">Enter a URL to crawl the entire website. This will process
                        up to the specified number of pages.</p>
                    <div class="flex gap-4">
                        <div class="flex-1">
                            <span class="p-input-icon-left w-full">
                                <i class="pi pi-link" />
                                <input v-model="crawlUrl" type="text" class="p-inputtext p-component w-full"
                                    placeholder="https://docs.example.com" :disabled="!hasConfigWithKey || crawling" />
                            </span>
                        </div>
                        <div style="width: 150px">
                            <select v-model="maxPages" class="p-inputtext p-component w-full"
                                :disabled="!hasConfigWithKey || crawling">
                                <option :value="5">5 Pages</option>
                                <option :value="10">10 Pages</option>
                                <option :value="25">25 Pages</option>
                                <option :value="50">50 Pages</option>
                            </select>
                        </div>
                        <Button label="Start Crawl" icon="pi pi-play" @click="startCrawl"
                            :loading="crawling" :disabled="!hasConfigWithKey || !crawlUrl" />
                    </div>
                </template>
            </Card>

            <div class="document-list mt-8">
                <h2 class="mb-4">Ingested Documents</h2>
                <div v-if="loading" class="loading-state">
                    <ProgressBar mode="indeterminate" style="height: 6px"></ProgressBar>
                </div>
                <div v-else-if="documents.length" class="docs-grid">
                    <Card v-for="doc in documents" :key="doc.id" class="doc-card">
                        <template #content>
                            <div class="doc-item">
                                <div class="doc-icon">
                                    <i :class="doc.contentType.includes('pdf') ? 'pi pi-file-pdf' : 'pi pi-file'"
                                        style="font-size: 1.5rem"></i>
                                </div>
                                <div class="doc-info">
                                    <h4 class="doc-name">{{ doc.fileName }}</h4>
                                    <div class="doc-meta">
                                        <span>{{ formatSize(doc.fileSize) }}</span>
                                        <span class="dot">·</span>
                                        <span v-if="doc.chunkCount > 0">{{ doc.chunkCount }} chunks</span>
                                        <span v-else class="text-surface-400">Awaiting processing</span>
                                    </div>
                                    <div v-if="doc.errorMessage" class="doc-error-text">
                                        {{ doc.errorMessage }}
                                    </div>
                                </div>
                                <div class="doc-status">
                                    <Tag v-if="doc.status === 'Completed'" severity="success" value="Ready" rounded />
                                    <Tag v-else-if="doc.status === 'Failed'" severity="danger" value="Failed" rounded
                                        v-tooltip="doc.errorMessage" />
                                    <Tag v-else severity="warn" value="Processing..." rounded />
                                </div>
                                <div class="doc-actions">
                                    <Button v-if="doc.status === 'Failed'" icon="pi pi-refresh" severity="warn" text
                                        rounded @click="retryDoc(doc.id)" v-tooltip="'Retry Processing'" />
                                    <Button icon="pi pi-trash" severity="danger" text rounded
                                        @click="deleteDoc(doc.id)" />
                                </div>
                            </div>
                        </template>
                    </Card>
                </div>
                <div v-else class="empty-state">
                    <p>No documents uploaded yet. Add some to enable RAG features.</p>
                </div>
            </div>
        </section>
    </div>
</template>

<style scoped>
.header {
    margin-bottom: 48px;
}

.back-link {
    color: var(--p-primary-400);
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

.upload-card {
    background: var(--p-surface-0);
    border: 1px dashed var(--p-surface-300);
    border-radius: 12px;
    overflow: hidden;
}

.upload-placeholder {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 16px;
    padding: 32px;
    color: var(--p-surface-500);
}

.files-list {
    display: grid;
    grid-template-columns: 1fr;
    gap: 8px;
    padding: 1rem;
}

.file-item {
    display: flex;
    flex-direction: column;
    gap: 8px;
    padding: 12px;
    border: 1px solid var(--p-surface-200);
    border-radius: 8px;
    background: var(--p-surface-50);
}

.file-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
}

.file-details {
    display: flex;
    align-items: center;
    gap: 12px;
}

.file-name {
    font-size: 0.9rem;
    font-weight: 500;
}

.mb-6 {
    margin-bottom: 1.5rem;
}

.warning-banner {
    display: flex;
    gap: 16px;
    padding: 20px;
    background: color-mix(in srgb, var(--p-warning-500), transparent 92%);
    border: 1px solid var(--p-warning-200);
    border-radius: 12px;
    color: var(--p-warning-700);
}

.warning-icon {
    color: var(--p-warning-500);
    padding-top: 2px;
}

.warning-content h3 {
    font-size: 1rem;
    margin-bottom: 4px;
    color: var(--p-warning-800);
}

.warning-content p {
    font-size: 0.9rem;
    margin: 0;
    opacity: 0.9;
}

.disabled-card {
    opacity: 0.6;
    pointer-events: none;
    filter: grayscale(0.5);
}

.doc-error-text {
    font-size: 0.75rem;
    color: var(--p-danger-500);
    margin-top: 4px;
    font-weight: 500;
}

.doc-actions {
    display: flex;
    gap: 4px;
}

.file-size {
    font-size: 0.75rem;
    color: var(--p-surface-500);
}

.upload-progress-container {
    padding: 1rem 1rem 0 1rem;
}

.flex {
    display: flex;
}

.flex-col {
    flex-direction: column;
}

.items-center {
    align-items: center;
}

.justify-between {
    justify-content: space-between;
}

.gap-3 {
    gap: 12px;
}

.text-primary {
    color: var(--p-primary-500);
}

.font-medium {
    font-weight: 500;
}

.text-xs {
    font-size: 0.75rem;
}

.mt-8 {
    margin-top: 2rem;
}

.mb-4 {
    margin-bottom: 1rem;
}

.docs-grid {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(450px, 1fr));
    gap: 24px;
}

.doc-card {
    background: var(--p-surface-0);
    border: 1px solid var(--p-surface-200);
}

.doc-item {
    display: flex;
    align-items: center;
    gap: 16px;
}

.doc-icon {
    width: 40px;
    height: 40px;
    border-radius: 8px;
    background: var(--p-surface-100);
    display: flex;
    align-items: center;
    justify-content: center;
    color: var(--p-primary-500);
}

.doc-info {
    flex: 1;
}

.doc-name {
    margin: 0;
    font-size: 0.95rem;
    font-weight: 600;
    width: 250px;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
}

.doc-meta {
    font-size: 0.8rem;
    color: var(--p-surface-500);
    display: flex;
    align-items: center;
    gap: 8px;
    margin-top: 2px;
}

.dot {
    opacity: 0.5;
}

.empty-state {
    text-align: center;
    padding: 48px;
    color: var(--p-surface-500);
    background: var(--p-surface-50);
    border-radius: 12px;
}

/* ── Mobile ── */
@media (max-width: 768px) {

    .docs-grid {
        display: grid;
        grid-template-columns: 1fr;
        gap: 24px;
    }

    .doc-item {
        flex-wrap: wrap;
    }

    
.doc-name {
    width: 200px;
}

    .doc-status {
        order: 4;
        width: 100%;
        margin-top: 8px;
    }
}
</style>

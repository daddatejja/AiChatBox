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
const loading = ref(false);

const uploadUrl = computed(() => `${API_BASE}/api/project/${projectId.value}/knowledge/upload`);

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

onMounted(loadDocuments);
</script>

<template>
    <div class="knowledge-base">
        <header class="header">
            <div>
                <router-link :to="'/project/' + projectId" class="back-link">
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="19" y1="12" x2="5" y2="12"></line><polyline points="12 19 5 12 12 5"></polyline></svg>
                    Back to Project
                </router-link>
                <h1>Knowledge Base (RAG)</h1>
                <p class="subtitle">Upload documents to provide extra context to your AI. Files are automatically chunked and embedded.</p>
            </div>
        </header>

        <section class="section">
            <Card class="upload-card">
                <template #content>
                    <FileUpload 
                        name="file" 
                        :url="uploadUrl" 
                        @upload="onUpload" 
                        @error="onError"
                        @progress="onProgress"
                        @before-send="onBeforeSend"
                        :multiple="true" 
                        accept=".pdf,.txt,.json,.md,.csv" 
                        :maxFileSize="10000000"
                        :withCredentials="true"
                    >
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
                                            <Button icon="pi pi-times" @click="removeFileCallback(index)" text rounded severity="danger" />
                                        </div>
                                    </div>
                                </div>
                            </div>
                        </template>
                        <template #empty>
                            <div class="upload-placeholder" v-if="!loading">
                                <i class="pi pi-cloud-upload" style="font-size: 2.5rem; color: var(--p-surface-400)"></i>
                                <p>Drag and drop files here to upload (PDF, TXT, JSON, MD, CSV)</p>
                            </div>
                        </template>
                    </FileUpload>
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
                                    <i :class="doc.contentType.includes('pdf') ? 'pi pi-file-pdf' : 'pi pi-file'" style="font-size: 1.5rem"></i>
                                </div>
                                <div class="doc-info">
                                    <h4 class="doc-name">{{ doc.fileName }}</h4>
                                    <div class="doc-meta">
                                        <span>{{ formatSize(doc.fileSize) }}</span>
                                        <span class="dot">·</span>
                                        <span>{{ doc.chunkCount }} chunks</span>
                                    </div>
                                </div>
                                <div class="doc-status">
                                    <Tag v-if="doc.isProcessed" severity="success" value="Ready" rounded />
                                    <Tag v-else severity="warn" value="Processing..." rounded />
                                </div>
                                <Button icon="pi pi-trash" severity="danger" text rounded @click="deleteDoc(doc.id)" />
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

.file-size {
    font-size: 0.75rem;
    color: var(--p-surface-500);
}

.upload-progress-container {
    padding: 1rem 1rem 0 1rem;
}

.flex { display: flex; }
.flex-col { flex-direction: column; }
.items-center { align-items: center; }
.justify-between { justify-content: space-between; }
.gap-3 { gap: 12px; }
.text-primary { color: var(--p-primary-500); }
.font-medium { font-weight: 500; }
.text-xs { font-size: 0.75rem; }
.mt-8 { margin-top: 2rem; }
.mb-4 { margin-bottom: 1rem; }

.docs-grid {
    display: grid;
    grid-template-columns: 1fr;
    gap: 12px;
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
    .doc-item {
        flex-wrap: wrap;
    }
    .doc-status {
        order: 4;
        width: 100%;
        margin-top: 8px;
    }
}
</style>

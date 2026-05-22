<script setup lang="ts">
import { ref, reactive, onMounted, computed, watch } from 'vue';
import { useRoute } from 'vue-router';
import { useApi } from '../composables/useApi';
import Button from 'primevue/button';
import Card from 'primevue/card';
import InputText from 'primevue/inputtext';
import Textarea from 'primevue/textarea';
import Select from 'primevue/select';
import DataTable from 'primevue/datatable';
import Column from 'primevue/column';
import FileUpload from 'primevue/fileupload';
import Tag from 'primevue/tag';
import Dialog from 'primevue/dialog';
import ConfirmDialog from 'primevue/confirmdialog';
import Toast from 'primevue/toast';
import { useToast } from 'primevue/usetoast';
import { useConfirm } from 'primevue/useconfirm';
import ColorPicker from 'primevue/colorpicker';
import InputNumber from 'primevue/inputnumber';
import Checkbox from 'primevue/checkbox';

const route = useRoute();
const { apiFetch, getToken, API_BASE } = useApi();
const toast = useToast();
const confirm = useConfirm();

const projectId = computed(() => route.params.projectId as string);
const project = ref<any>(null);
const configId = ref<string | null>(null);
const loading = ref(true);
const saving = ref(false);

// Scoped Embed Settings (decides visible tabs)
const embedSettings = ref({
    showPrompt: true,
    showKnowledgeBase: true,
    showRules: true,
    showWidgetCustomization: true
});

const activeTab = ref('');

// List of allowed tabs based on permissions
const allowedTabs = computed(() => {
    const list = [];
    if (embedSettings.value.showPrompt) list.push({ id: 'prompt', label: 'System Prompt', icon: 'pi pi-comment' });
    if (embedSettings.value.showKnowledgeBase) list.push({ id: 'knowledge', label: 'Knowledge Base', icon: 'pi pi-database' });
    if (embedSettings.value.showRules) list.push({ id: 'rules', label: 'Conversation Rules', icon: 'pi pi-book' });
    if (embedSettings.value.showWidgetCustomization) list.push({ id: 'widget', label: 'Widget Styling', icon: 'pi pi-palette' });
    return list;
});

// Height adjustment postMessage
function notifyParentHeight() {
    setTimeout(() => {
        const height = document.documentElement.scrollHeight || document.body.scrollHeight;
        window.parent.postMessage({
            type: 'aichatbox-embed-height',
            projectId: projectId.value,
            height: height
        }, '*');
    }, 150);
}

// ──────────────────────────────────────────
// Tab 1: System Prompt Settings
// ──────────────────────────────────────────
const promptForm = reactive({
    systemPrompt: '',
    defaultProvider: 'gemini',
    defaultModel: 'gemini-3.1-flash-lite-preview'
});

const providerOptions = [
    { label: 'Google Gemini', value: 'gemini' },
    { label: 'OpenAI', value: 'openai' },
    { label: 'Anthropic Claude', value: 'anthropic' },
    { label: 'Groq', value: 'groq' }
];

const modelOptions = computed(() => {
    switch (promptForm.defaultProvider) {
        case 'gemini':
            return [
                { label: 'Gemini 3.1 Flash Lite', value: 'gemini-3.1-flash-lite-preview' },
                { label: 'Gemini 3.1 Flash', value: 'gemini-3.1-flash' }
            ];
        case 'openai':
            return [
                { label: 'GPT-4o Mini', value: 'gpt-4o-mini' },
                { label: 'GPT-4o', value: 'gpt-4o' }
            ];
        case 'anthropic':
            return [
                { label: 'Claude 4 Sonnet', value: 'claude-sonnet-4-20250514' }
            ];
        case 'groq':
            return [
                { label: 'Llama 3.3 70B', value: 'llama-3.3-70b-versatile' }
            ];
        default:
            return [];
    }
});

async function savePromptSettings() {
    if (!configId.value) return;
    saving.value = true;
    try {
        const res = await apiFetch(`/api/configuration/${configId.value}`, {
            method: 'PUT',
            body: JSON.stringify({
                systemPrompt: promptForm.systemPrompt,
                defaultProvider: promptForm.defaultProvider,
                defaultModel: promptForm.defaultModel
            })
        });
        if (res.ok) {
            toast.add({ severity: 'success', summary: 'Updated', detail: 'System prompt saved.', life: 3000 });
        } else {
            toast.add({ severity: 'error', summary: 'Error', detail: 'Failed to update system prompt.', life: 3000 });
        }
    } catch (e) {
        console.error(e);
    } finally {
        saving.value = false;
    }
}

// ──────────────────────────────────────────
// Tab 2: Knowledge Base
// ──────────────────────────────────────────
const documents = ref<any[]>([]);
const crawling = ref(false);
const crawlUrl = ref('');
const maxPages = ref(10);
const kbLoading = ref(false);

const uploadUrl = computed(() => {
    return `${API_BASE}/api/project/${projectId.value}/knowledge/upload`;
});

async function loadDocuments(silent = false) {
    if (!silent) kbLoading.value = true;
    try {
        const res = await apiFetch(`/api/project/${projectId.value}/knowledge`);
        if (res.ok) documents.value = await res.json();
    } catch (e) {
        console.error(e);
    } finally {
        if (!silent) kbLoading.value = false;
        notifyParentHeight();
    }
}

function onBeforeSend(event: any) {
    const token = getToken();
    if (token) event.xhr.setRequestHeader('Authorization', `Bearer ${token}`);
}

function onUpload() {
    loadDocuments();
    toast.add({ severity: 'success', summary: 'Success', detail: 'File uploaded and processing started.', life: 3000 });
}

function onUploadError() {
    toast.add({ severity: 'error', summary: 'Failed', detail: 'Document upload failed.', life: 5000 });
}

async function deleteDoc(id: string) {
    confirm.require({
        message: 'Delete this document from the Knowledge Base?',
        header: 'Confirm Deletion',
        icon: 'pi pi-exclamation-triangle',
        acceptProps: { label: 'Delete', severity: 'danger' },
        rejectProps: { label: 'Cancel', severity: 'secondary', outlined: true },
        accept: async () => {
            await apiFetch(`/api/project/${projectId.value}/knowledge/${id}`, { method: 'DELETE' });
            toast.add({ severity: 'success', summary: 'Deleted', detail: 'Document removed.', life: 3000 });
            await loadDocuments();
        }
    });
}

async function startCrawl() {
    if (!crawlUrl.value) return;
    crawling.value = true;
    try {
        const res = await apiFetch(`/api/project/${projectId.value}/knowledge/crawl`, {
            method: 'POST',
            body: JSON.stringify({ url: crawlUrl.value, maxPages: maxPages.value })
        });
        if (res.ok) {
            toast.add({ severity: 'success', summary: 'Import Started', detail: 'Website crawler initiated.', life: 3000 });
            crawlUrl.value = '';
            setTimeout(loadDocuments, 5000);
        } else {
            toast.add({ severity: 'error', summary: 'Failed', detail: 'Failed to initiate crawl.', life: 5000 });
        }
    } catch (e) {
        console.error(e);
    } finally {
        crawling.value = false;
    }
}

function formatSize(bytes: number) {
    if (bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
}

// ──────────────────────────────────────────
// Tab 3: Conversation Rules
// ──────────────────────────────────────────
interface Rule {
    id: string;
    type: string;
    trigger: string;
    intentLabel: string | null;
    responseType: string;
    response: string;
    priority: number;
    isActive: boolean;
}

const rules = ref<Rule[]>([]);
const rulesLoading = ref(false);
const showRuleDialog = ref(false);
const editingRuleId = ref<string | null>(null);

const ruleForm = reactive({
    type: 'exact',
    trigger: '',
    response: '',
    priority: 0,
    isActive: true
});

const ruleTypeOptions = [
    { label: 'Exact Match', value: 'exact' },
    { label: 'Keyword Match', value: 'keyword' },
    { label: 'AI Intent (Smart)', value: 'intent' }
];

async function loadRules() {
    rulesLoading.value = true;
    try {
        const res = await apiFetch(`/api/rules/project/${projectId.value}`);
        if (res.ok) rules.value = await res.json();
    } catch (e) {
        console.error(e);
    } finally {
        rulesLoading.value = false;
        notifyParentHeight();
    }
}

function openCreateRule() {
    editingRuleId.value = null;
    ruleForm.type = 'exact';
    ruleForm.trigger = '';
    ruleForm.response = '';
    ruleForm.priority = 0;
    ruleForm.isActive = true;
    showRuleDialog.value = true;
}

function openEditRule(rule: Rule) {
    editingRuleId.value = rule.id;
    ruleForm.type = rule.type;
    ruleForm.trigger = rule.trigger;
    ruleForm.response = rule.response;
    ruleForm.priority = rule.priority;
    ruleForm.isActive = rule.isActive;
    showRuleDialog.value = true;
}

async function saveRule() {
    const payload = {
        type: ruleForm.type,
        trigger: ruleForm.trigger,
        response: ruleForm.response,
        responseType: 'text',
        priority: ruleForm.priority,
        isActive: ruleForm.isActive
    };

    try {
        let res;
        if (editingRuleId.value) {
            res = await apiFetch(`/api/rules/${editingRuleId.value}`, {
                method: 'PUT',
                body: JSON.stringify(payload)
            });
        } else {
            res = await apiFetch(`/api/rules/project/${projectId.value}`, {
                method: 'POST',
                body: JSON.stringify(payload)
            });
        }
        if (res.ok) {
            toast.add({ severity: 'success', summary: 'Saved', detail: 'Rule saved successfully.', life: 3000 });
            showRuleDialog.value = false;
            await loadRules();
        } else {
            toast.add({ severity: 'error', summary: 'Error', detail: 'Failed to save rule.', life: 3000 });
        }
    } catch (e) {
        console.error(e);
    }
}

async function deleteRule(ruleId: string) {
    confirm.require({
        message: 'Are you sure you want to delete this rule?',
        header: 'Confirm Deletion',
        icon: 'pi pi-trash',
        acceptProps: { label: 'Delete', severity: 'danger' },
        rejectProps: { label: 'Cancel', severity: 'secondary', outlined: true },
        accept: async () => {
            await apiFetch(`/api/rules/${ruleId}`, { method: 'DELETE' });
            toast.add({ severity: 'success', summary: 'Deleted', detail: 'Rule deleted.', life: 3000 });
            await loadRules();
        }
    });
}

// ──────────────────────────────────────────
// Tab 4: Widget Styling (Appearance)
// ──────────────────────────────────────────
const defaultTheme = {
    primaryColor: '#39a7b9',
    bgColor: '#ffffff',
    fontFamily: 'Outfit',
    position: 'bottom-right',
    headerBgColor: '#39a7b9',
    headerTextColor: '#ffffff',
    userBubbleBgColor: '#e0f2fe',
    userBubbleTextColor: '#0369a1',
    botBubbleBgColor: '#ffffff',
    botBubbleTextColor: '#1e293b',
    title: 'AI Support',
    subtitle: 'Always active',
    placeholder: 'Type a message...'
};

const theme = reactive({ ...defaultTheme });

const fontOptions = [
    { label: 'Outfit (Default)', value: 'Outfit' },
    { label: 'Inter', value: 'Inter' },
    { label: 'Roboto', value: 'Roboto' },
    { label: 'System Default', value: 'system-ui' }
];

const positionOptions = [
    { label: 'Bottom Right', value: 'bottom-right' },
    { label: 'Bottom Left', value: 'bottom-left' }
];

async function saveThemeSettings() {
    if (!configId.value) return;
    saving.value = true;
    try {
        const res = await apiFetch(`/api/configuration/${configId.value}`, {
            method: 'PUT',
            body: JSON.stringify({
                themeSettingsJson: JSON.stringify(theme)
            })
        });
        if (res.ok) {
            toast.add({ severity: 'success', summary: 'Updated', detail: 'Widget appearance saved.', life: 3000 });
        } else {
            toast.add({ severity: 'error', summary: 'Error', detail: 'Failed to update appearance.', life: 3000 });
        }
    } catch (e) {
        console.error(e);
    } finally {
        saving.value = false;
    }
}

// ──────────────────────────────────────────
// Initialization
// ──────────────────────────────────────────
async function init() {
    loading.value = true;
    try {
        // 1. Fetch Project Details
        const projectRes = await apiFetch(`/api/project/${projectId.value}`);
        if (!projectRes.ok) {
            loading.value = false;
            return;
        }
        project.value = await projectRes.json();
        
        // Parse Embed settings
        try {
            const parsed = JSON.parse(project.value.embedSettingsJson || '{}');
            embedSettings.value = {
                showPrompt: parsed.showPrompt !== false,
                showKnowledgeBase: parsed.showKnowledgeBase !== false,
                showRules: parsed.showRules !== false,
                showWidgetCustomization: parsed.showWidgetCustomization !== false
            };
        } catch {
            // Default to all true on failure
        }

        // Set active tab to first allowed tab
        if (allowedTabs.value.length > 0) {
            activeTab.value = allowedTabs.value[0].id;
        }

        // 2. Fetch Configurations for Project to get configId
        const configsRes = await apiFetch(`/api/project/${projectId.value}/configurations`);
        if (configsRes.ok) {
            const configs = await configsRes.json();
            if (configs.length > 0) {
                configId.value = configs[0].id;
                
                // Fetch the Configuration details
                const detailRes = await apiFetch(`/api/configuration/${configId.value}`);
                if (detailRes.ok) {
                    const configDetail = await detailRes.json();
                    
                    // Populate prompt settings
                    promptForm.systemPrompt = configDetail.systemPrompt || '';
                    promptForm.defaultProvider = configDetail.defaultProvider || 'gemini';
                    promptForm.defaultModel = configDetail.defaultModel || 'gemini-3.1-flash-lite-preview';

                    // Populate theme settings
                    if (configDetail.themeSettingsJson) {
                        try {
                            const parsedTheme = JSON.parse(configDetail.themeSettingsJson);
                            Object.assign(theme, { ...defaultTheme, ...parsedTheme });
                        } catch {
                            // keep default
                        }
                    }
                }
            }
        }

        // Trigger load for specific sub-tabs initially
        handleTabChange(activeTab.value);
        
    } catch (e) {
        console.error(e);
    } finally {
        loading.value = false;
        notifyParentHeight();
    }
}

function handleTabChange(tab: string) {
    activeTab.value = tab;
    if (tab === 'knowledge') {
        loadDocuments();
    } else if (tab === 'rules') {
        loadRules();
    } else {
        notifyParentHeight();
    }
}

// Watch activeTab to trigger height postMessage
watch(activeTab, () => {
    notifyParentHeight();
});

// Setup resize listener for iframe auto-resizing
onMounted(() => {
    init();
    window.addEventListener('resize', notifyParentHeight);
});
</script>

<template>
    <div class="embedded-container">
        <Toast />
        <ConfirmDialog />

        <div v-if="loading" class="loading-state">
            <i class="pi pi-spin pi-spinner text-4xl text-primary mb-4"></i>
            <p>Loading configuration settings...</p>
        </div>

        <div v-else-if="allowedTabs.length === 0" class="no-permissions-state">
            <i class="pi pi-lock text-5xl text-red-500 mb-4"></i>
            <h3>Settings Locked</h3>
            <p>Your service provider has disabled manual customizations for this chatbot.</p>
        </div>

        <div v-else class="settings-layout">
            <!-- Tabs Navigation -->
            <nav class="embedded-tab-nav">
                <button 
                    v-for="tab in allowedTabs" 
                    :key="tab.id"
                    class="tab-link-btn" 
                    :class="{ active: activeTab === tab.id }"
                    @click="handleTabChange(tab.id)"
                >
                    <i :class="tab.icon"></i>
                    <span>{{ tab.label }}</span>
                </button>
            </nav>

            <!-- Tab Panels -->
            <div class="tab-panels-content">
                
                <!-- System Prompt Tab -->
                <div v-if="activeTab === 'prompt'" class="panel-card animate-fade-in">
                    <div class="panel-header">
                        <h2>System Instructions</h2>
                        <p>Give your AI persona, target goals, behavioral instructions, and rules of engagement.</p>
                    </div>

                    <div class="form-content">
                        <div class="field-item">
                            <label for="prompt-area">System Prompt Instructions</label>
                            <Textarea 
                                id="prompt-area" 
                                v-model="promptForm.systemPrompt" 
                                rows="8" 
                                placeholder="e.g. You are a professional customer support representative for Dental Clinic. Answer politely..." 
                                class="w-full" 
                            />
                        </div>

                        <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
                            <div class="field-item">
                                <label for="provider-select">AI Engine Provider</label>
                                <Select 
                                    id="provider-select" 
                                    v-model="promptForm.defaultProvider" 
                                    :options="providerOptions" 
                                    optionLabel="label" 
                                    optionValue="value" 
                                    class="w-full" 
                                />
                            </div>
                            <div class="field-item">
                                <label for="model-select">AI Engine Model</label>
                                <Select 
                                    id="model-select" 
                                    v-model="promptForm.defaultModel" 
                                    :options="modelOptions" 
                                    optionLabel="label" 
                                    optionValue="value" 
                                    class="w-full" 
                                    :disabled="!promptForm.defaultProvider" 
                                />
                            </div>
                        </div>

                        <div class="action-footer mt-4">
                            <Button 
                                label="Save Instructions" 
                                icon="pi pi-save" 
                                @click="savePromptSettings" 
                                :loading="saving" 
                            />
                        </div>
                    </div>
                </div>

                <!-- Knowledge Base Tab -->
                <div v-if="activeTab === 'knowledge'" class="panel-card animate-fade-in">
                    <div class="panel-header">
                        <h2>Knowledge Documents</h2>
                        <p>Upload files or crawling websites to populate the AI's Retrieval-Augmented Generation (RAG) database.</p>
                    </div>

                    <div class="grid grid-cols-1 lg:grid-cols-3 gap-6 mb-6">
                        <!-- File Upload -->
                        <Card class="lg:col-span-2 shadow-none border border-surface-200">
                            <template #title>
                                <div class="text-sm font-semibold text-surface-700 mb-2">Upload Files</div>
                            </template>
                            <template #content>
                                <FileUpload 
                                    name="file" 
                                    :url="uploadUrl" 
                                    @upload="onUpload" 
                                    @error="onUploadError"
                                    @before-send="onBeforeSend" 
                                    :multiple="true" 
                                    accept=".pdf,.txt,.json,.md,.csv"
                                    :maxFileSize="10000000" 
                                    :withCredentials="true"
                                    mode="advanced" 
                                />
                            </template>
                        </Card>

                        <!-- Website Crawling -->
                        <Card class="shadow-none border border-surface-200">
                            <template #title>
                                <div class="text-sm font-semibold text-surface-700 mb-2">Import from URL</div>
                            </template>
                            <template #content>
                                <div class="flex flex-col gap-3">
                                    <InputText v-model="crawlUrl" placeholder="https://example.com" class="w-full" :disabled="crawling" />
                                    <Select v-model="maxPages" :options="[5, 10, 25]" placeholder="Pages limit" class="w-full" :disabled="crawling" />
                                    <Button label="Start Import" icon="pi pi-play" @click="startCrawl" :loading="crawling" :disabled="!crawlUrl" class="w-full" />
                                </div>
                            </template>
                        </Card>
                    </div>

                    <!-- Ingested Docs Table -->
                    <div class="table-container border rounded-lg overflow-hidden">
                        <DataTable :value="documents" :loading="kbLoading" class="p-datatable-sm" responsiveLayout="scroll">
                            <Column field="fileName" header="File Name">
                                <template #body="{ data }">
                                    <div class="flex items-center gap-2">
                                        <i :class="data.contentType.includes('pdf') ? 'pi pi-file-pdf text-red-500' : 'pi pi-file text-primary'"></i>
                                        <span class="font-medium text-sm truncate max-w-[180px]">{{ data.fileName }}</span>
                                    </div>
                                </template>
                            </Column>
                            <Column field="fileSize" header="Size">
                                <template #body="{ data }">
                                    <span class="text-xs text-surface-500">{{ formatSize(data.fileSize) }}</span>
                                </template>
                            </Column>
                            <Column field="chunkCount" header="Chunks">
                                <template #body="{ data }">
                                    <Tag :value="data.chunkCount || '0'" severity="secondary" rounded />
                                </template>
                            </Column>
                            <Column field="status" header="Status">
                                <template #body="{ data }">
                                    <Tag v-if="data.status === 'Completed'" severity="success" value="Ready" />
                                    <Tag v-else-if="data.status === 'Failed'" severity="danger" value="Failed" />
                                    <Tag v-else severity="warn" value="Processing" />
                                </template>
                            </Column>
                            <Column header="Action" bodyStyle="text-align: center">
                                <template #body="{ data }">
                                    <Button icon="pi pi-trash" severity="danger" text rounded @click="deleteDoc(data.id)" />
                                </template>
                            </Column>
                        </DataTable>
                    </div>
                </div>

                <!-- Conversation Rules Tab -->
                <div v-if="activeTab === 'rules'" class="panel-card animate-fade-in">
                    <div class="panel-header flex justify-between items-center">
                        <div>
                            <h2>Instant Match Rules</h2>
                            <p>Map triggers directly to instant plain-text replies. Save model latency and cost.</p>
                        </div>
                        <Button label="Add Rule" icon="pi pi-plus" size="small" @click="openCreateRule" />
                    </div>

                    <!-- Rules List -->
                    <div class="rules-list-container mt-4">
                        <div v-if="rules.length === 0" class="empty-rules text-center py-6 text-surface-400">
                            <i class="pi pi-info-circle text-2xl mb-2"></i>
                            <p>No conversation rules defined yet.</p>
                        </div>
                        <div v-else class="rules-grid">
                            <div v-for="rule in rules" :key="rule.id" class="rule-embed-card" :class="{ disabled: !rule.isActive }">
                                <div class="rule-embed-body">
                                    <div class="rule-embed-meta">
                                        <Tag :value="rule.type" severity="secondary" size="small" class="mr-2" />
                                        <span class="text-xs text-surface-500">Priority: {{ rule.priority }}</span>
                                    </div>
                                    <div class="rule-embed-trigger font-mono my-2 text-sm">
                                        <strong>Trigger:</strong> "{{ rule.trigger }}"
                                    </div>
                                    <div class="rule-embed-response text-xs text-surface-600 truncate max-w-[400px]">
                                        <strong>Reply:</strong> {{ rule.response }}
                                    </div>
                                </div>
                                <div class="rule-embed-actions">
                                    <Button icon="pi pi-pencil" text rounded severity="secondary" @click="openEditRule(rule)" />
                                    <Button icon="pi pi-trash" text rounded severity="danger" @click="deleteRule(rule.id)" />
                                </div>
                            </div>
                        </div>
                    </div>

                    <!-- Rule Create/Edit Dialog -->
                    <Dialog v-model:visible="showRuleDialog" :header="editingRuleId ? 'Edit Rule' : 'Create Rule'" modal :style="{ width: '450px' }">
                        <div class="flex flex-col gap-4 pt-2">
                            <div class="flex flex-col gap-1">
                                <label for="rule-type">Type</label>
                                <Select id="rule-type" v-model="ruleForm.type" :options="ruleTypeOptions" optionLabel="label" optionValue="value" class="w-full" />
                            </div>
                            <div class="flex flex-col gap-1">
                                <label for="rule-trigger">Trigger Phrase / Description</label>
                                <InputText id="rule-trigger" v-model="ruleForm.trigger" placeholder="e.g. pricing plans" class="w-full" />
                            </div>
                            <div class="flex flex-col gap-1">
                                <label for="rule-response">Instant Response Reply</label>
                                <Textarea id="rule-response" v-model="ruleForm.response" rows="3" placeholder="Our premium plans start at $19/mo..." class="w-full" />
                            </div>
                            <div class="grid grid-cols-2 gap-4">
                                <div class="flex flex-col gap-1">
                                    <label for="rule-priority">Priority</label>
                                    <InputNumber id="rule-priority" v-model="ruleForm.priority" class="w-full" />
                                </div>
                                <div class="flex items-center gap-2 mt-5">
                                    <Checkbox id="rule-active" v-model="ruleForm.isActive" :binary="true" />
                                    <label for="rule-active">Is Active</label>
                                </div>
                            </div>
                        </div>
                        <template #footer>
                            <Button label="Cancel" severity="secondary" text @click="showRuleDialog = false" />
                            <Button label="Save Rule" @click="saveRule" :disabled="!ruleForm.trigger || !ruleForm.response" />
                        </template>
                    </Dialog>
                </div>

                <!-- Widget Customization Tab -->
                <div v-if="activeTab === 'widget'" class="panel-card animate-fade-in">
                    <div class="panel-header">
                        <h2>Chat Widget Styling</h2>
                        <p>Change title text, brand colors, placement, and subtitles for the client widget bubble.</p>
                    </div>

                    <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
                        <!-- Form Settings -->
                        <div class="form-content flex flex-col gap-4">
                            <div class="field-item">
                                <label for="theme-title">Widget Header Title</label>
                                <InputText id="theme-title" v-model="theme.title" class="w-full" />
                            </div>
                            <div class="field-item">
                                <label for="theme-subtitle">Widget Subtitle</label>
                                <InputText id="theme-subtitle" v-model="theme.subtitle" class="w-full" />
                            </div>
                            <div class="field-item">
                                <label for="theme-placeholder">Input Field Placeholder</label>
                                <InputText id="theme-placeholder" v-model="theme.placeholder" class="w-full" />
                            </div>
                            <div class="grid grid-cols-2 gap-4">
                                <div class="field-item">
                                    <label for="theme-font">Font Family</label>
                                    <Select id="theme-font" v-model="theme.fontFamily" :options="fontOptions" optionLabel="label" optionValue="value" class="w-full" />
                                </div>
                                <div class="field-item">
                                    <label for="theme-position">Placement</label>
                                    <Select id="theme-position" v-model="theme.position" :options="positionOptions" optionLabel="label" optionValue="value" class="w-full" />
                                </div>
                            </div>

                            <div class="color-picker-grid">
                                <div class="picker-item">
                                    <span>Brand Primary Color</span>
                                    <div class="flex items-center gap-2">
                                        <ColorPicker v-model="theme.primaryColor" />
                                        <InputText v-model="theme.primaryColor" class="w-[90px] text-xs font-mono" />
                                    </div>
                                </div>
                                <div class="picker-item">
                                    <span>Header BG Color</span>
                                    <div class="flex items-center gap-2">
                                        <ColorPicker v-model="theme.headerBgColor" />
                                        <InputText v-model="theme.headerBgColor" class="w-[90px] text-xs font-mono" />
                                    </div>
                                </div>
                                <div class="picker-item">
                                    <span>User Bubble BG</span>
                                    <div class="flex items-center gap-2">
                                        <ColorPicker v-model="theme.userBubbleBgColor" />
                                        <InputText v-model="theme.userBubbleBgColor" class="w-[90px] text-xs font-mono" />
                                    </div>
                                </div>
                                <div class="picker-item">
                                    <span>Bot Bubble BG</span>
                                    <div class="flex items-center gap-2">
                                        <ColorPicker v-model="theme.botBubbleBgColor" />
                                        <InputText v-model="theme.botBubbleBgColor" class="w-[90px] text-xs font-mono" />
                                    </div>
                                </div>
                            </div>

                            <div class="action-footer mt-2">
                                <Button label="Save Appearance" icon="pi pi-save" @click="saveThemeSettings" :loading="saving" />
                            </div>
                        </div>

                        <!-- Live Preview Mockup -->
                        <div class="mockup-side flex justify-center items-center p-4 bg-surface-50 border border-surface-200 rounded-xl">
                            <div class="chat-mockup-window shadow-lg font-sans" :style="{ fontFamily: theme.fontFamily === 'system-ui' ? 'sans-serif' : theme.fontFamily }">
                                <div class="mockup-header" :style="{ backgroundColor: theme.headerBgColor || theme.primaryColor, color: theme.headerTextColor }">
                                    <div class="flex items-center gap-3">
                                        <div class="mockup-avatar bg-surface-100 flex items-center justify-center text-primary text-sm font-bold">AI</div>
                                        <div>
                                            <div class="mockup-title font-semibold text-sm">{{ theme.title || 'AI Support' }}</div>
                                            <div class="mockup-subtitle text-[10px] opacity-80">{{ theme.subtitle || 'Always Active' }}</div>
                                        </div>
                                    </div>
                                    <i class="pi pi-times text-xs opacity-75"></i>
                                </div>
                                
                                <div class="mockup-chat-body" :style="{ backgroundColor: theme.bgColor }">
                                    <div class="mockup-msg bot">
                                        <div class="bubble" :style="{ backgroundColor: theme.botBubbleBgColor, color: theme.botBubbleTextColor }">
                                            Hi! How can I help you today?
                                        </div>
                                    </div>
                                    <div class="mockup-msg user">
                                        <div class="bubble" :style="{ backgroundColor: theme.userBubbleBgColor, color: theme.userBubbleTextColor }">
                                            I need to learn more about your services.
                                        </div>
                                    </div>
                                    <div class="mockup-msg bot">
                                        <div class="bubble" :style="{ backgroundColor: theme.botBubbleBgColor, color: theme.botBubbleTextColor }">
                                            Sure! We provide fully automated AI integration systems.
                                        </div>
                                    </div>
                                </div>

                                <div class="mockup-input-bar bg-surface-0 border-t flex items-center px-3 py-2 gap-2">
                                    <div class="mockup-input-placeholder flex-1 text-xs text-surface-400">
                                        {{ theme.placeholder || 'Type a message...' }}
                                    </div>
                                    <i class="pi pi-send text-sm" :style="{ color: theme.primaryColor }"></i>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>

            </div>
        </div>
    </div>
</template>

<style scoped>
.embedded-container {
    width: 100%;
    min-height: 200px;
    background: transparent;
    padding: 0;
    color: var(--p-surface-800);
}

.loading-state, .no-permissions-state {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    padding: 48px 16px;
    text-align: center;
}

.no-permissions-state h3 {
    font-size: 1.2rem;
    font-weight: 600;
    margin-bottom: 8px;
}

.no-permissions-state p {
    color: var(--p-surface-500);
    max-width: 300px;
}

.settings-layout {
    display: flex;
    flex-direction: column;
    gap: 20px;
}

/* Custom embedded tab navigation */
.embedded-tab-nav {
    display: flex;
    gap: 8px;
    border-bottom: 1px solid var(--p-surface-200);
    padding-bottom: 8px;
    margin-bottom: 4px;
    flex-wrap: wrap;
}

.tab-link-btn {
    display: flex;
    align-items: center;
    gap: 8px;
    padding: 8px 16px;
    border: none;
    background: transparent;
    color: var(--p-surface-500);
    cursor: pointer;
    font-size: 0.9rem;
    font-weight: 500;
    border-radius: 6px;
    transition: all 0.2s ease;
}

.tab-link-btn:hover {
    color: var(--p-surface-900);
    background: var(--p-surface-100);
}

.tab-link-btn.active {
    color: var(--p-primary-600);
    background: color-mix(in srgb, var(--p-primary-500) 8%, transparent);
    font-weight: 600;
}

.tab-panels-content {
    background: transparent;
}

.panel-card {
    display: flex;
    flex-direction: column;
    gap: 16px;
}

.panel-header h2 {
    font-size: 1.15rem;
    font-weight: 600;
    margin: 0;
}

.panel-header p {
    font-size: 0.82rem;
    color: var(--p-surface-500);
    margin: 4px 0 0 0;
}

.form-content {
    display: flex;
    flex-direction: column;
    gap: 12px;
}

.field-item {
    display: flex;
    flex-direction: column;
    gap: 4px;
}

.field-item label {
    font-size: 0.8rem;
    font-weight: 600;
    color: var(--p-surface-600);
}

.action-footer {
    display: flex;
    justify-content: flex-end;
}

/* Knowledge Base styling */
.table-container {
    border: 1px solid var(--p-surface-200);
}

/* Rules Styling */
.rules-grid {
    display: grid;
    grid-template-columns: 1fr;
    gap: 12px;
}

@media(min-width: 768px) {
    .rules-grid {
        grid-template-columns: 1fr 1fr;
    }
}

.rule-embed-card {
    background: var(--p-surface-50);
    border: 1px solid var(--p-surface-200);
    border-radius: 8px;
    padding: 12px 14px;
    display: flex;
    justify-content: space-between;
    align-items: flex-start;
    gap: 12px;
    transition: all 0.2s;
}

.rule-embed-card:hover {
    border-color: var(--p-surface-400);
}

.rule-embed-card.disabled {
    opacity: 0.6;
}

.rule-embed-body {
    flex: 1;
    min-width: 0;
}

.rule-embed-actions {
    display: flex;
    gap: 4px;
}

/* Mockup Layout */
.chat-mockup-window {
    width: 290px;
    height: 380px;
    background: var(--p-surface-0);
    border-radius: 12px;
    display: flex;
    flex-direction: column;
    overflow: hidden;
    border: 1px solid var(--p-surface-200);
}

.mockup-header {
    padding: 10px 14px;
    display: flex;
    justify-content: space-between;
    align-items: center;
}

.mockup-avatar {
    width: 28px;
    height: 28px;
    border-radius: 50%;
    color: var(--p-primary-600);
}

.mockup-chat-body {
    flex: 1;
    padding: 12px;
    display: flex;
    flex-direction: column;
    gap: 10px;
    overflow-y: auto;
}

.mockup-msg {
    display: flex;
}

.mockup-msg.bot {
    justify-content: flex-start;
}

.mockup-msg.user {
    justify-content: flex-end;
}

.mockup-msg .bubble {
    max-width: 80%;
    padding: 8px 10px;
    font-size: 0.75rem;
    border-radius: 8px;
    line-height: 1.4;
    border: 1px solid var(--p-surface-200);
}

.mockup-msg.bot .bubble {
    border-top-left-radius: 2px;
}

.mockup-msg.user .bubble {
    border-top-right-radius: 2px;
}

.color-picker-grid {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 12px;
    background: var(--p-surface-50);
    padding: 12px;
    border-radius: 8px;
    border: 1px solid var(--p-surface-200);
}

.picker-item {
    display: flex;
    flex-direction: column;
    gap: 4px;
}

.picker-item span {
    font-size: 0.75rem;
    font-weight: 500;
    color: var(--p-surface-600);
}

/* Animations */
.animate-fade-in {
    animation: fadeIn 0.25s ease-out;
}

@keyframes fadeIn {
    from {
        opacity: 0;
        transform: translateY(4px);
    }
    to {
        opacity: 1;
        transform: translateY(0);
    }
}
</style>

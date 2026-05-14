<script setup lang="ts">
import { ref, reactive, onMounted, computed } from 'vue';
import { useRoute } from 'vue-router';
import { useApi } from '../composables/useApi';
import Button from 'primevue/button';
import Card from 'primevue/card';
import InputText from 'primevue/inputtext';
import Select from 'primevue/select';
import Textarea from 'primevue/textarea';
import Checkbox from 'primevue/checkbox';
import Password from 'primevue/password';
import InputNumber from 'primevue/inputnumber';

const route = useRoute();
const { apiFetch } = useApi();

const projectId = computed(() => route.params.projectId as string);
const configId = computed(() => route.params.configId as string);

interface ModelEntry {
    model: string;
    provider: string;
}

const config = reactive({ 
    name: '', 
    systemPrompt: '', 
    defaultProvider: 'gemini', 
    defaultModel: 'gemini-3.1-flash-lite-preview', 
    hasGeminiKey: false,
    hasGroqKey: false,
    hasOpenAiKey: false,
    hasFirecrawlKey: false,
    liveVoiceEnabled: false, 
    enabledModels: '',
    rateLimitRequests: 0,
    rateLimitWindowMinutes: 60,
    maxSpendLimit: 0,
    currentSpend: 0,
    logRetentionDays: 30,
    maxLogsPerSession: 500,
    maxSessionsPerProject: 50,
    suggestionsJson: '',
    suggestions: [] as string[]
});

// Separate reactive objects for key inputs — only sent on save, never pre-populated
const keyInputs = reactive({
    geminiApiKey: '',
    groqApiKey: '',
    openAiApiKey: '',
    firecrawlApiKey: ''
});

// Default model options derived from enabled models list
const defaultModelOptions = computed(() => {
    return enabledModels.value.map(e => ({
        label: `${e.model} (${e.provider})`,
        value: e.model
    }));
});

const providersList = [
    { key: 'geminiApiKey', hasKey: 'hasGeminiKey', label: 'Google Gemini', id: 'gemini' },
    { key: 'groqApiKey', hasKey: 'hasGroqKey', label: 'Groq', id: 'groq' },
    { key: 'openAiApiKey', hasKey: 'hasOpenAiKey', label: 'OpenAI', id: 'openai' },
    { key: 'firecrawlApiKey', hasKey: 'hasFirecrawlKey', label: 'Firecrawl (Crawling)', id: 'firecrawl' }
];

const fetchingModels = ref<string | null>(null);
const providerModels = reactive<Record<string, any[]>>({});
const enabledModels = ref<ModelEntry[]>([]);
const saving = ref(false);
const saved = ref(false);

// Check if a model from a given provider is currently enabled
function isModelEnabled(modelId: string, providerId: string): boolean {
    return enabledModels.value.some(e => e.model === modelId && e.provider === providerId);
}

// Toggle a model on/off for a given provider
function toggleModel(modelId: string, providerId: string) {
    const idx = enabledModels.value.findIndex(e => e.model === modelId && e.provider === providerId);
    if (idx >= 0) {
        enabledModels.value.splice(idx, 1);
        // If the removed model was the default, clear it
        if (config.defaultModel === modelId) {
            config.defaultModel = enabledModels.value.length > 0 ? enabledModels.value[0].model : '';
            config.defaultProvider = enabledModels.value.length > 0 ? enabledModels.value[0].provider : 'gemini';
        }
    } else {
        enabledModels.value.push({ model: modelId, provider: providerId });
        // Auto-select first model as default if none set
        if (!config.defaultModel) {
            config.defaultModel = modelId;
            config.defaultProvider = providerId;
        }
    }
}

// When default model changes, auto-set the provider
function onDefaultModelChange(modelId: string) {
    config.defaultModel = modelId;
    const entry = enabledModels.value.find(e => e.model === modelId);
    if (entry) {
        config.defaultProvider = entry.provider;
    }
}

async function load() {
    const res = await apiFetch(`/api/configuration/${configId.value}`);
    if (res.ok) {
        const data = await res.json();
        Object.assign(config, data);
        
        // Handle enabled models
        if (data.enabledModels) {
            try {
                const parsed = JSON.parse(data.enabledModels);
                if (Array.isArray(parsed) && parsed.length > 0) {
                    if (typeof parsed[0] === 'object' && parsed[0].model) {
                        enabledModels.value = parsed;
                    } else {
                        enabledModels.value = parsed.map((m: string) => ({ 
                            model: m, 
                            provider: config.defaultProvider || 'gemini' 
                        }));
                    }
                }
            } catch {}
        }

        // Handle suggestions
        if (data.suggestionsJson) {
            try {
                config.suggestions = JSON.parse(data.suggestionsJson);
                if (!Array.isArray(config.suggestions)) config.suggestions = [];
            } catch {
                config.suggestions = [];
            }
        } else {
            config.suggestions = [];
        }
    }
}

async function fetchModels(providerId: string) {
    fetchingModels.value = providerId;
    try {
        const res = await apiFetch(`/api/configuration/${configId.value}/models/${providerId}`);
        if (res.ok) {
            providerModels[providerId] = await res.json();
        }
    } catch(e) {
        console.error(e);
    }
    fetchingModels.value = null;
}

async function save() {
    saving.value = true;
    saved.value = false;

    const body: Record<string, any> = {
        name: config.name, 
        systemPrompt: config.systemPrompt,
        defaultProvider: config.defaultProvider, 
        defaultModel: config.defaultModel,
        liveVoiceEnabled: config.liveVoiceEnabled,
        enabledModels: JSON.stringify(enabledModels.value),
        rateLimitRequests: config.rateLimitRequests,
        rateLimitWindowMinutes: config.rateLimitWindowMinutes,
        maxSpendLimit: config.maxSpendLimit,
        logRetentionDays: config.logRetentionDays,
        maxLogsPerSession: config.maxLogsPerSession,
        maxSessionsPerProject: config.maxSessionsPerProject,
        suggestionsJson: JSON.stringify(config.suggestions.filter(s => s.trim() !== ''))
    };

    if (keyInputs.geminiApiKey) body.geminiApiKey = keyInputs.geminiApiKey;
    if (keyInputs.groqApiKey) body.groqApiKey = keyInputs.groqApiKey;
    if (keyInputs.openAiApiKey) body.openAiApiKey = keyInputs.openAiApiKey;
    if (keyInputs.firecrawlApiKey) body.firecrawlApiKey = keyInputs.firecrawlApiKey;

    await apiFetch(`/api/configuration/${configId.value}`, {
        method: 'PUT',
        body: JSON.stringify(body)
    });
    saving.value = false;
    saved.value = true;

    await load();

    keyInputs.geminiApiKey = '';
    keyInputs.groqApiKey = '';
    keyInputs.openAiApiKey = '';
    keyInputs.firecrawlApiKey = '';

    setTimeout(() => saved.value = false, 3000);
}

async function clearKey(providerId: string) {
    const keyField = providerId === 'gemini' ? 'geminiApiKey' : providerId === 'groq' ? 'groqApiKey' : providerId === 'openai' ? 'openAiApiKey' : 'firecrawlApiKey';
    await apiFetch(`/api/configuration/${configId.value}`, {
        method: 'PUT',
        body: JSON.stringify({ [keyField]: '' })
    });
    await load();
}

function addSuggestion() {
    if (config.suggestions.length < 4) {
        config.suggestions.push('');
    }
}

function removeSuggestion(index: number) {
    config.suggestions.splice(index, 1);
}

onMounted(load);
</script>

<template>
    <div>
        <header class="header">
            <div>
                <router-link :to="'/project/' + projectId" class="back-link">
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="19" y1="12" x2="5" y2="12"></line><polyline points="12 19 5 12 12 5"></polyline></svg>
                    Back to Project
                </router-link>
                <h1>{{ config.name || 'Configuration' }}</h1>
            </div>
        </header>

        <section class="section">
            <Card class="config-card">
                <template #content>
                    <div class="form-group">
                        <label>System Prompt</label>
                        <Textarea v-model="config.systemPrompt" rows="5" fluid />
                    </div>
                    
                    <div class="form-group">
                        <label>Default Model</label>
                        <Select 
                            :modelValue="config.defaultModel" 
                            @update:modelValue="onDefaultModelChange" 
                            :options="defaultModelOptions" 
                            optionLabel="label" 
                            optionValue="value" 
                            placeholder="Enable models below, then select a default" 
                            fluid 
                        />
                        <span v-if="enabledModels.length === 0" class="info-text">Add provider API keys and enable models below first</span>
                    </div>
                    
                    <div v-if="config.hasGeminiKey" class="form-group checkbox-group">
                        <Checkbox v-model="config.liveVoiceEnabled" :binary="true" :disabled="!config.hasGeminiKey" inputId="liveVoice" />
                        <label for="liveVoice">Live Voice Mode</label>
                        <span class="info-text">(requires Gemini API key)</span>
                    </div>
                </template>
            </Card>

            <h2 class="section-title">Administrative Controls</h2>
            <Card class="admin-card">
                <template #content>
                    <div class="grid-2">
                        <div class="form-group">
                            <label>Rate Limit (Requests)</label>
                            <InputNumber v-model="config.rateLimitRequests" placeholder="0 = No limit" fluid />
                            <small class="info-text">Maximum requests allowed within the window.</small>
                        </div>
                        <div class="form-group">
                            <label>Window (Minutes)</label>
                            <InputNumber v-model="config.rateLimitWindowMinutes" fluid />
                            <small class="info-text">Time window for rate limiting.</small>
                        </div>
                    </div>

                    <div class="grid-3 mt-4">
                        <div class="form-group">
                            <label>Log Retention (Days)</label>
                            <InputNumber v-model="config.logRetentionDays" fluid />
                            <small class="info-text">Days to keep unpinned logs.</small>
                        </div>
                        <div class="form-group">
                            <label>Max Logs Per Session</label>
                            <InputNumber v-model="config.maxLogsPerSession" placeholder="0 = No limit" fluid />
                            <small class="info-text">Prune logs exceeding limit.</small>
                        </div>
                        <div class="form-group">
                            <label>Max Sessions Per Project</label>
                            <InputNumber v-model="config.maxSessionsPerProject" placeholder="0 = No limit" fluid />
                            <small class="info-text">Prune oldest inactive sessions.</small>
                        </div>
                    </div>
                    
                    <div class="grid-2 mt-4">
                        <div class="form-group">
                            <label>Spending Cap (USD)</label>
                            <InputNumber v-model="config.maxSpendLimit" mode="currency" currency="USD" locale="en-US" placeholder="0 = No limit" fluid />
                            <small class="info-text">Total budget for this configuration.</small>
                        </div>
                        <div class="form-group">
                            <label>Current Spend (Read-only)</label>
                            <div class="spend-display">
                                <span class="spend-value">${{ config.currentSpend.toFixed(6) }}</span>
                                <span class="spend-progress" :style="{ width: config.maxSpendLimit > 0 ? (Math.min(config.currentSpend / config.maxSpendLimit, 1) * 100) + '%' : '0%' }"></span>
                            </div>
                        </div>
                    </div>

                    <div class="form-group mt-4">
                        <div class="flex-between">
                            <label>Chat Suggestions</label>
                            <Button 
                                icon="pi pi-plus" 
                                label="Add" 
                                severity="secondary" 
                                size="small" 
                                text 
                                @click="addSuggestion" 
                                :disabled="config.suggestions.length >= 4" 
                            />
                        </div>
                        <div class="suggestions-list">
                            <div v-for="(suggestion, index) in config.suggestions" :key="index" class="suggestion-item">
                                <InputText v-model="config.suggestions[index]" placeholder="Enter a suggested prompt..." fluid />
                                <Button icon="pi pi-times" severity="danger" text rounded @click="removeSuggestion(index)" />
                            </div>
                            <div v-if="config.suggestions.length === 0" class="empty-suggestions">
                                No suggestions added yet. These will appear as quick-start buttons in the chat.
                            </div>
                        </div>
                        <small class="info-text">Maximum of 4 suggested prompts shown to the user on start.</small>
                    </div>
                </template>
            </Card>

            <h2 class="section-title">Provider API Keys</h2>
            
            <Card v-for="provider in providersList" :key="provider.key" class="provider-card">
                <template #title>
                    <div class="provider-header">
                        <h3>{{ provider.label }}</h3>
                        <span v-if="config[provider.hasKey as keyof typeof config]" class="badge badge-success">Configured</span>
                    </div>
                </template>
                <template #content>
                    <div class="api-key-input">
                        <Password 
                            v-model="keyInputs[provider.key as keyof typeof keyInputs]" 
                            :feedback="false" 
                            toggleMask 
                            :placeholder="config[provider.hasKey as keyof typeof config] ? '••••••••••••••••••••' : provider.label + ' API key'" 
                            fluid 
                            class="flex-1" 
                        />
                        <Button 
                            v-if="provider.id !== 'firecrawl'"
                            :label="fetchingModels === provider.id ? 'Loading...' : 'Fetch Models'" 
                            severity="secondary" 
                            outlined 
                            :disabled="!config[provider.hasKey as keyof typeof config] || fetchingModels === provider.id" 
                            @click="fetchModels(provider.id)" 
                        />
                        <Button 
                            v-if="config[provider.hasKey as keyof typeof config]"
                            icon="pi pi-trash" 
                            severity="danger" 
                            text 
                            rounded 
                            @click="clearKey(provider.id)" 
                            v-tooltip="'Remove key'"
                        />
                    </div>

                    <div v-if="providerModels[provider.id] && providerModels[provider.id].length" class="models-list">
                        <p class="models-title">Available models (check to enable):</p>
                        <div v-for="m in providerModels[provider.id]" :key="provider.id + '-' + m.id" class="model-item">
                            <Checkbox 
                                :modelValue="isModelEnabled(m.id, provider.id)" 
                                @update:modelValue="toggleModel(m.id, provider.id)" 
                                :binary="true"
                                :inputId="provider.id + '-' + m.id" 
                            />
                            <label :for="provider.id + '-' + m.id" class="model-name">{{ m.name }}</label>
                            <span class="model-desc">{{ m.description }}</span>
                        </div>
                    </div>
                </template>
            </Card>

            <div class="actions">
                <Button :label="saving ? 'Saving...' : 'Save Configuration'" @click="save" :disabled="saving" />
                <span v-if="saved" class="saved-text">Saved!</span>
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
.section {
    margin-bottom: 48px;
}
.config-card {
    background-color: var(--p-surface-0);
    border: 1px solid var(--p-surface-200);
    margin-bottom: 32px;
}
.provider-card, .admin-card {
    background-color: var(--p-surface-0);
    border: 1px solid var(--p-surface-200);
    margin-bottom: 16px;
}
.mt-4 { margin-top: 1.5rem; }
.spend-display {
    background: var(--p-surface-100);
    border-radius: 6px;
    padding: 8px 12px;
    position: relative;
    overflow: hidden;
    height: 38px;
    display: flex;
    align-items: center;
}
.spend-value {
    position: relative;
    z-index: 2;
    font-family: monospace;
    font-weight: 600;
}
.spend-progress {
    position: absolute;
    left: 0;
    top: 0;
    bottom: 0;
    background: var(--p-primary-100);
    transition: width 0.3s ease;
}
.section-title {
    margin-bottom: 16px;
}
.provider-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
}
.provider-header h3 {
    font-size: 1rem;
    margin: 0;
    color: var(--p-surface-900);
}
.badge {
    font-size: 0.75rem;
    padding: 2px 8px;
    border-radius: 12px;
    background: var(--p-surface-100);
}
.badge-success {
    color: var(--p-green-600);
}
.form-group {
    display: flex;
    flex-direction: column;
    gap: 8px;
    margin-bottom: 16px;
}
.form-group label {
    font-weight: 500;
    font-size: 0.9rem;
    color: var(--p-surface-700);
}
.grid-2 {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 24px;
}
.grid-3 {
    display: grid;
    grid-template-columns: 1fr 1fr 1fr;
    gap: 24px;
}
@media (max-width: 768px) {
    .grid-2, .grid-3 {
        grid-template-columns: 1fr;
    }
    .api-key-input {
        flex-direction: column;
    }
}
.checkbox-group {
    flex-direction: row;
    align-items: center;
    gap: 12px;
    margin-bottom: 0;
    margin-top: 24px;
}
.checkbox-group label {
    margin: 0;
}
.info-text {
    color: var(--p-surface-500);
    font-size: 0.8rem;
}
.api-key-input {
    display: flex;
    gap: 16px;
    margin-top: 16px;
    align-items: center;
}
.flex-1 {
    flex: 1;
}
:deep(.p-password-input) {
    width: 100%;
}
.models-list {
    margin-top: 24px;
    padding-top: 16px;
    border-top: 1px solid var(--p-surface-200);
}
.models-title {
    font-size: 0.85rem;
    color: var(--p-surface-500);
    margin-bottom: 12px;
}
.model-item {
    display: flex;
    align-items: center;
    gap: 12px;
    padding: 4px 0;
}
.model-name {
    font-size: 0.85rem;
    color: var(--p-surface-900);
    margin: 0;
}
.model-desc {
    font-size: 0.75rem;
    color: var(--p-surface-500);
}
.actions {
    display: flex;
    align-items: center;
    gap: 16px;
    margin-top: 32px;
}
.saved-text {
    color: var(--p-green-600);
    font-weight: 500;
}

/* ── Mobile Responsive ── */
@media (max-width: 768px) {
    .grid-2 {
        grid-template-columns: 1fr;
        gap: 16px;
    }
    .api-key-input {
        flex-direction: column;
        align-items: stretch;
    }
    .provider-header {
        flex-direction: column;
        align-items: flex-start;
        gap: 8px;
    }
    .provider-header .p-button {
        align-self: flex-start;
    }
    .actions {
        flex-direction: column;
        align-items: stretch;
    }
    .model-item {
        flex-wrap: wrap;
    }
    .model-desc {
        flex-basis: 100%;
        margin-left: 36px; /* Align with label text */
    }
}
.flex-between {
    display: flex;
    justify-content: space-between;
    align-items: center;
}
.suggestions-list {
    display: flex;
    flex-direction: column;
    gap: 8px;
    margin-top: 8px;
}
.suggestion-item {
    display: flex;
    gap: 8px;
    align-items: center;
}
.empty-suggestions {
    padding: 12px;
    background: var(--p-surface-50);
    border: 1px dashed var(--p-surface-200);
    border-radius: 6px;
    color: var(--p-surface-500);
    font-size: 0.85rem;
    text-align: center;
}
</style>

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
    hasAnthropicKey: false,
    configuredProviders: '' as string,
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
    suggestions: [] as string[],
    customProviderName: '',
    customProviderBaseUrl: '',
    hasCustomProviderKey: false,
    handoffEnabled: false,
    handoffTriggerKeywords: '',
    handoffQueueMessage: '',
    themeSettingsJson: '',
    channelSettingsJson: ''
});

const channels = reactive({
    whatsApp: {
        phoneNumberId: '',
        accessToken: '',
        verifyToken: ''
    },
    slack: {
        botToken: '',
        signingSecret: ''
    },
    telegram: {
        botToken: ''
    },
    teams: {
        appId: '',
        appPassword: ''
    }
});

// ─── Theme Engine ───
const defaultTheme = {
    primaryColor: '#39a7b9',
    bgColor: '#ffffff',
    fontFamily: 'Outfit',
    position: 'bottom-right'
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

// Separate reactive objects for key inputs — only sent on save, never pre-populated
const keyInputs = reactive({
    geminiApiKey: '',
    groqApiKey: '',
    openAiApiKey: '',
    firecrawlApiKey: '',
    anthropicApiKey: '',
    customProviderApiKey: ''
});

// Dynamic provider key inputs for OpenAI-compatible providers
const providerKeyInputs = reactive<Record<string, string>>({});

// Parsed map of which extra providers have keys configured
const configuredProvidersMap = computed(() => {
    if (!config.configuredProviders) return {} as Record<string, boolean>;
    try { return JSON.parse(config.configuredProviders) as Record<string, boolean>; }
    catch { return {} as Record<string, boolean>; }
});

// Default model options derived from enabled models list
const defaultModelOptions = computed(() => {
    return enabledModels.value.map(e => ({
        label: `${e.model} (${e.provider})`,
        value: e.model
    }));
});

// Core providers with dedicated API key fields
const coreProviders = [
    { key: 'geminiApiKey', hasKey: 'hasGeminiKey', label: 'Google Gemini', id: 'gemini' },
    { key: 'groqApiKey', hasKey: 'hasGroqKey', label: 'Groq', id: 'groq' },
    { key: 'openAiApiKey', hasKey: 'hasOpenAiKey', label: 'OpenAI', id: 'openai' },
    { key: 'anthropicApiKey', hasKey: 'hasAnthropicKey', label: 'Anthropic Claude', id: 'anthropic' },
    { key: 'firecrawlApiKey', hasKey: 'hasFirecrawlKey', label: 'Firecrawl (Crawling)', id: 'firecrawl' }
];

// OpenAI-compatible providers loaded from backend registry
interface RegistryProvider { id: string; name: string; defaultModel: string; isOpenAiCompatible: boolean; }
const registryProviders = ref<RegistryProvider[]>([]);

// Filter to only show OpenAI-compatible providers not already in coreProviders
const extraProviders = computed(() => {
    const coreIds = new Set(coreProviders.map(p => p.id));
    return registryProviders.value.filter(p => p.isOpenAiCompatible && !coreIds.has(p.id));
});

const fetchingModels = ref<string | null>(null);
const providerModels = reactive<Record<string, any[]>>({});
const enabledModels = ref<ModelEntry[]>([]);
const saving = ref(false);
const saved = ref(false);

// ─── Prompt Versioning ───
interface HistoryEntry {
    id: string;
    systemPrompt: string;
    defaultModel: string;
    defaultProvider: string;
    changeNote: string | null;
    createdAt: string;
}
const historyEntries = ref<HistoryEntry[]>([]);
const showHistory = ref(false);
const loadingHistory = ref(false);
const restoringId = ref<string | null>(null);
const changeNote = ref('');
const promptDirty = ref(false);
const originalPrompt = ref('');

// ─── Template Variables ───
const templateVars = ref<{ key: string; value: string }[]>([]);
const builtInVars = ['date', 'time'];
const suggestedVars = ['company', 'user_name', 'product', 'support_email', 'website'];

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

async function loadProviders() {
    try {
        const res = await apiFetch('/api/providers');
        if (res.ok) registryProviders.value = await res.json();
    } catch(e) { console.error(e); }
}

async function load() {
    const res = await apiFetch(`/api/configuration/${configId.value}`);
    if (res.ok) {
        const data = await res.json();
        Object.assign(config, data);
        originalPrompt.value = data.systemPrompt || '';
        promptDirty.value = false;

        // Parse template variables
        parseTemplateVars(data.promptTemplateVariablesJson);
        
        // Parse theme settings
        if (data.themeSettingsJson) {
            try {
                const parsedTheme = JSON.parse(data.themeSettingsJson);
                Object.assign(theme, parsedTheme);
            } catch {
                Object.assign(theme, defaultTheme);
            }
        } else {
            Object.assign(theme, defaultTheme);
        }

        // Parse channel settings
        if (data.channelSettingsJson) {
            try {
                const parsedChannels = JSON.parse(data.channelSettingsJson);
                if (parsedChannels.whatsApp) Object.assign(channels.whatsApp, parsedChannels.whatsApp);
                if (parsedChannels.slack) Object.assign(channels.slack, parsedChannels.slack);
                if (parsedChannels.telegram) Object.assign(channels.telegram, parsedChannels.telegram);
                if (parsedChannels.teams) Object.assign(channels.teams, parsedChannels.teams);
            } catch (e) {
                console.error('Failed to parse channel settings JSON', e);
            }
        }
        
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
        suggestionsJson: JSON.stringify(config.suggestions.filter(s => s.trim() !== '')),
        promptTemplateVariablesJson: serializeTemplateVars(),
        changeNote: changeNote.value || null,
        handoffEnabled: config.handoffEnabled,
        handoffTriggerKeywords: config.handoffTriggerKeywords,
        handoffQueueMessage: config.handoffQueueMessage,
        themeSettingsJson: JSON.stringify(theme),
        channelSettingsJson: JSON.stringify(channels)
    };

    if (keyInputs.geminiApiKey) body.geminiApiKey = keyInputs.geminiApiKey;
    if (keyInputs.groqApiKey) body.groqApiKey = keyInputs.groqApiKey;
    if (keyInputs.openAiApiKey) body.openAiApiKey = keyInputs.openAiApiKey;
    if (keyInputs.firecrawlApiKey) body.firecrawlApiKey = keyInputs.firecrawlApiKey;
    if (keyInputs.anthropicApiKey) body.anthropicApiKey = keyInputs.anthropicApiKey;

    // Collect OpenAI-compatible provider keys
    const providerKeys: Record<string, string> = {};
    let hasProviderKeys = false;
    for (const [pid, val] of Object.entries(providerKeyInputs)) {
        if (val) { providerKeys[pid] = val; hasProviderKeys = true; }
    }
    if (hasProviderKeys) body.providerKeys = JSON.stringify(providerKeys);

    // Custom provider
    if (config.customProviderName) body.customProviderName = config.customProviderName;
    if (config.customProviderBaseUrl) body.customProviderBaseUrl = config.customProviderBaseUrl;
    if (keyInputs.customProviderApiKey) body.customProviderApiKey = keyInputs.customProviderApiKey;

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
    keyInputs.anthropicApiKey = '';
    keyInputs.customProviderApiKey = '';
    for (const k of Object.keys(providerKeyInputs)) providerKeyInputs[k] = '';
    changeNote.value = '';
    promptDirty.value = false;

    // Refresh history if the panel is open
    if (showHistory.value) await loadHistory();

    setTimeout(() => saved.value = false, 3000);
}

async function clearKey(providerId: string) {
    const coreMap: Record<string, string> = {
        gemini: 'geminiApiKey', groq: 'groqApiKey', openai: 'openAiApiKey',
        firecrawl: 'firecrawlApiKey', anthropic: 'anthropicApiKey'
    };
    const keyField = coreMap[providerId];
    if (keyField) {
        await apiFetch(`/api/configuration/${configId.value}`, {
            method: 'PUT', body: JSON.stringify({ [keyField]: '' })
        });
    } else {
        // Clear from providerKeysJson
        await apiFetch(`/api/configuration/${configId.value}`, {
            method: 'PUT', body: JSON.stringify({ providerKeys: JSON.stringify({ [providerId]: '' }) })
        });
    }
    await load();
}

function isExtraProviderConfigured(pid: string): boolean {
    return !!configuredProvidersMap.value[pid];
}

function addSuggestion() {
    if (config.suggestions.length < 4) {
        config.suggestions.push('');
    }
}

function removeSuggestion(index: number) {
    config.suggestions.splice(index, 1);
}

// ─── Template Variable Helpers ───
function insertVariable(varName: string) {
    const placeholder = `{{${varName}}}`;
    config.systemPrompt += placeholder;
    promptDirty.value = true;
}

function addTemplateVar() {
    templateVars.value.push({ key: '', value: '' });
}

function removeTemplateVar(index: number) {
    templateVars.value.splice(index, 1);
}

function parseTemplateVars(json: string | null) {
    if (!json) { templateVars.value = []; return; }
    try {
        const obj = JSON.parse(json) as Record<string, string>;
        templateVars.value = Object.entries(obj).map(([key, value]) => ({ key, value }));
    } catch { templateVars.value = []; }
}

function serializeTemplateVars(): string {
    const obj: Record<string, string> = {};
    for (const v of templateVars.value) {
        if (v.key.trim()) obj[v.key.trim()] = v.value;
    }
    return JSON.stringify(obj);
}

// ─── Prompt History ───
async function loadHistory() {
    loadingHistory.value = true;
    try {
        const res = await apiFetch(`/api/configuration/${configId.value}/history`);
        if (res.ok) historyEntries.value = await res.json();
    } catch(e) { console.error(e); }
    loadingHistory.value = false;
}

async function restoreVersion(historyId: string) {
    restoringId.value = historyId;
    try {
        const res = await apiFetch(`/api/configuration/${configId.value}/history/${historyId}/restore`, { method: 'POST' });
        if (res.ok) {
            await load();
            await loadHistory();
        }
    } catch(e) { console.error(e); }
    restoringId.value = null;
}

function onPromptInput() {
    promptDirty.value = config.systemPrompt !== originalPrompt.value;
}

function truncate(text: string, len: number): string {
    return text.length > len ? text.substring(0, len) + '...' : text;
}

function formatDate(iso: string): string {
    return new Date(iso).toLocaleString();
}

onMounted(() => { loadProviders(); load(); });
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
                        <Textarea v-model="config.systemPrompt" rows="5" fluid @input="onPromptInput" />
                        
                        <!-- Template Variable Chips -->
                        <div class="variable-chips">
                            <span class="chips-label">Insert variable:</span>
                            <button 
                                v-for="v in suggestedVars" 
                                :key="v" 
                                class="var-chip" 
                                @click="insertVariable(v)"
                                type="button"
                                v-text="'{{' + v + '}}'"
                            ></button>
                            <button 
                                v-for="v in builtInVars" 
                                :key="v" 
                                class="var-chip var-chip-builtin" 
                                @click="insertVariable(v)"
                                type="button"
                            >
                                <span v-text="'{{' + v + '}}'"></span>
                                <span class="var-chip-auto">auto</span>
                            </button>
                        </div>

                        <!-- Change Note (shown when prompt is dirty) -->
                        <div v-if="promptDirty" class="change-note-row">
                            <InputText v-model="changeNote" placeholder="Optional: describe this change..." fluid />
                        </div>
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
                            <div v-for="(_, index) in config.suggestions" :key="index" class="suggestion-item">
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
            
            <Card v-for="provider in coreProviders" :key="provider.key" class="provider-card">
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
                            v-if="provider.id !== 'firecrawl' && provider.id !== 'anthropic'"
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

            <!-- OpenAI-Compatible Providers (Together, Fireworks, Mistral, etc.) -->
            <h2 v-if="extraProviders.length" class="section-title mt-4">Additional Providers</h2>
            <p v-if="extraProviders.length" class="section-subtitle">OpenAI-compatible providers — many offer free tiers. Add an API key to get started.</p>

            <Card v-for="ep in extraProviders" :key="ep.id" class="provider-card">
                <template #title>
                    <div class="provider-header">
                        <h3>{{ ep.name }}</h3>
                        <span v-if="isExtraProviderConfigured(ep.id)" class="badge badge-success">Configured</span>
                    </div>
                </template>
                <template #content>
                    <div class="api-key-input">
                        <Password 
                            v-model="providerKeyInputs[ep.id]" 
                            :feedback="false" 
                            toggleMask 
                            :placeholder="isExtraProviderConfigured(ep.id) ? '••••••••••••••••••••' : ep.name + ' API key'" 
                            fluid 
                            class="flex-1" 
                        />
                        <Button 
                            :label="fetchingModels === ep.id ? 'Loading...' : 'Fetch Models'" 
                            severity="secondary" 
                            outlined 
                            :disabled="!isExtraProviderConfigured(ep.id) || fetchingModels === ep.id" 
                            @click="fetchModels(ep.id)" 
                        />
                        <Button 
                            v-if="isExtraProviderConfigured(ep.id)"
                            icon="pi pi-trash" 
                            severity="danger" 
                            text 
                            rounded 
                            @click="clearKey(ep.id)" 
                            v-tooltip="'Remove key'"
                        />
                    </div>
                    <small class="info-text">Default model: {{ ep.defaultModel }}</small>

                    <div v-if="providerModels[ep.id] && providerModels[ep.id].length" class="models-list">
                        <p class="models-title">Available models (check to enable):</p>
                        <div v-for="m in providerModels[ep.id]" :key="ep.id + '-' + m.id" class="model-item">
                            <Checkbox 
                                :modelValue="isModelEnabled(m.id, ep.id)" 
                                @update:modelValue="toggleModel(m.id, ep.id)" 
                                :binary="true"
                                :inputId="ep.id + '-' + m.id" 
                            />
                            <label :for="ep.id + '-' + m.id" class="model-name">{{ m.name }}</label>
                            <span class="model-desc">{{ m.description }}</span>
                        </div>
                    </div>
                </template>
            </Card>

            <!-- Custom Provider -->
            <h2 class="section-title mt-4">Custom Provider</h2>
            <p class="section-subtitle">Connect any OpenAI-compatible API endpoint.</p>
            <Card class="provider-card">
                <template #title>
                    <div class="provider-header">
                        <h3>Custom OpenAI-Compatible</h3>
                        <span v-if="config.hasCustomProviderKey" class="badge badge-success">Configured</span>
                    </div>
                </template>
                <template #content>
                    <div class="grid-2 mt-2">
                        <div class="form-group">
                            <label>Provider Name</label>
                            <InputText v-model="config.customProviderName" placeholder="e.g. my-local-llm" fluid />
                        </div>
                        <div class="form-group">
                            <label>Base URL</label>
                            <InputText v-model="config.customProviderBaseUrl" placeholder="https://my-api.com/v1" fluid />
                        </div>
                    </div>
                    <div class="api-key-input">
                        <Password 
                            v-model="keyInputs.customProviderApiKey" 
                            :feedback="false" 
                            toggleMask 
                            :placeholder="config.hasCustomProviderKey ? '••••••••••••••••••••' : 'API key for custom provider'" 
                            fluid 
                            class="flex-1" 
                        />
                    </div>
                    <small class="info-text">Must support the OpenAI chat completions API format (POST /chat/completions).</small>
                </template>
            </Card>

            <!-- Multi-Channel Integrations -->
            <h2 class="section-title mt-4">Multi-Channel Integrations</h2>
            <p class="section-subtitle">Expose your AI assistant and Human agents directly inside messaging applications.</p>
            <Card class="provider-card">
                <template #title>
                    <div class="provider-header flex items-center gap-2">
                        <i class="pi pi-share-alt text-primary"></i>
                        <h3>WhatsApp, Slack & Telegram Settings</h3>
                    </div>
                </template>
                <template #content>
                    <div class="flex flex-col gap-6 mt-2">
                        
                        <!-- WhatsApp Integration -->
                        <div class="p-4 border rounded-lg bg-surface-50 flex flex-col gap-3">
                            <h4 class="font-semibold text-sm text-surface-700 flex items-center gap-2">
                                <i class="pi pi-whatsapp text-emerald-500"></i>
                                <span>WhatsApp (Meta Graph API)</span>
                            </h4>
                            <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
                                <div class="form-group flex flex-col gap-1">
                                    <label class="text-xs font-semibold text-surface-500 uppercase tracking-wider">Phone Number ID</label>
                                    <InputText v-model="channels.whatsApp.phoneNumberId" placeholder="e.g. 1092837498172" fluid />
                                </div>
                                <div class="form-group flex flex-col gap-1">
                                    <label class="text-xs font-semibold text-surface-500 uppercase tracking-wider">Verify Token</label>
                                    <InputText v-model="channels.whatsApp.verifyToken" placeholder="e.g. my_secure_verification_token" fluid />
                                </div>
                            </div>
                            <div class="form-group flex flex-col gap-1">
                                <label class="text-xs font-semibold text-surface-500 uppercase tracking-wider">Access Token</label>
                                <Password v-model="channels.whatsApp.accessToken" :feedback="false" toggleMask placeholder="Meta Graph API Access Token" fluid />
                            </div>
                            <div class="text-xs text-surface-500">
                                <strong>Webhook URL:</strong> <code>/api/channel/whatsapp/{{projectId}}</code>
                            </div>
                        </div>

                        <!-- Slack Integration -->
                        <div class="p-4 border rounded-lg bg-surface-50 flex flex-col gap-3">
                            <h4 class="font-semibold text-sm text-surface-700 flex items-center gap-2">
                                <i class="pi pi-slack text-purple-500"></i>
                                <span>Slack App Integration</span>
                            </h4>
                            <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
                                <div class="form-group flex flex-col gap-1">
                                    <label class="text-xs font-semibold text-surface-500 uppercase tracking-wider">Bot User OAuth Token</label>
                                    <Password v-model="channels.slack.botToken" :feedback="false" toggleMask placeholder="xoxb-your-bot-token" fluid />
                                </div>
                                <div class="form-group flex flex-col gap-1">
                                    <label class="text-xs font-semibold text-surface-500 uppercase tracking-wider">Signing Secret</label>
                                    <Password v-model="channels.slack.signingSecret" :feedback="false" toggleMask placeholder="Slack Signing Secret" fluid />
                                </div>
                            </div>
                            <div class="text-xs text-surface-500">
                                <strong>Request URL (Event Subscriptions):</strong> <code>/api/channel/slack/{{projectId}}</code>
                            </div>
                        </div>

                        <!-- Telegram Integration -->
                        <div class="p-4 border rounded-lg bg-surface-50 flex flex-col gap-3">
                            <h4 class="font-semibold text-sm text-surface-700 flex items-center gap-2">
                                <i class="pi pi-telegram text-sky-500"></i>
                                <span>Telegram Bot</span>
                            </h4>
                            <div class="form-group flex flex-col gap-1">
                                <label class="text-xs font-semibold text-surface-500 uppercase tracking-wider">Bot Token API</label>
                                <Password v-model="channels.telegram.botToken" :feedback="false" toggleMask placeholder="e.g. 123456789:ABCdefGhIJKlmNoPQRsTUVwxyZ" fluid />
                            </div>
                            <div class="text-xs text-surface-500">
                                <strong>Webhook URL:</strong> <code>/api/channel/telegram/{{projectId}}</code>
                            </div>
                        </div>

                        <!-- Microsoft Teams Integration -->
                        <div class="p-4 border rounded-lg bg-surface-50 flex flex-col gap-3">
                            <h4 class="font-semibold text-sm text-surface-700 flex items-center gap-2">
                                <i class="pi pi-microsoft text-blue-500"></i>
                                <span>Microsoft Teams Bot</span>
                            </h4>
                            <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
                                <div class="form-group flex flex-col gap-1">
                                    <label class="text-xs font-semibold text-surface-500 uppercase tracking-wider">Microsoft App ID</label>
                                    <InputText v-model="channels.teams.appId" placeholder="e.g. aaaa-bbbb-cccc-dddd" fluid />
                                </div>
                                <div class="form-group flex flex-col gap-1">
                                    <label class="text-xs font-semibold text-surface-500 uppercase tracking-wider">Microsoft App Password</label>
                                    <Password v-model="channels.teams.appPassword" :feedback="false" toggleMask placeholder="App Password / Client Secret" fluid />
                                </div>
                            </div>
                            <div class="text-xs text-surface-500">
                                <strong>Webhook URL:</strong> <code>/api/channel/teams/{{projectId}}</code>
                            </div>
                        </div>

                    </div>
                </template>
            </Card>

            <!-- Human Handoff -->
            <h2 class="section-title mt-4">Human Handoff (Live Chat)</h2>
            <p class="section-subtitle">Allow agents to take over conversations when the AI cannot resolve the issue.</p>
            <Card class="provider-card">
                <template #content>
                    <div class="grid-2 mt-2">
                        <div class="form-group flex items-center gap-3">
                            <Checkbox v-model="config.handoffEnabled" :binary="true" inputId="handoffEnabled" />
                            <label for="handoffEnabled" class="font-medium">Enable Human Handoff</label>
                        </div>
                    </div>
                    <div v-if="config.handoffEnabled" class="grid-2 mt-4">
                        <div class="form-group">
                            <label>Trigger Keywords (Comma separated)</label>
                            <InputText v-model="config.handoffTriggerKeywords" placeholder="e.g. human, agent, support, escalate" fluid />
                            <small class="info-text">If a user message contains any of these words, they will be placed in the queue.</small>
                        </div>
                        <div class="form-group">
                            <label>Queue Message</label>
                            <InputText v-model="config.handoffQueueMessage" placeholder="I'm connecting you with a live agent. Please hold on." fluid />
                            <small class="info-text">The message shown to the user while they wait for an agent.</small>
                        </div>
                    </div>
                </template>
            </Card>

            <!-- Template Variables Key-Value Editor -->
            <h2 class="section-title mt-4">Template Variables</h2>
            <p class="section-subtitle">Define values for <code v-pre>{{variable}}</code> placeholders in your system prompt. <code v-pre>{{date}}</code> and <code v-pre>{{time}}</code> are auto-filled at runtime.</p>
            <Card class="provider-card">
                <template #content>
                    <div class="template-vars-list">
                        <div v-for="(v, index) in templateVars" :key="index" class="template-var-row">
                            <InputText v-model="v.key" placeholder="Variable name (e.g. company)" fluid />
                            <span class="var-equals">=</span>
                            <InputText v-model="v.value" placeholder="Value (e.g. Acme Inc)" fluid />
                            <Button icon="pi pi-times" severity="danger" text rounded @click="removeTemplateVar(index)" />
                        </div>
                        <div v-if="templateVars.length === 0" class="empty-suggestions">
                            No template variables defined. Add variables to personalize your system prompt.
                        </div>
                    </div>
                    <Button 
                        icon="pi pi-plus" 
                        label="Add Variable" 
                        severity="secondary" 
                        text 
                        size="small" 
                        class="mt-2" 
                        @click="addTemplateVar" 
                    />
                </template>
            </Card>

            <!-- Widget Appearance -->
            <h2 class="section-title mt-4">Widget Appearance</h2>
            <p class="section-subtitle">Customize how the chat widget looks on your website.</p>
            <Card class="provider-card">
                <template #content>
                    <div class="grid-2">
                        <div class="form-group">
                            <label>Primary Color</label>
                            <div class="color-picker-wrapper">
                                <input type="color" v-model="theme.primaryColor" class="color-input" />
                                <InputText v-model="theme.primaryColor" class="color-text" fluid />
                            </div>
                        </div>
                        <div class="form-group">
                            <label>Background Color</label>
                            <div class="color-picker-wrapper">
                                <input type="color" v-model="theme.bgColor" class="color-input" />
                                <InputText v-model="theme.bgColor" class="color-text" fluid />
                            </div>
                        </div>
                    </div>
                    <div class="grid-2 mt-4">
                        <div class="form-group">
                            <label>Font Family</label>
                            <Select 
                                v-model="theme.fontFamily" 
                                :options="fontOptions" 
                                optionLabel="label" 
                                optionValue="value" 
                                fluid 
                            />
                        </div>
                        <div class="form-group">
                            <label>Widget Position</label>
                            <Select 
                                v-model="theme.position" 
                                :options="positionOptions" 
                                optionLabel="label" 
                                optionValue="value" 
                                fluid 
                            />
                        </div>
                    </div>
                    
                    <div class="theme-preview mt-4" :style="{ '--preview-primary': theme.primaryColor, '--preview-bg': theme.bgColor, '--preview-font': theme.fontFamily === 'system-ui' ? 'system-ui, sans-serif' : theme.fontFamily + ', sans-serif', 'justify-content': theme.position === 'bottom-left' ? 'flex-start' : 'flex-end' }">
                        <div class="preview-widget">
                            <div class="preview-header">
                                <div class="preview-title">Chat with us</div>
                            </div>
                            <div class="preview-body">
                                <div class="preview-msg bot">Hi! How can I help you today?</div>
                                <div class="preview-msg user">I have a question about pricing.</div>
                            </div>
                            <div class="preview-input">
                                <span>Type your message...</span>
                                <div class="preview-send"><i class="pi pi-send"></i></div>
                            </div>
                        </div>
                    </div>
                </template>
            </Card>

            <!-- Prompt History -->
            <h2 class="section-title mt-4">
                <span>Prompt History</span>
                <Button 
                    :label="showHistory ? 'Hide' : 'View History'" 
                    :icon="showHistory ? 'pi pi-chevron-up' : 'pi pi-history'" 
                    severity="secondary" 
                    text 
                    size="small" 
                    @click="showHistory = !showHistory; if(showHistory && historyEntries.length === 0) loadHistory()" 
                />
            </h2>

            <Card v-if="showHistory" class="provider-card history-card">
                <template #content>
                    <div v-if="loadingHistory" class="history-loading">
                        <i class="pi pi-spin pi-spinner"></i> Loading history...
                    </div>
                    <div v-else-if="historyEntries.length === 0" class="empty-suggestions">
                        No prompt history yet. History is created automatically when you change the system prompt or model.
                    </div>
                    <div v-else class="history-timeline">
                        <div v-for="entry in historyEntries" :key="entry.id" class="history-entry">
                            <div class="history-dot"></div>
                            <div class="history-content">
                                <div class="history-header">
                                    <span class="history-date">{{ formatDate(entry.createdAt) }}</span>
                                    <span class="history-model">{{ entry.defaultProvider }} / {{ entry.defaultModel }}</span>
                                </div>
                                <p class="history-prompt">{{ truncate(entry.systemPrompt, 200) }}</p>
                                <div v-if="entry.changeNote" class="history-note">
                                    <i class="pi pi-comment"></i> {{ entry.changeNote }}
                                </div>
                                <Button 
                                    :label="restoringId === entry.id ? 'Restoring...' : 'Restore This Version'" 
                                    icon="pi pi-replay" 
                                    severity="secondary" 
                                    outlined 
                                    size="small" 
                                    :disabled="restoringId !== null" 
                                    @click="restoreVersion(entry.id)" 
                                />
                            </div>
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
.section-subtitle {
    color: var(--p-surface-500);
    font-size: 0.85rem;
    margin: -8px 0 16px 0;
}
.mt-2 { margin-top: 0.75rem; }
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

/* ── Template Variable Chips ── */
.variable-chips {
    display: flex;
    align-items: center;
    gap: 6px;
    flex-wrap: wrap;
    margin-top: 4px;
}
.chips-label {
    font-size: 0.78rem;
    color: var(--p-surface-500);
    margin-right: 4px;
}
.var-chip {
    display: inline-flex;
    align-items: center;
    gap: 2px;
    padding: 3px 10px;
    border-radius: 14px;
    border: 1px solid var(--p-surface-200);
    background: var(--p-surface-50);
    color: var(--p-surface-700);
    font-family: monospace;
    font-size: 0.78rem;
    cursor: pointer;
    transition: all 0.15s ease;
    white-space: nowrap;
}
.var-chip:hover {
    background: var(--p-primary-50);
    border-color: var(--p-primary-300);
    color: var(--p-primary-700);
}
.var-chip-braces {
    color: var(--p-primary-400);
    font-weight: 700;
}
.var-chip-builtin {
    border-style: dashed;
}
.var-chip-auto {
    font-size: 0.65rem;
    text-transform: uppercase;
    color: var(--p-primary-400);
    margin-left: 4px;
    font-weight: 600;
    font-family: system-ui;
}
.change-note-row {
    margin-top: 8px;
}

/* ── Template Variables Editor ── */
.template-vars-list {
    display: flex;
    flex-direction: column;
    gap: 12px;
}
.template-var-row {
    display: flex;
    align-items: center;
    gap: 12px;
}
.var-equals {
    font-weight: bold;
    color: var(--p-text-color-secondary);
}

.color-picker-wrapper {
    display: flex;
    gap: 12px;
    align-items: center;
}
.color-input {
    appearance: none;
    -webkit-appearance: none;
    border: none;
    width: 40px;
    height: 40px;
    border-radius: 8px;
    cursor: pointer;
    padding: 0;
    overflow: hidden;
}
.color-input::-webkit-color-swatch-wrapper {
    padding: 0;
}
.color-input::-webkit-color-swatch {
    border: none;
    border-radius: 8px;
}
.color-text {
    font-family: monospace;
    flex: 1;
}

.theme-preview {
    background: url('data:image/svg+xml;utf8,<svg width="20" height="20" xmlns="http://www.w3.org/2000/svg"><rect width="10" height="10" fill="%23f1f5f9"/><rect x="10" y="10" width="10" height="10" fill="%23f1f5f9"/></svg>') repeat;
    border: 1px solid var(--p-surface-200);
    border-radius: 12px;
    padding: 24px;
    display: flex;
    height: 350px;
    align-items: flex-end;
}

.preview-widget {
    width: 280px;
    background: var(--preview-bg);
    border-radius: 16px;
    box-shadow: 0 10px 25px rgba(0,0,0,0.1);
    display: flex;
    flex-direction: column;
    overflow: hidden;
    font-family: var(--preview-font);
    border: 1px solid var(--p-surface-200);
}

.preview-header {
    background: var(--preview-primary);
    color: white;
    padding: 16px;
    font-weight: 600;
}

.preview-body {
    padding: 16px;
    display: flex;
    flex-direction: column;
    gap: 12px;
    background: #f8fafc;
}

.preview-msg {
    padding: 10px 14px;
    border-radius: 12px;
    font-size: 0.85rem;
    max-width: 85%;
}

.preview-msg.bot {
    background: var(--preview-bg);
    border: 1px solid var(--p-surface-200);
    color: var(--p-text-color);
    align-self: flex-start;
    border-bottom-left-radius: 4px;
}

.preview-msg.user {
    background: var(--preview-primary);
    color: white;
    align-self: flex-end;
    border-bottom-right-radius: 4px;
}

.preview-input {
    background: var(--preview-bg);
    padding: 12px 16px;
    display: flex;
    justify-content: space-between;
    align-items: center;
    border-top: 1px solid var(--p-surface-200);
    color: var(--p-text-color-secondary);
    font-size: 0.85rem;
}

.preview-send {
    background: var(--preview-primary);
    color: white;
    width: 28px;
    height: 28px;
    border-radius: 50%;
    display: flex;
    align-items: center;
    justify-content: center;
}
.preview-send i {
    font-size: 0.75rem;
}

/* ── Prompt History Timeline ── */
.section-title {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 16px;
}
.history-card {
    max-height: 600px;
    overflow-y: auto;
}
.history-loading {
    text-align: center;
    padding: 24px;
    color: var(--p-surface-500);
}
.history-timeline {
    position: relative;
    padding-left: 24px;
}
.history-timeline::before {
    content: '';
    position: absolute;
    left: 7px;
    top: 8px;
    bottom: 8px;
    width: 2px;
    background: var(--p-surface-200);
}
.history-entry {
    position: relative;
    padding: 12px 0;
    border-bottom: 1px solid var(--p-surface-100);
}
.history-entry:last-child {
    border-bottom: none;
}
.history-dot {
    position: absolute;
    left: -20px;
    top: 18px;
    width: 12px;
    height: 12px;
    border-radius: 50%;
    background: var(--p-primary-400);
    border: 2px solid var(--p-surface-0);
    box-shadow: 0 0 0 2px var(--p-surface-200);
}
.history-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 6px;
}
.history-date {
    font-size: 0.8rem;
    font-weight: 600;
    color: var(--p-surface-700);
}
.history-model {
    font-size: 0.75rem;
    color: var(--p-surface-400);
    font-family: monospace;
    background: var(--p-surface-50);
    padding: 2px 8px;
    border-radius: 10px;
}
.history-prompt {
    font-size: 0.82rem;
    color: var(--p-surface-600);
    line-height: 1.5;
    margin: 0 0 8px 0;
    white-space: pre-wrap;
    word-break: break-word;
}
.history-note {
    font-size: 0.78rem;
    color: var(--p-primary-500);
    display: flex;
    align-items: center;
    gap: 6px;
    margin-bottom: 8px;
    font-style: italic;
}

@media (max-width: 768px) {
    .template-var-row {
        flex-direction: column;
        align-items: stretch;
    }
    .var-equals {
        display: none;
    }
    .variable-chips {
        gap: 4px;
    }
    .history-header {
        flex-direction: column;
        align-items: flex-start;
        gap: 4px;
    }
}
</style>

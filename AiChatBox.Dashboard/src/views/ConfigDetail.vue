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
    defaultModel: 'gemini-1.5-flash', 
    geminiApiKey: '', 
    groqApiKey: '', 
    openAiApiKey: '', 
    liveVoiceEnabled: false, 
    enabledModels: '' 
});

// Default model options derived from enabled models list
const defaultModelOptions = computed(() => {
    return enabledModels.value.map(e => ({
        label: `${e.model} (${e.provider})`,
        value: e.model
    }));
});

const providersList = [
    { key: 'geminiApiKey', label: 'Google Gemini', id: 'gemini' },
    { key: 'groqApiKey', label: 'Groq', id: 'groq' },
    { key: 'openAiApiKey', label: 'OpenAI', id: 'openai' }
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
        if (data.enabledModels) {
            try {
                const parsed = JSON.parse(data.enabledModels);
                if (Array.isArray(parsed) && parsed.length > 0) {
                    if (typeof parsed[0] === 'object' && parsed[0].model) {
                        // New format: [{model, provider}]
                        enabledModels.value = parsed;
                    } else {
                        // Legacy format: ["model1", "model2"] — use defaultProvider
                        enabledModels.value = parsed.map((m: string) => ({ 
                            model: m, 
                            provider: config.defaultProvider || 'gemini' 
                        }));
                    }
                }
            } catch {}
        }
    }
}

async function fetchModels(providerId: string) {
    const keyField = providerId === 'gemini' ? 'geminiApiKey' : providerId === 'groq' ? 'groqApiKey' : 'openAiApiKey';
    if (!config[keyField as keyof typeof config]) return;
    
    fetchingModels.value = providerId;
    try {
        const res = await apiFetch(`/api/provider/models?provider=${providerId}&apiKey=${encodeURIComponent(config[keyField as keyof typeof config] as string)}`);
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
    await apiFetch(`/api/configuration/${configId.value}`, {
        method: 'PUT',
        body: JSON.stringify({
            name: config.name, 
            systemPrompt: config.systemPrompt,
            defaultProvider: config.defaultProvider, 
            defaultModel: config.defaultModel,
            geminiApiKey: config.geminiApiKey, 
            groqApiKey: config.groqApiKey,
            openAiApiKey: config.openAiApiKey,
            liveVoiceEnabled: config.liveVoiceEnabled,
            enabledModels: JSON.stringify(enabledModels.value)
        })
    });
    saving.value = false;
    saved.value = true;
    setTimeout(() => saved.value = false, 3000);
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
                    
                    <div class="form-group checkbox-group">
                        <Checkbox v-model="config.liveVoiceEnabled" :binary="true" :disabled="!config.geminiApiKey" inputId="liveVoice" />
                        <label for="liveVoice">Live Voice Mode</label>
                        <span v-if="!config.geminiApiKey" class="info-text">(requires Gemini API key)</span>
                    </div>
                </template>
            </Card>

            <h2 class="section-title">Provider API Keys</h2>
            
            <Card v-for="provider in providersList" :key="provider.key" class="provider-card">
                <template #title>
                    <div class="provider-header">
                        <h3>{{ provider.label }}</h3>
                        <span v-if="config[provider.key as keyof typeof config]" class="badge badge-success">Configured</span>
                    </div>
                </template>
                <template #content>
                    <div class="api-key-input">
                        <Password v-model="config[provider.key as keyof typeof config] as string" :feedback="false" toggleMask :placeholder="provider.label + ' API key'" fluid class="flex-1" />
                        <Button 
                            :label="fetchingModels === provider.id ? 'Loading...' : 'Fetch Models'" 
                            severity="secondary" 
                            outlined 
                            :disabled="!config[provider.key as keyof typeof config] || fetchingModels === provider.id" 
                            @click="fetchModels(provider.id)" 
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
.provider-card {
    background-color: var(--p-surface-0);
    border: 1px solid var(--p-surface-200);
    margin-bottom: 16px;
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
</style>

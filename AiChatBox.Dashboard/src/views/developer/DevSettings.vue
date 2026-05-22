<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
import { useApi } from '../../composables/useApi';
import Card from 'primevue/card';
import Button from 'primevue/button';
import InputText from 'primevue/inputtext';
import Textarea from 'primevue/textarea';
import Select from 'primevue/select';
import Message from 'primevue/message';
import Dialog from 'primevue/dialog';

const { apiFetch } = useApi();

const account = ref<any>(null);
const loading = ref(false);
const saving = ref(false);
const rotating = ref(false);
const error = ref('');
const success = ref('');

// Master key rotation modal
const showKeyDialog = ref(false);
const rotatedKey = ref('');

const providerOptions = [
    { label: 'Google Gemini', value: 'gemini' },
    { label: 'OpenAI', value: 'openai' },
    { label: 'Anthropic Claude', value: 'anthropic' },
    { label: 'Groq', value: 'groq' }
];

const modelOptions = computed(() => {
    if (!account.value) return [];
    switch (account.value.defaultProvider) {
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

const loadAccount = async () => {
    loading.value = true;
    error.value = '';
    try {
        const res = await apiFetch('/api/partner/account');
        if (res.ok) {
            account.value = await res.json();
        } else {
            error.value = 'Failed to load partner account settings.';
        }
    } catch (e) {
        error.value = 'An error occurred while loading settings.';
    } finally {
        loading.value = false;
    }
};

const handleSaveSettings = async () => {
    if (!account.value) return;
    saving.value = true;
    error.value = '';
    success.value = '';
    
    try {
        const res = await apiFetch('/api/partner/account', {
            method: 'PUT',
            body: JSON.stringify({
                companyName: account.value.companyName,
                allowedDomainPattern: account.value.allowedDomainPattern,
                defaultSystemPrompt: account.value.defaultSystemPrompt,
                defaultProvider: account.value.defaultProvider,
                defaultModel: account.value.defaultModel,
                defaultThemeSettingsJson: account.value.defaultThemeSettingsJson
            })
        });
        if (res.ok) {
            success.value = 'Partner configurations saved successfully.';
            await loadAccount();
        } else {
            error.value = 'Failed to save partner configurations.';
        }
    } catch (e) {
        error.value = 'An error occurred during save.';
    } finally {
        saving.value = false;
    }
};

const handleRotateKey = async () => {
    if (!confirm('Are you sure you want to rotate the Master API Key? Any application currently calling the B2B provisioning endpoints using the old key will instantly fail.')) return;
    rotating.value = true;
    error.value = '';
    success.value = '';
    
    try {
        const res = await apiFetch('/api/partner/master-key/rotate', {
            method: 'POST'
        });
        if (res.ok) {
            const data = await res.json();
            rotatedKey.value = data.masterKey;
            showKeyDialog.value = true;
            success.value = 'Master API key rotated successfully.';
            await loadAccount();
        } else {
            error.value = 'Failed to rotate Master API key.';
        }
    } catch (e) {
        error.value = 'An error occurred.';
    } finally {
        rotating.value = false;
    }
};

onMounted(() => {
    loadAccount();
});
</script>

<template>
    <div class="settings-view">
        <header class="header">
            <div>
                <h1>Partner Settings</h1>
                <p class="subtitle">Update company information, configure provisioning defaults, and manage master authentication credentials.</p>
            </div>
        </header>

        <Message v-if="error" severity="error" variant="simple" class="mb-4">{{ error }}</Message>
        <Message v-if="success" severity="success" variant="simple" class="mb-4">{{ success }}</Message>

        <div v-if="loading && !account" class="loading-state">
            <i class="pi pi-spin pi-spinner" style="font-size: 2rem"></i>
            <p>Loading configurations...</p>
        </div>

        <div class="settings-grid" v-else-if="account">
            <!-- Left Side Forms -->
            <div class="main-settings">
                <!-- Company and Domains -->
                <Card class="settings-card mb-4">
                    <template #title><span class="card-title">Company Profile & Scope</span></template>
                    <template #content>
                        <div class="card-form">
                            <div class="form-field">
                                <label for="company-name">Company Name</label>
                                <InputText id="company-name" v-model="account.companyName" placeholder="e.g., Acme Corp" class="w-full" />
                            </div>
                            <div class="form-field mt-3">
                                <label for="domain-pattern">Global Whitelisted Domain Pattern</label>
                                <InputText id="domain-pattern" v-model="account.allowedDomainPattern" placeholder="e.g., *.acmeapp.com" class="w-full" />
                                <small class="text-muted-small">Used to restrict loading of the widget scripts across all provisioned client chatbots.</small>
                            </div>
                        </div>
                    </template>
                </Card>

                <!-- Defaults Template -->
                <Card class="settings-card mb-4">
                    <template #title><span class="card-title">New Tenant Defaults</span></template>
                    <template #content>
                        <div class="card-form">
                            <p class="form-desc">Define standard templates applied automatically when provisioning new chatbots without overrides.</p>
                            <div class="form-grid">
                                <div class="form-field">
                                    <label for="default-provider">Default LLM Provider</label>
                                    <Select id="default-provider" v-model="account.defaultProvider" :options="providerOptions" optionLabel="label" optionValue="value" placeholder="Select default" class="w-full" showClear />
                                </div>
                                <div class="form-field">
                                    <label for="default-model">Default LLM Model</label>
                                    <Select id="default-model" v-model="account.defaultModel" :options="modelOptions" optionLabel="label" optionValue="value" placeholder="Select default" class="w-full" :disabled="!account.defaultProvider" showClear />
                                </div>
                            </div>
                            <div class="form-field mt-3">
                                <label for="default-prompt">Default System Prompt Template</label>
                                <Textarea id="default-prompt" v-model="account.defaultSystemPrompt" rows="4" placeholder="You are a helpful AI assistant..." class="w-full" />
                            </div>
                        </div>
                    </template>
                </Card>

                <div class="save-actions">
                    <Button label="Save Configurations" icon="pi pi-check" :loading="saving" @click="handleSaveSettings" />
                </div>
            </div>

            <!-- Right Side API Info -->
            <div class="side-settings">
                <!-- Master API Key Management -->
                <Card class="settings-card mb-4">
                    <template #title><span class="card-title">Master API Credentials</span></template>
                    <template #content>
                        <div class="api-card-content">
                            <p class="api-desc">Use this credential to authenticate with the B2B partner endpoints from your server backend.</p>
                            
                            <div class="api-status-box">
                                <div class="status-indicator">
                                    <span class="dot" :class="{ active: account.masterKeyActive }"></span>
                                    <span>Master API Key Status: <strong>{{ account.masterKeyActive ? 'Active' : 'Disabled' }}</strong></span>
                                </div>
                            </div>

                            <div class="api-warning">
                                <i class="pi pi-exclamation-triangle mr-2"></i>
                                <span>Keep your Master Key secret. Never expose it on frontends.</span>
                            </div>

                            <div class="rotate-btn-wrapper">
                                <Button label="Rotate Master Key" icon="pi pi-refresh" severity="warning" :loading="rotating" @click="handleRotateKey" class="w-full" />
                            </div>
                        </div>
                    </template>
                </Card>

                <!-- Quota Card -->
                <Card class="settings-card">
                    <template #title><span class="card-title">Quotas and Billing Limits</span></template>
                    <template #content>
                        <div class="quota-list">
                            <div class="quota-item">
                                <span class="label">Total Chatbot Limit</span>
                                <span class="value font-semibold">{{ account.tenantCount }} / {{ account.maxTenants }} deployed</span>
                            </div>
                            <div class="quota-item">
                                <span class="label">Monthly Spending Limit</span>
                                <span class="value font-semibold">{{ account.creditLimit > 0 ? '$' + account.creditLimit.toFixed(2) : 'Unlimited' }}</span>
                            </div>
                            <div class="quota-item">
                                <span class="label">Current Month Spend</span>
                                <span class="value font-semibold text-emerald-600">${{ account.currentSpend.toFixed(2) }}</span>
                            </div>
                        </div>
                    </template>
                </Card>
            </div>
        </div>

        <!-- Master Key Dialog (Shown Once on Rotation) -->
        <Dialog v-model:visible="showKeyDialog" header="New Master API Key" modal :closable="false" :style="{ width: '500px' }">
            <div class="key-display-content">
                <Message severity="warn" variant="simple" class="mb-4">Copy this master API key now. It will not be shown again. Update your backend services immediately with the new key.</Message>
                <div class="key-box">
                    <code>{{ rotatedKey }}</code>
                </div>
            </div>
            <template #footer>
                <Button label="Done" @click="showKeyDialog = false; rotatedKey = ''" />
            </template>
        </Dialog>
    </div>
</template>

<style scoped>
.settings-view {
    padding: 12px 0;
}
.header {
    margin-bottom: 32px;
}
.subtitle {
    color: var(--p-surface-500);
    margin-top: 4px;
    font-size: 0.9rem;
}
.loading-state {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    padding: 64px 0;
    color: var(--p-surface-400);
}
.loading-state p {
    margin-top: 12px;
}

/* Layout Grid */
.settings-grid {
    display: grid;
    grid-template-columns: 3fr 2fr;
    gap: 24px;
}
.settings-card {
    background: var(--p-surface-0);
    border: 1px solid var(--p-surface-200);
}
.card-title {
    font-size: 1rem;
    font-weight: 600;
}

/* Forms */
.card-form {
    display: flex;
    flex-direction: column;
}
.form-field {
    display: flex;
    flex-direction: column;
    gap: 6px;
}
.form-field label {
    font-size: 0.85rem;
    font-weight: 600;
    color: var(--p-surface-600);
}
.form-grid {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 12px;
}
.form-desc {
    font-size: 0.85rem;
    color: var(--p-surface-500);
    margin: 0 0 16px 0;
}
.text-muted-small {
    font-size: 0.75rem;
    color: var(--p-surface-400);
    margin-top: 2px;
}
.save-actions {
    display: flex;
    justify-content: flex-end;
}

/* Right Side - API Card */
.api-card-content {
    display: flex;
    flex-direction: column;
    gap: 16px;
}
.api-desc {
    font-size: 0.85rem;
    color: var(--p-surface-500);
    margin: 0;
}
.api-status-box {
    background: var(--p-surface-50);
    border: 1px solid var(--p-surface-200);
    padding: 12px;
    border-radius: 8px;
}
.status-indicator {
    display: flex;
    align-items: center;
    gap: 8px;
    font-size: 0.85rem;
    color: var(--p-surface-700);
}
.status-indicator .dot {
    width: 8px;
    height: 8px;
    border-radius: 50%;
    background: var(--p-surface-400);
}
.status-indicator .dot.active {
    background: var(--p-emerald-500);
    box-shadow: 0 0 8px var(--p-emerald-400);
}
.api-warning {
    display: flex;
    align-items: center;
    background: color-mix(in srgb, var(--p-yellow-50) 40%, transparent);
    border: 1px solid var(--p-yellow-200);
    color: var(--p-yellow-700);
    padding: 10px 12px;
    border-radius: 8px;
    font-size: 0.8rem;
}
.rotate-btn-wrapper {
    margin-top: 4px;
}

/* Quota List */
.quota-list {
    display: flex;
    flex-direction: column;
    gap: 12px;
}
.quota-item {
    display: flex;
    justify-content: space-between;
    align-items: center;
    font-size: 0.85rem;
    border-bottom: 1px solid var(--p-surface-100);
    padding-bottom: 8px;
}
.quota-item:last-child {
    border: none;
    padding: 0;
}
.quota-item .label {
    color: var(--p-surface-500);
}
.quota-item .value {
    color: var(--p-surface-800);
}
.font-semibold {
    font-weight: 600;
}
.text-emerald-600 {
    color: var(--p-emerald-600);
}

/* Dialog content */
.key-display-content {
    display: flex;
    flex-direction: column;
}
.key-box {
    background: var(--p-surface-900);
    color: var(--p-emerald-400);
    padding: 16px;
    border-radius: 8px;
    font-family: monospace;
    font-size: 1rem;
    word-break: break-all;
    text-align: center;
    border: 1px solid var(--p-surface-800);
    box-shadow: inset 0 2px 4px rgba(0,0,0,0.2);
}

@media (max-width: 992px) {
    .settings-grid {
        grid-template-columns: 1fr;
    }
}
</style>

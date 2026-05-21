<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useApi } from '../../composables/useApi';
import DataTable from 'primevue/datatable';
import Column from 'primevue/column';
import Button from 'primevue/button';
import Tag from 'primevue/tag';
import Dialog from 'primevue/dialog';
import InputText from 'primevue/inputtext';
import Textarea from 'primevue/textarea';
import Select from 'primevue/select';
import Message from 'primevue/message';
import ToggleSwitch from 'primevue/toggleswitch';
import Accordion from 'primevue/accordion';
import AccordionPanel from 'primevue/accordionpanel';
import AccordionHeader from 'primevue/accordionheader';
import AccordionContent from 'primevue/accordioncontent';

const route = useRoute();
const router = useRouter();
const { apiFetch } = useApi();

const tenants = ref<any[]>([]);
const loading = ref(false);
const error = ref('');
const success = ref('');

// Dialog states
const showProvisionDialog = ref(false);
const showManageDialog = ref(false);
const showKeyDialog = ref(false);
const showSnippetDialog = ref(false);

// Form data for Provisioning
const provisionForm = ref({
    tenantName: '',
    tenantIdentifier: '',
    systemPrompt: '',
    provider: '',
    modelName: '',
    allowedDomains: '',
    themeSettingsJson: '',
    // Permissions toggles
    showPrompt: true,
    showKnowledgeBase: true,
    showRules: true,
    showWidgetCustomization: true
});

const showOverrides = ref(false);

// Selected Tenant for Management
const selectedTenant = ref<any>(null);
const manageEmbedSettings = ref({
    showPrompt: true,
    showKnowledgeBase: true,
    showRules: true,
    showWidgetCustomization: true
});

// Response details
const provisionedTenantDetails = ref<any>(null);
const currentSnippet = ref('');

const providerOptions = [
    { label: 'Google Gemini', value: 'gemini' },
    { label: 'OpenAI', value: 'openai' },
    { label: 'Anthropic Claude', value: 'anthropic' },
    { label: 'Groq', value: 'groq' }
];

const modelOptions = computed(() => {
    switch (provisionForm.value.provider) {
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

const loadTenants = async () => {
    loading.value = true;
    error.value = '';
    try {
        const res = await apiFetch('/api/partner/tenants');
        if (res.ok) {
            tenants.value = await res.json();
        } else {
            error.value = 'Failed to load tenant projects.';
        }
    } catch (e) {
        error.value = 'An error occurred while fetching tenants.';
    } finally {
        loading.value = false;
    }
};

const handleProvision = async () => {
    error.value = '';
    success.value = '';
    
    // Construct EmbedSettingsJson
    const embedSettingsObj = {
        showPrompt: provisionForm.value.showPrompt,
        showKnowledgeBase: provisionForm.value.showKnowledgeBase,
        showRules: provisionForm.value.showRules,
        showWidgetCustomization: provisionForm.value.showWidgetCustomization
    };

    const requestBody = {
        tenantName: provisionForm.value.tenantName,
        tenantIdentifier: provisionForm.value.tenantIdentifier || null,
        systemPrompt: provisionForm.value.systemPrompt || null,
        provider: provisionForm.value.provider || null,
        modelName: provisionForm.value.modelName || null,
        allowedDomains: provisionForm.value.allowedDomains || null,
        themeSettingsJson: provisionForm.value.themeSettingsJson || null,
        embedSettingsJson: JSON.stringify(embedSettingsObj)
    };

    try {
        const res = await apiFetch('/api/partner/tenants', {
            method: 'POST',
            body: JSON.stringify(requestBody)
        });
        if (res.ok) {
            const data = await res.json();
            provisionedTenantDetails.value = data;
            showProvisionDialog.value = false;
            showKeyDialog.value = true;
            success.value = `Tenant '${provisionForm.value.tenantName}' provisioned.`;
            
            // Clear form
            provisionForm.value = {
                tenantName: '',
                tenantIdentifier: '',
                systemPrompt: '',
                provider: '',
                modelName: '',
                allowedDomains: '',
                themeSettingsJson: '',
                showPrompt: true,
                showKnowledgeBase: true,
                showRules: true,
                showWidgetCustomization: true
            };
            showOverrides.value = false;
            await loadTenants();
        } else {
            const errData = await res.json();
            error.value = errData.message || 'Failed to provision tenant.';
        }
    } catch (e) {
        error.value = 'An error occurred during tenant provisioning.';
    }
};

const openManageDialog = (tenant: any) => {
    selectedTenant.value = tenant;
    
    // Parse embed settings
    try {
        const parsed = JSON.parse(tenant.embedSettingsJson || '{}');
        manageEmbedSettings.value = {
            showPrompt: parsed.showPrompt !== false,
            showKnowledgeBase: parsed.showKnowledgeBase !== false,
            showRules: parsed.showRules !== false,
            showWidgetCustomization: parsed.showWidgetCustomization !== false
        };
    } catch (e) {
        manageEmbedSettings.value = {
            showPrompt: true,
            showKnowledgeBase: true,
            showRules: true,
            showWidgetCustomization: true
        };
    }

    showManageDialog.value = true;
};

const handleSaveEmbedSettings = async () => {
    if (!selectedTenant.value) return;
    error.value = '';
    success.value = '';

    const newSettingsJson = JSON.stringify(manageEmbedSettings.value);

    try {
        const res = await apiFetch(`/api/partner/tenants/${selectedTenant.value.projectId}/embed-settings`, {
            method: 'PUT',
            body: JSON.stringify({ embedSettingsJson: newSettingsJson })
        });
        if (res.ok) {
            success.value = `Embed settings updated for ${selectedTenant.value.name}.`;
            showManageDialog.value = false;
            await loadTenants();
        } else {
            error.value = 'Failed to update embed settings.';
        }
    } catch (e) {
        error.value = 'An error occurred.';
    }
};

const handleDeleteTenant = async (tenantId: string) => {
    if (!confirm('Are you sure you want to delete this tenant chatbot? This cannot be undone.')) return;
    error.value = '';
    success.value = '';
    try {
        const res = await apiFetch(`/api/partner/tenants/${tenantId}`, {
            method: 'DELETE'
        });
        if (res.ok) {
            success.value = 'Tenant deleted successfully.';
            showManageDialog.value = false;
            await loadTenants();
        } else {
            error.value = 'Failed to delete tenant.';
        }
    } catch (e) {
        error.value = 'An error occurred.';
    }
};

const showSnippet = (tenant: any) => {
    // Generate snippet
    const baseUrl = import.meta.env.DEV ? 'https://localhost:44385' : window.location.origin;
    currentSnippet.value = `<!-- Copy and paste this script into your website HTML body -->
<script 
  src="${baseUrl}/widget/chatbox.js" 
  data-project-id="${tenant.projectId}"
  defer>
</` + `script>`;
    showSnippetDialog.value = true;
};

const copyToClipboard = (text: string) => {
    navigator.clipboard.writeText(text);
    alert('Copied to clipboard!');
};

onMounted(() => {
    loadTenants();
    
    // Check if quick-provisioning action was triggered
    if (route.query.action === 'provision') {
        showProvisionDialog.value = true;
        // Clean query param
        router.replace({ query: {} });
    }
});
</script>

<template>
    <div class="tenants-view">
        <header class="header">
            <div>
                <h1>Deployed Tenants</h1>
                <p class="subtitle">Provision, configure permission overrides, and fetch integration scripts for your customers.</p>
            </div>
            <Button label="Provision Tenant" icon="pi pi-plus" @click="showProvisionDialog = true" />
        </header>

        <Message v-if="error" severity="error" variant="simple" class="mb-4">{{ error }}</Message>
        <Message v-if="success" severity="success" variant="simple" class="mb-4">{{ success }}</Message>

        <DataTable :value="tenants" :loading="loading" class="p-datatable-lg" responsiveLayout="scroll">
            <Column field="name" header="Name" sortable />
            <Column field="tenantIdentifier" header="Identifier" sortable>
                <template #body="{ data }">
                    <span class="tenant-id" v-if="data.tenantIdentifier">{{ data.tenantIdentifier }}</span>
                    <span class="text-muted" v-else>-</span>
                </template>
            </Column>
            <Column field="provider" header="Provider/Model">
                <template #body="{ data }">
                    <span class="font-mono text-xs">{{ data.provider || 'default' }} / {{ data.modelName || 'default' }}</span>
                </template>
            </Column>
            <Column field="sessionCount" header="Chats Run" sortable>
                <template #body="{ data }">
                    <span class="session-count">{{ data.sessionCount }}</span>
                </template>
            </Column>
            <Column field="hasApiKey" header="API Key Status">
                <template #body="{ data }">
                    <Tag :severity="data.hasApiKey ? 'success' : 'danger'" :value="data.hasApiKey ? 'Active' : 'Missing'" />
                </template>
            </Column>
            <Column field="createdAt" header="Provisioned" sortable>
                <template #body="{ data }">
                    {{ new Date(data.createdAt).toLocaleDateString() }}
                </template>
            </Column>
            <Column header="Actions" headerStyle="width: 12rem; text-align: center" bodyStyle="text-align: center">
                <template #body="{ data }">
                    <div class="actions-group">
                        <Button label="Get Code" icon="pi pi-code" size="small" severity="secondary" outlined @click="showSnippet(data)" />
                        <Button label="Manage" icon="pi pi-cog" size="small" severity="secondary" @click="openManageDialog(data)" />
                    </div>
                </template>
            </Column>
        </DataTable>

        <!-- Provision Tenant Dialog -->
        <Dialog v-model:visible="showProvisionDialog" header="Provision Tenant Chatbot" modal :style="{ width: '550px' }">
            <div class="dialog-form">
                <div class="form-field">
                    <label for="tenant-name">Tenant / Customer Name</label>
                    <InputText id="tenant-name" v-model="provisionForm.tenantName" placeholder="e.g., Dental Clinic Inc" class="w-full" required />
                </div>
                <div class="form-field">
                    <label for="tenant-id">Tenant Identifier (Optional)</label>
                    <InputText id="tenant-id" v-model="provisionForm.tenantIdentifier" placeholder="e.g., dental-clinic-subdomain" class="w-full" />
                    <small class="text-muted-small">A unique subdomain or client ID from your multi-tenant app to link sessions.</small>
                </div>

                <!-- Accordion for model overrides -->
                <Accordion :value="['overrides']" class="override-accordion">
                    <AccordionPanel value="overrides">
                        <AccordionHeader>LLM Config & Domain Overrides (Optional)</AccordionHeader>
                        <AccordionContent>
                            <div class="accordion-form">
                                <div class="form-grid">
                                    <div class="form-field">
                                        <label for="override-provider">LLM Provider</label>
                                        <Select id="override-provider" v-model="provisionForm.provider" :options="providerOptions" optionLabel="label" optionValue="value" placeholder="Inherit default" class="w-full" showClear />
                                    </div>
                                    <div class="form-field">
                                        <label for="override-model">LLM Model</label>
                                        <Select id="override-model" v-model="provisionForm.modelName" :options="modelOptions" optionLabel="label" optionValue="value" placeholder="Inherit default" class="w-full" :disabled="!provisionForm.provider" showClear />
                                    </div>
                                </div>
                                <div class="form-field mt-3">
                                    <label for="override-prompt">Override System Prompt</label>
                                    <Textarea id="override-prompt" v-model="provisionForm.systemPrompt" rows="3" placeholder="You are a helpful assistant..." class="w-full" />
                                </div>
                                <div class="form-field mt-3">
                                    <label for="override-domains">Whitelisted Domains</label>
                                    <InputText id="override-domains" v-model="provisionForm.allowedDomains" placeholder="e.g., app.dentalclinic.com" class="w-full" />
                                </div>
                            </div>
                        </AccordionContent>
                    </AccordionPanel>
                </Accordion>

                <!-- Embed Permissions -->
                <div class="permission-section">
                    <h3>Iframe Embed Options</h3>
                    <p class="section-desc">Toggle which setup views this customer is allowed to edit inside their embedded settings iframe.</p>
                    <div class="switches-grid">
                        <div class="switch-row">
                            <span>System Prompt Editing</span>
                            <ToggleSwitch v-model="provisionForm.showPrompt" />
                        </div>
                        <div class="switch-row">
                            <span>Knowledge Base Management</span>
                            <ToggleSwitch v-model="provisionForm.showKnowledgeBase" />
                        </div>
                        <div class="switch-row">
                            <span>Conversation Rules</span>
                            <ToggleSwitch v-model="provisionForm.showRules" />
                        </div>
                        <div class="switch-row">
                            <span>Widget Style Customization</span>
                            <ToggleSwitch v-model="provisionForm.showWidgetCustomization" />
                        </div>
                    </div>
                </div>
            </div>
            <template #footer>
                <Button label="Cancel" severity="secondary" text @click="showProvisionDialog = false" />
                <Button label="Provision Now" @click="handleProvision" />
            </template>
        </Dialog>

        <!-- Manage Tenant Dialog -->
        <Dialog v-model:visible="showManageDialog" header="Manage Tenant Permissions" modal :style="{ width: '500px' }">
            <div class="dialog-form" v-if="selectedTenant">
                <div class="selected-tenant-header">
                    <h4>{{ selectedTenant.name }}</h4>
                    <span class="tenant-id-sm font-mono">{{ selectedTenant.projectId }}</span>
                </div>

                <div class="permission-section">
                    <h3>Custom Embedded Permissions</h3>
                    <p class="section-desc">Configure what tabs this specific customer will see inside the embedded iframe.</p>
                    <div class="switches-grid">
                        <div class="switch-row">
                            <span>System Prompt Editing</span>
                            <ToggleSwitch v-model="manageEmbedSettings.showPrompt" />
                        </div>
                        <div class="switch-row">
                            <span>Knowledge Base Management</span>
                            <ToggleSwitch v-model="manageEmbedSettings.showKnowledgeBase" />
                        </div>
                        <div class="switch-row">
                            <span>Conversation Rules</span>
                            <ToggleSwitch v-model="manageEmbedSettings.showRules" />
                        </div>
                        <div class="switch-row">
                            <span>Widget Style Customization</span>
                            <ToggleSwitch v-model="manageEmbedSettings.showWidgetCustomization" />
                        </div>
                    </div>
                </div>

                <div class="danger-zone">
                    <h3>Danger Zone</h3>
                    <div class="danger-row">
                        <div class="danger-desc">
                            <strong>Delete Chatbot</strong>
                            <span>Remove this chatbot and all of its configurations permanently.</span>
                        </div>
                        <Button label="Delete" severity="danger" size="small" outlined @click="handleDeleteTenant(selectedTenant.projectId)" />
                    </div>
                </div>
            </div>
            <template #footer>
                <Button label="Cancel" severity="secondary" text @click="showManageDialog = false" />
                <Button label="Save Changes" @click="handleSaveEmbedSettings" />
            </template>
        </Dialog>

        <!-- Widget API Key Dialog (Shown Once) -->
        <Dialog v-model:visible="showKeyDialog" header="Tenant Widget API Key" modal :closable="false" :style="{ width: '500px' }">
            <div class="key-display-content" v-if="provisionedTenantDetails">
                <Message severity="warn" variant="simple" class="mb-4">This raw widget API key is only shown once. Use this key to load the chatbot widget on the client side.</Message>
                <div class="key-label-title">Widget API Key</div>
                <div class="key-box mb-4">
                    <code>{{ provisionedTenantDetails.widgetApiKey }}</code>
                    <Button icon="pi pi-copy" severity="secondary" text rounded @click="copyToClipboard(provisionedTenantDetails.widgetApiKey)" />
                </div>
                <div class="key-label-title">Project ID (Tenant ID)</div>
                <div class="key-box-small">
                    <code>{{ provisionedTenantDetails.projectId }}</code>
                    <Button icon="pi pi-copy" severity="secondary" text rounded @click="copyToClipboard(provisionedTenantDetails.projectId)" />
                </div>
            </div>
            <template #footer>
                <Button label="Done" @click="showKeyDialog = false; provisionedTenantDetails = null" />
            </template>
        </Dialog>

        <!-- Script Snippet Dialog -->
        <Dialog v-model:visible="showSnippetDialog" header="Chatbot Embed Script" modal :style="{ width: '550px' }">
            <div class="snippet-dialog-content">
                <p>Include this script tag in your client web application pages where you want the chat box widget to load.</p>
                <div class="code-box">
                    <pre>{{ currentSnippet }}</pre>
                    <Button label="Copy Snippet" icon="pi pi-copy" size="small" class="copy-snippet-btn" @click="copyToClipboard(currentSnippet)" />
                </div>
            </div>
            <template #footer>
                <Button label="Close" severity="secondary" text @click="showSnippetDialog = false" />
            </template>
        </Dialog>
    </div>
</template>

<style scoped>
.tenants-view {
    padding: 12px 0;
}
.header {
    display: flex;
    justify-content: space-between;
    align-items: flex-end;
    margin-bottom: 32px;
}
.subtitle {
    color: var(--p-surface-500);
    margin-top: 4px;
    font-size: 0.9rem;
}
.tenant-id {
    font-family: monospace;
    background: var(--p-surface-50);
    padding: 2px 6px;
    border-radius: 4px;
    border: 1px solid var(--p-surface-200);
}
.session-count {
    font-weight: 600;
}
.actions-group {
    display: flex;
    gap: 8px;
    justify-content: center;
}
.text-muted {
    color: var(--p-surface-400);
}
.text-muted-small {
    font-size: 0.75rem;
    color: var(--p-surface-400);
    margin-top: 2px;
}

/* Dialog Form */
.dialog-form {
    display: flex;
    flex-direction: column;
    gap: 16px;
    padding-top: 8px;
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

.override-accordion {
    border: 1px solid var(--p-surface-200);
    border-radius: 8px;
    margin-top: 8px;
    overflow: hidden;
}
.accordion-form {
    display: flex;
    flex-direction: column;
    gap: 12px;
    padding: 12px 4px;
}

/* Permission Section */
.permission-section {
    border-top: 1px solid var(--p-surface-200);
    padding-top: 16px;
    margin-top: 8px;
}
.permission-section h3 {
    font-size: 0.95rem;
    font-weight: 600;
    margin: 0;
}
.section-desc {
    font-size: 0.8rem;
    color: var(--p-surface-400);
    margin: 4px 0 12px 0;
}
.switches-grid {
    display: flex;
    flex-direction: column;
    gap: 12px;
}
.switch-row {
    display: flex;
    justify-content: space-between;
    align-items: center;
    background: var(--p-surface-50);
    padding: 10px 12px;
    border-radius: 8px;
    border: 1px solid var(--p-surface-200);
}
.switch-row span {
    font-size: 0.85rem;
    font-weight: 500;
    color: var(--p-surface-700);
}

/* Selected Tenant Header */
.selected-tenant-header {
    background: var(--p-surface-50);
    padding: 12px 16px;
    border-radius: 8px;
    border: 1px solid var(--p-surface-200);
    margin-bottom: 8px;
}
.selected-tenant-header h4 {
    margin: 0;
    font-size: 1rem;
}
.tenant-id-sm {
    font-size: 0.75rem;
    color: var(--p-surface-400);
    display: block;
    margin-top: 2px;
}

/* Danger Zone */
.danger-zone {
    border-top: 1px solid var(--p-red-200);
    padding-top: 16px;
    margin-top: 8px;
}
.danger-zone h3 {
    font-size: 0.95rem;
    font-weight: 600;
    color: var(--p-red-600);
    margin: 0 0 12px 0;
}
.danger-row {
    display: flex;
    justify-content: space-between;
    align-items: center;
    background: color-mix(in srgb, var(--p-red-50) 25%, transparent);
    border: 1px solid var(--p-red-200);
    padding: 12px;
    border-radius: 8px;
}
.danger-desc {
    display: flex;
    flex-direction: column;
    gap: 2px;
}
.danger-desc strong {
    font-size: 0.85rem;
    color: var(--p-red-700);
}
.danger-desc span {
    font-size: 0.75rem;
    color: var(--p-surface-500);
}

/* Key display dialog */
.key-display-content {
    display: flex;
    flex-direction: column;
}
.key-label-title {
    font-size: 0.8rem;
    font-weight: 600;
    color: var(--p-surface-500);
    margin-bottom: 4px;
}
.key-box {
    background: var(--p-surface-900);
    color: var(--p-emerald-400);
    padding: 12px 16px;
    border-radius: 8px;
    font-family: monospace;
    font-size: 0.95rem;
    word-break: break-all;
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 12px;
    border: 1px solid var(--p-surface-800);
}
.key-box-small {
    background: var(--p-surface-100);
    color: var(--p-surface-800);
    padding: 8px 12px;
    border-radius: 8px;
    font-family: monospace;
    font-size: 0.85rem;
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 12px;
    border: 1px solid var(--p-surface-200);
}

/* Snippet Dialog */
.code-box {
    position: relative;
    background: var(--p-surface-900);
    border-radius: 8px;
    padding: 16px;
    margin-top: 12px;
    border: 1px solid var(--p-surface-800);
}
.code-box pre {
    color: var(--p-surface-100);
    font-family: monospace;
    font-size: 0.8rem;
    margin: 0;
    white-space: pre-wrap;
    word-break: break-all;
}
.copy-snippet-btn {
    margin-top: 12px;
}
</style>

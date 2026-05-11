<script setup lang="ts">
import { ref, reactive, onMounted, computed } from 'vue';
import { useRoute } from 'vue-router';
import { useApi } from '../composables/useApi';
import Button from 'primevue/button';
import Card from 'primevue/card';
import Dialog from 'primevue/dialog';
import InputText from 'primevue/inputtext';
import Select from 'primevue/select';
import Textarea from 'primevue/textarea';

const route = useRoute();
const { apiFetch } = useApi();

const projectId = computed(() => route.params.id as string);
const project = ref<any>({});
const configs = ref<any[]>([]);
const keys = ref<any[]>([]);
const tools = ref<any[]>([]);

const showNewConfig = ref(false);
const showNewKey = ref(false);
const showNewTool = ref(false);
const isEditingTool = ref(false);
const editingToolId = ref<string | null>(null);
const generatedKey = ref('');

const newConfig = reactive({ name: '', systemPrompt: '' });
const newKey = reactive({ label: '', configId: null as string | null });
const newTool = reactive({ name: '', description: '', parametersJsonSchema: '{\n  "type": "object",\n  "properties": {}\n}' });

async function loadProject() {
    try {
        const res = await apiFetch(`/api/project/${projectId.value}`);
        if (res.ok) project.value = await res.json();
    } catch(e) { console.error(e); }
}

async function loadConfigs() {
    try {
        const res = await apiFetch(`/api/project/${projectId.value}/configurations`);
        if (res.ok) configs.value = await res.json();
    } catch(e) { console.error(e); }
}

async function loadKeys() {
    try {
        const res = await apiFetch(`/api/project/${projectId.value}/keys`);
        if (res.ok) keys.value = await res.json();
    } catch(e) { console.error(e); }
}

async function loadTools() {
    try {
        const res = await apiFetch(`/api/tool/project/${projectId.value}`);
        if (res.ok) tools.value = await res.json();
    } catch(e) { console.error(e); }
}

async function createConfig() {
    await apiFetch(`/api/project/${projectId.value}/configurations`, {
        method: 'POST', 
        body: JSON.stringify({ 
            name: newConfig.name, 
            systemPrompt: newConfig.systemPrompt
        })
    });
    showNewConfig.value = false;
    newConfig.name = ''; newConfig.systemPrompt = '';
    loadConfigs();
}

async function deleteConfig(id: string) {
    if (!confirm('Delete this configuration?')) return;
    await apiFetch(`/api/configuration/${id}`, { method: 'DELETE' });
    loadConfigs();
}

async function generateKey() {
    const res = await apiFetch(`/api/project/${projectId.value}/keys`, {
        method: 'POST', 
        body: JSON.stringify({ label: newKey.label, configurationId: newKey.configId })
    });
    if (res.ok) {
        const data = await res.json();
        generatedKey.value = data.key;
        loadKeys();
    }
}

async function revokeKey(id: string) {
    if (!confirm('Revoke this key?')) return;
    await apiFetch(`/api/project/keys/${id}`, { method: 'DELETE' });
    loadKeys();
}

function openNewTool() {
    isEditingTool.value = false;
    editingToolId.value = null;
    newTool.name = '';
    newTool.description = '';
    newTool.parametersJsonSchema = '{\n  "type": "object",\n  "properties": {}\n}';
    showNewTool.value = true;
}

function openEditTool(tool: any) {
    isEditingTool.value = true;
    editingToolId.value = tool.id;
    newTool.name = tool.name;
    newTool.description = tool.description;
    newTool.parametersJsonSchema = tool.parametersJsonSchema;
    showNewTool.value = true;
}

async function saveTool() {
    const payload = { 
        name: newTool.name, 
        description: newTool.description, 
        parametersJsonSchema: newTool.parametersJsonSchema,
        isActive: true
    };

    if (isEditingTool.value && editingToolId.value) {
        await apiFetch(`/api/tool/${editingToolId.value}`, {
            method: 'PUT',
            body: JSON.stringify(payload)
        });
    } else {
        await apiFetch(`/api/tool/project/${projectId.value}`, {
            method: 'POST', 
            body: JSON.stringify(payload)
        });
    }
    
    showNewTool.value = false;
    loadTools();
}

async function deleteTool(id: string) {
    if (!confirm('Delete this tool?')) return;
    await apiFetch(`/api/tool/${id}`, { method: 'DELETE' });
    loadTools();
}

async function saveProjectSettings() {
    await apiFetch(`/api/project/${projectId.value}`, {
        method: 'PUT',
        body: JSON.stringify({
            name: project.value.name,
            systemPrompt: project.value.systemPrompt,
            provider: project.value.provider,
            modelName: project.value.modelName,
            webhookUrl: project.value.webhookUrl,
            allowedDomains: project.value.allowedDomains
        })
    });
    alert('Settings saved!');
}

onMounted(() => { 
    loadProject(); 
    loadConfigs(); 
    loadKeys(); 
    loadTools();
});
</script>

<template>
    <div>
        <header class="header">
            <div>
                <router-link to="/" class="back-link">
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="19" y1="12" x2="5" y2="12"></line><polyline points="12 19 5 12 12 5"></polyline></svg>
                    Back to Projects
                </router-link>
                <h1>{{ project.name || 'Project' }}</h1>
            </div>
            <div class="header-actions">
                <Button label="Save Project Settings" icon="pi pi-save" severity="primary" @click="saveProjectSettings" />
            </div>
        </header>

        <!-- Project Settings -->
        <section class="section">
            <div class="section-header">
                <div>
                    <h2>Project Settings</h2>
                    <p class="section-subtitle">Core settings for your project integration and security.</p>
                </div>
            </div>
            <Card class="list-card settings-card">
                <template #content>
                    <div class="form-grid">
                        <div class="form-group">
                            <label>System Prompt (Base)</label>
                            <Textarea v-model="project.systemPrompt" rows="2" fluid />
                        </div>
                        <div class="form-group">
                            <label>Allowed Domains (Comma separated)</label>
                            <InputText v-model="project.allowedDomains" placeholder="localhost:5173, example.com, *" fluid />
                            <small>Use * to allow all (not recommended for production). Use comma-separated hostnames.</small>
                        </div>
                    </div>
                </template>
            </Card>
        </section>

        <!-- Configurations -->
        <section class="section">
            <div class="section-header">
                <div>
                    <h2>Configurations (Environments)</h2>
                    <p class="section-subtitle">Configurations define the persona, provider, and allowed models for your bot.</p>
                </div>
                <Button label="New Config" icon="pi pi-plus" severity="secondary" outlined @click="showNewConfig = true" />
            </div>
            
            <div v-if="configs.length" class="config-list">
                <Card v-for="c in configs" :key="c.id" class="list-card">
                    <template #content>
                        <div class="list-card-content">
                            <div class="info">
                                <h3>{{ c.name }}</h3>
                                <p class="subtitle">{{ c.defaultProvider }} / {{ c.defaultModel }}</p>
                            </div>
                            <div class="actions">
                                <span v-if="c.hasGeminiKey" class="badge badge-success">Gemini</span>
                                <span v-if="c.hasGroqKey" class="badge badge-success">Groq</span>
                                <span class="keys-count">{{ c.apiKeyCount }} keys</span>
                                <router-link :to="'/project/' + projectId + '/config/' + c.id" custom v-slot="{ navigate }">
                                    <Button label="Edit" size="small" severity="secondary" outlined @click="navigate" />
                                </router-link>
                                <Button label="Del" size="small" severity="danger" outlined @click="deleteConfig(c.id)" />
                            </div>
                        </div>
                    </template>
                </Card>
            </div>
            <p v-else class="empty-text">No configurations yet. Create one to start.</p>
        </section>

        <!-- Custom Tools -->
        <section class="section">
            <div class="section-header">
                <div>
                    <h2>Custom Tools</h2>
                    <p class="section-subtitle">Define tools that the AI can call. These can be executed via webhooks or client-side JS.</p>
                </div>
                <Button label="New Tool" icon="pi pi-plus" severity="secondary" outlined @click="openNewTool" />
            </div>
            
            <div v-if="tools.length" class="config-list">
                <Card v-for="t in tools" :key="t.id" class="list-card">
                    <template #content>
                        <div class="list-card-content">
                            <div class="info">
                                <h3>{{ t.name }}</h3>
                                <p class="subtitle">{{ t.description }}</p>
                            </div>
                            <div class="actions">
                                <Button label="Edit" size="small" severity="secondary" outlined @click="openEditTool(t)" />
                                <Button label="Del" size="small" severity="danger" outlined @click="deleteTool(t.id)" />
                            </div>
                        </div>
                    </template>
                </Card>
            </div>
            <p v-else class="empty-text">No custom tools yet.</p>
        </section>

        <!-- API Keys -->
        <section class="section">
            <div class="section-header">
                <h2>API Access Keys</h2>
                <Button label="Generate Key" severity="secondary" outlined @click="showNewKey = true; generatedKey = '';" />
            </div>
            
            <div v-if="keys.length" class="key-list">
                <Card v-for="k in keys" :key="k.id" class="list-card">
                    <template #content>
                        <div class="list-card-content">
                            <div class="info">
                                <span class="key-label">{{ k.label || 'API Key' }}</span>
                                <span v-if="k.configurationName" class="key-config">Config: {{ k.configurationName }}</span>
                            </div>
                            <div class="actions">
                                <span class="key-date">{{ new Date(k.createdAt).toLocaleDateString() }}</span>
                                <Button label="Revoke" size="small" severity="danger" outlined @click="revokeKey(k.id)" />
                            </div>
                        </div>
                    </template>
                </Card>
            </div>
            <p v-else class="empty-text">No API keys yet.</p>
        </section>

        <!-- New Config Modal -->
        <Dialog v-model:visible="showNewConfig" modal header="New Configuration" :style="{ width: '400px' }">
            <form @submit.prevent="createConfig" class="form">
                <div class="form-group">
                    <label>Name</label>
                    <InputText v-model="newConfig.name" placeholder="Production" required fluid />
                </div>
                <div class="form-group">
                    <label>System Prompt</label>
                    <Textarea v-model="newConfig.systemPrompt" rows="4" fluid />
                </div>
                <span class="info-text">You can configure API keys, models, and voice mode after creation.</span>
                <div class="dialog-actions">
                    <Button label="Cancel" severity="secondary" outlined @click="showNewConfig = false" />
                    <Button type="submit" label="Create" />
                </div>
            </form>
        </Dialog>

        <!-- New Key Modal -->
        <Dialog v-model:visible="showNewKey" modal header="Generate API Key" :style="{ width: '400px' }">
            <form @submit.prevent="generateKey" class="form">
                <div class="form-group">
                    <label>Label</label>
                    <InputText v-model="newKey.label" placeholder="Production Key" fluid />
                </div>
                <div class="form-group">
                    <label>Configuration</label>
                    <Select v-model="newKey.configId" :options="configs" optionLabel="name" optionValue="id" placeholder="Select a configuration (Required)" fluid required />
                </div>
                
                <div v-if="generatedKey" class="generated-key-container">
                    <div class="code-block">{{ generatedKey }}</div>
                    <p class="warning-text">Copy this now — you won't see it again.</p>
                </div>
                
                <div class="dialog-actions">
                    <Button label="Close" severity="secondary" outlined @click="showNewKey = false" />
                    <Button type="submit" label="Generate" :disabled="!!generatedKey" />
                </div>
            </form>
        </Dialog>

        <!-- Tool Modal -->
        <Dialog v-model:visible="showNewTool" modal :header="isEditingTool ? 'Edit Custom Tool' : 'New Custom Tool'" :style="{ width: '600px' }">
            <form @submit.prevent="saveTool" class="form">
                <div class="form-group">
                    <label>Tool Name</label>
                    <InputText v-model="newTool.name" placeholder="e.g. check_inventory" required fluid />
                </div>
                <div class="form-group">
                    <label>Description</label>
                    <InputText v-model="newTool.description" placeholder="e.g. Checks inventory for a given product id" required fluid />
                </div>
                <div class="form-group">
                    <label>Parameters JSON Schema</label>
                    <Textarea v-model="newTool.parametersJsonSchema" rows="8" style="font-family: monospace;" required fluid />
                </div>
                <div class="dialog-actions">
                    <Button label="Cancel" severity="secondary" outlined @click="showNewTool = false" />
                    <Button type="submit" :label="isEditingTool ? 'Save Changes' : 'Create'" />
                </div>
            </form>
        </Dialog>
    </div>
</template>

<style scoped>
.header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 48px;
}
.header-actions {
    display: flex;
    gap: 12px;
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
.section-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 24px;
}
.list-card {
    background-color: var(--p-surface-0);
    border: 1px solid var(--p-surface-200);
    margin-bottom: 12px;
}
.settings-card {
    padding: 24px;
}
.form-grid {
    display: grid;
    grid-template-columns: 1fr;
    gap: 24px;
}
@media (min-width: 768px) {
    .form-grid {
        grid-template-columns: 1fr 1fr;
    }
}
:deep(.p-card-content) {
    padding: 0;
}
.list-card-content {
    display: flex;
    align-items: center;
    justify-content: space-between;
}
.list-card-content h3 {
    font-size: 1rem;
    margin: 0;
    color: var(--p-surface-900);
}
.subtitle {
    color: var(--p-surface-500);
    font-size: 0.85rem;
    margin: 4px 0 0 0;
}
.section-subtitle {
    color: var(--p-surface-500);
    font-size: 0.85rem;
    margin: 4px 0 0 0;
}
.actions {
    display: flex;
    align-items: center;
    gap: 16px;
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
.keys-count {
    font-size: 0.8rem;
    color: var(--p-surface-500);
}
.empty-text {
    color: var(--p-surface-500);
}
.key-label {
    font-weight: 700;
    font-size: 0.85rem;
    color: var(--p-primary-500);
    text-transform: uppercase;
}
.key-config {
    margin-left: 16px;
    font-size: 0.75rem;
    color: var(--p-surface-500);
}
.key-date {
    font-size: 0.8rem;
    color: var(--p-surface-500);
}

.form {
    display: flex;
    flex-direction: column;
    gap: 16px;
}
.form-group {
    display: flex;
    flex-direction: column;
    gap: 8px;
}
.form-group label {
    font-weight: 500;
    font-size: 0.9rem;
    color: var(--p-surface-700);
}
.form-group small {
    font-size: 0.75rem;
    color: var(--p-surface-500);
}
.dialog-actions {
    display: flex;
    justify-content: flex-end;
    gap: 12px;
    margin-top: 24px;
}
.generated-key-container {
    margin-top: 16px;
}
.code-block {
    padding: 16px;
    border: 1px dashed var(--p-surface-300);
    border-radius: 8px;
    color: var(--p-primary-500);
    background: var(--p-surface-50);
    word-break: break-all;
    font-family: 'JetBrains Mono', monospace;
}
.warning-text {
    color: var(--p-surface-500);
    font-size: 0.8rem;
    margin-top: 8px;
}
.info-text {
    color: var(--p-surface-500);
    font-size: 0.8rem;
    margin-top: 4px;
}
</style>

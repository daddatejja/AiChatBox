<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue';
import { useApi } from '../composables/useApi';
import Button from 'primevue/button';
import Card from 'primevue/card';
import Dialog from 'primevue/dialog';
import InputText from 'primevue/inputtext';
import { useConfirm } from 'primevue/useconfirm';
import { useToast } from 'primevue/usetoast';

const { apiFetch } = useApi();
const confirm = useConfirm();
const toast = useToast();

const projects = ref<any[]>([]);
const loading = ref(true);
const showCreate = ref(false);

const form = reactive({ name: '' });
const selectedStarter = ref('support');

const starterTemplates = [
    { id: 'support', label: 'Customer Support', name: 'Support Assistant', prompt: 'Track order status and handle common support requests.' },
    { id: 'sales', label: 'Sales Assistant', name: 'Sales Copilot', prompt: 'Answer pricing and product-fit questions for prospects.' },
    { id: 'ops', label: 'Internal Ops', name: 'Ops Helper', prompt: 'Help teammates with internal process and policy questions.' }
];

async function load() {
    loading.value = true;
    try {
        const res = await apiFetch('/api/project');
        if (res.ok) {
            projects.value = await res.json();
        }
    } catch (e) {
        console.error(e);
    }
    loading.value = false;
}

async function createProject() {
    const res = await apiFetch('/api/project', {
        method: 'POST',
        body: JSON.stringify(form)
    });
    if (res.ok) {
        showCreate.value = false;
        form.name = ''; 
        load();
    }
}

function applyStarter(templateId: string) {
    const template = starterTemplates.find(t => t.id === templateId);
    if (!template) return;
    selectedStarter.value = template.id;
    form.name = template.name;
}

async function deleteProject(id: string) {
    confirm.require({
        message: 'Delete this project? All associated configurations, keys, and documents will be lost.',
        header: 'Confirm Project Deletion',
        icon: 'pi pi-exclamation-triangle',
        rejectProps: {
            label: 'Cancel',
            severity: 'secondary',
            outlined: true
        },
        acceptProps: {
            label: 'Delete',
            severity: 'danger'
        },
        accept: async () => {
            await apiFetch(`/api/project/${id}`, { method: 'DELETE' });
            toast.add({ severity: 'success', summary: 'Deleted', detail: 'Project removed successfully.', life: 3000 });
            load();
        }
    });
}

onMounted(load);
</script>

<template>
    <div>
        <header class="header">
            <div>
                <h1>My Projects</h1>
                <p class="subtitle">Organize your AI assistants into projects</p>
            </div>
            <Button label="Create Project" icon="pi pi-plus" @click="showCreate = true" />
        </header>

        <div v-if="loading" class="loading">Loading...</div>
        
        <div v-else class="grid">
            <Card v-for="p in projects" :key="p.id" class="project-card">
                <template #title>
                    <div class="card-header">
                        <h3>{{ p.name }}</h3>
                    </div>
                </template>
                <template #content>
                    <div class="meta-info">
                        <span>{{ p.apiKeyCount }} API Keys</span>
                        <span>Created {{ new Date(p.createdAt).toLocaleDateString() }}</span>
                    </div>
                </template>
                <template #footer>
                    <div class="card-actions">
                        <router-link :to="'/project/' + p.id" custom v-slot="{ navigate }">
                            <Button label="Configure" severity="secondary" outlined @click="navigate" class="action-btn" />
                        </router-link>
                        <Button icon="pi pi-trash" severity="danger" outlined @click="deleteProject(p.id)" />
                    </div>
                </template>
            </Card>

            <div v-if="projects.length === 0" class="empty-state">
                <p>No projects yet. Create your first one!</p>
            </div>
        </div>

        <Dialog v-model:visible="showCreate" modal header="Create New Project" :style="{ width: '520px' }">
            <p class="dialog-subtitle">Pick a starter, then create. You can fully customize everything after setup.</p>
            <form @submit.prevent="createProject" class="form">
                <div class="form-group">
                    <label>Starter Template</label>
                    <div class="starter-grid">
                        <button
                            v-for="starter in starterTemplates"
                            :key="starter.id"
                            type="button"
                            class="starter-card"
                            :class="{ active: selectedStarter === starter.id }"
                            @click="applyStarter(starter.id)"
                        >
                            <strong>{{ starter.label }}</strong>
                            <span>{{ starter.prompt }}</span>
                        </button>
                    </div>
                </div>
                <div class="form-group">
                    <label for="name">Project Name</label>
                    <InputText id="name" v-model="form.name" placeholder="E.g., Customer Support Bot" required fluid />
                </div>
                <div class="wizard-checklist">
                    <h4>What happens next</h4>
                    <ul>
                        <li>Add your first configuration and model</li>
                        <li>Generate an API key for the widget</li>
                        <li>Run one test conversation in Playground</li>
                    </ul>
                </div>
                <div class="dialog-actions">
                    <Button label="Cancel" severity="secondary" outlined @click="showCreate = false" />
                    <Button type="submit" label="Create Project" />
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
    margin-bottom: 32px;
}
.subtitle {
    color: var(--p-surface-400);
    margin-top: 4px;
}
.loading, .empty-state {
    text-align: center;
    padding: 64px;
    color: var(--p-surface-400);
}
.grid {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(320px, 1fr));
    gap: 24px;
}
.project-card {
    background-color: var(--p-surface-0);
    border: 1px solid var(--p-surface-200);
}
.card-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
}
.card-header h3 {
    font-size: 1.1rem;
    color: var(--p-surface-900);
    margin: 0;
}
.provider-badge {
    font-size: 0.7rem;
    font-weight: 700;
    color: var(--p-primary-400);
    background-color: var(--p-surface-100);
    padding: 4px 10px;
    border-radius: 20px;
    text-transform: uppercase;
}
.system-prompt {
    color: var(--p-surface-500);
    font-size: 0.85rem;
    margin-bottom: 1.5rem;
    height: 2.6rem;
    overflow: hidden;
    display: -webkit-box;
    line-clamp: 2;
    -webkit-line-clamp: 2;
    -webkit-box-orient: vertical;
}
.meta-info {
    display: flex;
    gap: 1rem;
    font-size: 0.8rem;
    color: var(--p-surface-500);
    margin-bottom: 1.5rem;
}
.card-actions {
    display: flex;
    gap: 12px;
}
.action-btn {
    flex: 1;
}
.dialog-subtitle {
    color: var(--p-surface-500);
    margin-bottom: 32px;
    margin-top: 0;
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
.dialog-actions {
    display: flex;
    justify-content: flex-end;
    gap: 12px;
    margin-top: 24px;
}
.starter-grid {
    display: grid;
    grid-template-columns: 1fr;
    gap: 8px;
}
.starter-card {
    border: 1px solid var(--p-surface-300);
    background: var(--p-surface-0);
    border-radius: 10px;
    padding: 10px 12px;
    text-align: left;
    cursor: pointer;
    display: flex;
    flex-direction: column;
    gap: 4px;
}
.starter-card.active {
    border-color: var(--p-primary-400);
    background: var(--p-primary-50);
}
.starter-card span {
    font-size: 0.8rem;
    color: var(--p-surface-500);
}
.wizard-checklist {
    background: var(--p-surface-50);
    border: 1px dashed var(--p-surface-300);
    border-radius: 10px;
    padding: 12px;
}
.wizard-checklist h4 {
    margin: 0 0 8px;
    font-size: 0.85rem;
}
.wizard-checklist ul {
    margin: 0;
    padding-left: 18px;
    font-size: 0.8rem;
    color: var(--p-surface-600);
}

/* ── Mobile Responsive ── */
@media (max-width: 768px) {
    .header {
        flex-direction: column;
        align-items: stretch;
        gap: 16px;
    }
    .grid {
        grid-template-columns: 1fr;
    }
}
</style>

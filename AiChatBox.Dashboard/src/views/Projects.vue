<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue';
import { useApi } from '../composables/useApi';
import Button from 'primevue/button';
import Card from 'primevue/card';
import Dialog from 'primevue/dialog';
import InputText from 'primevue/inputtext';

const { apiFetch } = useApi();

const projects = ref<any[]>([]);
const loading = ref(true);
const showCreate = ref(false);

const form = reactive({ name: '' });

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

async function deleteProject(id: string) {
    if (!confirm('Delete this project?')) return;
    await apiFetch(`/api/project/${id}`, { method: 'DELETE' });
    load();
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

        <Dialog v-model:visible="showCreate" modal header="Create New Project" :style="{ width: '400px' }">
            <p class="dialog-subtitle">Projects are containers for your different bot configurations and API keys.</p>
            <form @submit.prevent="createProject" class="form">
                <div class="form-group">
                    <label for="name">Project Name</label>
                    <InputText id="name" v-model="form.name" placeholder="E.g., Customer Support Bot" required fluid />
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

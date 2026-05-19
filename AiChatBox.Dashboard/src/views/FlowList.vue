<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useApi } from '../composables/useApi';

const route = useRoute();
const router = useRouter();
const { apiFetch } = useApi();

const projectId = route.params.id as string;

interface FlowSummary {
  id: string;
  name: string;
  description: string | null;
  triggerKeyword: string | null;
  isActive: boolean;
  createdAt: string;
}

const flows = ref<FlowSummary[]>([]);
const loading = ref(true);
const creating = ref(false);
const deletingId = ref<string | null>(null);
const showNewDialog = ref(false);
const newFlowName = ref('');
const newFlowTrigger = ref('');

const loadFlows = async () => {
  loading.value = true;
  try {
    const res = await apiFetch(`/api/projects/${projectId}/flows`);
    if (res.ok) flows.value = await res.json();
  } catch (e) {
    console.error(e);
  } finally {
    loading.value = false;
  }
};

const openEditor = (flowId: string) => {
  router.push(`/project/${projectId}/flow/${flowId}`);
};

const createFlow = async () => {
  if (!newFlowName.value.trim()) return;
  creating.value = true;
  try {
    const res = await apiFetch(`/api/projects/${projectId}/flows`, {
      method: 'POST',
      body: JSON.stringify({ name: newFlowName.value, triggerKeyword: newFlowTrigger.value, isActive: true, nodes: [], edges: [] })
    });
    if (res.ok) {
      const created = await res.json();
      showNewDialog.value = false;
      newFlowName.value = '';
      newFlowTrigger.value = '';
      router.push(`/project/${projectId}/flow/${created.id}`);
    }
  } catch (e) {
    console.error(e);
  } finally {
    creating.value = false;
  }
};

const toggleActive = async (flow: FlowSummary) => {
  try {
    const res = await apiFetch(`/api/projects/${projectId}/flows/${flow.id}`, {
      method: 'PUT',
      body: JSON.stringify({ name: flow.name, description: flow.description, triggerKeyword: flow.triggerKeyword, isActive: !flow.isActive, nodes: [], edges: [] })
    });
    if (res.ok) flow.isActive = !flow.isActive;
  } catch (e) {
    console.error(e);
  }
};

const duplicateFlow = async (flow: FlowSummary) => {
  try {
    // Fetch full detail first
    const detRes = await apiFetch(`/api/projects/${projectId}/flows/${flow.id}`);
    if (!detRes.ok) return;
    const detail = await detRes.json();

    const res = await apiFetch(`/api/projects/${projectId}/flows`, {
      method: 'POST',
      body: JSON.stringify({
        name: `${flow.name} (Copy)`,
        description: flow.description,
        triggerKeyword: flow.triggerKeyword,
        isActive: false,
        nodes: detail.nodes,
        edges: detail.edges
      })
    });
    if (res.ok) await loadFlows();
  } catch (e) {
    console.error(e);
  }
};

const deleteFlow = async (id: string) => {
  if (!confirm('Delete this flow? This cannot be undone.')) return;
  deletingId.value = id;
  try {
    const res = await apiFetch(`/api/projects/${projectId}/flows/${id}`, { method: 'DELETE' });
    if (res.ok) {
      flows.value = flows.value.filter(f => f.id !== id);
    }
  } catch (e) {
    console.error(e);
  } finally {
    deletingId.value = null;
  }
};

const formatDate = (iso: string) => new Date(iso).toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' });

onMounted(loadFlows);
</script>

<template>
  <div class="flow-list-page">
    <!-- Header -->
    <header class="fl-header">
      <div class="fl-header-left">
        <router-link :to="`/project/${projectId}`" class="fl-back">
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5">
            <line x1="19" y1="12" x2="5" y2="12" /><polyline points="12 19 5 12 12 5" />
          </svg>
          Project
        </router-link>
        <span class="fl-divider">/</span>
        <div class="fl-title-row">
          <i class="pi pi-sitemap"></i>
          <h1>Flow Builder</h1>
        </div>
      </div>
      <button class="fl-new-btn" @click="showNewDialog = true">
        <i class="pi pi-plus"></i> New Flow
      </button>
    </header>

    <!-- Loading -->
    <div v-if="loading" class="fl-loading">
      <i class="pi pi-spin pi-spinner"></i>
      <span>Loading flows…</span>
    </div>

    <!-- Empty state -->
    <div v-else-if="flows.length === 0" class="fl-empty">
      <div class="fl-empty-icon"><i class="pi pi-sitemap"></i></div>
      <h2>No flows yet</h2>
      <p>Flows let you build deterministic, branching conversation paths without code.</p>
      <button class="fl-new-btn" @click="showNewDialog = true">
        <i class="pi pi-plus"></i> Create First Flow
      </button>
    </div>

    <!-- Flow Grid -->
    <div v-else class="fl-grid">
      <div
        v-for="flow in flows"
        :key="flow.id"
        class="fl-card"
        @click="openEditor(flow.id)"
      >
        <!-- Card Header -->
        <div class="fl-card-header">
          <div class="fl-card-icon-wrap">
            <i class="pi pi-sitemap"></i>
          </div>
          <div class="fl-card-meta">
            <h3 class="fl-card-name">{{ flow.name }}</h3>
            <span v-if="flow.triggerKeyword" class="fl-trigger-chip">
              <i class="pi pi-key"></i> {{ flow.triggerKeyword }}
            </span>
            <span v-else class="fl-trigger-chip muted">No trigger</span>
          </div>
          <div
            class="fl-status-dot"
            :class="flow.isActive ? 'active' : 'inactive'"
            :title="flow.isActive ? 'Active' : 'Inactive'"
          ></div>
        </div>

        <!-- Description -->
        <p v-if="flow.description" class="fl-desc">{{ flow.description }}</p>

        <!-- Footer -->
        <div class="fl-card-footer" @click.stop>
          <span class="fl-date">{{ formatDate(flow.createdAt) }}</span>
          <div class="fl-actions">
            <button
              class="fl-action-btn toggle"
              :class="{ on: flow.isActive }"
              @click="toggleActive(flow)"
              :title="flow.isActive ? 'Deactivate' : 'Activate'"
            >
              <i :class="flow.isActive ? 'pi pi-pause-circle' : 'pi pi-play-circle'"></i>
              {{ flow.isActive ? 'Active' : 'Inactive' }}
            </button>
            <button class="fl-action-btn" @click="duplicateFlow(flow)" title="Duplicate">
              <i class="pi pi-copy"></i>
            </button>
            <button
              class="fl-action-btn danger"
              @click="deleteFlow(flow.id)"
              :disabled="deletingId === flow.id"
              title="Delete"
            >
              <i class="pi pi-trash"></i>
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- New Flow Dialog -->
    <Transition name="dialog-fade">
      <div v-if="showNewDialog" class="fl-dialog-overlay" @click.self="showNewDialog = false">
        <div class="fl-dialog">
          <div class="fl-dialog-header">
            <i class="pi pi-sitemap"></i>
            <h2>New Flow</h2>
          </div>
          <div class="fl-dialog-body">
            <div class="fl-field">
              <label>Flow Name <span class="req">*</span></label>
              <input v-model="newFlowName" placeholder="e.g. Support Onboarding" @keydown.enter="createFlow" autofocus />
            </div>
            <div class="fl-field">
              <label>Trigger Keyword <span class="hint">(optional)</span></label>
              <input v-model="newFlowTrigger" placeholder="e.g. support" @keydown.enter="createFlow" />
              <small>The word or phrase that auto-starts this flow during chat.</small>
            </div>
          </div>
          <div class="fl-dialog-footer">
            <button class="fl-cancel-btn" @click="showNewDialog = false">Cancel</button>
            <button class="fl-create-btn" :disabled="!newFlowName.trim() || creating" @click="createFlow">
              <i class="pi pi-plus"></i>
              {{ creating ? 'Creating…' : 'Create & Edit' }}
            </button>
          </div>
        </div>
      </div>
    </Transition>
  </div>
</template>

<style scoped>
.flow-list-page {
  min-height: 100vh;
  background: var(--p-surface-0);
  color: var(--p-text-color);
  padding: 0 0 48px;
}

/* ── Header ── */
.fl-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 20px 32px;
  background: var(--p-surface-card);
  border-bottom: 1px solid var(--p-content-border-color);
  position: sticky;
  top: 0;
  z-index: 10;
}
.fl-header-left { display: flex; align-items: center; gap: 10px; }
.fl-back {
  display: flex; align-items: center; gap: 6px;
  color: var(--p-text-muted-color);
  text-decoration: none; font-size: 0.85rem;
  transition: color 0.15s;
}
.fl-back:hover { color: var(--p-primary-color); }
.fl-divider { color: var(--p-text-muted-color); font-size: 1.2rem; }
.fl-title-row { display: flex; align-items: center; gap: 8px; }
.fl-title-row i { color: var(--p-primary-color); font-size: 1.1rem; }
.fl-title-row h1 { margin: 0; font-size: 1.1rem; font-weight: 600; }

.fl-new-btn {
  display: flex; align-items: center; gap: 6px;
  background: var(--p-primary-color);
  color: var(--p-primary-contrast-color);
  border: none; border-radius: 8px;
  padding: 8px 16px; font-size: 0.85rem; font-weight: 600;
  cursor: pointer; transition: opacity 0.15s, transform 0.1s;
}
.fl-new-btn:hover { opacity: 0.88; transform: translateY(-1px); }

/* ── States ── */
.fl-loading {
  display: flex; align-items: center; gap: 12px;
  justify-content: center; padding: 80px 0;
  color: var(--p-text-muted-color); font-size: 0.95rem;
}
.fl-loading i { font-size: 1.4rem; color: var(--p-primary-color); }

.fl-empty {
  display: flex; flex-direction: column; align-items: center;
  gap: 16px; padding: 100px 24px; text-align: center;
}
.fl-empty-icon {
  width: 64px; height: 64px; border-radius: 16px;
  background: color-mix(in srgb, var(--p-primary-color) 10%, transparent);
  display: flex; align-items: center; justify-content: center;
}
.fl-empty-icon i { font-size: 1.8rem; color: var(--p-primary-color); }
.fl-empty h2 { margin: 0; font-size: 1.3rem; }
.fl-empty p { color: var(--p-text-muted-color); max-width: 400px; margin: 0; }

/* ── Grid ── */
.fl-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(320px, 1fr));
  gap: 20px;
  padding: 28px 32px;
}

/* ── Card ── */
.fl-card {
  background: var(--p-surface-card);
  border: 1px solid var(--p-content-border-color);
  border-radius: 14px;
  padding: 20px;
  cursor: pointer;
  transition: border-color 0.2s, box-shadow 0.2s, transform 0.15s;
  display: flex; flex-direction: column; gap: 12px;
}
.fl-card:hover {
  border-color: var(--p-primary-color);
  box-shadow: 0 0 0 1px var(--p-primary-color), 0 8px 24px color-mix(in srgb, var(--p-primary-color) 12%, transparent);
  transform: translateY(-2px);
}

.fl-card-header {
  display: flex; align-items: flex-start; gap: 12px;
}
.fl-card-icon-wrap {
  width: 40px; height: 40px; border-radius: 10px; flex-shrink: 0;
  background: color-mix(in srgb, var(--p-primary-color) 12%, transparent);
  display: flex; align-items: center; justify-content: center;
}
.fl-card-icon-wrap i { color: var(--p-primary-color); font-size: 1rem; }

.fl-card-meta { flex: 1; min-width: 0; }
.fl-card-name {
  margin: 0 0 6px; font-size: 0.95rem; font-weight: 600;
  white-space: nowrap; overflow: hidden; text-overflow: ellipsis;
}
.fl-trigger-chip {
  display: inline-flex; align-items: center; gap: 4px;
  background: color-mix(in srgb, var(--p-primary-color) 10%, transparent);
  color: var(--p-primary-color);
  border-radius: 20px; padding: 2px 8px;
  font-size: 0.72rem; font-weight: 600;
}
.fl-trigger-chip.muted {
  background: var(--p-surface-section);
  color: var(--p-text-muted-color);
}

.fl-status-dot {
  width: 9px; height: 9px; border-radius: 50%; flex-shrink: 0;
  margin-top: 4px;
}
.fl-status-dot.active { background: #22c55e; box-shadow: 0 0 6px #22c55e80; }
.fl-status-dot.inactive { background: var(--p-text-muted-color); opacity: 0.5; }

.fl-desc {
  font-size: 0.82rem; color: var(--p-text-muted-color);
  margin: 0; line-height: 1.4;
  display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden;
}

.fl-card-footer {
  display: flex; align-items: center; justify-content: space-between;
  padding-top: 12px; border-top: 1px solid var(--p-content-border-color);
  margin-top: auto;
}
.fl-date { font-size: 0.75rem; color: var(--p-text-muted-color); }
.fl-actions { display: flex; align-items: center; gap: 4px; }

.fl-action-btn {
  display: flex; align-items: center; gap: 5px;
  background: transparent;
  border: 1px solid var(--p-content-border-color);
  border-radius: 7px; padding: 5px 10px;
  font-size: 0.75rem; color: var(--p-text-muted-color);
  cursor: pointer; transition: all 0.15s;
}
.fl-action-btn:hover {
  background: var(--p-surface-section);
  color: var(--p-text-color);
}
.fl-action-btn.toggle.on {
  border-color: #22c55e40;
  color: #22c55e;
  background: color-mix(in srgb, #22c55e 8%, transparent);
}
.fl-action-btn.danger:hover { border-color: #ef4444; color: #ef4444; background: #ef444410; }
.fl-action-btn:disabled { opacity: 0.4; cursor: not-allowed; }

/* ── Dialog ── */
.fl-dialog-overlay {
  position: fixed; inset: 0;
  background: color-mix(in srgb, var(--p-surface-0) 40%, transparent);
  backdrop-filter: blur(6px);
  display: flex; align-items: center; justify-content: center;
  z-index: 1000;
}
.fl-dialog {
  background: var(--p-surface-card);
  border: 1px solid var(--p-content-border-color);
  border-radius: 16px; width: 420px; max-width: 95vw;
  box-shadow: 0 24px 60px rgba(0,0,0,0.18);
}
.fl-dialog-header {
  display: flex; align-items: center; gap: 10px;
  padding: 20px 24px 0;
}
.fl-dialog-header i { color: var(--p-primary-color); font-size: 1.1rem; }
.fl-dialog-header h2 { margin: 0; font-size: 1.05rem; font-weight: 600; }

.fl-dialog-body { padding: 20px 24px; display: flex; flex-direction: column; gap: 16px; }
.fl-field { display: flex; flex-direction: column; gap: 6px; }
.fl-field label { font-size: 0.8rem; font-weight: 600; text-transform: uppercase; letter-spacing: 0.04em; color: var(--p-text-muted-color); }
.fl-field .req { color: #ef4444; }
.fl-field .hint { font-weight: 400; text-transform: none; letter-spacing: 0; }
.fl-field input {
  background: var(--p-surface-section);
  border: 1px solid var(--p-content-border-color);
  border-radius: 8px; padding: 9px 12px;
  color: var(--p-text-color); font-size: 0.88rem;
  transition: border-color 0.15s;
  outline: none;
}
.fl-field input:focus { border-color: var(--p-primary-color); }
.fl-field small { font-size: 0.75rem; color: var(--p-text-muted-color); }

.fl-dialog-footer {
  display: flex; justify-content: flex-end; gap: 10px;
  padding: 16px 24px; border-top: 1px solid var(--p-content-border-color);
}
.fl-cancel-btn {
  background: transparent; border: 1px solid var(--p-content-border-color);
  border-radius: 8px; padding: 8px 16px; font-size: 0.85rem;
  color: var(--p-text-muted-color); cursor: pointer; transition: all 0.15s;
}
.fl-cancel-btn:hover { background: var(--p-surface-section); }
.fl-create-btn {
  display: flex; align-items: center; gap: 6px;
  background: var(--p-primary-color); color: var(--p-primary-contrast-color);
  border: none; border-radius: 8px; padding: 8px 18px;
  font-size: 0.85rem; font-weight: 600; cursor: pointer;
  transition: opacity 0.15s;
}
.fl-create-btn:disabled { opacity: 0.5; cursor: not-allowed; }
.fl-create-btn:hover:not(:disabled) { opacity: 0.88; }

/* ── Transitions ── */
.dialog-fade-enter-active, .dialog-fade-leave-active { transition: opacity 0.2s; }
.dialog-fade-enter-from, .dialog-fade-leave-to { opacity: 0; }
</style>

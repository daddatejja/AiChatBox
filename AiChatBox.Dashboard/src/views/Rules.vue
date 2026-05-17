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
import Dialog from 'primevue/dialog';
import InputNumber from 'primevue/inputnumber';

const route = useRoute();
const { apiFetch } = useApi();
const projectId = computed(() => route.params.projectId as string);

interface Rule {
    id: string;
    type: string;
    trigger: string;
    response: string;
    priority: number;
    isActive: boolean;
    createdAt: string;
}

const rules = ref<Rule[]>([]);
const loading = ref(false);
const showDialog = ref(false);
const editing = ref<string | null>(null);
const testMessage = ref('');
const testResult = ref<{ matched: boolean; response: string | null } | null>(null);
const testing = ref(false);

const form = reactive({
    type: 'keyword',
    trigger: '',
    response: '',
    priority: 0,
    isActive: true
});

const typeOptions = [
    { label: 'Keyword Match', value: 'keyword', desc: 'Matches when ALL comma-separated keywords appear in the message' },
    { label: 'Exact Match', value: 'exact', desc: 'Matches the entire message exactly (case-insensitive)' },
    { label: 'Regex Pattern', value: 'regex', desc: 'Matches using a regular expression pattern' }
];

const selectedTypeDesc = computed(() => {
    return typeOptions.find(t => t.value === form.type)?.desc || '';
});

async function loadRules() {
    loading.value = true;
    const res = await apiFetch(`/api/rules/project/${projectId.value}`);
    if (res.ok) rules.value = await res.json();
    loading.value = false;
}

function openCreate() {
    editing.value = null;
    form.type = 'keyword';
    form.trigger = '';
    form.response = '';
    form.priority = 0;
    form.isActive = true;
    showDialog.value = true;
}

function openEdit(rule: Rule) {
    editing.value = rule.id;
    form.type = rule.type;
    form.trigger = rule.trigger;
    form.response = rule.response;
    form.priority = rule.priority;
    form.isActive = rule.isActive;
    showDialog.value = true;
}

async function saveRule() {
    if (editing.value) {
        await apiFetch(`/api/rules/${editing.value}`, {
            method: 'PUT',
            body: JSON.stringify(form)
        });
    } else {
        await apiFetch(`/api/rules/project/${projectId.value}`, {
            method: 'POST',
            body: JSON.stringify(form)
        });
    }
    showDialog.value = false;
    await loadRules();
}

async function deleteRule(ruleId: string) {
    if (!confirm('Delete this rule?')) return;
    await apiFetch(`/api/rules/${ruleId}`, { method: 'DELETE' });
    await loadRules();
}

async function toggleActive(rule: Rule) {
    await apiFetch(`/api/rules/${rule.id}`, {
        method: 'PUT',
        body: JSON.stringify({ isActive: !rule.isActive })
    });
    await loadRules();
}

async function testRules() {
    if (!testMessage.value.trim()) return;
    testing.value = true;
    testResult.value = null;
    const res = await apiFetch(`/api/rules/project/${projectId.value}/test`, {
        method: 'POST',
        body: JSON.stringify({ message: testMessage.value })
    });
    if (res.ok) testResult.value = await res.json();
    testing.value = false;
}

function triggerPlaceholder(type: string): string {
    switch (type) {
        case 'keyword': return 'pricing, plans (comma-separated, ALL must match)';
        case 'exact': return 'What are your business hours?';
        case 'regex': return '\\b(refund|return)\\b';
        default: return '';
    }
}

onMounted(loadRules);
</script>

<template>
    <div>
        <header class="header">
            <div>
                <router-link :to="'/project/' + projectId" class="back-link">
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="19" y1="12" x2="5" y2="12"></line><polyline points="12 19 5 12 12 5"></polyline></svg>
                    Back to Project
                </router-link>
                <h1>Conversation Rules</h1>
                <p class="subtitle">Define rules to auto-respond to common queries without calling an LLM — zero cost, instant replies.</p>
            </div>
        </header>

        <!-- Test Panel -->
        <Card class="test-card">
            <template #content>
                <div class="test-panel">
                    <div class="test-input-row">
                        <InputText v-model="testMessage" placeholder="Test a message against your rules..." fluid class="flex-1" @keyup.enter="testRules" />
                        <Button :label="testing ? 'Testing...' : 'Test'" icon="pi pi-play" @click="testRules" :disabled="testing || !testMessage.trim()" />
                    </div>
                    <div v-if="testResult" class="test-result" :class="testResult.matched ? 'matched' : 'no-match'">
                        <div class="test-result-header">
                            <svg v-if="testResult.matched" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"/><polyline points="22 4 12 14.01 9 11.01"/></svg>
                            <svg v-else width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><line x1="15" y1="9" x2="9" y2="15"/><line x1="9" y1="9" x2="15" y2="15"/></svg>
                            <span>{{ testResult.matched ? 'Rule matched!' : 'No rule matched — message will be sent to LLM' }}</span>
                        </div>
                        <div v-if="testResult.matched && testResult.response" class="test-response">
                            {{ testResult.response }}
                        </div>
                    </div>
                </div>
            </template>
        </Card>

        <!-- Rules List -->
        <div class="rules-header">
            <h2 class="section-title">Active Rules ({{ rules.length }})</h2>
            <Button label="Add Rule" icon="pi pi-plus" @click="openCreate" />
        </div>

        <div v-if="loading" class="loading">Loading rules...</div>

        <div v-else-if="rules.length === 0" class="empty-state">
            <svg width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="16" y1="13" x2="8" y2="13"/><line x1="16" y1="17" x2="8" y2="17"/><polyline points="10 9 9 9 8 9"/></svg>
            <h3>No rules yet</h3>
            <p>Create rules to auto-respond to common queries like FAQs, greetings, and business hours — saving LLM costs.</p>
            <Button label="Create First Rule" icon="pi pi-plus" @click="openCreate" />
        </div>

        <Card v-for="rule in rules" :key="rule.id" class="rule-card" :class="{ inactive: !rule.isActive }">
            <template #content>
                <div class="rule-row">
                    <div class="rule-info">
                        <div class="rule-meta">
                            <span class="rule-type" :class="'type-' + rule.type">{{ rule.type }}</span>
                            <span class="rule-priority">Priority: {{ rule.priority }}</span>
                            <span v-if="!rule.isActive" class="rule-disabled">Disabled</span>
                        </div>
                        <div class="rule-trigger">
                            <strong>Trigger:</strong> <code>{{ rule.trigger }}</code>
                        </div>
                        <div class="rule-response-preview">
                            <strong>Response:</strong> {{ rule.response.length > 120 ? rule.response.substring(0, 120) + '...' : rule.response }}
                        </div>
                    </div>
                    <div class="rule-actions">
                        <Button icon="pi pi-power-off" :severity="rule.isActive ? 'secondary' : 'success'" text rounded @click="toggleActive(rule)" v-tooltip="rule.isActive ? 'Disable' : 'Enable'" />
                        <Button icon="pi pi-pencil" severity="secondary" text rounded @click="openEdit(rule)" v-tooltip="'Edit'" />
                        <Button icon="pi pi-trash" severity="danger" text rounded @click="deleteRule(rule.id)" v-tooltip="'Delete'" />
                    </div>
                </div>
            </template>
        </Card>

        <!-- Create/Edit Dialog -->
        <Dialog v-model:visible="showDialog" :header="editing ? 'Edit Rule' : 'Create Rule'" modal :style="{ width: '600px' }">
            <div class="dialog-form">
                <div class="form-group">
                    <label>Match Type</label>
                    <Select v-model="form.type" :options="typeOptions" optionLabel="label" optionValue="value" fluid />
                    <small class="info-text">{{ selectedTypeDesc }}</small>
                </div>

                <div class="form-group">
                    <label>Trigger</label>
                    <InputText v-model="form.trigger" :placeholder="triggerPlaceholder(form.type)" fluid />
                </div>

                <div class="form-group">
                    <label>Response</label>
                    <Textarea v-model="form.response" rows="4" placeholder="The response to send when this rule matches..." fluid />
                </div>

                <div class="form-row">
                    <div class="form-group flex-1">
                        <label>Priority</label>
                        <InputNumber v-model="form.priority" fluid />
                        <small class="info-text">Higher values are checked first.</small>
                    </div>
                    <div class="form-group checkbox-inline">
                        <Checkbox v-model="form.isActive" :binary="true" inputId="ruleActive" />
                        <label for="ruleActive">Active</label>
                    </div>
                </div>
            </div>

            <template #footer>
                <Button label="Cancel" severity="secondary" text @click="showDialog = false" />
                <Button :label="editing ? 'Update Rule' : 'Create Rule'" @click="saveRule" :disabled="!form.trigger.trim() || !form.response.trim()" />
            </template>
        </Dialog>
    </div>
</template>

<style scoped>
.header { margin-bottom: 32px; }
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
.subtitle {
    color: var(--p-surface-500);
    font-size: 0.9rem;
    margin-top: 4px;
}

/* Test Panel */
.test-card {
    background: var(--p-surface-0);
    border: 1px solid var(--p-surface-200);
    margin-bottom: 32px;
}
.test-input-row {
    display: flex;
    gap: 12px;
    align-items: center;
}
.flex-1 { flex: 1; }
.test-result {
    margin-top: 16px;
    padding: 12px 16px;
    border-radius: 8px;
    font-size: 0.9rem;
}
.test-result.matched {
    background: color-mix(in srgb, var(--p-green-500) 10%, transparent);
    border: 1px solid var(--p-green-300);
}
.test-result.no-match {
    background: color-mix(in srgb, var(--p-orange-500) 10%, transparent);
    border: 1px solid var(--p-orange-300);
}
.test-result-header {
    display: flex;
    align-items: center;
    gap: 8px;
    font-weight: 600;
}
.matched .test-result-header { color: var(--p-green-700); }
.no-match .test-result-header { color: var(--p-orange-700); }
.test-response {
    margin-top: 8px;
    padding: 8px 12px;
    background: var(--p-surface-0);
    border-radius: 6px;
    white-space: pre-wrap;
    color: var(--p-surface-700);
}

/* Rules Header */
.rules-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 16px;
}
.section-title { margin: 0; }

/* Empty State */
.empty-state {
    text-align: center;
    padding: 48px 24px;
    color: var(--p-surface-500);
}
.empty-state svg { margin-bottom: 16px; opacity: 0.4; }
.empty-state h3 { color: var(--p-surface-700); margin-bottom: 8px; }
.empty-state p { max-width: 400px; margin: 0 auto 16px; font-size: 0.9rem; }

/* Rule Cards */
.rule-card {
    background: var(--p-surface-0);
    border: 1px solid var(--p-surface-200);
    margin-bottom: 12px;
    transition: opacity 0.2s, border-color 0.2s;
}
.rule-card.inactive {
    opacity: 0.55;
    border-style: dashed;
}
.rule-row {
    display: flex;
    justify-content: space-between;
    align-items: flex-start;
    gap: 16px;
}
.rule-info { flex: 1; min-width: 0; }
.rule-meta {
    display: flex;
    align-items: center;
    gap: 10px;
    margin-bottom: 8px;
}
.rule-type {
    font-size: 0.7rem;
    font-weight: 700;
    text-transform: uppercase;
    letter-spacing: 0.05em;
    padding: 2px 8px;
    border-radius: 4px;
    background: var(--p-surface-100);
    color: var(--p-surface-600);
}
.type-keyword { background: color-mix(in srgb, var(--p-blue-500) 15%, transparent); color: var(--p-blue-700); }
.type-exact { background: color-mix(in srgb, var(--p-green-500) 15%, transparent); color: var(--p-green-700); }
.type-regex { background: color-mix(in srgb, var(--p-purple-500) 15%, transparent); color: var(--p-purple-700); }
.rule-priority {
    font-size: 0.75rem;
    color: var(--p-surface-500);
}
.rule-disabled {
    font-size: 0.7rem;
    font-weight: 600;
    color: var(--p-orange-600);
}
.rule-trigger {
    font-size: 0.85rem;
    margin-bottom: 4px;
    color: var(--p-surface-700);
}
.rule-trigger code {
    background: var(--p-surface-100);
    padding: 1px 6px;
    border-radius: 4px;
    font-size: 0.8rem;
}
.rule-response-preview {
    font-size: 0.8rem;
    color: var(--p-surface-500);
}
.rule-actions {
    display: flex;
    gap: 4px;
    flex-shrink: 0;
}

/* Dialog Form */
.dialog-form { display: flex; flex-direction: column; gap: 16px; }
.form-group { display: flex; flex-direction: column; gap: 6px; }
.form-group label { font-weight: 500; font-size: 0.9rem; color: var(--p-surface-700); }
.info-text { color: var(--p-surface-500); font-size: 0.8rem; }
.form-row { display: flex; gap: 24px; align-items: flex-end; }
.checkbox-inline { flex-direction: row; align-items: center; gap: 10px; padding-bottom: 8px; }
.loading { text-align: center; padding: 24px; color: var(--p-surface-500); }

@media (max-width: 768px) {
    .rules-header { flex-direction: column; gap: 12px; align-items: stretch; }
    .rule-row { flex-direction: column; }
    .rule-actions { align-self: flex-end; }
    .test-input-row { flex-direction: column; }
    .form-row { flex-direction: column; gap: 16px; }
}
</style>

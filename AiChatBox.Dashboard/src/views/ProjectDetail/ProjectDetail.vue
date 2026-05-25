<script setup lang="ts">
import { useProjectDetail } from './ProjectDetail';
import { ref, watch, nextTick, computed } from 'vue';
import Button from 'primevue/button';
import Card from 'primevue/card';
import Dialog from 'primevue/dialog';
import InputText from 'primevue/inputtext';
import Select from 'primevue/select';

// ── CodeMirror 6 imports ──────────────────────────────────────────────────────
import { sql } from '@codemirror/lang-sql';
import { json } from '@codemirror/lang-json';
import { useCodeMirror } from '../../composables/useCodeMirror';
// ─────────────────────────────────────────────────────────────────────────────

import './ProjectDetail.css';

const {
    projectId, project,
    configs, keys, tools,
    activeTab,
    showNewConfig, showNewKey, showNewTool, showSettingsDialog,
    isEditingTool, generatedKey,
    newConfig, newKey, newTool,
    dbConfig, dbTypes, detectingSchema,
    detectedTables, detectedColumns,
    selectedTablesArray, selectedColumnsPerTable,
    toggleTable, toggleColumn, toggleAllColumns, toggleColumnIsolation,
    sessionContextMap,
    showSchemaEditor,
    savingProject, savedProject,
    saveProjectSettings, detectSchema, saveDbConfig,
    createConfig, deleteConfig,
    generateKey, revokeKey,
    openNewTool, openEditTool, saveTool, deleteTool,
    testingWebhook, webhookTestResult, testWebhookConnection,
    showTestTool, testingTool, activeTestTool, toolTestResult, testToolArguments, openTestTool, executeToolTest
} = useProjectDetail();

// ── Schema DDL editor ────────────────────────────────────────────────────────
const schemaEditorEl = ref<HTMLElement | null>(null);
const schemaModelRef = ref(dbConfig.schemaDefinition);
// Keep schemaModelRef in sync with reactive dbConfig
watch(() => dbConfig.schemaDefinition, v => { schemaModelRef.value = v; });
watch(schemaModelRef, v => { dbConfig.schemaDefinition = v; });

const { mount: mountSchema, destroy: destroySchema } = useCodeMirror(
    schemaEditorEl, schemaModelRef, [sql()], { maxHeight: '400px' }
);

watch(showSchemaEditor, async (visible) => {
    if (visible) {
        await nextTick();
        mountSchema();
    } else {
        destroySchema();
    }
});

// ── Session Context Filter JSON editor ───────────────────────────────────────

// ── Tool Parameters JSON Schema editor (new / edit dialog) ───────────────────
const toolParamsEditorEl = ref<HTMLElement | null>(null);
// newTool.parametersJsonSchema is a reactive string — wrap in a ref bridge
const toolParamsRef = ref(newTool.parametersJsonSchema);
watch(() => newTool.parametersJsonSchema, v => { toolParamsRef.value = v; });
watch(toolParamsRef, v => { newTool.parametersJsonSchema = v; });
const toolParamsCm = useCodeMirror(toolParamsEditorEl, toolParamsRef, [json()], { maxHeight: '280px' });

watch(showNewTool, async (visible) => {
    if (visible) {
        await nextTick();
        toolParamsCm.mount();
    } else {
        toolParamsCm.destroy();
    }
});

// ── Tool Parameters (read-only preview in Test dialog) ───────────────────────
const toolSchemaPreviewEl = ref<HTMLElement | null>(null);
const toolSchemaPreviewRef = ref('');
const toolSchemaPreviewCm = useCodeMirror(
    toolSchemaPreviewEl, toolSchemaPreviewRef, [json()], { readonly: true, maxHeight: '140px' }
);

// ── Test Arguments JSON editor ────────────────────────────────────────────────
const testArgsEditorEl = ref<HTMLElement | null>(null);
const testArgsRef = ref(testToolArguments.value);
watch(testToolArguments, v => { testArgsRef.value = v; });
watch(testArgsRef, v => { testToolArguments.value = v; });
const testArgsCm = useCodeMirror(testArgsEditorEl, testArgsRef, [json()], { maxHeight: '220px' });

watch(showTestTool, async (visible) => {
    if (visible) {
        await nextTick();
        // Sync latest value before mount
        toolSchemaPreviewRef.value = activeTestTool.value?.parametersJsonSchema ?? '';
        toolSchemaPreviewCm.mount();
        testArgsCm.mount();
    } else {
        toolSchemaPreviewCm.destroy();
        testArgsCm.destroy();
    }
});

// ── System Prompt editor (Settings dialog) ────────────────────────────────────
const settingsPromptEl = ref<HTMLElement | null>(null);
const settingsPromptRef = ref(project.value?.systemPrompt ?? '');
watch(() => project.value?.systemPrompt, v => { if (v !== undefined) settingsPromptRef.value = v; });
watch(settingsPromptRef, v => { if (project.value) project.value.systemPrompt = v; });
const settingsPromptCm = useCodeMirror(settingsPromptEl, settingsPromptRef, [], { maxHeight: '160px' });

watch(showSettingsDialog, async (visible) => {
    if (visible) {
        await nextTick();
        settingsPromptRef.value = project.value?.systemPrompt ?? '';
        settingsPromptCm.mount();
    } else {
        settingsPromptCm.destroy();
    }
});

// ── New Config — System Prompt editor ────────────────────────────────────────
const newConfigPromptEl = ref<HTMLElement | null>(null);
const newConfigPromptRef = ref(newConfig.systemPrompt);
watch(() => newConfig.systemPrompt, v => { newConfigPromptRef.value = v; });
watch(newConfigPromptRef, v => { newConfig.systemPrompt = v; });
const newConfigPromptCm = useCodeMirror(newConfigPromptEl, newConfigPromptRef, [], { maxHeight: '160px' });

watch(showNewConfig, async (visible) => {
    if (visible) {
        await nextTick();
        newConfigPromptRef.value = newConfig.systemPrompt;
        newConfigPromptCm.mount();
    } else {
        newConfigPromptCm.destroy();
    }
});
// ─────────────────────────────────────────────────────────────────────────────

// ── Expand / collapse state for each table's column panel ─────────────────────
const expandedTables = ref<Set<string>>(new Set());
function toggleTableExpand(table: string) {
    if (expandedTables.value.has(table)) {
        expandedTables.value.delete(table);
    } else {
        expandedTables.value.add(table);
    }
    // Force reactivity on Set
    expandedTables.value = new Set(expandedTables.value);
}

// ── Helper: is every column for a table selected ─────────────────────────────
function allColumnsSelected(table: string): boolean {
    const cols = detectedColumns.value[table] ?? [];
    if (!cols.length) return true;
    const sel = selectedColumnsPerTable[table];
    if (!sel) return false;
    return cols.every(c => sel.has(c));
}

function someColumnsSelected(table: string): boolean {
    const cols = detectedColumns.value[table] ?? [];
    const sel = selectedColumnsPerTable[table];
    if (!sel || !cols.length) return false;
    return cols.some(c => sel.has(c)) && !allColumnsSelected(table);
}

const onboardingChecklist = computed(() => {
    const items = [
        { label: 'Create at least one configuration', done: configs.value.length > 0, tab: 'configs' },
        { label: 'Generate at least one API key', done: keys.value.length > 0, tab: 'keys' },
        { label: 'Set webhook URL or add a custom tool', done: !!project.value.webhookUrl || tools.value.length > 0, tab: 'tools' },
        { label: 'Configure database (optional but recommended)', done: dbConfig.hasConnectionString, tab: 'database' },
        { label: 'Save project settings once', done: !!project.value.systemPrompt || !!project.value.allowedDomains, tab: 'overview' }
    ];
    const completed = items.filter(i => i.done).length;
    return { items, completed, total: items.length };
});


// \u2500\u2500 Session Context Filter JSON editor \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500
// Replaced with a visual table→column picker \u2014 no CodeMirror needed here.
// \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500
</script>

<template>
    <div>
        <header class="page-header">
            <div class="header-left">
                <router-link to="/" class="back-link">
                    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5">
                        <line x1="19" y1="12" x2="5" y2="12" />
                        <polyline points="12 19 5 12 12 5" />
                    </svg>
                    Projects
                </router-link>
                <span class="header-divider">/</span>
                <h1 class="header-title">{{ project.name || 'Project' }}</h1>
            </div>

            <div class="header-right">
                <Transition name="fade">
                    <span v-if="savedProject" class="saved-badge">
                        <i class="pi pi-check-circle"></i> Saved
                    </span>
                </Transition>
                <Button
                    icon="pi pi-cog"
                    label="Settings"
                    severity="secondary"
                    outlined
                    @click="showSettingsDialog = true"
                />
                <Button
                    icon="pi pi-save"
                    :label="savingProject ? 'Saving…' : 'Save'"
                    :disabled="savingProject"
                    :loading="savingProject"
                    @click="saveProjectSettings"
                />
            </div>
        </header>

        <main class="page-body">

            <nav class="tab-nav">
                <button class="tab-btn" :class="{ active: activeTab === 'overview' }" @click="activeTab = 'overview'">
                    <i class="pi pi-home"></i> Overview
                </button>
                <button class="tab-btn" :class="{ active: activeTab === 'configs' }" @click="activeTab = 'configs'">
                    <i class="pi pi-sliders-h"></i> Configurations
                    <span class="tab-badge">{{ configs.length }}</span>
                </button>
                <button class="tab-btn" :class="{ active: activeTab === 'tools' }" @click="activeTab = 'tools'">
                    <i class="pi pi-wrench"></i> Tools
                    <span class="tab-badge">{{ tools.length }}</span>
                </button>
                <button class="tab-btn" :class="{ active: activeTab === 'keys' }" @click="activeTab = 'keys'">
                    <i class="pi pi-key"></i> API Keys
                    <span class="tab-badge">{{ keys.length }}</span>
                </button>
                <button class="tab-btn" :class="{ active: activeTab === 'database' }" @click="activeTab = 'database'">
                    <i class="pi pi-database"></i> Database
                </button>
            </nav>

            <!-- ── Overview tab (unchanged) ─────────────────────────────── -->
            <div v-if="activeTab === 'overview'">
                <div class="overview-grid">
                    <div class="overview-card" @click="activeTab = 'configs'">
                        <i class="pi pi-sliders-h overview-card-icon"></i>
                        <div class="overview-card-value">{{ configs.length }}</div>
                        <div class="overview-card-label">Configurations</div>
                        <div class="overview-card-sub">Click to manage</div>
                    </div>
                    <div class="overview-card" @click="activeTab = 'tools'">
                        <i class="pi pi-wrench overview-card-icon"></i>
                        <div class="overview-card-value">{{ tools.length }}</div>
                        <div class="overview-card-label">Custom Tools</div>
                        <div class="overview-card-sub">Click to manage</div>
                    </div>
                    <div class="overview-card" @click="activeTab = 'keys'">
                        <i class="pi pi-key overview-card-icon"></i>
                        <div class="overview-card-value">{{ keys.length }}</div>
                        <div class="overview-card-label">API Keys</div>
                        <div class="overview-card-sub">Click to manage</div>
                    </div>
                    <div class="overview-card" @click="activeTab = 'database'">
                        <i class="pi pi-database overview-card-icon"></i>
                        <div class="overview-card-value">
                            <i v-if="dbConfig.hasConnectionString" class="pi pi-check-circle" style="font-size:1.4rem; color: var(--p-green-500);"></i>
                            <i v-else class="pi pi-minus-circle" style="font-size:1.4rem; color: var(--p-surface-300);"></i>
                        </div>
                        <div class="overview-card-label">Database</div>
                        <div class="overview-card-sub">{{ dbConfig.hasConnectionString ? 'Connected' : 'Not configured' }}</div>
                    </div>
                </div>

                <div class="section-header" style="margin-top: 8px;">
                    <div>
                        <h3 class="section-heading">Feature Modules</h3>
                        <p class="section-sub">Navigate to specialized areas of your project.</p>
                    </div>
                </div>

                <div class="quick-links">
                    <router-link :to="'/project/' + projectId + '/knowledge'" class="quick-link-card">
                        <div class="quick-link-icon"><i class="pi pi-book"></i></div>
                        <div class="quick-link-text">
                            <h4>Knowledge Base (RAG)</h4>
                            <p>Upload documents for private AI context</p>
                        </div>
                    </router-link>
                    <router-link :to="'/project/' + projectId + '/rules'" class="quick-link-card">
                        <div class="quick-link-icon"><i class="pi pi-bolt"></i></div>
                        <div class="quick-link-text">
                            <h4>Conversation Rules</h4>
                            <p>Zero-cost static responses before the LLM</p>
                        </div>
                    </router-link>
                    <router-link :to="'/project/' + projectId + '/flow'" class="quick-link-card">
                        <div class="quick-link-icon"><i class="pi pi-sitemap"></i></div>
                        <div class="quick-link-text">
                            <h4>Flow Builder</h4>
                            <p>Visual deterministic conversation paths</p>
                        </div>
                    </router-link>
                </div>

                <Card class="onboarding-card">
                    <template #content>
                        <div class="onboarding-header">
                            <h3 class="section-heading">First 5 Steps</h3>
                            <span class="onboarding-progress">{{ onboardingChecklist.completed }}/{{ onboardingChecklist.total }} complete</span>
                        </div>
                        <div class="onboarding-list">
                            <button
                                v-for="item in onboardingChecklist.items"
                                :key="item.label"
                                class="onboarding-item"
                                :class="{ done: item.done }"
                                @click="activeTab = item.tab"
                            >
                                <i :class="item.done ? 'pi pi-check-circle' : 'pi pi-circle'"></i>
                                <span>{{ item.label }}</span>
                            </button>
                        </div>
                    </template>
                </Card>
            </div>

            <!-- ── Configs tab (unchanged) ──────────────────────────────── -->
            <div v-if="activeTab === 'configs'">
                <div class="section-header">
                    <div>
                        <h2 class="section-heading">Configurations (Environments)</h2>
                        <p class="section-sub">Configurations define the persona, provider, and allowed models for your bot.</p>
                    </div>
                    <Button label="New Config" icon="pi pi-plus" severity="secondary" outlined @click="showNewConfig = true" />
                </div>
                <div v-if="configs.length">
                    <Card v-for="c in configs" :key="c.id" class="list-card">
                        <template #content>
                            <div class="list-card-content">
                                <div class="info">
                                    <h3>{{ c.name }}</h3>
                                    <p class="subtitle">{{ c.defaultProvider }} / {{ c.defaultModel }}</p>
                                </div>
                                <div class="card-actions">
                                    <span v-if="c.hasGeminiKey" class="badge badge-success">Gemini</span>
                                    <span v-if="c.hasGroqKey" class="badge badge-success">Groq</span>
                                    <span class="badge">{{ c.apiKeyCount }} keys</span>
                                    <router-link :to="'/project/' + projectId + '/config/' + c.id" custom v-slot="{ navigate }">
                                        <Button label="Edit" size="small" severity="secondary" outlined @click="navigate" />
                                    </router-link>
                                    <Button label="Delete" size="small" severity="danger" outlined @click="deleteConfig(c.id)" />
                                </div>
                            </div>
                        </template>
                    </Card>
                </div>
                <div v-else class="empty-state">
                    <div class="empty-state-icon"><i class="pi pi-sliders-h"></i></div>
                    <p>No configurations yet. Create one to define a persona and connect providers.</p>
                    <Button label="Create First Config" icon="pi pi-plus" @click="showNewConfig = true" />
                </div>
            </div>

            <!-- ── Tools tab (unchanged) ───────────────────────────────── -->
            <div v-if="activeTab === 'tools'">
                <div class="section-header">
                    <div>
                        <h2 class="section-heading">Custom Tools</h2>
                        <p class="section-sub">Define tools the AI can call via webhooks or client-side JS.</p>
                    </div>
                    <Button label="New Tool" icon="pi pi-plus" severity="secondary" outlined @click="openNewTool" />
                </div>
                <div v-if="tools.length">
                    <Card v-for="t in tools" :key="t.id" class="list-card">
                        <template #content>
                            <div class="list-card-content">
                                <div class="info">
                                    <h3>{{ t.name }}</h3>
                                    <p class="subtitle">{{ t.description }}</p>
                                </div>
                                <div class="card-actions">
                                    <Button label="Test" icon="pi pi-play" size="small" severity="warn" outlined @click="openTestTool(t)" />
                                    <Button label="Edit" size="small" severity="secondary" outlined @click="openEditTool(t)" />
                                    <Button label="Delete" size="small" severity="danger" outlined @click="deleteTool(t.id)" />
                                </div>
                            </div>
                        </template>
                    </Card>
                </div>
                <div v-else class="empty-state">
                    <div class="empty-state-icon"><i class="pi pi-wrench"></i></div>
                    <p>No custom tools yet. Define tools that the AI can invoke during conversations.</p>
                    <Button label="Create First Tool" icon="pi pi-plus" @click="openNewTool" />
                </div>
            </div>

            <!-- ── Keys tab (unchanged) ────────────────────────────────── -->
            <div v-if="activeTab === 'keys'">
                <div class="section-header">
                    <div>
                        <h2 class="section-heading">API Access Keys</h2>
                        <p class="section-sub">Keys grant access to a specific configuration via the chat API.</p>
                    </div>
                    <Button label="Generate Key" icon="pi pi-plus" severity="secondary" outlined @click="showNewKey = true; generatedKey = '';" />
                </div>
                <div v-if="keys.length">
                    <Card v-for="k in keys" :key="k.id" class="list-card">
                        <template #content>
                            <div class="list-card-content">
                                <div class="info">
                                    <span class="key-label">{{ k.label || 'API Key' }}</span>
                                    <div class="key-meta">
                                        <span v-if="k.configurationName" class="key-config">
                                            <i class="pi pi-sliders-h" style="font-size:0.7rem;"></i>
                                            {{ k.configurationName }}
                                        </span>
                                        <span class="key-date">
                                            <i class="pi pi-calendar" style="font-size:0.7rem;"></i>
                                            {{ new Date(k.createdAt).toLocaleDateString() }}
                                        </span>
                                    </div>
                                </div>
                                <div class="card-actions">
                                    <Button label="Revoke" size="small" severity="danger" outlined @click="revokeKey(k.id)" />
                                </div>
                            </div>
                        </template>
                    </Card>
                </div>
                <div v-else class="empty-state">
                    <div class="empty-state-icon"><i class="pi pi-key"></i></div>
                    <p>No API keys yet. Generate a key to allow external access to a configuration.</p>
                    <Button label="Generate First Key" icon="pi pi-plus" @click="showNewKey = true; generatedKey = '';" />
                </div>
            </div>

            <!-- ══════════════════════════════════════════════════════════ -->
            <!-- ── DATABASE TAB ──────────────────────────────────────── -->
            <!-- ══════════════════════════════════════════════════════════ -->
            <div v-if="activeTab === 'database'">
                <div class="section-header">
                    <div>
                        <h2 class="section-heading">AI Reporting Database</h2>
                        <p class="section-sub">Connect your database to enable AI-powered SQL reporting and analytics.</p>
                    </div>
                    <Button label="Save Database Settings" icon="pi pi-save" @click="saveDbConfig" />
                </div>

                <!-- ── 1. Connection ──────────────────────────────────── -->
                <Card class="list-card db-section-card">
                    <template #content>
                        <div class="db-section-body">
                            <div class="db-section-label">
                                <i class="pi pi-link db-section-icon"></i>
                                <div>
                                    <div class="db-section-title">Connection</div>
                                    <div class="db-section-sub">Database type and credentials</div>
                                </div>
                                <span v-if="dbConfig.hasConnectionString" class="badge badge-success" style="margin-left: auto;">Connected</span>
                            </div>
                            <div class="form-grid" style="margin-top: 16px;">
                                <div class="form-group">
                                    <label>Database Type</label>
                                    <Select
                                        v-model="dbConfig.type"
                                        :options="dbTypes"
                                        optionLabel="label"
                                        optionValue="value"
                                        placeholder="Select Database Type"
                                        fluid
                                    />
                                </div>
                                <div class="form-group">
                                    <label>Connection String</label>
                                    <InputText
                                        v-model="dbConfig.connectionString"
                                        :placeholder="dbConfig.hasConnectionString ? '******** (Hidden — enter a new value to update)' : 'Server=...;Database=...;'"
                                        fluid
                                    />
                                    <small>Encrypted at rest. Use a read-only user for safety.</small>
                                </div>
                            </div>
                        </div>
                    </template>
                </Card>

                <!-- ── 2. Schema Definition (collapsible, CodeMirror) ─── -->
                <Card class="list-card db-section-card">
                    <template #content>
                        <div class="db-section-body">
                            <div class="db-section-label">
                                <i class="pi pi-code db-section-icon"></i>
                                <div>
                                    <div class="db-section-title">Schema Definition (DDL)</div>
                                    <div class="db-section-sub">
                                        The AI uses this to understand your data structure.
                                        <span v-if="detectedTables.length" class="schema-table-count">
                                            {{ detectedTables.length }} table{{ detectedTables.length !== 1 ? 's' : '' }} detected
                                        </span>
                                    </div>
                                </div>
                                <div class="schema-header-actions">
                                    <Button
                                        icon="pi pi-refresh"
                                        label="Auto-Detect"
                                        size="small"
                                        severity="secondary"
                                        outlined
                                        :loading="detectingSchema"
                                        @click="detectSchema"
                                    />
                                    <button class="schema-toggle-btn" @click="showSchemaEditor = !showSchemaEditor">
                                        <i :class="showSchemaEditor ? 'pi pi-eye-slash' : 'pi pi-eye'"></i>
                                        {{ showSchemaEditor ? 'Hide' : 'View & Edit' }}
                                    </button>
                                </div>
                            </div>

                            <!-- Collapsed summary chips -->
                            <div v-if="!showSchemaEditor && detectedTables.length" class="schema-chip-row">
                                <span v-for="t in detectedTables" :key="t" class="schema-chip">{{ t }}</span>
                            </div>
                            <div v-if="!showSchemaEditor && !detectedTables.length" class="schema-empty-hint">
                                No schema loaded. Click <strong>Auto-Detect</strong> after saving your connection string, or click <strong>View &amp; Edit</strong> to paste DDL manually.
                            </div>

                            <!-- CodeMirror editor (mounted when visible) -->
                            <Transition name="schema-slide">
                                <div v-if="showSchemaEditor" class="schema-editor-wrapper">
                                    <div ref="schemaEditorEl" class="schema-codemirror"></div>
                                    <p class="info-text" style="margin-top: 8px;">
                                        Full SQL syntax highlighting. Changes are saved with <strong>Save Database Settings</strong> above.
                                    </p>
                                </div>
                            </Transition>
                        </div>
                    </template>
                </Card>

                <!-- ── 3. Security & Safeguards ───────────────────────── -->
                <Card class="list-card db-section-card">
                    <template #content>
                        <div class="db-section-body">
                            <div class="db-section-label">
                                <i class="pi pi-shield db-section-icon"></i>
                                <div>
                                    <div class="db-section-title">Security &amp; Safeguards</div>
                                    <div class="db-section-sub">Query limits, table whitelisting, and row-level isolation</div>
                                </div>
                            </div>

                            <div class="form-grid" style="margin-top: 16px;">

                                <!-- Timeout & row limit -->
                                <div class="form-group">
                                    <label>Max Query Timeout (Seconds)</label>
                                    <input type="number" v-model.number="dbConfig.maxQueryTimeoutSeconds" min="1" max="30" class="p-inputtext w-full" />
                                    <small>Abort long-running queries (1–30 s).</small>
                                </div>
                                <div class="form-group">
                                    <label>Max Records Per Query</label>
                                    <input type="number" v-model.number="dbConfig.maxRecordsPerQuery" min="1" max="1000" class="p-inputtext w-full" />
                                    <small>Maximum row limit to prevent memory strain (1–1000).</small>
                                </div>

                                <!-- ── Allowed Tables (Whitelisting) ──── -->
                                <div class="form-group col-span-2">
                                    <div class="whitelist-header">
                                        <div>
                                            <label>Allowed Tables &amp; Columns (Whitelisting &amp; Isolation)</label>
                                            <small style="display: block; margin-top: 2px;">
                                                Only AI queries that access whitelisted tables (and columns) will be permitted.
                                                Expand a table to restrict which columns the AI can read and to configure Row-Level Data Isolation.
                                            </small>
                                        </div>
                                        <span v-if="selectedTablesArray.length" class="badge">
                                            {{ selectedTablesArray.length }} / {{ detectedTables.length }} tables
                                        </span>
                                    </div>

                                    <!-- No schema loaded yet -->
                                    <div v-if="!detectedTables.length" class="whitelist-no-schema">
                                        <i class="pi pi-info-circle"></i>
                                        Load a schema first — detected tables will appear here as selectable checkboxes.
                                        You can also type table names manually below.
                                    </div>

                                    <!-- Table + column picker -->
                                    <div v-else class="whitelist-table-list">
                                        <div
                                            v-for="table in detectedTables"
                                            :key="table"
                                            :class="['whitelist-table-row', { 'is-selected': selectedTablesArray.includes(table) }]"
                                        >
                                            <!-- Table row header -->
                                            <div class="whitelist-table-header">
                                                <label class="whitelist-table-check-label">
                                                    <input
                                                        type="checkbox"
                                                        class="whitelist-checkbox"
                                                        :checked="selectedTablesArray.includes(table)"
                                                        @change="toggleTable(table)"
                                                    />
                                                    <span class="whitelist-table-name">{{ table }}</span>
                                                    <span v-if="detectedColumns[table]?.length" class="whitelist-col-count">
                                                        {{ detectedColumns[table].length }} cols
                                                    </span>
                                                </label>

                                                <!-- Column restriction summary & expand toggle -->
                                                <div v-if="selectedTablesArray.includes(table)" class="whitelist-table-meta">
                                                    <span
                                                        v-if="allColumnsSelected(table)"
                                                        class="col-restriction-badge all"
                                                    >All columns</span>
                                                    <span
                                                        v-else-if="someColumnsSelected(table)"
                                                        class="col-restriction-badge partial"
                                                    >{{ selectedColumnsPerTable[table]?.size ?? 0 }} / {{ detectedColumns[table]?.length }} cols</span>
                                                    <span
                                                        v-else
                                                        class="col-restriction-badge none"
                                                    >No columns</span>

                                                    <button
                                                        v-if="detectedColumns[table]?.length"
                                                        class="whitelist-expand-btn"
                                                        @click="toggleTableExpand(table)"
                                                    >
                                                        <i :class="expandedTables.has(table) ? 'pi pi-chevron-up' : 'pi pi-chevron-down'"></i>
                                                        {{ expandedTables.has(table) ? 'Hide options' : 'Configure options' }}
                                                    </button>
                                                </div>
                                            </div>

                                            <!-- Columns panel (expanded) -->
                                            <Transition name="cols-slide">
                                                <div
                                                    v-if="selectedTablesArray.includes(table) && expandedTables.has(table) && detectedColumns[table]?.length"
                                                    class="whitelist-cols-panel"
                                                >
                                                    <div class="whitelist-cols-toolbar" style="display: flex; align-items: center; justify-content: space-between; margin-bottom: 10px;">
                                                        <span style="font-size: 0.8rem; font-weight: 600; color: var(--p-text-color);">Allowed Columns &amp; Isolation</span>
                                                        <div style="display: flex; gap: 10px;">
                                                            <button class="cols-toolbar-btn" @click="toggleAllColumns(table, true)">Select all</button>
                                                            <button class="cols-toolbar-btn" @click="toggleAllColumns(table, false)">Deselect all</button>
                                                        </div>
                                                    </div>
                                                    <div class="whitelist-cols-grid">
                                                        <label
                                                            v-for="col in detectedColumns[table]"
                                                            :key="col"
                                                            class="whitelist-col-item"
                                                            :class="{ 'col-selected': selectedColumnsPerTable[table]?.has(col) }"
                                                        >
                                                            <input
                                                                type="checkbox"
                                                                class="whitelist-checkbox"
                                                                :checked="selectedColumnsPerTable[table]?.has(col)"
                                                                @change="toggleColumn(table, col)"
                                                            />
                                                            <i class="pi pi-table col-icon"></i>
                                                            <span class="col-name">{{ col }}</span>
                                                            <button
                                                                v-if="selectedColumnsPerTable[table]?.has(col)"
                                                                type="button"
                                                                class="col-isolation-btn"
                                                                :class="{ 'is-active': sessionContextMap[table] === col }"
                                                                @click.stop.prevent="toggleColumnIsolation(table, col)"
                                                                :title="sessionContextMap[table] === col ? 'Row-level isolation active (click to disable)' : 'Set as row-level isolation column'"
                                                            >
                                                                <i class="pi pi-shield"></i>
                                                            </button>
                                                        </label>
                                                    </div>

                                                    <!-- Isolation Preview Banner -->
                                                    <div v-if="sessionContextMap[table]" class="isolation-preview-banner" style="margin-top: 14px; display: flex; align-items: center; gap: 8px; font-size: 0.76rem; color: var(--p-text-color-secondary); background: var(--p-surface-50); border: 1px dashed var(--p-surface-200); border-radius: 6px; padding: 10px 12px;">
                                                        <i class="pi pi-shield" style="color: var(--p-primary-500); font-size: 0.85rem;"></i>
                                                        <span>
                                                            Row-level isolation enabled on <strong>{{ sessionContextMap[table] }}</strong>. Queries auto-inject <code>WHERE {{ sessionContextMap[table] }} = @sessionValue</code>.
                                                        </span>
                                                    </div>
                                                </div>
                                            </Transition>
                                        </div>
                                    </div>

                                    <!-- Manual override input -->
                                    <div class="whitelist-manual">
                                        <label>Manual override (comma-separated)</label>
                                        <InputText v-model="dbConfig.allowedTables" placeholder="users, orders, products" fluid />
                                        <small>This field stays in sync with the checkboxes above and can be edited directly.</small>
                                    </div>
                                </div>

                            </div>
                        </div>
                    </template>
                </Card>
            </div>
            <!-- ── / DATABASE TAB ────────────────────────────────────── -->

        </main>

        <!-- ─── Dialogs (unchanged) ──────────────────────────────────────── -->
        <Dialog v-model:visible="showSettingsDialog" header="Project Settings" :modal="true" :style="{ width: '640px' }" :draggable="false">
            <p class="info-text" style="margin-bottom: 20px;">Core settings for your project integration and security.</p>
            <div class="form-grid">
                <div class="form-group col-span-2">
                    <label>System Prompt (Base)</label>
                    <div ref="settingsPromptEl" class="cm-host"></div>
                </div>
                <div class="form-group">
                    <label>Allowed Domains</label>
                    <InputText v-model="project.allowedDomains" placeholder="localhost:5173, example.com, *" fluid />
                    <small>Use * to allow all (not recommended for production). Comma-separated hostnames.</small>
                </div>
                <div class="form-group">
                    <label>Webhook URL (For Custom Tools)</label>
                    <InputText v-model="project.webhookUrl" placeholder="https://your-api.com/webhooks/aichat" fluid />
                    <small>All custom tools will POST to this URL.</small>
                </div>
                <div class="form-group col-span-2">
                    <label>Webhook Secret (Optional)</label>
                    <InputText v-model="project.webhookSecret" type="password" :placeholder="project.hasWebhookSecret ? '******** (Hidden)' : 'Leave blank to disable'" fluid />
                    <small>If set, requests will include an X-Hub-Signature HMAC-SHA256 header.</small>
                </div>
                <div class="form-group col-span-2" style="margin-top: 10px;">
                    <Button label="Test Webhook Connection" icon="pi pi-play" severity="warn" outlined :loading="testingWebhook" @click="testWebhookConnection" style="width: 100%;" />
                </div>
                <div v-if="webhookTestResult" class="form-group col-span-2" style="background: var(--p-surface-50); border: 1px solid var(--p-surface-200); border-radius: 8px; padding: 14px; margin-top: 10px;">
                    <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 6px;">
                        <span style="font-weight: 600; font-size: 0.9rem;">Test Connection Results:</span>
                        <span :class="webhookTestResult.success ? 'badge badge-success' : 'badge badge-danger'">{{ webhookTestResult.success ? 'Success' : 'Failed' }}</span>
                    </div>
                    <div style="font-size: 0.82rem; color: var(--p-text-color-secondary); margin-bottom: 8px; display: flex; gap: 16px;">
                        <span>Status: <strong>{{ webhookTestResult.statusCode }}</strong></span>
                        <span>Duration: <strong>{{ webhookTestResult.responseTimeMs }} ms</strong></span>
                    </div>
                    <pre style="background: var(--p-surface-100); color: var(--p-text-color); padding: 10px; border: 1px solid var(--p-surface-200); border-radius: 6px; font-family: monospace; font-size: 0.8rem; max-height: 150px; overflow-y: auto; margin: 0; white-space: pre-wrap; word-break: break-all;">{{ webhookTestResult.responseBody || '(Empty response)' }}</pre>
                </div>
            </div>
            <template #footer>
                <Button label="Cancel" severity="secondary" outlined @click="showSettingsDialog = false" />
                <Button label="Save Settings" icon="pi pi-save" :loading="savingProject" @click="saveProjectSettings(); showSettingsDialog = false;" />
            </template>
        </Dialog>

        <Dialog v-model:visible="showNewConfig" modal header="New Configuration" :style="{ width: '480px' }" :draggable="false">
            <div class="form">
                <div class="dialog-form-group">
                    <label>Name</label>
                    <InputText v-model="newConfig.name" placeholder="Production" fluid />
                </div>
                <div class="dialog-form-group">
                    <label>System Prompt</label>
                    <div ref="newConfigPromptEl" class="cm-host"></div>
                </div>
                <span class="info-text">You can configure API keys, models, and voice mode after creation.</span>
            </div>
            <template #footer>
                <Button label="Cancel" severity="secondary" outlined @click="showNewConfig = false" />
                <Button label="Create" icon="pi pi-plus" :disabled="!newConfig.name" @click="createConfig" />
            </template>
        </Dialog>

        <Dialog v-model:visible="showNewKey" modal header="Generate API Key" :style="{ width: '420px' }" :draggable="false">
            <div class="form">
                <div class="dialog-form-group">
                    <label>Label</label>
                    <InputText v-model="newKey.label" placeholder="e.g. Production Key" fluid />
                </div>
                <div class="dialog-form-group">
                    <label>Configuration</label>
                    <Select v-model="newKey.configId" :options="configs" optionLabel="name" optionValue="id" placeholder="Select a configuration (required)" fluid />
                </div>
                <div v-if="generatedKey" class="generated-key-container">
                    <div class="code-block">{{ generatedKey }}</div>
                    <p class="warning-text"><i class="pi pi-exclamation-triangle"></i> Copy this now — you won't see it again.</p>
                </div>
            </div>
            <template #footer>
                <Button label="Close" severity="secondary" outlined @click="showNewKey = false" />
                <Button label="Generate" icon="pi pi-key" :disabled="!!generatedKey || !newKey.configId" @click="generateKey" />
            </template>
        </Dialog>

        <Dialog v-model:visible="showNewTool" modal :header="isEditingTool ? 'Edit Custom Tool' : 'New Custom Tool'" :style="{ width: '600px' }" :draggable="false">
            <div class="form">
                <div class="dialog-form-group">
                    <label>Tool Name</label>
                    <InputText v-model="newTool.name" placeholder="e.g. check_inventory" fluid />
                    <small>Use snake_case. This is what the AI calls internally.</small>
                </div>
                <div class="dialog-form-group">
                    <label>Description</label>
                    <InputText v-model="newTool.description" placeholder="e.g. Checks inventory for a given product ID" fluid />
                    <small>Describe clearly — the AI uses this to decide when to invoke the tool.</small>
                </div>
                <div class="dialog-form-group">
                    <label>Parameters JSON Schema</label>
                    <div ref="toolParamsEditorEl" class="cm-host"></div>
                    <small>Standard JSON Schema defining the tool's input parameters.</small>
                </div>
            </div>
            <template #footer>
                <Button label="Cancel" severity="secondary" outlined @click="showNewTool = false" />
                <Button :label="isEditingTool ? 'Save Changes' : 'Create Tool'" icon="pi pi-check" :disabled="!newTool.name || !newTool.description" @click="saveTool" />
            </template>
        </Dialog>

        <Dialog v-model:visible="showTestTool" modal :header="'Test Custom Tool: ' + (activeTestTool?.name || '')" :style="{ width: '640px' }" :draggable="false">
            <div class="form" v-if="activeTestTool">
                <div class="dialog-form-group">
                    <label>Description</label>
                    <div style="font-size: 0.85rem; color: var(--p-text-color-secondary);">{{ activeTestTool.description }}</div>
                </div>
                <div class="dialog-form-group">
                    <label>Parameters Schema (Read-only)</label>
                    <div ref="toolSchemaPreviewEl" class="cm-host"></div>
                </div>
                <div class="dialog-form-group">
                    <label>Test Arguments (JSON)</label>
                    <div ref="testArgsEditorEl" class="cm-host"></div>
                    <small>Provide arguments matching the schema in valid JSON.</small>
                </div>
                <div v-if="toolTestResult" style="background: var(--p-surface-50); border: 1px solid var(--p-surface-200); border-radius: 8px; padding: 14px; margin-top: 10px;">
                    <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 6px;">
                        <span style="font-weight: 600; font-size: 0.9rem;">Execution Results:</span>
                        <span :class="toolTestResult.success ? 'badge badge-success' : 'badge badge-danger'">{{ toolTestResult.success ? 'Success' : 'Failed' }}</span>
                    </div>
                    <div v-if="toolTestResult.success" style="font-size: 0.85rem;">
                        <div style="font-weight: 600; margin-bottom: 4px;">Content:</div>
                        <pre style="background: var(--p-surface-100); color: var(--p-text-color); padding: 10px; border: 1px solid var(--p-surface-200); border-radius: 6px; font-family: monospace; font-size: 0.8rem; max-height: 150px; overflow-y: auto; margin: 0; white-space: pre-wrap; word-break: break-all;">{{ toolTestResult.content }}</pre>
                    </div>
                    <div v-else style="font-size: 0.85rem; color: var(--p-red-600);">
                        <div style="font-weight: 600; margin-bottom: 4px;">Error:</div>
                        <div style="background: var(--p-red-50); border: 1px solid var(--p-red-200); color: var(--p-red-700); padding: 10px; border-radius: 6px; font-family: monospace; font-size: 0.8rem; white-space: pre-wrap; word-break: break-all;">{{ toolTestResult.error }}</div>
                    </div>
                </div>
            </div>
            <template #footer>
                <Button label="Close" severity="secondary" outlined @click="showTestTool = false" />
                <Button label="Run Execution Test" icon="pi pi-play" severity="warn" :loading="testingTool" @click="executeToolTest" />
            </template>
        </Dialog>

    </div>
</template>

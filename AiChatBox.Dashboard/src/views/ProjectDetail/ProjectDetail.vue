<script setup lang="ts">
import { useProjectDetail } from './ProjectDetail';
import Button from 'primevue/button';
import Card from 'primevue/card';
import Dialog from 'primevue/dialog';
import InputText from 'primevue/inputtext';
import Select from 'primevue/select';
import Textarea from 'primevue/textarea';

import './ProjectDetail.css';

const {
    projectId, project,
    configs, keys, tools, configOptions,
    activeTab,
    showNewConfig, showNewKey, showNewTool, showSettingsDialog,
    isEditingTool, generatedKey,
    newConfig, newKey, newTool,
    dbConfig, dbTypes, detectingSchema,
    savingProject, savedProject,
    saveProjectSettings, detectSchema, saveDbConfig,
    createConfig, deleteConfig,
    generateKey, revokeKey,
    openNewTool, openEditTool, saveTool, deleteTool
} = useProjectDetail();
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
            </div>

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

            <div v-if="activeTab === 'keys'">
                <div class="section-header">
                    <div>
                        <h2 class="section-heading">API Access Keys</h2>
                        <p class="section-sub">Keys grant access to a specific configuration via the chat API.</p>
                    </div>
                    <Button
                        label="Generate Key"
                        icon="pi pi-plus"
                        severity="secondary"
                        outlined
                        @click="showNewKey = true; generatedKey = '';"
                    />
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

            <div v-if="activeTab === 'database'">
                <div class="section-header">
                    <div>
                        <h2 class="section-heading">AI Reporting Database</h2>
                        <p class="section-sub">Connect your database to enable AI-powered SQL reporting and analytics.</p>
                    </div>
                    <Button label="Save Database Settings" icon="pi pi-save" severity="secondary" outlined @click="saveDbConfig" />
                </div>

                <Card class="list-card">
                    <template #content>
                        <div style="padding: 20px;">
                            <div class="form-grid">
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
                                        :placeholder="dbConfig.hasConnectionString ? '******** (Hidden)' : 'Server=...;Database=...;'"
                                        fluid
                                    />
                                    <small>Encrypted at rest. Use a read-only user for safety.</small>
                                </div>
                                <div class="form-group col-span-2">
                                    <div class="schema-label-row">
                                        <label>Schema Definition (DDL)</label>
                                        <Button
                                            icon="pi pi-refresh"
                                            label="Auto-Detect Schema"
                                            size="small"
                                            text
                                            :loading="detectingSchema"
                                            @click="detectSchema"
                                        />
                                    </div>
                                    <Textarea
                                        v-model="dbConfig.schemaDefinition"
                                        rows="8"
                                        placeholder="CREATE TABLE users (...); CREATE TABLE orders (...);"
                                        style="font-family: monospace; font-size: 0.82rem;"
                                        fluid
                                    />
                                    <small>Provide the DDL of your tables so the AI understands your data structure. Click "Auto-Detect Schema" after saving the connection string.</small>
                                </div>
                            </div>
                        </div>
                    </template>
                </Card>
            </div>

        </main>

        <Dialog
            v-model:visible="showSettingsDialog"
            header="Project Settings"
            :modal="true"
            :style="{ width: '640px' }"
            :draggable="false"
        >
            <p class="info-text" style="margin-bottom: 20px;">Core settings for your project integration and security.</p>
            <div class="form-grid">
                <div class="form-group col-span-2">
                    <label>System Prompt (Base)</label>
                    <Textarea v-model="project.systemPrompt" rows="3" fluid />
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
                    <InputText
                        v-model="project.webhookSecret"
                        type="password"
                        :placeholder="project.hasWebhookSecret ? '******** (Hidden)' : 'Leave blank to disable'"
                        fluid
                    />
                    <small>If set, requests will include an X-Hub-Signature HMAC-SHA256 header.</small>
                </div>
            </div>

            <template #footer>
                <Button label="Cancel" severity="secondary" outlined @click="showSettingsDialog = false" />
                <Button
                    label="Save Settings"
                    icon="pi pi-save"
                    :loading="savingProject"
                    @click="saveProjectSettings(); showSettingsDialog = false;"
                />
            </template>
        </Dialog>

        <Dialog
            v-model:visible="showNewConfig"
            modal
            header="New Configuration"
            :style="{ width: '480px' }"
            :draggable="false"
        >
            <div class="form">
                <div class="dialog-form-group">
                    <label>Name</label>
                    <InputText v-model="newConfig.name" placeholder="Production" fluid />
                </div>
                <div class="dialog-form-group">
                    <label>System Prompt</label>
                    <Textarea v-model="newConfig.systemPrompt" rows="4" fluid />
                </div>
                <span class="info-text">You can configure API keys, models, and voice mode after creation.</span>
            </div>

            <template #footer>
                <Button label="Cancel" severity="secondary" outlined @click="showNewConfig = false" />
                <Button label="Create" icon="pi pi-plus" :disabled="!newConfig.name" @click="createConfig" />
            </template>
        </Dialog>

        <Dialog
            v-model:visible="showNewKey"
            modal
            header="Generate API Key"
            :style="{ width: '420px' }"
            :draggable="false"
        >
            <div class="form">
                <div class="dialog-form-group">
                    <label>Label</label>
                    <InputText v-model="newKey.label" placeholder="e.g. Production Key" fluid />
                </div>
                <div class="dialog-form-group">
                    <label>Configuration</label>
                    <Select
                        v-model="newKey.configId"
                        :options="configs"
                        optionLabel="name"
                        optionValue="id"
                        placeholder="Select a configuration (required)"
                        fluid
                    />
                </div>

                <div v-if="generatedKey" class="generated-key-container">
                    <div class="code-block">{{ generatedKey }}</div>
                    <p class="warning-text"><i class="pi pi-exclamation-triangle"></i> Copy this now — you won't see it again.</p>
                </div>
            </div>

            <template #footer>
                <Button label="Close" severity="secondary" outlined @click="showNewKey = false" />
                <Button
                    label="Generate"
                    icon="pi pi-key"
                    :disabled="!!generatedKey || !newKey.configId"
                    @click="generateKey"
                />
            </template>
        </Dialog>

        <Dialog
            v-model:visible="showNewTool"
            modal
            :header="isEditingTool ? 'Edit Custom Tool' : 'New Custom Tool'"
            :style="{ width: '600px' }"
            :draggable="false"
        >
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
                    <Textarea
                        v-model="newTool.parametersJsonSchema"
                        rows="9"
                        style="font-family: 'JetBrains Mono', monospace; font-size: 0.82rem;"
                        fluid
                    />
                    <small>Standard JSON Schema defining the tool's input parameters.</small>
                </div>
            </div>

            <template #footer>
                <Button label="Cancel" severity="secondary" outlined @click="showNewTool = false" />
                <Button
                    :label="isEditingTool ? 'Save Changes' : 'Create Tool'"
                    icon="pi pi-check"
                    :disabled="!newTool.name || !newTool.description"
                    @click="saveTool"
                />
            </template>
        </Dialog>

    </div>
</template>

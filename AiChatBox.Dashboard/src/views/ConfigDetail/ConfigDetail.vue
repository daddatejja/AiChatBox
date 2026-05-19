<script setup lang="ts">
import { useConfigDetail } from './ConfigDetail';
import Button from 'primevue/button';
import Card from 'primevue/card';
import InputText from 'primevue/inputtext';
import Select from 'primevue/select';
import Textarea from 'primevue/textarea';
import Checkbox from 'primevue/checkbox';
import Password from 'primevue/password';
import InputNumber from 'primevue/inputnumber';
import Slider from 'primevue/slider';
import Dialog from 'primevue/dialog';

import './ConfigDetail.css';

const {
    projectId,
    activeTab, showAdminDialog, showTemplateVarsDialog, showHistoryDialog,
    sectionsOpen, toggleSection,
    config, channels, theme, keyInputs, providerKeyInputs,
    enabledModels, providerModels, fetchingModels,
    coreProviders, extraProviders,
    defaultModelOptions,
    fontOptions, positionOptions,
    saving, saved,
    historyEntries, loadingHistory, restoringId, changeNote, promptDirty,
    templateVars, builtInVars, suggestedVars,
    isModelEnabled, toggleModel, onDefaultModelChange,
    isExtraProviderConfigured, fetchModels,
    save, clearKey,
    addSuggestion, removeSuggestion,
    insertVariable, addTemplateVar, removeTemplateVar,
    restoreVersion, openHistoryDialog,
    onPromptInput, truncate, formatDate
} = useConfigDetail();
</script>

<template>
    <div>
        <!-- ═══════════════════════════════════════════════════
             FIXED HEADER
        ═══════════════════════════════════════════════════ -->
        <header class="page-header">
            <div class="header-left">
                <router-link :to="'/project/' + projectId" class="back-link">
                    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5">
                        <line x1="19" y1="12" x2="5" y2="12" />
                        <polyline points="12 19 5 12 12 5" />
                    </svg>
                    Back
                </router-link>
                <span style="color: var(--p-surface-300);">/</span>
                <h1 class="header-title">{{ config.name || 'Configuration' }}</h1>
            </div>

            <div class="header-right">
                <Transition name="fade">
                    <span v-if="saved" class="saved-badge">
                        <i class="pi pi-check-circle"></i> Saved
                    </span>
                </Transition>
                <Button
                    :label="saving ? 'Saving…' : 'Save'"
                    icon="pi pi-save"
                    :disabled="saving"
                    :loading="saving"
                    @click="save"
                />
            </div>
        </header>

        <!-- ═══════════════════════════════════════════════════
             PAGE BODY
        ═══════════════════════════════════════════════════ -->
        <main class="page-body">

            <!-- ── Tab Navigation ── -->
            <nav class="tab-nav">
                <button class="tab-btn" :class="{ active: activeTab === 'general' }" @click="activeTab = 'general'">
                    <i class="pi pi-sliders-h"></i> General
                </button>
                <button class="tab-btn" :class="{ active: activeTab === 'providers' }" @click="activeTab = 'providers'">
                    <i class="pi pi-key"></i> Providers
                </button>
                <button class="tab-btn" :class="{ active: activeTab === 'channels' }" @click="activeTab = 'channels'">
                    <i class="pi pi-share-alt"></i> Channels
                </button>
                <button class="tab-btn" :class="{ active: activeTab === 'handoff' }" @click="activeTab = 'handoff'">
                    <i class="pi pi-users"></i> Handoff
                </button>
                <button class="tab-btn" :class="{ active: activeTab === 'appearance' }" @click="activeTab = 'appearance'">
                    <i class="pi pi-palette"></i> Appearance
                </button>
            </nav>

            <!-- ════════════════════════════════════════════════
                 TAB: GENERAL
            ════════════════════════════════════════════════ -->
            <div v-if="activeTab === 'general'">

                <!-- System Prompt -->
                <Card class="config-card">
                    <template #title>
                        <div class="flex-between">
                            <span>System Prompt</span>
                            <Button
                                icon="pi pi-history"
                                label="History"
                                severity="secondary"
                                text
                                size="small"
                                @click="openHistoryDialog"
                            />
                        </div>
                    </template>
                    <template #content>
                        <div class="form-group">
                            <Textarea v-model="config.systemPrompt" rows="6" fluid @input="onPromptInput" />
                            <div class="variable-chips">
                                <span class="chips-label">Insert:</span>
                                <button
                                    v-for="v in suggestedVars"
                                    :key="v"
                                    class="var-chip"
                                    @click="insertVariable(v)"
                                    type="button"
                                    v-text="'{{' + v + '}}'"
                                ></button>
                                <button
                                    v-for="v in builtInVars"
                                    :key="v"
                                    class="var-chip var-chip-builtin"
                                    @click="insertVariable(v)"
                                    type="button"
                                >
                                    <span v-text="'{{' + v + '}}'"></span>
                                    <span class="var-chip-auto">auto</span>
                                </button>
                            </div>
                            <div v-if="promptDirty" class="change-note-row">
                                <InputText v-model="changeNote" placeholder="Optional: describe this change…" fluid />
                            </div>
                        </div>
                    </template>
                </Card>

                <!-- Default Model + Voice -->
                <Card class="config-card">
                    <template #title>Model Settings</template>
                    <template #content>
                        <div class="form-group">
                            <label>Default Model</label>
                            <Select
                                :modelValue="config.defaultModel"
                                @update:modelValue="onDefaultModelChange"
                                :options="defaultModelOptions"
                                optionLabel="label"
                                optionValue="value"
                                placeholder="Enable models in Providers tab first"
                                fluid
                            />
                            <span v-if="enabledModels.length === 0" class="info-text">
                                Add provider API keys and enable models in the Providers tab first.
                            </span>
                        </div>
                        <div v-if="config.hasGeminiKey" class="checkbox-group form-group">
                            <Checkbox v-model="config.liveVoiceEnabled" :binary="true" inputId="liveVoice" />
                            <label for="liveVoice" style="text-transform:none; letter-spacing:0;">Live Voice Mode</label>
                            <span class="info-text">(requires Gemini API key)</span>
                        </div>
                    </template>
                </Card>

                <!-- Quick-access buttons for dialogs -->
                <div style="display:flex; gap:10px; flex-wrap:wrap; margin-bottom:16px;">
                    <Button
                        icon="pi pi-cog"
                        label="Administrative Controls"
                        severity="secondary"
                        outlined
                        @click="showAdminDialog = true"
                    />
                    <Button
                        icon="pi pi-code"
                        label="Template Variables"
                        severity="secondary"
                        outlined
                        @click="showTemplateVarsDialog = true"
                    />
                </div>
            </div>

            <!-- ════════════════════════════════════════════════
                 TAB: PROVIDERS
            ════════════════════════════════════════════════ -->
            <div v-if="activeTab === 'providers'">

                <!-- Core Providers (collapsible) -->
                <div
                    class="collapsible-header"
                    @click="toggleSection('coreProviders')"
                >
                    <div class="collapsible-header-left">
                        <i class="pi pi-building"></i>
                        Core Providers
                        <span class="info-text" style="text-transform:none; font-weight:400;">(Gemini, Groq, OpenAI, Anthropic, Firecrawl)</span>
                    </div>
                    <i class="pi pi-chevron-down collapsible-chevron" :class="{ open: sectionsOpen.coreProviders }"></i>
                </div>

                <div v-show="sectionsOpen.coreProviders" class="collapsible-body">
                    <Card v-for="provider in coreProviders" :key="provider.key" class="provider-card">
                        <template #title>
                            <div class="provider-header">
                                <h3>{{ provider.label }}</h3>
                                <span v-if="config[provider.hasKey as keyof typeof config]" class="badge badge-success">Configured</span>
                            </div>
                        </template>
                        <template #content>
                            <div class="api-key-input">
                                <Password
                                    v-model="keyInputs[provider.key as keyof typeof keyInputs]"
                                    :feedback="false"
                                    toggleMask
                                    :placeholder="config[provider.hasKey as keyof typeof config] ? '••••••••••••••••••••' : provider.label + ' API key'"
                                    fluid
                                    class="flex-1"
                                />
                                <Button
                                    v-if="provider.id !== 'firecrawl' && provider.id !== 'anthropic'"
                                    :label="fetchingModels === provider.id ? 'Loading…' : 'Fetch Models'"
                                    severity="secondary"
                                    outlined
                                    size="small"
                                    :disabled="!config[provider.hasKey as keyof typeof config] || fetchingModels === provider.id"
                                    @click="fetchModels(provider.id)"
                                />
                                <Button
                                    v-if="config[provider.hasKey as keyof typeof config]"
                                    icon="pi pi-trash"
                                    severity="danger"
                                    text
                                    rounded
                                    @click="clearKey(provider.id)"
                                    v-tooltip="'Remove key'"
                                />
                            </div>
                            <div v-if="providerModels[provider.id]?.length" class="models-list">
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
                </div>

                <!-- Extra Providers (collapsible) -->
                <div
                    v-if="extraProviders.length"
                    class="collapsible-header mt-4"
                    @click="toggleSection('extraProviders')"
                >
                    <div class="collapsible-header-left">
                        <i class="pi pi-server"></i>
                        Additional Providers
                        <span class="info-text" style="text-transform:none; font-weight:400;">(OpenAI-compatible · many free tiers)</span>
                    </div>
                    <i class="pi pi-chevron-down collapsible-chevron" :class="{ open: sectionsOpen.extraProviders }"></i>
                </div>

                <div v-if="extraProviders.length" v-show="sectionsOpen.extraProviders" class="collapsible-body">
                    <Card v-for="ep in extraProviders" :key="ep.id" class="provider-card">
                        <template #title>
                            <div class="provider-header">
                                <h3>{{ ep.name }}</h3>
                                <span v-if="isExtraProviderConfigured(ep.id)" class="badge badge-success">Configured</span>
                            </div>
                        </template>
                        <template #content>
                            <div class="api-key-input">
                                <Password
                                    v-model="providerKeyInputs[ep.id]"
                                    :feedback="false"
                                    toggleMask
                                    :placeholder="isExtraProviderConfigured(ep.id) ? '••••••••••••••••••••' : ep.name + ' API key'"
                                    fluid
                                    class="flex-1"
                                />
                                <Button
                                    :label="fetchingModels === ep.id ? 'Loading…' : 'Fetch Models'"
                                    severity="secondary"
                                    outlined
                                    size="small"
                                    :disabled="!isExtraProviderConfigured(ep.id) || fetchingModels === ep.id"
                                    @click="fetchModels(ep.id)"
                                />
                                <Button
                                    v-if="isExtraProviderConfigured(ep.id)"
                                    icon="pi pi-trash"
                                    severity="danger"
                                    text
                                    rounded
                                    @click="clearKey(ep.id)"
                                    v-tooltip="'Remove key'"
                                />
                            </div>
                            <small class="info-text">Default model: {{ ep.defaultModel }}</small>
                            <div v-if="providerModels[ep.id]?.length" class="models-list">
                                <p class="models-title">Available models (check to enable):</p>
                                <div v-for="m in providerModels[ep.id]" :key="ep.id + '-' + m.id" class="model-item">
                                    <Checkbox
                                        :modelValue="isModelEnabled(m.id, ep.id)"
                                        @update:modelValue="toggleModel(m.id, ep.id)"
                                        :binary="true"
                                        :inputId="ep.id + '-' + m.id"
                                    />
                                    <label :for="ep.id + '-' + m.id" class="model-name">{{ m.name }}</label>
                                    <span class="model-desc">{{ m.description }}</span>
                                </div>
                            </div>
                        </template>
                    </Card>
                </div>

                <!-- Custom Provider (collapsible) -->
                <div
                    class="collapsible-header mt-4"
                    @click="toggleSection('customProvider')"
                >
                    <div class="collapsible-header-left">
                        <i class="pi pi-link"></i>
                        Custom OpenAI-Compatible Provider
                        <span v-if="config.hasCustomProviderKey" class="badge badge-success" style="margin-left:4px;">Configured</span>
                    </div>
                    <i class="pi pi-chevron-down collapsible-chevron" :class="{ open: sectionsOpen.customProvider }"></i>
                </div>

                <div v-show="sectionsOpen.customProvider" class="collapsible-body">
                    <Card class="provider-card">
                        <template #content>
                            <div class="grid-2">
                                <div class="form-group">
                                    <label>Provider Name</label>
                                    <InputText v-model="config.customProviderName" placeholder="e.g. my-local-llm" fluid />
                                </div>
                                <div class="form-group">
                                    <label>Base URL</label>
                                    <InputText v-model="config.customProviderBaseUrl" placeholder="https://my-api.com/v1" fluid />
                                </div>
                            </div>
                            <div class="api-key-input mt-2">
                                <Password
                                    v-model="keyInputs.customProviderApiKey"
                                    :feedback="false"
                                    toggleMask
                                    :placeholder="config.hasCustomProviderKey ? '••••••••••••••••••••' : 'API key for custom provider'"
                                    fluid
                                    class="flex-1"
                                />
                            </div>
                            <small class="info-text">Must support OpenAI chat completions format (POST /chat/completions).</small>
                        </template>
                    </Card>
                </div>
            </div>

            <!-- ════════════════════════════════════════════════
                 TAB: CHANNELS
            ════════════════════════════════════════════════ -->
            <div v-if="activeTab === 'channels'">
                <p class="section-sub">Expose your AI assistant directly inside messaging apps. Each channel posts to its webhook URL below.</p>

                <!-- WhatsApp -->
                <div class="channel-block">
                    <h4 class="channel-title">
                        <i class="pi pi-whatsapp" style="color:#25D366;"></i>
                        WhatsApp (Meta Graph API)
                    </h4>
                    <div class="grid-2">
                        <div class="form-group">
                            <label>Phone Number ID</label>
                            <InputText v-model="channels.whatsApp.phoneNumberId" placeholder="e.g. 1092837498172" fluid />
                        </div>
                        <div class="form-group">
                            <label>Verify Token</label>
                            <InputText v-model="channels.whatsApp.verifyToken" placeholder="e.g. my_secure_verification_token" fluid />
                        </div>
                    </div>
                    <div class="form-group">
                        <label>Access Token</label>
                        <Password v-model="channels.whatsApp.accessToken" :feedback="false" toggleMask placeholder="Meta Graph API Access Token" fluid />
                    </div>
                    <div class="webhook-info">
                        <strong>Webhook URL:</strong> <code>/api/channel/whatsapp/{{ projectId }}</code>
                    </div>
                </div>

                <!-- Slack -->
                <div class="channel-block">
                    <h4 class="channel-title">
                        <i class="pi pi-slack" style="color:#611f69;"></i>
                        Slack App Integration
                    </h4>
                    <div class="grid-2">
                        <div class="form-group">
                            <label>Bot User OAuth Token</label>
                            <Password v-model="channels.slack.botToken" :feedback="false" toggleMask placeholder="xoxb-your-bot-token" fluid />
                        </div>
                        <div class="form-group">
                            <label>Signing Secret</label>
                            <Password v-model="channels.slack.signingSecret" :feedback="false" toggleMask placeholder="Slack Signing Secret" fluid />
                        </div>
                    </div>
                    <div class="webhook-info">
                        <strong>Request URL (Event Subscriptions):</strong> <code>/api/channel/slack/{{ projectId }}</code>
                    </div>
                </div>

                <!-- Telegram -->
                <div class="channel-block">
                    <h4 class="channel-title">
                        <i class="pi pi-telegram" style="color:#0088cc;"></i>
                        Telegram Bot
                    </h4>
                    <div class="form-group">
                        <label>Bot Token</label>
                        <Password v-model="channels.telegram.botToken" :feedback="false" toggleMask placeholder="123456789:ABCdefGhIJKlmNoPQRsTUVwxyZ" fluid />
                    </div>
                    <div class="webhook-info">
                        <strong>Webhook URL:</strong> <code>/api/channel/telegram/{{ projectId }}</code>
                    </div>
                </div>

                <!-- Microsoft Teams -->
                <div class="channel-block">
                    <h4 class="channel-title">
                        <i class="pi pi-microsoft" style="color:#0078d4;"></i>
                        Microsoft Teams Bot
                    </h4>
                    <div class="grid-2">
                        <div class="form-group">
                            <label>Microsoft App ID</label>
                            <InputText v-model="channels.teams.appId" placeholder="aaaa-bbbb-cccc-dddd" fluid />
                        </div>
                        <div class="form-group">
                            <label>Microsoft App Password</label>
                            <Password v-model="channels.teams.appPassword" :feedback="false" toggleMask placeholder="App Password / Client Secret" fluid />
                        </div>
                    </div>
                    <div class="webhook-info">
                        <strong>Webhook URL:</strong> <code>/api/channel/teams/{{ projectId }}</code>
                    </div>
                </div>
            </div>

            <!-- ════════════════════════════════════════════════
                 TAB: HANDOFF
            ════════════════════════════════════════════════ -->
            <div v-if="activeTab === 'handoff'">
                <p class="section-sub">Allow human agents to take over conversations when the AI cannot resolve the issue.</p>
                <Card class="provider-card">
                    <template #content>
                        <div class="checkbox-group form-group">
                            <Checkbox v-model="config.handoffEnabled" :binary="true" inputId="handoffEnabled" />
                            <label for="handoffEnabled" style="text-transform:none; letter-spacing:0; font-weight:600;">Enable Human Handoff</label>
                        </div>

                        <div v-if="config.handoffEnabled">
                            <div class="form-group">
                                <label>🧠 Escalation Criteria (AI-Powered)</label>
                                <Textarea
                                    v-model="config.handoffEscalationCriteria"
                                    rows="3"
                                    placeholder="e.g. User is frustrated, requests human help, has a billing dispute…"
                                    fluid
                                />
                                <small class="info-text">Describe in plain English when to escalate. The AI detects these situations semantically.</small>
                            </div>

                            <div class="form-group mt-4">
                                <label>Escalation Confidence: {{ config.handoffConfidenceThreshold }}%</label>
                                <Slider v-model="config.handoffConfidenceThreshold" :min="30" :max="100" :step="5" />
                                <small class="info-text">Lower = more sensitive. Higher = more precise.</small>
                            </div>

                            <div class="grid-2 mt-4">
                                <div class="form-group">
                                    <label>Fallback Keywords <small class="info-text">(optional, comma-separated)</small></label>
                                    <InputText v-model="config.handoffTriggerKeywords" placeholder="e.g. human, agent, support" fluid />
                                    <small class="info-text">Instant-match keywords. Run before AI classification at zero cost.</small>
                                </div>
                                <div class="form-group">
                                    <label>Queue Message</label>
                                    <InputText v-model="config.handoffQueueMessage" placeholder="Connecting you with a live agent. Please hold on." fluid />
                                    <small class="info-text">Shown to the user while waiting for an agent.</small>
                                </div>
                            </div>
                        </div>
                    </template>
                </Card>
            </div>

            <!-- ════════════════════════════════════════════════
                 TAB: APPEARANCE
            ════════════════════════════════════════════════ -->
            <div v-if="activeTab === 'appearance'">
                <p class="section-sub">Customize how the chat widget looks on your website. All changes reflect in real-time in the preview below.</p>
                
                <div class="grid-2">
                    <!-- Left Side: Styling Controls -->
                    <div style="display: flex; flex-direction: column; gap: 16px;">
                        <!-- Core Layout -->
                        <Card class="config-card">
                            <template #content>
                                <h3 class="section-heading" style="margin-bottom: 12px; font-size: 0.95rem; display: flex; align-items: center; gap: 8px;">
                                    <i class="pi pi-cog" style="color: var(--p-primary-500)"></i> Core Layout & Typography
                                </h3>
                                <div class="grid-2">
                                    <div class="form-group">
                                        <label>Primary Accent Color</label>
                                        <div class="color-picker-wrapper">
                                            <input type="color" v-model="theme.primaryColor" class="color-input" />
                                            <InputText v-model="theme.primaryColor" class="color-text" placeholder="#39a7b9" fluid />
                                        </div>
                                    </div>
                                    <div class="form-group">
                                        <label>Widget Background</label>
                                        <div class="color-picker-wrapper">
                                            <input type="color" v-model="theme.bgColor" class="color-input" />
                                            <InputText v-model="theme.bgColor" class="color-text" placeholder="#ffffff" fluid />
                                        </div>
                                    </div>
                                    <div class="form-group">
                                        <label>Font Family</label>
                                        <Select v-model="theme.fontFamily" :options="fontOptions" optionLabel="label" optionValue="value" fluid />
                                    </div>
                                    <div class="form-group">
                                        <label>Position on Screen</label>
                                        <Select v-model="theme.position" :options="positionOptions" optionLabel="label" optionValue="value" fluid />
                                    </div>
                                </div>
                            </template>
                        </Card>

                        <!-- Header Customization -->
                        <Card class="config-card">
                            <template #content>
                                <h3 class="section-heading" style="margin-bottom: 12px; font-size: 0.95rem; display: flex; align-items: center; gap: 8px;">
                                    <i class="pi pi-window-minimize" style="color: var(--p-primary-500)"></i> Header Branding
                                </h3>
                                <div class="grid-2" style="margin-bottom: 12px;">
                                    <div class="form-group">
                                        <label>Widget Title</label>
                                        <InputText v-model="theme.title" :placeholder="config.name || 'AI Assistant'" fluid />
                                    </div>
                                    <div class="form-group">
                                        <label>Widget Subtitle</label>
                                        <InputText v-model="theme.subtitle" placeholder="e.g. Ask me anything" fluid />
                                    </div>
                                </div>
                                <div class="grid-2">
                                    <div class="form-group">
                                        <label>Header Background Color</label>
                                        <div class="color-picker-wrapper">
                                            <input type="color" v-model="theme.headerBgColor" class="color-input" />
                                            <InputText v-model="theme.headerBgColor" class="color-text" placeholder="Same as primary" fluid />
                                        </div>
                                    </div>
                                    <div class="form-group">
                                        <label>Header Text Color</label>
                                        <div class="color-picker-wrapper">
                                            <input type="color" v-model="theme.headerTextColor" class="color-input" />
                                            <InputText v-model="theme.headerTextColor" class="color-text" placeholder="#ffffff" fluid />
                                        </div>
                                    </div>
                                </div>
                            </template>
                        </Card>

                        <!-- Message Bubbles & Radii -->
                        <Card class="config-card">
                            <template #content>
                                <h3 class="section-heading" style="margin-bottom: 12px; font-size: 0.95rem; display: flex; align-items: center; gap: 8px;">
                                    <i class="pi pi-comments" style="color: var(--p-primary-500)"></i> Message Bubbles & Chat Area
                                </h3>
                                <div class="grid-2" style="margin-bottom: 12px;">
                                    <div class="form-group">
                                        <label>User Bubble Background</label>
                                        <div class="color-picker-wrapper">
                                            <input type="color" v-model="theme.userBubbleBgColor" class="color-input" />
                                            <InputText v-model="theme.userBubbleBgColor" class="color-text" placeholder="Same as primary" fluid />
                                        </div>
                                    </div>
                                    <div class="form-group">
                                        <label>User Bubble Text</label>
                                        <div class="color-picker-wrapper">
                                            <input type="color" v-model="theme.userBubbleTextColor" class="color-input" />
                                            <InputText v-model="theme.userBubbleTextColor" class="color-text" placeholder="#ffffff" fluid />
                                        </div>
                                    </div>
                                </div>
                                <div class="grid-2" style="margin-bottom: 12px;">
                                    <div class="form-group">
                                        <label>Bot Bubble Background</label>
                                        <div class="color-picker-wrapper">
                                            <input type="color" v-model="theme.botBubbleBgColor" class="color-input" />
                                            <InputText v-model="theme.botBubbleBgColor" class="color-text" placeholder="#ffffff" fluid />
                                        </div>
                                    </div>
                                    <div class="form-group">
                                        <label>Bot Bubble Text</label>
                                        <div class="color-picker-wrapper">
                                            <input type="color" v-model="theme.botBubbleTextColor" class="color-input" />
                                            <InputText v-model="theme.botBubbleTextColor" class="color-text" placeholder="#1e293b" fluid />
                                        </div>
                                    </div>
                                </div>
                                <div class="grid-2">
                                    <div class="form-group">
                                        <label>Chat Area Background</label>
                                        <div class="color-picker-wrapper">
                                            <input type="color" v-model="theme.chatBgColor" class="color-input" />
                                            <InputText v-model="theme.chatBgColor" class="color-text" placeholder="#f7f9fc" fluid />
                                        </div>
                                    </div>
                                    <div class="form-group">
                                        <label>Input Placeholder Text</label>
                                        <InputText v-model="theme.placeholder" placeholder="Message AI Assistant..." fluid />
                                    </div>
                                </div>
                            </template>
                        </Card>

                        <!-- Launcher & Shapes -->
                        <Card class="config-card">
                            <template #content>
                                <h3 class="section-heading" style="margin-bottom: 12px; font-size: 0.95rem; display: flex; align-items: center; gap: 8px;">
                                    <i class="pi pi-tablet" style="color: var(--p-primary-500)"></i> Launcher Button & Border Radii
                                </h3>
                                <div class="grid-2" style="margin-bottom: 12px;">
                                    <div class="form-group">
                                        <label>Launcher Background</label>
                                        <div class="color-picker-wrapper">
                                            <input type="color" v-model="theme.launcherBgColor" class="color-input" />
                                            <InputText v-model="theme.launcherBgColor" class="color-text" placeholder="Same as primary" fluid />
                                        </div>
                                    </div>
                                    <div class="form-group">
                                        <label>Launcher Icon Color</label>
                                        <div class="color-picker-wrapper">
                                            <input type="color" v-model="theme.launcherIconColor" class="color-input" />
                                            <InputText v-model="theme.launcherIconColor" class="color-text" placeholder="#ffffff" fluid />
                                        </div>
                                    </div>
                                </div>
                                <div class="grid-3">
                                    <div class="form-group">
                                        <label>Chatbox Corner Radius</label>
                                        <InputNumber v-model="theme.chatBorderRadius" :min="0" :max="40" suffix="px" showButtons fluid />
                                    </div>
                                    <div class="form-group">
                                        <label>Message Corner Radius</label>
                                        <InputNumber v-model="theme.bubbleBorderRadius" :min="0" :max="30" suffix="px" showButtons fluid />
                                    </div>
                                    <div class="form-group">
                                        <label>Launcher Corner Radius</label>
                                        <InputNumber v-model="theme.launcherBorderRadius" :min="0" :max="30" suffix="px" showButtons fluid />
                                    </div>
                                </div>
                            </template>
                        </Card>
                    </div>

                    <!-- Right Side: Real-Time Preview Panel -->
                    <div style="display: flex; flex-direction: column; gap: 12px;">
                        <h3 class="section-heading" style="margin-left: 8px; margin-bottom: 0;">Interactive Live Preview</h3>
                        <div
                            class="theme-preview"
                            :style="{
                                '--preview-primary': theme.primaryColor,
                                '--preview-bg': theme.bgColor,
                                '--preview-font': theme.fontFamily === 'system-ui' ? 'system-ui, sans-serif' : theme.fontFamily + ', sans-serif',
                                '--preview-header-bg': theme.headerBgColor || theme.primaryColor,
                                '--preview-header-text': theme.headerTextColor || '#ffffff',
                                '--preview-user-msg-bg': theme.userBubbleBgColor || theme.primaryColor,
                                '--preview-user-msg-text': theme.userBubbleTextColor || '#ffffff',
                                '--preview-bot-msg-bg': theme.botBubbleBgColor || '#ffffff',
                                '--preview-bot-msg-text': theme.botBubbleTextColor || '#1e293b',
                                '--preview-chat-bg': theme.chatBgColor || '#f8fafc',
                                '--preview-launcher-bg': theme.launcherBgColor || theme.primaryColor,
                                '--preview-launcher-icon': theme.launcherIconColor || '#ffffff',
                                '--preview-launcher-radius': (theme.launcherBorderRadius !== undefined ? theme.launcherBorderRadius : 16) + 'px',
                                '--preview-chat-radius': (theme.chatBorderRadius !== undefined ? theme.chatBorderRadius : 10) + 'px',
                                '--preview-bubble-radius': (theme.bubbleBorderRadius !== undefined ? theme.bubbleBorderRadius : 20) + 'px',
                                'justify-content': theme.position === 'bottom-left' ? 'flex-start' : 'flex-end',
                                'align-items': 'stretch',
                                'height': 'auto',
                                'min-height': '460px'
                            }"
                        >
                            <div :style="{ display: 'flex', 'flex-direction': 'column', 'align-items': theme.position === 'bottom-left' ? 'flex-start' : 'flex-end', gap: '16px', width: '100%', 'justify-content': 'flex-end' }">
                                <!-- Mock Chatbox -->
                                <div class="preview-widget" :style="{ 'border-radius': 'var(--preview-chat-radius)', width: '100%', 'max-width': '340px' }">
                                    <div class="preview-header" :style="{ background: 'var(--preview-header-bg)', color: 'var(--preview-header-text)', display: 'flex', 'flex-direction': 'column', gap: '2px', padding: '12px 16px' }">
                                        <span style="font-weight: 600; font-size: 0.95rem; line-height: 1.2;">{{ theme.title || config.name || 'AI Assistant' }}</span>
                                        <span v-if="theme.subtitle" style="font-size: 0.75rem; opacity: 0.85; font-weight: 400; line-height: 1.2;">{{ theme.subtitle }}</span>
                                    </div>
                                    <div class="preview-body" :style="{ background: 'var(--preview-chat-bg)', padding: '16px', display: 'flex', 'flex-direction': 'column', gap: '12px' }">
                                        <div class="preview-msg bot" :style="{ background: 'var(--preview-bot-msg-bg)', color: 'var(--preview-bot-msg-text)', 'border-radius': 'var(--preview-bubble-radius)', 'border-bottom-left-radius': '4px', padding: '10px 14px', 'font-size': '0.85rem', 'align-self': 'flex-start', 'max-width': '85%', 'box-shadow': '0 1px 2px rgba(0,0,0,0.05)' }">
                                            Hi! How can I help you today?
                                        </div>
                                        <div class="preview-msg user" :style="{ background: 'var(--preview-user-msg-bg)', color: 'var(--preview-user-msg-text)', 'border-radius': 'var(--preview-bubble-radius)', 'border-bottom-right-radius': '4px', padding: '10px 14px', 'font-size': '0.85rem', 'align-self': 'flex-end', 'max-width': '85%', 'box-shadow': '0 1px 2px rgba(0,0,0,0.05)' }">
                                            I have a question about pricing.
                                        </div>
                                    </div>
                                    <div class="preview-input" :style="{ background: 'var(--preview-bg)', padding: '12px 16px', display: 'flex', 'justify-content': 'space-between', 'align-items': 'center', 'border-top': '1px solid var(--p-surface-200)' }">
                                        <span style="font-size: 0.85rem; opacity: 0.7; color: var(--p-text-color-secondary);">{{ theme.placeholder || 'Message AI Assistant...' }}</span>
                                        <div class="preview-send" :style="{ background: 'var(--preview-user-msg-bg)', color: 'var(--preview-user-msg-text)', width: '28px', height: '28px', 'border-radius': '50%', display: 'flex', 'align-items': 'center', 'justify-content': 'center' }">
                                            <i class="pi pi-send" style="font-size: 0.75rem;"></i>
                                        </div>
                                    </div>
                                </div>
                                
                                <!-- Floating Mock Toggle FAB -->
                                <div class="preview-launcher" :style="{ background: 'var(--preview-launcher-bg)', color: 'var(--preview-launcher-icon)', 'border-radius': 'var(--preview-launcher-radius)', display: 'flex', 'align-items': 'center', 'justify-content': 'center', width: '52px', height: '52px', 'box-shadow': '0 4px 12px rgba(0,0,0,0.15)', cursor: 'pointer' }">
                                    <i class="pi pi-comments" style="font-size: 1.4rem;"></i>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>

        </main>

        <!-- ═══════════════════════════════════════════════════
             DIALOG: ADMINISTRATIVE CONTROLS
        ═══════════════════════════════════════════════════ -->
        <Dialog
            v-model:visible="showAdminDialog"
            header="Administrative Controls"
            :modal="true"
            :style="{ width: '680px' }"
            :draggable="false"
        >
            <div class="grid-2">
                <div class="form-group">
                    <label>Rate Limit (Requests)</label>
                    <InputNumber v-model="config.rateLimitRequests" placeholder="0 = No limit" fluid />
                    <small class="info-text">Max requests allowed within the window.</small>
                </div>
                <div class="form-group">
                    <label>Window (Minutes)</label>
                    <InputNumber v-model="config.rateLimitWindowMinutes" fluid />
                    <small class="info-text">Time window for rate limiting.</small>
                </div>
            </div>

            <div class="grid-3 mt-4">
                <div class="form-group">
                    <label>Log Retention (Days)</label>
                    <InputNumber v-model="config.logRetentionDays" fluid />
                    <small class="info-text">Days to keep unpinned logs.</small>
                </div>
                <div class="form-group">
                    <label>Max Logs / Session</label>
                    <InputNumber v-model="config.maxLogsPerSession" placeholder="0 = No limit" fluid />
                    <small class="info-text">Prune logs exceeding limit.</small>
                </div>
                <div class="form-group">
                    <label>Max Sessions / Project</label>
                    <InputNumber v-model="config.maxSessionsPerProject" placeholder="0 = No limit" fluid />
                    <small class="info-text">Prune oldest inactive sessions.</small>
                </div>
            </div>

            <div class="grid-2 mt-4">
                <div class="form-group">
                    <label>Spending Cap (USD)</label>
                    <InputNumber v-model="config.maxSpendLimit" mode="currency" currency="USD" locale="en-US" placeholder="0 = No limit" fluid />
                    <small class="info-text">Total budget for this configuration.</small>
                </div>
                <div class="form-group">
                    <label>Current Spend (read-only)</label>
                    <div class="spend-display">
                        <span class="spend-value">${{ config.currentSpend.toFixed(6) }}</span>
                        <span class="spend-progress" :style="{ width: config.maxSpendLimit > 0 ? (Math.min(config.currentSpend / config.maxSpendLimit, 1) * 100) + '%' : '0%' }"></span>
                    </div>
                </div>
            </div>

            <div class="form-group mt-4">
                <div class="flex-between">
                    <label>Chat Suggestions</label>
                    <Button icon="pi pi-plus" label="Add" severity="secondary" size="small" text @click="addSuggestion" :disabled="config.suggestions.length >= 4" />
                </div>
                <div class="suggestions-list">
                    <div v-for="(_, index) in config.suggestions" :key="index" class="suggestion-item">
                        <InputText v-model="config.suggestions[index]" placeholder="Enter a suggested prompt…" fluid />
                        <Button icon="pi pi-times" severity="danger" text rounded @click="removeSuggestion(index)" />
                    </div>
                    <div v-if="config.suggestions.length === 0" class="empty-suggestions">
                        No suggestions yet. These appear as quick-start buttons in the chat.
                    </div>
                </div>
                <small class="info-text">Maximum 4 suggested prompts shown to the user on start.</small>
            </div>

            <template #footer>
                <Button label="Done" @click="showAdminDialog = false" />
            </template>
        </Dialog>

        <!-- ═══════════════════════════════════════════════════
             DIALOG: TEMPLATE VARIABLES
        ═══════════════════════════════════════════════════ -->
        <Dialog
            v-model:visible="showTemplateVarsDialog"
            header="Template Variables"
            :modal="true"
            :style="{ width: '475px' }"
            :draggable="false"
        >
            <p class="info-text" style="margin-bottom:16px;">
                Define values for <code v-pre>{{variable}}</code> placeholders in your system prompt.
                <code v-pre>{{date}}</code> and <code v-pre>{{time}}</code> are auto-filled at runtime.
            </p>
            <div class="template-vars-list">
                <div v-for="(v, index) in templateVars" :key="index" class="template-var-row">
                    <InputText v-model="v.key" placeholder="Variable name (e.g. company)" fluid />
                    <span class="var-equals">=</span>
                    <InputText v-model="v.value" placeholder="Value (e.g. Acme Inc)" fluid />
                    <Button icon="pi pi-times" severity="danger" text rounded @click="removeTemplateVar(index)" />
                </div>
                <div v-if="templateVars.length === 0" class="empty-suggestions">
                    No template variables defined. Add variables to personalize your system prompt.
                </div>
            </div>
            <Button icon="pi pi-plus" label="Add Variable" severity="secondary" text size="small" class="mt-2" @click="addTemplateVar" />

            <template #footer>
                <Button label="Done" @click="showTemplateVarsDialog = false" />
            </template>
        </Dialog>

        <!-- ═══════════════════════════════════════════════════
             DIALOG: PROMPT HISTORY
        ═══════════════════════════════════════════════════ -->
        <Dialog
            v-model:visible="showHistoryDialog"
            header="Prompt History"
            :modal="true"
            :style="{ width: '640px', maxHeight: '80vh' }"
            :draggable="false"
            :contentStyle="{ overflowY: 'auto' }"
        >
            <div v-if="loadingHistory" class="history-loading">
                <i class="pi pi-spin pi-spinner"></i> Loading history…
            </div>
            <div v-else-if="historyEntries.length === 0" class="empty-suggestions">
                No prompt history yet. History is created automatically when you change the system prompt or model.
            </div>
            <div v-else class="history-timeline">
                <div v-for="entry in historyEntries" :key="entry.id" class="history-entry">
                    <div class="history-dot"></div>
                    <div>
                        <div class="history-header-row">
                            <span class="history-date">{{ formatDate(entry.createdAt) }}</span>
                            <span class="history-model">{{ entry.defaultProvider }} / {{ entry.defaultModel }}</span>
                        </div>
                        <p class="history-prompt">{{ truncate(entry.systemPrompt, 200) }}</p>
                        <div v-if="entry.changeNote" class="history-note">
                            <i class="pi pi-comment"></i> {{ entry.changeNote }}
                        </div>
                        <Button
                            :label="restoringId === entry.id ? 'Restoring…' : 'Restore This Version'"
                            icon="pi pi-replay"
                            severity="secondary"
                            outlined
                            size="small"
                            :disabled="restoringId !== null"
                            @click="restoreVersion(entry.id)"
                        />
                    </div>
                </div>
            </div>

            <template #footer>
                <Button label="Close" severity="secondary" @click="showHistoryDialog = false" />
            </template>
        </Dialog>

    </div>
</template>

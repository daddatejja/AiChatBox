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
import Slider from 'primevue/slider';

const route = useRoute();
const { apiFetch } = useApi();
const projectId = computed(() => route.params.projectId as string);

interface Rule {
    id: string;
    type: string;
    trigger: string;
    intentLabel: string | null;
    commandName: string | null;
    commandTriggerChar: string;
    commandDescription: string | null;
    responseType: string;
    responsePayload: string | null;
    response: string;
    confidenceThreshold: number;
    priority: number;
    isActive: boolean;
    createdAt: string;
}

const rules = ref<Rule[]>([]);
const loading = ref(false);
const showDialog = ref(false);
const editing = ref<string | null>(null);
const testMessage = ref('');
const testResult = ref<{ matched: boolean; response: string | null; matchType?: string; confidence?: number } | null>(null);
const testing = ref(false);

const form = reactive({
    type: 'intent',
    trigger: '',
    intentLabel: '',
    commandName: '',
    commandTriggerChar: '/',
    commandDescription: '',
    responseType: 'text',
    responsePayload: '',
    response: '',
    confidenceThreshold: 75,
    priority: 0,
    isActive: true,
    
    // Rich response visual sub-models
    redirectUrl: '',
    countdownText: 'Redirecting you in {seconds} seconds...',
    redirectSeconds: 5,
    
    cardTitle: '',
    cardBody: '',
    cardButtonLabel: '',
    cardButtonUrl: '',
    
    fileName: '',
    fileUrl: '',
    fileMimeType: 'pdf',
    
    formTitle: '',
    formSubmitLabel: 'Submit',
    formSubmitUrl: '',
    formFields: [] as Array<{ name: string; label: string; type: string; required: boolean; options?: string }>,
    
    toolName: '',
    toolArguments: '{}'
});

const typeOptions = [
    { label: '🧠 AI Intent (Smart)', value: 'intent', desc: 'Uses AI to understand user intent — describe what the user is asking about in plain English' },
    { label: '⚡ Command Trigger', value: 'command', desc: 'Matches when a user types a command with a special character (e.g. /pricing)' },
    { label: 'Keyword Match', value: 'keyword', desc: 'Matches when ALL comma-separated keywords appear in the message' },
    { label: 'Exact Match', value: 'exact', desc: 'Matches the entire message exactly (case-insensitive)' },
    { label: 'Regex Pattern', value: 'regex', desc: 'Matches using a regular expression pattern' }
];

const responseTypeOptions = [
    { label: '📝 Plain Text Response', value: 'text' },
    { label: '🔗 Countdown Redirect Link', value: 'redirect' },
    { label: '🎴 Inline Action Card', value: 'card' },
    { label: '📁 File Attachment/Download', value: 'file' },
    { label: '📋 Interactive Dynamic Form', value: 'form' },
    { label: '🛠️ Trigger Frontend Tool', value: 'tool_call' }
];

const triggerCharOptions = [
    { label: 'Slash (/)', value: '/' },
    { label: 'Hash (#)', value: '#' },
    { label: 'At (@)', value: '@' }
];

const fileMimeOptions = [
    { label: 'PDF Document (.pdf)', value: 'pdf' },
    { label: 'Excel Spreadsheet (.xlsx, .csv)', value: 'excel' },
    { label: 'Generic File Download', value: 'generic' }
];

const formFieldTypeOptions = [
    { label: 'Single-line Text', value: 'text' },
    { label: 'Multi-line Textarea', value: 'textarea' },
    { label: 'Email Address', value: 'email' },
    { label: 'Dropdown / Select Option List', value: 'select' },
    { label: 'Checkbox Options List', value: 'checkbox' },
    { label: 'Radio Options List', value: 'radio' }
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
    form.type = 'intent';
    form.trigger = '';
    form.intentLabel = '';
    
    form.commandName = '';
    form.commandTriggerChar = '/';
    form.commandDescription = '';
    
    form.responseType = 'text';
    form.responsePayload = '';
    form.response = '';
    form.confidenceThreshold = 75;
    form.priority = 0;
    form.isActive = true;
    
    form.redirectUrl = '';
    form.countdownText = 'Redirecting you to our page in {seconds} seconds...';
    form.redirectSeconds = 5;
    form.cardTitle = '';
    form.cardBody = '';
    form.cardButtonLabel = '';
    form.cardButtonUrl = '';
    form.fileName = '';
    form.fileUrl = '';
    form.fileMimeType = 'pdf';
    form.formTitle = '';
    form.formSubmitLabel = 'Submit';
    form.formSubmitUrl = '';
    form.formFields = [];
    form.toolName = '';
    form.toolArguments = '{}';
    
    showDialog.value = true;
}

function openEdit(rule: Rule) {
    editing.value = rule.id;
    form.type = rule.type;
    form.trigger = rule.trigger;
    form.intentLabel = rule.intentLabel || '';
    
    form.commandName = rule.commandName || '';
    form.commandTriggerChar = rule.commandTriggerChar || '/';
    form.commandDescription = rule.commandDescription || '';
    
    form.responseType = rule.responseType || 'text';
    form.responsePayload = rule.responsePayload || '';
    form.response = rule.response;
    form.confidenceThreshold = Math.round(rule.confidenceThreshold * 100);
    form.priority = rule.priority;
    form.isActive = rule.isActive;
    
    // Reset visual sub-models first
    form.redirectUrl = '';
    form.countdownText = 'Redirecting you in {seconds} seconds...';
    form.redirectSeconds = 5;
    form.cardTitle = '';
    form.cardBody = '';
    form.cardButtonLabel = '';
    form.cardButtonUrl = '';
    form.fileName = '';
    form.fileUrl = '';
    form.fileMimeType = 'pdf';
    form.formTitle = '';
    form.formSubmitLabel = 'Submit';
    form.formSubmitUrl = '';
    form.formFields = [];
    form.toolName = '';
    form.toolArguments = '{}';
    
    // Parse response payload if present
    if (rule.responsePayload) {
        try {
            const data = JSON.parse(rule.responsePayload);
            if (form.responseType === 'redirect') {
                form.redirectUrl = data.url || '';
                form.countdownText = data.countdownText || '';
                form.redirectSeconds = typeof data.seconds === 'number' ? data.seconds : 5;
            } else if (form.responseType === 'card') {
                form.cardTitle = data.title || '';
                form.cardBody = data.body || '';
                form.cardButtonLabel = data.buttonLabel || '';
                form.cardButtonUrl = data.buttonUrl || '';
            } else if (form.responseType === 'file') {
                form.fileName = data.fileName || '';
                form.fileUrl = data.fileUrl || '';
                form.fileMimeType = data.mimeType || 'pdf';
            } else if (form.responseType === 'form') {
                form.formTitle = data.title || '';
                form.formSubmitLabel = data.submitLabel || 'Submit';
                form.formSubmitUrl = data.submitUrl || '';
                form.formFields = Array.isArray(data.fields) ? data.fields.map((f: any) => ({
                    name: f.name || '',
                    label: f.label || '',
                    type: f.type || 'text',
                    required: !!f.required,
                    options: Array.isArray(f.options) ? f.options.join(', ') : (f.options || '')
                })) : [];
            } else if (form.responseType === 'tool_call') {
                form.toolName = data.toolName || '';
                form.toolArguments = data.arguments ? JSON.stringify(data.arguments, null, 2) : '{}';
            }
        } catch (e) {
            console.error("Error parsing response payload:", e);
        }
    }
    showDialog.value = true;
}

async function saveRule() {
    let payloadStr: string | null = null;
    let textResponse = form.response;
    
    if (form.responseType === 'redirect') {
        payloadStr = JSON.stringify({
            url: form.redirectUrl,
            countdownText: form.countdownText,
            seconds: Number(form.redirectSeconds) || 5
        });
        textResponse = `Redirecting you to ${form.redirectUrl}...`;
    } else if (form.responseType === 'card') {
        payloadStr = JSON.stringify({
            title: form.cardTitle,
            body: form.cardBody,
            buttonLabel: form.cardButtonLabel,
            buttonUrl: form.cardButtonUrl
        });
        textResponse = `${form.cardTitle}\n\n${form.cardBody}\n\nLink: ${form.cardButtonUrl}`;
    } else if (form.responseType === 'file') {
        payloadStr = JSON.stringify({
            fileName: form.fileName,
            fileUrl: form.fileUrl,
            mimeType: form.fileMimeType
        });
        textResponse = `Download File: ${form.fileName} (${form.fileUrl})`;
    } else if (form.responseType === 'form') {
        const fields = form.formFields.map(f => ({
            name: f.name,
            label: f.label,
            type: f.type,
            required: f.required,
            options: (f.type === 'select' || f.type === 'checkbox' || f.type === 'radio') && f.options ? f.options.split(',').map(s => s.trim()) : undefined
        }));
        payloadStr = JSON.stringify({
            title: form.formTitle,
            submitLabel: form.formSubmitLabel,
            submitUrl: form.formSubmitUrl,
            fields
        });
        textResponse = `Please fill out the form: ${form.formTitle}`;
    } else if (form.responseType === 'tool_call') {
        let argsObj = {};
        try {
            argsObj = JSON.parse(form.toolArguments);
        } catch (e) {
            console.error("Invalid tool arguments JSON, keeping empty object");
        }
        payloadStr = JSON.stringify({
            toolName: form.toolName,
            arguments: argsObj
        });
        textResponse = `Executing custom tool: ${form.toolName}`;
    }
    
    // Set trigger pattern for command rule type
    let finalTrigger = form.trigger;
    if (form.type === 'command') {
        finalTrigger = `${form.commandTriggerChar}${form.commandName}`;
    }
    
    const payload = {
        type: form.type,
        trigger: finalTrigger,
        intentLabel: form.type === 'intent' && form.intentLabel?.trim() ? form.intentLabel.trim() : null,
        
        commandName: form.type === 'command' ? form.commandName : null,
        commandTriggerChar: form.type === 'command' ? form.commandTriggerChar : null,
        commandDescription: form.type === 'command' ? form.commandDescription : null,
        
        responseType: form.responseType,
        responsePayload: payloadStr,
        response: textResponse,
        
        confidenceThreshold: form.confidenceThreshold / 100,
        priority: form.priority,
        isActive: form.isActive
    };
    
    if (editing.value) {
        await apiFetch(`/api/rules/${editing.value}`, {
            method: 'PUT',
            body: JSON.stringify(payload)
        });
    } else {
        await apiFetch(`/api/rules/project/${projectId.value}`, {
            method: 'POST',
            body: JSON.stringify(payload)
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
        case 'intent': return 'User is asking about pricing, subscription plans, or costs';
        case 'keyword': return 'pricing, plans (comma-separated, ALL must match)';
        case 'exact': return 'What are your business hours?';
        case 'regex': return '\\b(refund|return)\\b';
        default: return '';
    }
}

function triggerLabel(type: string): string {
    return type === 'intent' ? 'Intent Description' : 'Trigger';
}

function responseTypeLabel(type: string): string {
    switch (type) {
        case 'text': return 'TEXT';
        case 'redirect': return 'REDIRECT';
        case 'card': return 'CARD';
        case 'file': return 'FILE';
        case 'form': return 'FORM';
        case 'tool_call': return 'TOOL CALL';
        default: return type?.toUpperCase() || 'TEXT';
    }
}

function addFormField() {
    form.formFields.push({
        name: '',
        label: '',
        type: 'text',
        required: false,
        options: ''
    });
}

function removeFormField(index: number) {
    form.formFields.splice(index, 1);
}

function isFormSaveDisabled() {
    if (form.type === 'command') {
        if (!form.commandName.trim()) return true;
    } else {
        if (!form.trigger.trim()) return true;
    }
    
    // Check based on response type
    if (form.responseType === 'text') {
        return !form.response.trim();
    } else if (form.responseType === 'redirect') {
        return !form.redirectUrl.trim();
    } else if (form.responseType === 'card') {
        return !form.cardTitle.trim() || !form.cardBody.trim();
    } else if (form.responseType === 'file') {
        return !form.fileName.trim() || !form.fileUrl.trim();
    } else if (form.responseType === 'form') {
        if (!form.formTitle.trim()) return true;
        if (form.formFields.length === 0) return true;
        return form.formFields.some(f => !f.name.trim() || !f.label.trim());
    } else if (form.responseType === 'tool_call') {
        return !form.toolName.trim();
    }
    return false;
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
                            <span v-if="testResult.matched && testResult.matchType" class="test-match-type">via {{ testResult.matchType }}</span>
                            <span v-if="testResult.matched && testResult.confidence" class="test-confidence">{{ Math.round(testResult.confidence * 100) }}% confidence</span>
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
                            <strong>{{ rule.type === 'intent' ? 'Intent Description:' : (rule.type === 'command' ? 'Command:' : 'Trigger Pattern:') }}</strong>
                            <span v-if="rule.type === 'intent'" class="intent-text">{{ rule.trigger }}</span>
                            <span v-else-if="rule.type === 'command'" class="command-trigger-text">
                                <span class="cmd-trigger-char">{{ rule.commandTriggerChar || '/' }}</span>{{ rule.commandName }}
                                <span class="cmd-desc-sub" v-if="rule.commandDescription"> — {{ rule.commandDescription }}</span>
                            </span>
                            <code v-else>{{ rule.trigger }}</code>
                        </div>
                        <div class="rule-response-preview">
                            <div class="response-badge-row">
                                <span class="response-type-badge" :class="'res-' + (rule.responseType || 'text')">
                                    {{ responseTypeLabel(rule.responseType || 'text') }}
                                </span>
                                <span class="response-preview-text">
                                    {{ rule.response.length > 120 ? rule.response.substring(0, 120) + '...' : rule.response }}
                                </span>
                            </div>
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
        <Dialog v-model:visible="showDialog" :header="editing ? 'Edit Rule' : 'Create Rule'" modal :style="{ width: '720px' }">
            <div class="dialog-form">
                <div class="form-group">
                    <label>Match Trigger Type</label>
                    <Select v-model="form.type" :options="typeOptions" optionLabel="label" optionValue="value" fluid />
                    <small class="info-text">{{ selectedTypeDesc }}</small>
                </div>

                <!-- Match Type Conditional Content -->
                <div v-if="form.type === 'command'" class="command-settings-panel">
                    <div class="form-row">
                        <div class="form-group flex-1">
                            <label>Trigger Character</label>
                            <Select v-model="form.commandTriggerChar" :options="triggerCharOptions" optionLabel="label" optionValue="value" fluid />
                        </div>
                        <div class="form-group flex-2">
                            <label>Command Name</label>
                            <InputText v-model="form.commandName" placeholder="pricing" fluid />
                            <small class="info-text font-italic">No spaces allowed. Users type {{ form.commandTriggerChar || '/' }}{{ form.commandName || 'pricing' }} to run.</small>
                        </div>
                    </div>
                    <div class="form-group mt-2">
                        <label>Command Description</label>
                        <InputText v-model="form.commandDescription" placeholder="View current product pricing and tiers" fluid />
                        <small class="info-text">Shown to the user in the autocomplete popup list.</small>
                    </div>
                </div>

                <div v-else class="form-group">
                    <label>{{ triggerLabel(form.type) }}</label>
                    <Textarea v-if="form.type === 'intent'" v-model="form.trigger" rows="3" :placeholder="triggerPlaceholder(form.type)" fluid />
                    <InputText v-else v-model="form.trigger" :placeholder="triggerPlaceholder(form.type)" fluid />
                    <small v-if="form.type === 'intent'" class="info-text">Describe in plain English what the user is asking about. The AI will match semantically — no keyword guessing needed.</small>
                </div>

                <div v-if="form.type === 'intent'" class="form-group">
                    <label>Intent Label <small class="info-text">(optional short ID, e.g. "pricing")</small></label>
                    <InputText v-model="form.intentLabel" placeholder="pricing" fluid />
                </div>

                <hr class="form-divider" />

                <!-- Rich Response Builder Section -->
                <h3 class="form-section-title">✨ Rule Response Configuration</h3>

                <div class="form-group">
                    <label>Response Type</label>
                    <Select v-model="form.responseType" :options="responseTypeOptions" optionLabel="label" optionValue="value" fluid />
                </div>

                <!-- Rich Response Type Fields -->
                <div v-if="form.responseType === 'text'" class="form-group">
                    <label>Plain Text Response</label>
                    <Textarea v-model="form.response" rows="4" placeholder="The text response to send when this rule matches..." fluid />
                </div>

                <div v-else-if="form.responseType === 'redirect'" class="response-sub-panel">
                    <div class="form-group">
                        <label>Destination Redirect URL</label>
                        <InputText v-model="form.redirectUrl" placeholder="https://example.com/pricing" fluid />
                    </div>
                    <div class="form-row mt-2">
                        <div class="form-group flex-3">
                            <label>Countdown Message</label>
                            <InputText v-model="form.countdownText" placeholder="Redirecting you in {seconds} seconds..." fluid />
                            <small class="info-text">Use <code>{seconds}</code> to render the countdown timer.</small>
                        </div>
                        <div class="form-group flex-1">
                            <label>Delay (Seconds)</label>
                            <InputNumber v-model="form.redirectSeconds" :min="1" :max="60" fluid />
                        </div>
                    </div>
                </div>

                <div v-else-if="form.responseType === 'card'" class="response-sub-panel">
                    <div class="form-group">
                        <label>Card Title</label>
                        <InputText v-model="form.cardTitle" placeholder="Special Offer Available! 🎉" fluid />
                    </div>
                    <div class="form-group mt-2">
                        <label>Card Body Description</label>
                        <Textarea v-model="form.cardBody" rows="3" placeholder="Get 20% off our yearly premium plans if you subscribe today!" fluid />
                    </div>
                    <div class="form-row mt-2">
                        <div class="form-group flex-1">
                            <label>Button Label (Optional)</label>
                            <InputText v-model="form.cardButtonLabel" placeholder="Claim Discount" fluid />
                        </div>
                        <div class="form-group flex-1">
                            <label>Button URL (Optional)</label>
                            <InputText v-model="form.cardButtonUrl" placeholder="https://example.com/checkout?promo=discount" fluid />
                        </div>
                    </div>
                </div>

                <div v-else-if="form.responseType === 'file'" class="response-sub-panel">
                    <div class="form-row">
                        <div class="form-group flex-2">
                            <label>File Name</label>
                            <InputText v-model="form.fileName" placeholder="Quarterly_Report_2026.pdf" fluid />
                        </div>
                        <div class="form-group flex-1">
                            <label>File Style Theme</label>
                            <Select v-model="form.fileMimeType" :options="fileMimeOptions" optionLabel="label" optionValue="value" fluid />
                        </div>
                    </div>
                    <div class="form-group mt-2">
                        <label>Download Link URL</label>
                        <InputText v-model="form.fileUrl" placeholder="https://example.com/files/report.pdf" fluid />
                    </div>
                </div>

                <div v-else-if="form.responseType === 'form'" class="response-sub-panel">
                    <div class="form-group">
                        <label>Form Title</label>
                        <InputText v-model="form.formTitle" placeholder="Request a Consultation" fluid />
                    </div>
                    <div class="form-row mt-2">
                        <div class="form-group flex-1">
                            <label>Submit Button Text</label>
                            <InputText v-model="form.formSubmitLabel" placeholder="Send Details" fluid />
                        </div>
                        <div class="form-group flex-1">
                            <label>Webhook / Submit URL <small class="info-text">(Optional)</small></label>
                            <InputText v-model="form.formSubmitUrl" placeholder="https://api.yourdomain.com/webhook" fluid />
                        </div>
                    </div>

                    <div class="form-fields-builder mt-3">
                        <div class="builder-header">
                            <strong>Form Input Fields ({{ form.formFields.length }})</strong>
                            <Button label="Add Field" icon="pi pi-plus" size="small" severity="secondary" @click="addFormField" />
                        </div>

                        <div v-if="form.formFields.length === 0" class="builder-empty">
                            No inputs defined yet. Add at least one field to capture user details.
                        </div>

                        <div v-else class="builder-list">
                            <div v-for="(field, index) in form.formFields" :key="index" class="field-item-row">
                                <div class="field-item-col flex-1">
                                    <InputText v-model="field.label" placeholder="Label (e.g. Full Name)" size="small" fluid />
                                </div>
                                <div class="field-item-col flex-1">
                                    <InputText v-model="field.name" placeholder="Name Key (e.g. full_name)" size="small" fluid />
                                </div>
                                <div class="field-item-col flex-1">
                                    <Select v-model="field.type" :options="formFieldTypeOptions" optionLabel="label" optionValue="value" size="small" fluid />
                                </div>
                                <div class="field-item-col" style="min-width: 100px;">
                                    <div class="flex align-items-center gap-1">
                                        <Checkbox v-model="field.required" :binary="true" :inputId="'req-' + index" />
                                        <label :for="'req-' + index" class="text-xs">Required</label>
                                    </div>
                                </div>
                                <div class="field-item-col flex-2" v-if="field.type === 'select' || field.type === 'checkbox' || field.type === 'radio'">
                                    <InputText v-model="field.options" placeholder="Option A, Option B" size="small" fluid />
                                </div>
                                <div class="field-item-col">
                                    <Button icon="pi pi-trash" severity="danger" text rounded size="small" @click="removeFormField(index)" />
                                </div>
                            </div>
                        </div>
                    </div>
                </div>

                <div v-else-if="form.responseType === 'tool_call'" class="response-sub-panel">
                    <div class="form-group">
                        <label>Registered Tool Name</label>
                        <InputText v-model="form.toolName" placeholder="get_order_status" fluid />
                        <small class="info-text">Executes the registered tool directly on the user's browser client.</small>
                    </div>
                    <div class="form-group mt-2">
                        <label>Static Tool Arguments (JSON)</label>
                        <Textarea v-model="form.toolArguments" rows="4" placeholder='{ "productId": "premium_tier" }' fluid />
                    </div>
                </div>

                <hr class="form-divider" />

                <div v-if="form.type === 'intent'" class="form-group">
                    <label>Confidence Threshold: {{ form.confidenceThreshold }}%</label>
                    <Slider v-model="form.confidenceThreshold" :min="30" :max="100" :step="5" />
                    <small class="info-text">Lower = more sensitive (may cause false positives). Higher = more precise (may miss some matches).</small>
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
                <Button :label="editing ? 'Update Rule' : 'Create Rule'" @click="saveRule" :disabled="isFormSaveDisabled()" />
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
.type-intent { background: color-mix(in srgb, var(--p-amber-500) 20%, transparent); color: var(--p-amber-700); font-weight: 700; }
.type-command { background: color-mix(in srgb, var(--p-primary-500) 15%, transparent); color: var(--p-primary-700); border: 1px solid var(--p-primary-200); font-weight: 700; }
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
    font-size: 0.9rem;
    margin-bottom: 6px;
    color: var(--p-surface-800);
    display: flex;
    align-items: center;
    gap: 6px;
    flex-wrap: wrap;
}
.command-trigger-text {
    font-family: var(--font-mono, monospace);
    font-weight: 600;
    color: var(--p-primary-600);
    font-size: 0.95rem;
    background: var(--p-surface-50);
    padding: 2px 8px;
    border-radius: 6px;
    border: 1px solid var(--p-surface-200);
}
.cmd-trigger-char {
    color: var(--p-primary-500);
    font-weight: 800;
}
.cmd-desc-sub {
    font-family: var(--font-sans, system-ui, sans-serif);
    font-weight: 400;
    color: var(--p-surface-500);
    font-size: 0.85rem;
}
.rule-trigger code {
    background: var(--p-surface-100);
    padding: 1px 6px;
    border-radius: 4px;
    font-size: 0.8rem;
}
.intent-text {
    font-style: italic;
    color: var(--p-surface-600);
}
.rule-response-preview {
    font-size: 0.85rem;
    color: var(--p-surface-600);
    margin-top: 8px;
}
.response-badge-row {
    display: flex;
    align-items: center;
    gap: 8px;
}
.response-type-badge {
    font-size: 0.7rem;
    font-weight: 700;
    text-transform: uppercase;
    padding: 2px 6px;
    border-radius: 4px;
    flex-shrink: 0;
}
.res-text { background: var(--p-surface-100); color: var(--p-surface-700); }
.res-redirect { background: color-mix(in srgb, var(--p-cyan-500) 15%, transparent); color: var(--p-cyan-700); }
.res-card { background: color-mix(in srgb, var(--p-indigo-500) 15%, transparent); color: var(--p-indigo-700); }
.res-file { background: color-mix(in srgb, var(--p-teal-500) 15%, transparent); color: var(--p-teal-700); }
.res-form { background: color-mix(in srgb, var(--p-pink-500) 15%, transparent); color: var(--p-pink-700); }
.res-tool_call { background: color-mix(in srgb, var(--p-purple-500) 15%, transparent); color: var(--p-purple-700); }

.response-preview-text {
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    max-width: 500px;
}
.rule-actions {
    display: flex;
    gap: 4px;
    flex-shrink: 0;
}
.test-match-type {
    font-size: 0.75rem;
    padding: 1px 6px;
    border-radius: 4px;
    background: var(--p-surface-100);
    color: var(--p-surface-600);
}
.test-confidence {
    font-size: 0.75rem;
    font-weight: 600;
    color: var(--p-primary-600);
}

/* Custom Rule Fields */
.form-divider {
    border: 0;
    border-top: 1px solid var(--p-surface-200);
    margin: 20px 0;
}
.form-section-title {
    font-size: 1rem;
    font-weight: 600;
    color: var(--p-surface-800);
    margin: 0 0 12px 0;
}
.response-sub-panel {
    background: var(--p-surface-50);
    border-left: 3px solid var(--p-primary-500);
    padding: 16px;
    border-radius: 0 8px 8px 0;
    margin-top: 12px;
    box-shadow: inset 0 1px 2px rgba(0,0,0,0.02);
}
.command-settings-panel {
    background: color-mix(in srgb, var(--p-primary-50) 40%, transparent);
    border: 1px solid var(--p-primary-100);
    padding: 14px;
    border-radius: 8px;
    margin-top: 12px;
}
.mt-2 { margin-top: 8px; }
.mt-3 { margin-top: 16px; }
.font-italic { font-style: italic; }
.flex-2 { flex: 2; }
.flex-3 { flex: 3; }

/* Dynamic Fields Builder */
.form-fields-builder {
    border: 1px solid var(--p-surface-200);
    border-radius: 8px;
    overflow: hidden;
    background: var(--p-surface-0);
}
.builder-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    background: var(--p-surface-50);
    padding: 8px 12px;
    border-bottom: 1px solid var(--p-surface-200);
    font-size: 0.85rem;
}
.builder-empty {
    padding: 24px;
    text-align: center;
    color: var(--p-surface-400);
    font-size: 0.85rem;
}
.builder-list {
    display: flex;
    flex-direction: column;
    padding: 8px;
    gap: 8px;
}
.field-item-row {
    display: flex;
    align-items: center;
    gap: 8px;
    padding: 8px;
    background: var(--p-surface-50);
    border: 1px solid var(--p-surface-100);
    border-radius: 6px;
    transition: background 0.15s;
}
.field-item-row:hover {
    background: var(--p-surface-100);
}
.field-item-col {
    display: flex;
    align-items: center;
}
.text-xs { font-size: 0.75rem; }

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

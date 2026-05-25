import { ref, reactive, onMounted, computed } from 'vue';
import { useRoute } from 'vue-router';
import { useApi } from '../../composables/useApi';

export function useConfigDetail() {
    const route = useRoute();
    const { apiFetch, API_BASE } = useApi();

    const projectId = computed(() => route.params.projectId as string);
    const configId = computed(() => route.params.configId as string);

    // ─── Active Tab / Dialog State ───
    const activeTab = ref('general'); // 'general' | 'providers' | 'channels' | 'handoff' | 'appearance'
    const showAdminDialog = ref(false);
    const showTemplateVarsDialog = ref(false);
    const showHistoryDialog = ref(false);

    // ─── Collapsible section state ───
    const sectionsOpen = reactive<Record<string, boolean>>({
        coreProviders: true,
        extraProviders: false,
        customProvider: false,
    });

    function toggleSection(key: string) {
        sectionsOpen[key] = !sectionsOpen[key];
    }

    // ─── Types ───
    interface ModelEntry { model: string; provider: string; }
    interface RegistryProvider { id: string; name: string; defaultModel: string; isOpenAiCompatible: boolean; }
    interface HistoryEntry {
        id: string; systemPrompt: string; defaultModel: string;
        defaultProvider: string; changeNote: string | null; createdAt: string;
    }

    // ─── Config State ───
    const config = reactive({
        name: '',
        systemPrompt: '',
        defaultProvider: 'gemini',
        defaultModel: 'gemini-3.1-flash-lite-preview',
        hasGeminiKey: false,
        hasGroqKey: false,
        hasOpenAiKey: false,
        hasFirecrawlKey: false,
        hasAnthropicKey: false,
        configuredProviders: '' as string,
        liveVoiceEnabled: false,
        enabledModels: '',
        rateLimitRequests: 0,
        rateLimitWindowMinutes: 60,
        maxSpendLimit: 0,
        currentSpend: 0,
        logRetentionDays: 30,
        maxLogsPerSession: 500,
        maxSessionsPerProject: 50,
        suggestionsJson: '',
        suggestions: [] as string[],
        customProviderName: '',
        customProviderBaseUrl: '',
        hasCustomProviderKey: false,
        handoffEnabled: false,
        handoffTriggerKeywords: '',
        handoffEscalationCriteria: '',
        handoffConfidenceThreshold: 70,
        handoffQueueMessage: '',
        themeSettingsJson: '',
        channelSettingsJson: ''
    });

    const channels = reactive({
        whatsApp: { phoneNumberId: '', accessToken: '', verifyToken: '', appSecret: '' },
        slack: { botToken: '', signingSecret: '' },
        telegram: { botToken: '', secretToken: '' },
        teams: { appId: '', appPassword: '' },
        openWa: { instanceUrl: '', sessionName: '', apiKey: '' }
    });

    // ─── Theme Engine ───
    const defaultTheme = {
        primaryColor: '#39a7b9',
        bgColor: '#ffffff',
        fontFamily: 'Outfit',
        position: 'bottom-right',
        headerBgColor: '',
        headerTextColor: '#ffffff',
        userBubbleBgColor: '',
        userBubbleTextColor: '#ffffff',
        botBubbleBgColor: '#ffffff',
        botBubbleTextColor: '#1e293b',
        chatBgColor: '',
        launcherBgColor: '',
        launcherIconColor: '#ffffff',
        launcherBorderRadius: 16,
        chatBorderRadius: 10,
        bubbleBorderRadius: 20,
        title: '',
        subtitle: '',
        placeholder: ''
    };
    const theme = reactive({ ...defaultTheme });
    const fontOptions = [
        { label: 'Outfit (Default)', value: 'Outfit' },
        { label: 'Inter', value: 'Inter' },
        { label: 'Roboto', value: 'Roboto' },
        { label: 'System Default', value: 'system-ui' }
    ];
    const positionOptions = [
        { label: 'Bottom Right', value: 'bottom-right' },
        { label: 'Bottom Left', value: 'bottom-left' }
    ];

    // ─── Key Inputs (never pre-populated) ───
    const keyInputs = reactive({
        geminiApiKey: '', groqApiKey: '', openAiApiKey: '',
        firecrawlApiKey: '', anthropicApiKey: '', customProviderApiKey: ''
    });

    const providerKeyInputs = reactive<Record<string, string>>({});

    const configuredProvidersMap = computed(() => {
        if (!config.configuredProviders) return {} as Record<string, boolean>;
        try { return JSON.parse(config.configuredProviders) as Record<string, boolean>; }
        catch { return {} as Record<string, boolean>; }
    });

    const defaultModelOptions = computed(() =>
        enabledModels.value.map(e => ({ label: `${e.model} (${e.provider})`, value: e.model }))
    );

    const coreProviders = [
        { key: 'geminiApiKey', hasKey: 'hasGeminiKey', label: 'Google Gemini', id: 'gemini' },
        { key: 'groqApiKey', hasKey: 'hasGroqKey', label: 'Groq', id: 'groq' },
        { key: 'openAiApiKey', hasKey: 'hasOpenAiKey', label: 'OpenAI', id: 'openai' },
        { key: 'anthropicApiKey', hasKey: 'hasAnthropicKey', label: 'Anthropic Claude', id: 'anthropic' },
        { key: 'firecrawlApiKey', hasKey: 'hasFirecrawlKey', label: 'Firecrawl (Crawling)', id: 'firecrawl' }
    ];

    const registryProviders = ref<RegistryProvider[]>([]);
    const extraProviders = computed(() => {
        const coreIds = new Set(coreProviders.map(p => p.id));
        return registryProviders.value.filter(p => p.isOpenAiCompatible && !coreIds.has(p.id));
    });

    const fetchingModels = ref<string | null>(null);
    const providerModels = reactive<Record<string, any[]>>({});
    const enabledModels = ref<ModelEntry[]>([]);
    const saving = ref(false);
    const saved = ref(false);

    // ─── Prompt Versioning ───
    const historyEntries = ref<HistoryEntry[]>([]);
    const loadingHistory = ref(false);
    const restoringId = ref<string | null>(null);
    const changeNote = ref('');
    const promptDirty = ref(false);
    const originalPrompt = ref('');

    // ─── Template Variables ───
    const templateVars = ref<{ key: string; value: string }[]>([]);
    const builtInVars = ['date', 'time'];
    const suggestedVars = ['company', 'user_name', 'product', 'support_email', 'website'];

    // ─── Model Helpers ───
    function isModelEnabled(modelId: string, providerId: string): boolean {
        return enabledModels.value.some(e => e.model === modelId && e.provider === providerId);
    }

    function toggleModel(modelId: string, providerId: string) {
        const idx = enabledModels.value.findIndex(e => e.model === modelId && e.provider === providerId);
        if (idx >= 0) {
            enabledModels.value.splice(idx, 1);
            if (config.defaultModel === modelId) {
                config.defaultModel = enabledModels.value.length > 0 ? enabledModels.value[0].model : '';
                config.defaultProvider = enabledModels.value.length > 0 ? enabledModels.value[0].provider : 'gemini';
            }
        } else {
            enabledModels.value.push({ model: modelId, provider: providerId });
            if (!config.defaultModel) {
                config.defaultModel = modelId;
                config.defaultProvider = providerId;
            }
        }
    }

    function onDefaultModelChange(modelId: string) {
        config.defaultModel = modelId;
        const entry = enabledModels.value.find(e => e.model === modelId);
        if (entry) config.defaultProvider = entry.provider;
    }

    function isExtraProviderConfigured(pid: string): boolean {
        return !!configuredProvidersMap.value[pid];
    }

    // ─── API Calls ───
    async function loadProviders() {
        try {
            const res = await apiFetch('/api/providers');
            if (res.ok) registryProviders.value = await res.json();
        } catch (e) { console.error(e); }
    }

    async function load() {
        const res = await apiFetch(`/api/configuration/${configId.value}`);
        if (res.ok) {
            const data = await res.json();
            Object.assign(config, data);
            if (data.handoffConfidenceThreshold != null)
                config.handoffConfidenceThreshold = Math.round(data.handoffConfidenceThreshold * 100);
            originalPrompt.value = data.systemPrompt || '';
            promptDirty.value = false;

            parseTemplateVars(data.promptTemplateVariablesJson);

            if (data.themeSettingsJson) {
                try {
                    const parsed = JSON.parse(data.themeSettingsJson);
                    Object.assign(theme, { ...defaultTheme, ...parsed });
                }
                catch { Object.assign(theme, defaultTheme); }
            } else {
                Object.assign(theme, defaultTheme);
            }

            if (data.channelSettingsJson) {
                try {
                    const pc = JSON.parse(data.channelSettingsJson);
                    if (pc.whatsApp) Object.assign(channels.whatsApp, pc.whatsApp);
                    if (pc.slack) Object.assign(channels.slack, pc.slack);
                    if (pc.telegram) Object.assign(channels.telegram, pc.telegram);
                    if (pc.teams) Object.assign(channels.teams, pc.teams);
                    if (pc.openWa) Object.assign(channels.openWa, pc.openWa);
                } catch (e) { console.error('Failed to parse channel settings JSON', e); }
            }

            if (data.enabledModels) {
                try {
                    const parsed = JSON.parse(data.enabledModels);
                    if (Array.isArray(parsed) && parsed.length > 0) {
                        enabledModels.value = typeof parsed[0] === 'object' && parsed[0].model
                            ? parsed
                            : parsed.map((m: string) => ({ model: m, provider: config.defaultProvider || 'gemini' }));
                    }
                } catch { /* ignore */ }
            }

            if (data.suggestionsJson) {
                try {
                    config.suggestions = JSON.parse(data.suggestionsJson);
                    if (!Array.isArray(config.suggestions)) config.suggestions = [];
                } catch { config.suggestions = []; }
            } else {
                config.suggestions = [];
            }
        }
    }

    async function fetchModels(providerId: string) {
        fetchingModels.value = providerId;
        try {
            const res = await apiFetch(`/api/configuration/${configId.value}/models/${providerId}`);
            if (res.ok) providerModels[providerId] = await res.json();
        } catch (e) { console.error(e); }
        fetchingModels.value = null;
    }

    async function save() {
        saving.value = true;
        saved.value = false;

        const body: Record<string, any> = {
            name: config.name,
            systemPrompt: config.systemPrompt,
            defaultProvider: config.defaultProvider,
            defaultModel: config.defaultModel,
            liveVoiceEnabled: config.liveVoiceEnabled,
            enabledModels: JSON.stringify(enabledModels.value),
            rateLimitRequests: config.rateLimitRequests,
            rateLimitWindowMinutes: config.rateLimitWindowMinutes,
            maxSpendLimit: config.maxSpendLimit,
            logRetentionDays: config.logRetentionDays,
            maxLogsPerSession: config.maxLogsPerSession,
            maxSessionsPerProject: config.maxSessionsPerProject,
            suggestionsJson: JSON.stringify(config.suggestions.filter(s => s.trim() !== '')),
            promptTemplateVariablesJson: serializeTemplateVars(),
            changeNote: changeNote.value || null,
            handoffEnabled: config.handoffEnabled,
            handoffTriggerKeywords: config.handoffTriggerKeywords,
            handoffEscalationCriteria: config.handoffEscalationCriteria,
            handoffConfidenceThreshold: config.handoffConfidenceThreshold / 100,
            handoffQueueMessage: config.handoffQueueMessage,
            themeSettingsJson: JSON.stringify(theme),
            channelSettingsJson: JSON.stringify(channels)
        };

        if (keyInputs.geminiApiKey) body.geminiApiKey = keyInputs.geminiApiKey;
        if (keyInputs.groqApiKey) body.groqApiKey = keyInputs.groqApiKey;
        if (keyInputs.openAiApiKey) body.openAiApiKey = keyInputs.openAiApiKey;
        if (keyInputs.firecrawlApiKey) body.firecrawlApiKey = keyInputs.firecrawlApiKey;
        if (keyInputs.anthropicApiKey) body.anthropicApiKey = keyInputs.anthropicApiKey;

        const providerKeys: Record<string, string> = {};
        let hasProviderKeys = false;
        for (const [pid, val] of Object.entries(providerKeyInputs)) {
            if (val) { providerKeys[pid] = val; hasProviderKeys = true; }
        }
        if (hasProviderKeys) body.providerKeys = JSON.stringify(providerKeys);

        if (config.customProviderName) body.customProviderName = config.customProviderName;
        if (config.customProviderBaseUrl) body.customProviderBaseUrl = config.customProviderBaseUrl;
        if (keyInputs.customProviderApiKey) body.customProviderApiKey = keyInputs.customProviderApiKey;

        await apiFetch(`/api/configuration/${configId.value}`, {
            method: 'PUT',
            body: JSON.stringify(body)
        });

        saving.value = false;
        saved.value = true;
        await load();

        keyInputs.geminiApiKey = '';
        keyInputs.groqApiKey = '';
        keyInputs.openAiApiKey = '';
        keyInputs.firecrawlApiKey = '';
        keyInputs.anthropicApiKey = '';
        keyInputs.customProviderApiKey = '';
        for (const k of Object.keys(providerKeyInputs)) providerKeyInputs[k] = '';
        changeNote.value = '';
        promptDirty.value = false;

        if (showHistoryDialog.value) await loadHistory();
        setTimeout(() => saved.value = false, 3000);
    }

    async function clearKey(providerId: string) {
        const coreMap: Record<string, string> = {
            gemini: 'geminiApiKey', groq: 'groqApiKey', openai: 'openAiApiKey',
            firecrawl: 'firecrawlApiKey', anthropic: 'anthropicApiKey'
        };
        const keyField = coreMap[providerId];
        if (keyField) {
            await apiFetch(`/api/configuration/${configId.value}`, {
                method: 'PUT', body: JSON.stringify({ [keyField]: '' })
            });
        } else {
            await apiFetch(`/api/configuration/${configId.value}`, {
                method: 'PUT', body: JSON.stringify({ providerKeys: JSON.stringify({ [providerId]: '' }) })
            });
        }
        await load();
    }

    // ─── Suggestions ───
    function addSuggestion() { if (config.suggestions.length < 4) config.suggestions.push(''); }
    function removeSuggestion(index: number) { config.suggestions.splice(index, 1); }

    // ─── Template Variables ───
    function insertVariable(varName: string) {
        config.systemPrompt += `{{${varName}}}`;
        promptDirty.value = true;
    }
    function addTemplateVar() { templateVars.value.push({ key: '', value: '' }); }
    function removeTemplateVar(index: number) { templateVars.value.splice(index, 1); }

    function parseTemplateVars(json: string | null) {
        if (!json) { templateVars.value = []; return; }
        try {
            const obj = JSON.parse(json) as Record<string, string>;
            templateVars.value = Object.entries(obj).map(([key, value]) => ({ key, value }));
        } catch { templateVars.value = []; }
    }

    function serializeTemplateVars(): string {
        const obj: Record<string, string> = {};
        for (const v of templateVars.value) {
            if (v.key.trim()) obj[v.key.trim()] = v.value;
        }
        return JSON.stringify(obj);
    }

    // ─── Prompt History ───
    async function loadHistory() {
        loadingHistory.value = true;
        try {
            const res = await apiFetch(`/api/configuration/${configId.value}/history`);
            if (res.ok) historyEntries.value = await res.json();
        } catch (e) { console.error(e); }
        loadingHistory.value = false;
    }

    async function restoreVersion(historyId: string) {
        restoringId.value = historyId;
        try {
            const res = await apiFetch(`/api/configuration/${configId.value}/history/${historyId}/restore`, { method: 'POST' });
            if (res.ok) { await load(); await loadHistory(); }
        } catch (e) { console.error(e); }
        restoringId.value = null;
    }

    function onPromptInput() { promptDirty.value = config.systemPrompt !== originalPrompt.value; }
    function truncate(text: string, len: number): string { return text.length > len ? text.substring(0, len) + '...' : text; }
    function formatDate(iso: string): string { return new Date(iso).toLocaleString(); }

    function openHistoryDialog() {
        showHistoryDialog.value = true;
        if (historyEntries.value.length === 0) loadHistory();
    }

    onMounted(() => { loadProviders(); load(); });

    return {
        // API URL base
        API_BASE,
        // route
        projectId, configId,
        // ui state
        activeTab, showAdminDialog, showTemplateVarsDialog, showHistoryDialog,
        sectionsOpen, toggleSection,
        // data
        config, channels, theme, keyInputs, providerKeyInputs,
        enabledModels, providerModels, fetchingModels,
        coreProviders, extraProviders, registryProviders,
        configuredProvidersMap, defaultModelOptions,
        fontOptions, positionOptions,
        saving, saved,
        // prompt / history
        historyEntries, loadingHistory, restoringId, changeNote, promptDirty,
        templateVars, builtInVars, suggestedVars,
        // methods
        isModelEnabled, toggleModel, onDefaultModelChange,
        isExtraProviderConfigured, fetchModels,
        save, clearKey,
        addSuggestion, removeSuggestion,
        insertVariable, addTemplateVar, removeTemplateVar,
        loadHistory, restoreVersion, openHistoryDialog,
        onPromptInput, truncate, formatDate
    };
}

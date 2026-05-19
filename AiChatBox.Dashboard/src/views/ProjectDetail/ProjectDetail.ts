import { ref, reactive, onMounted, computed } from 'vue';
import { useRoute } from 'vue-router';
import { useApi } from '../../composables/useApi';
import { useToast } from 'primevue/usetoast';
import { useConfirm } from 'primevue/useconfirm';

export function useProjectDetail() {
    const route = useRoute();
    const { apiFetch } = useApi();
    const toast = useToast();
    const confirm = useConfirm();

    const projectId = computed(() => route.params.id as string);

    // ─── Data ───────────────────────────────────────────────
    const project = ref<any>({});
    const configs = ref<any[]>([]);
    const keys = ref<any[]>([]);
    const tools = ref<any[]>([]);

    // ─── Active Tab ──────────────────────────────────────────
    const activeTab = ref('overview'); // overview | configs | tools | keys | database

    // ─── Dialog Visibility ───────────────────────────────────
    const showNewConfig = ref(false);
    const showNewKey = ref(false);
    const showNewTool = ref(false);
    const showSettingsDialog = ref(false);

    // ─── Tool Edit State ─────────────────────────────────────
    const isEditingTool = ref(false);
    const editingToolId = ref<string | null>(null);

    // ─── Generated Key ───────────────────────────────────────
    const generatedKey = ref('');

    // ─── Form Models ─────────────────────────────────────────
    const newConfig = reactive({ name: '', systemPrompt: '' });
    const newKey = reactive({ label: '', configId: null as string | null });
    const newTool = reactive({
        name: '',
        description: '',
        parametersJsonSchema: '{\n  "type": "object",\n  "properties": {}\n}'
    });

    // ─── Database Config ─────────────────────────────────────
    const dbConfig = reactive({
        type: 0,
        connectionString: '',
        schemaDefinition: '',
        hasConnectionString: false
    });

    const dbTypes = [
        { label: 'PostgreSQL', value: 0 },
        { label: 'MySQL', value: 1 },
        { label: 'SQL Server', value: 2 },
        { label: 'SQLite', value: 3 }
    ];

    const detectingSchema = ref(false);
    const savingProject = ref(false);
    const savedProject = ref(false);

    // ─── Config options for key select ───────────────────────
    const configOptions = computed(() =>
        configs.value.map(c => ({ label: c.name, value: c.id }))
    );

    // ─── Load Functions ──────────────────────────────────────
    async function loadProject() {
        try {
            const res = await apiFetch(`/api/project/${projectId.value}`);
            if (res.ok) project.value = await res.json();
        } catch (e) { console.error(e); }
    }

    async function loadConfigs() {
        try {
            const res = await apiFetch(`/api/project/${projectId.value}/configurations`);
            if (res.ok) configs.value = await res.json();
        } catch (e) { console.error(e); }
    }

    async function loadKeys() {
        try {
            const res = await apiFetch(`/api/project/${projectId.value}/keys`);
            if (res.ok) keys.value = await res.json();
        } catch (e) { console.error(e); }
    }

    async function loadTools() {
        try {
            const res = await apiFetch(`/api/tool/project/${projectId.value}`);
            if (res.ok) tools.value = await res.json();
        } catch (e) { console.error(e); }
    }

    async function loadDbConfig() {
        try {
            const res = await apiFetch(`/api/database/${projectId.value}`);
            if (res.ok) {
                const data = await res.json();
                if (data) {
                    dbConfig.type = data.type;
                    dbConfig.schemaDefinition = data.schemaDefinition;
                    dbConfig.hasConnectionString = data.hasConnectionString;
                    dbConfig.connectionString = '';
                }
            }
        } catch (e) { console.error(e); }
    }

    // ─── Save / Action Functions ─────────────────────────────
    async function saveProjectSettings() {
        savingProject.value = true;
        savedProject.value = false;
        await apiFetch(`/api/project/${projectId.value}`, {
            method: 'PUT',
            body: JSON.stringify({
                name: project.value.name,
                systemPrompt: project.value.systemPrompt,
                provider: project.value.provider,
                modelName: project.value.modelName,
                webhookUrl: project.value.webhookUrl,
                webhookSecret: project.value.webhookSecret || null,
                allowedDomains: project.value.allowedDomains
            })
        });
        if (project.value.webhookSecret) project.value.webhookSecret = '';
        savingProject.value = false;
        savedProject.value = true;
        toast.add({ severity: 'success', summary: 'Saved', detail: 'Project settings updated.', life: 3000 });
        setTimeout(() => savedProject.value = false, 3000);
    }

    async function detectSchema() {
        if (!dbConfig.connectionString && !dbConfig.hasConnectionString) {
            toast.add({ severity: 'warn', summary: 'Missing Connection', detail: 'Please provide a connection string first.', life: 3000 });
            return;
        }
        detectingSchema.value = true;
        try {
            const res = await apiFetch(`/api/database/${projectId.value}/detect-schema`, { method: 'POST' });
            if (res.ok) {
                const data = await res.json();
                dbConfig.schemaDefinition = data.schema;
                toast.add({ severity: 'success', summary: 'Schema Detected', detail: 'Database schema successfully updated.', life: 3000 });
            } else {
                throw new Error('Could not connect to database.');
            }
        } catch (error: any) {
            toast.add({ severity: 'error', summary: 'Detection Failed', detail: error.message || 'Could not connect to database.', life: 5000 });
        } finally {
            detectingSchema.value = false;
        }
    }

    async function saveDbConfig() {
        try {
            const res = await apiFetch(`/api/database/${projectId.value}`, {
                method: 'POST',
                body: JSON.stringify({
                    type: dbConfig.type,
                    connectionString: dbConfig.connectionString || null,
                    schemaDefinition: dbConfig.schemaDefinition
                })
            });
            if (res.ok) {
                toast.add({ severity: 'success', summary: 'Saved', detail: 'Database configuration updated.', life: 3000 });
                loadDbConfig();
            }
        } catch (e) { console.error(e); }
    }

    async function createConfig() {
        await apiFetch(`/api/project/${projectId.value}/configurations`, {
            method: 'POST',
            body: JSON.stringify({ name: newConfig.name, systemPrompt: newConfig.systemPrompt })
        });
        showNewConfig.value = false;
        newConfig.name = '';
        newConfig.systemPrompt = '';
        loadConfigs();
    }

    async function deleteConfig(id: string) {
        confirm.require({
            message: 'Delete this configuration?',
            header: 'Confirm Deletion',
            icon: 'pi pi-exclamation-triangle',
            rejectProps: { label: 'Cancel', severity: 'secondary', outlined: true },
            acceptProps: { label: 'Delete', severity: 'danger' },
            accept: async () => {
                await apiFetch(`/api/configuration/${id}`, { method: 'DELETE' });
                toast.add({ severity: 'success', summary: 'Deleted', detail: 'Configuration removed.', life: 3000 });
                loadConfigs();
            }
        });
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
        confirm.require({
            message: 'Revoke this key?',
            header: 'Confirm Revoke',
            icon: 'pi pi-exclamation-triangle',
            rejectProps: { label: 'Cancel', severity: 'secondary', outlined: true },
            acceptProps: { label: 'Revoke', severity: 'danger' },
            accept: async () => {
                await apiFetch(`/api/project/keys/${id}`, { method: 'DELETE' });
                toast.add({ severity: 'success', summary: 'Revoked', detail: 'API key revoked.', life: 3000 });
                loadKeys();
            }
        });
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
            await apiFetch(`/api/tool/${editingToolId.value}`, { method: 'PUT', body: JSON.stringify(payload) });
        } else {
            await apiFetch(`/api/tool/project/${projectId.value}`, { method: 'POST', body: JSON.stringify(payload) });
        }
        showNewTool.value = false;
        loadTools();
    }

    async function deleteTool(id: string) {
        confirm.require({
            message: 'Delete this tool?',
            header: 'Confirm Deletion',
            icon: 'pi pi-exclamation-triangle',
            rejectProps: { label: 'Cancel', severity: 'secondary', outlined: true },
            acceptProps: { label: 'Delete', severity: 'danger' },
            accept: async () => {
                await apiFetch(`/api/tool/${id}`, { method: 'DELETE' });
                toast.add({ severity: 'success', summary: 'Deleted', detail: 'Tool removed.', life: 3000 });
                loadTools();
            }
        });
    }

    // ─── Webhook Test State ──────────────────────────────────
    const testingWebhook = ref(false);
    const webhookTestResult = ref<any>(null);

    // ─── Tool Test State ─────────────────────────────────────
    const showTestTool = ref(false);
    const testingTool = ref(false);
    const activeTestTool = ref<any>(null);
    const toolTestResult = ref<any>(null);
    const testToolArguments = ref('');

    async function testWebhookConnection() {
        testingWebhook.value = true;
        webhookTestResult.value = null;
        try {
            const res = await apiFetch(`/api/tool/project/${projectId.value}/test-webhook-connection`, {
                method: 'POST',
                body: JSON.stringify({
                    webhookUrl: project.value.webhookUrl || '',
                    webhookSecret: project.value.webhookSecret || null
                })
            });
            if (res.ok) {
                webhookTestResult.value = await res.json();
                if (webhookTestResult.value.success) {
                    toast.add({ severity: 'success', summary: 'Success', detail: 'Webhook connection test succeeded.', life: 3000 });
                } else {
                    toast.add({ severity: 'error', summary: 'Failed', detail: 'Webhook returned an error status or exception.', life: 5000 });
                }
            } else {
                const text = await res.text();
                toast.add({ severity: 'error', summary: 'Error', detail: text || 'Failed to trigger connection test.', life: 5000 });
            }
        } catch (e: any) {
            console.error(e);
            toast.add({ severity: 'error', summary: 'Error', detail: e.message || 'An error occurred during testing.', life: 5000 });
        } finally {
            testingWebhook.value = false;
        }
    }

    function openTestTool(tool: any) {
        activeTestTool.value = tool;
        toolTestResult.value = null;
        
        let defaultArgs: any = {};
        try {
            if (tool.parametersJsonSchema) {
                const schema = JSON.parse(tool.parametersJsonSchema);
                if (schema && schema.properties) {
                    for (const key of Object.keys(schema.properties)) {
                        const prop = schema.properties[key];
                        if (prop.type === 'string') {
                            defaultArgs[key] = `test_${key}`;
                        } else if (prop.type === 'number' || prop.type === 'integer') {
                            defaultArgs[key] = 123;
                        } else if (prop.type === 'boolean') {
                            defaultArgs[key] = true;
                        } else if (prop.type === 'array') {
                            defaultArgs[key] = [];
                        } else if (prop.type === 'object') {
                            defaultArgs[key] = {};
                        } else {
                            defaultArgs[key] = '';
                        }
                    }
                }
            }
        } catch (e) {
            console.error('Error parsing schema for defaults', e);
        }
        
        testToolArguments.value = JSON.stringify(defaultArgs, null, 2);
        showTestTool.value = true;
    }

    async function executeToolTest() {
        if (!activeTestTool.value) return;
        
        try {
            JSON.parse(testToolArguments.value);
        } catch (e) {
            toast.add({ severity: 'error', summary: 'Invalid JSON', detail: 'Please enter valid JSON arguments.', life: 3000 });
            return;
        }

        testingTool.value = true;
        toolTestResult.value = null;
        try {
            const res = await apiFetch(`/api/tool/${activeTestTool.value.id}/execute`, {
                method: 'POST',
                body: JSON.stringify({
                    argumentsJson: testToolArguments.value
                })
            });
            if (res.ok) {
                toolTestResult.value = await res.json();
                if (toolTestResult.value.success) {
                    toast.add({ severity: 'success', summary: 'Test Complete', detail: 'Tool execution test succeeded.', life: 3000 });
                } else {
                    toast.add({ severity: 'error', summary: 'Execution Failed', detail: toolTestResult.value.error || 'Tool execution returned success=false.', life: 5000 });
                }
            } else {
                const text = await res.text();
                toast.add({ severity: 'error', summary: 'Error', detail: text || 'Failed to trigger tool execution.', life: 5000 });
            }
        } catch (e: any) {
            console.error(e);
            toast.add({ severity: 'error', summary: 'Error', detail: e.message || 'An error occurred during tool testing.', life: 5000 });
        } finally {
            testingTool.value = false;
        }
    }

    onMounted(() => {
        loadProject();
        loadConfigs();
        loadKeys();
        loadTools();
        loadDbConfig();
    });

    return {
        projectId, project,
        configs, keys, tools, configOptions,
        activeTab,
        showNewConfig, showNewKey, showNewTool, showSettingsDialog,
        isEditingTool, editingToolId, generatedKey,
        newConfig, newKey, newTool,
        dbConfig, dbTypes, detectingSchema,
        savingProject, savedProject,
        saveProjectSettings, detectSchema, saveDbConfig,
        createConfig, deleteConfig,
        generateKey, revokeKey,
        openNewTool, openEditTool, saveTool, deleteTool,
        testingWebhook, webhookTestResult, testWebhookConnection,
        showTestTool, testingTool, activeTestTool, toolTestResult, testToolArguments, openTestTool, executeToolTest
    };
}

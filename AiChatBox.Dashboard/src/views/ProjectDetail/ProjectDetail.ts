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
        hasConnectionString: false,
        allowedTables: '',
        maxQueryTimeoutSeconds: 5,
        maxRecordsPerQuery: 100,
        sessionContextFilterJson: ''
    });

    const dbTypes = [
        { label: 'PostgreSQL', value: 0 },
        { label: 'MySQL', value: 1 },
        { label: 'SQLite', value: 2 },
        { label: 'SQL Server', value: 3 }
    ];

    const detectingSchema = ref(false);
    const savingProject = ref(false);
    const savedProject = ref(false);

    // ─── Schema Panel Visibility ─────────────────────────────
    // Schema DDL is hidden by default; user can toggle it open
    const showSchemaEditor = ref(false);

    // ─── Column-level selection state ────────────────────────
    // Maps table name → Set of allowed column names (null means "all columns allowed")
    const selectedColumnsPerTable = reactive<Record<string, Set<string>>>({});

    // ─── Parse tables + columns from DDL ─────────────────────
    const detectedTables = computed(() => {
        if (!dbConfig.schemaDefinition) return [];
        const matches = [...dbConfig.schemaDefinition.matchAll(/CREATE\s+TABLE\s+(?:[a-zA-Z0-9_""`\[\]]+\.)?["`\[]?([a-zA-Z0-9_]+)["`\]]?\s*\(/gi)];
        return Array.from(new Set(matches.map(m => m[1])));
    });

    /**
     * For each detected table, parse its column names from the DDL block.
     * Returns a map of  tableName → string[]
     */
    const detectedColumns = computed<Record<string, string[]>>(() => {
        const result: Record<string, string[]> = {};
        if (!dbConfig.schemaDefinition) return result;

        // Match each CREATE TABLE block
        const tableBlockRegex = /CREATE\s+TABLE\s+(?:[a-zA-Z0-9_""`\[\]]+\.)?["`\[]?([a-zA-Z0-9_]+)["`\]]?\s*\(([\s\S]*?)(?=\)\s*;|\)\s*CREATE|\)\s*$)/gi;
        let tableMatch: RegExpExecArray | null;

        while ((tableMatch = tableBlockRegex.exec(dbConfig.schemaDefinition)) !== null) {
            const tableName = tableMatch[1];
            const body = tableMatch[2];

            const columns: string[] = [];
            // Each line in the table body that starts with a column name (not a constraint keyword)
            const constraintKeywords = /^\s*(PRIMARY|UNIQUE|CHECK|FOREIGN|CONSTRAINT|INDEX|KEY)\b/i;
            const lines = body.split('\n');
            for (const line of lines) {
                const trimmed = line.trim();
                if (!trimmed || constraintKeywords.test(trimmed)) continue;

                // Column name is the first token; strip quoting
                const colMatch = trimmed.match(/^["`\[]?([a-zA-Z_][a-zA-Z0-9_]*)["`\]]?\s+\S/);
                if (colMatch) {
                    columns.push(colMatch[1]);
                }
            }

            if (columns.length > 0) result[tableName] = columns;
        }
        return result;
    });

    // ─── Selected tables (derived from allowedTables string) ─
    const selectedTablesArray = computed({
        get() {
            return dbConfig.allowedTables
                ? dbConfig.allowedTables.split(',').map(t => t.trim()).filter(Boolean)
                : [];
        },
        set(val: string[]) {
            dbConfig.allowedTables = val.join(', ');
        }
    });

    /**
     * Toggle a table's inclusion in the whitelist.
     * When a table is added, initialise its column selection to "all".
     * When removed, clear its column selection.
     */
    function toggleTable(tableName: string) {
        const current = new Set(selectedTablesArray.value);
        if (current.has(tableName)) {
            current.delete(tableName);
            delete selectedColumnsPerTable[tableName];
            delete sessionContextMap[tableName];
        } else {
            current.add(tableName);
            // Default: all columns allowed (represented as a full set)
            const cols = detectedColumns.value[tableName] ?? [];
            selectedColumnsPerTable[tableName] = new Set(cols);
        }
        selectedTablesArray.value = Array.from(current);
    }

    /** Toggle a single column for a given table */
    function toggleColumn(tableName: string, columnName: string) {
        if (!selectedColumnsPerTable[tableName]) {
            selectedColumnsPerTable[tableName] = new Set();
        }
        const set = selectedColumnsPerTable[tableName];
        if (set.has(columnName)) {
            set.delete(columnName);
            if (sessionContextMap[tableName] === columnName) {
                delete sessionContextMap[tableName];
            }
        } else {
            set.add(columnName);
        }
    }

    /** Select / deselect all columns for a table */
    function toggleAllColumns(tableName: string, selectAll: boolean) {
        const cols = detectedColumns.value[tableName] ?? [];
        if (selectAll) {
            selectedColumnsPerTable[tableName] = new Set(cols);
        } else {
            selectedColumnsPerTable[tableName] = new Set();
            delete sessionContextMap[tableName];
        }
    }

    /** Toggle designates/un-designates a column as the isolation column for a table */
    function toggleColumnIsolation(tableName: string, columnName: string) {
        if (sessionContextMap[tableName] === columnName) {
            delete sessionContextMap[tableName];
        } else {
            sessionContextMap[tableName] = columnName;
        }
    }

    /**
     * Build the allowedColumns JSON payload for the save call.
     * Shape: { "TableName": ["col1", "col2"] }
     * Tables with all columns selected omit the column list (null = unrestricted).
     */
    const allowedColumnsPayload = computed<Record<string, string[] | null>>(() => {
        const payload: Record<string, string[] | null> = {};
        for (const table of selectedTablesArray.value) {
            const allCols = detectedColumns.value[table] ?? [];
            const selectedCols = selectedColumnsPerTable[table];
            if (!selectedCols || selectedCols.size === allCols.length) {
                payload[table] = null; // all columns — no restriction needed
            } else {
                payload[table] = Array.from(selectedCols);
            }
        }
        return payload;
    });

    // ─── Session Context (Row-Level Isolation) ────────────────
    /**
     * Visual map: table → isolation column name.
     * Only tables with a non-empty column value are included in the saved JSON.
     * `undefined` key means the table has NO isolation configured.
     */
    const sessionContextMap = reactive<Record<string, string>>({});

    /** Derived JSON string sent to / received from the backend. */
    const sessionContextFilterJsonComputed = computed<string | null>(() => {
        const result: Record<string, string> = {};
        for (const [table, col] of Object.entries(sessionContextMap)) {
            if (col && col.trim()) result[table] = col.trim();
        }
        return Object.keys(result).length ? JSON.stringify(result) : null;
    });

    /** Toggle isolation on/off for a table. Defaults to the first detected column. */
    function toggleIsolation(table: string) {
        if (sessionContextMap[table] !== undefined) {
            delete sessionContextMap[table];
        } else {
            const firstCol = detectedColumns.value[table]?.[0] ?? '';
            sessionContextMap[table] = firstCol;
        }
    }

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
                    dbConfig.schemaDefinition = data.schemaDefinition || '';
                    dbConfig.hasConnectionString = data.hasConnectionString;
                    dbConfig.allowedTables = data.allowedTables || '';
                    dbConfig.maxQueryTimeoutSeconds = data.maxQueryTimeoutSeconds || 5;
                    dbConfig.maxRecordsPerQuery = data.maxRecordsPerQuery || 100;
                    dbConfig.sessionContextFilterJson = data.sessionContextFilterJson || '';
                    dbConfig.connectionString = '';

                    // Rehydrate column selections from saved payload if present
                    if (data.allowedColumnsJson) {
                        try {
                            const saved: Record<string, string[] | null> = JSON.parse(data.allowedColumnsJson);
                            for (const [table, cols] of Object.entries(saved)) {
                                if (cols === null) {
                                    const allCols = detectedColumns.value[table] ?? [];
                                    selectedColumnsPerTable[table] = new Set<string>(allCols);
                                } else {
                                    selectedColumnsPerTable[table] = new Set(cols);
                                }
                            }
                        } catch { /* ignore parse errors */ }
                    }

                    // Rehydrate session context map for the visual isolation picker
                    Object.keys(sessionContextMap).forEach(k => delete sessionContextMap[k]);
                    if (data.sessionContextFilterJson) {
                        try {
                            const saved: Record<string, string> = JSON.parse(data.sessionContextFilterJson);
                            for (const [table, col] of Object.entries(saved)) {
                                if (typeof col === 'string') sessionContextMap[table] = col;
                            }
                        } catch { /* ignore */ }
                    }
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
                // Auto-reveal the schema editor after detection so the user can inspect it
                showSchemaEditor.value = true;
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
                    schemaDefinition: dbConfig.schemaDefinition,
                    allowedTables: dbConfig.allowedTables || null,
                    allowedColumnsJson: JSON.stringify(allowedColumnsPayload.value),
                    maxQueryTimeoutSeconds: dbConfig.maxQueryTimeoutSeconds,
                    maxRecordsPerQuery: dbConfig.maxRecordsPerQuery,
                    sessionContextFilterJson: sessionContextFilterJsonComputed.value
                })
            });
            if (res.ok) {
                dbConfig.hasConnectionString = !!dbConfig.connectionString || dbConfig.hasConnectionString;
                dbConfig.connectionString = '';
                toast.add({ severity: 'success', summary: 'Saved', detail: 'Database configuration saved.', life: 3000 });
            } else {
                const errorText = await res.text().catch(() => '');
                toast.add({
                    severity: 'error',
                    summary: 'Save Failed',
                    detail: errorText || `Server returned ${res.status}. Check the API logs.`,
                    life: 6000
                });
            }
        } catch (e: any) {
            toast.add({ severity: 'error', summary: 'Error', detail: e.message || 'Failed to save database config.', life: 5000 });
        }
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
            message: 'Delete this configuration? This will also remove all associated API keys.',
            header: 'Confirm Deletion',
            icon: 'pi pi-exclamation-triangle',
            rejectProps: { label: 'Cancel', severity: 'secondary', outlined: true },
            acceptProps: { label: 'Delete', severity: 'danger' },
            accept: async () => {
                await apiFetch(`/api/project/${projectId.value}/configurations/${id}`, { method: 'DELETE' });
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
            message: 'Revoke this API key? This cannot be undone.',
            header: 'Revoke Key',
            icon: 'pi pi-exclamation-triangle',
            rejectProps: { label: 'Cancel', severity: 'secondary', outlined: true },
            acceptProps: { label: 'Revoke', severity: 'danger' },
            accept: async () => {
                await apiFetch(`/api/project/${projectId.value}/keys/${id}`, { method: 'DELETE' });
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
                        if (prop.type === 'string') defaultArgs[key] = `test_${key}`;
                        else if (prop.type === 'number' || prop.type === 'integer') defaultArgs[key] = 123;
                        else if (prop.type === 'boolean') defaultArgs[key] = true;
                        else if (prop.type === 'array') defaultArgs[key] = [];
                        else if (prop.type === 'object') defaultArgs[key] = {};
                        else defaultArgs[key] = '';
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
                body: JSON.stringify({ argumentsJson: testToolArguments.value })
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
        detectedTables, detectedColumns,
        selectedTablesArray, selectedColumnsPerTable, allowedColumnsPayload,
        toggleTable, toggleColumn, toggleAllColumns, toggleColumnIsolation,
        sessionContextMap, sessionContextFilterJsonComputed, toggleIsolation,
        showSchemaEditor,
        savingProject, savedProject,
        saveProjectSettings, detectSchema, saveDbConfig,
        createConfig, deleteConfig,
        generateKey, revokeKey,
        openNewTool, openEditTool, saveTool, deleteTool,
        testingWebhook, webhookTestResult, testWebhookConnection,
        showTestTool, testingTool, activeTestTool, toolTestResult, testToolArguments, openTestTool, executeToolTest
    };
}
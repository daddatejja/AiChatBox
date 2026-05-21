<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
import { useApi } from '../../composables/useApi';
import Card from 'primevue/card';
import Button from 'primevue/button';
import Message from 'primevue/message';

const { apiFetch } = useApi();

const account = ref<any>(null);
const tenants = ref<any[]>([]);
const loading = ref(false);
const error = ref('');

const loadData = async () => {
    loading.value = true;
    error.value = '';
    try {
        const [accRes, tenRes] = await Promise.all([
            apiFetch('/api/partner/account'),
            apiFetch('/api/partner/tenants')
        ]);
        if (accRes.ok) account.value = await accRes.json();
        if (tenRes.ok) tenants.value = await tenRes.json();
    } catch (e) {
        error.value = 'Failed to load developer account information.';
        console.error(e);
    } finally {
        loading.value = false;
    }
};

onMounted(() => {
    loadData();
});

const totalSessions = computed(() => {
    return tenants.value.reduce((acc, t) => acc + (t.sessionCount || 0), 0);
});

const activeTenantsCount = computed(() => {
    return tenants.value.length;
});

const usagePercentage = computed(() => {
    if (!account.value || !account.value.maxTenants) return 0;
    return Math.round((activeTenantsCount.value / account.value.maxTenants) * 100);
});

function formatNumber(n: number): string {
    if (!n) return '0';
    if (n >= 1000000) return (n / 1000000).toFixed(1) + 'M';
    if (n >= 1000) return (n / 1000).toFixed(1) + 'K';
    return String(n);
}
</script>

<template>
    <div class="dev-dashboard">
        <header class="header">
            <div>
                <h1>Developer Hub</h1>
                <p class="subtitle">Programmatic integration stats, API access, and tenant deployments overview.</p>
            </div>
            <router-link to="/developer/tenants?action=provision">
                <Button label="Provision New Tenant" icon="pi pi-plus" />
            </router-link>
        </header>

        <Message v-if="error" severity="error" variant="simple" class="mb-4">{{ error }}</Message>

        <div v-if="loading && !account" class="loading-state">
            <i class="pi pi-spin pi-spinner" style="font-size: 2rem"></i>
            <p>Loading developer configurations...</p>
        </div>

        <div v-else-if="account">
            <!-- Stats Overview -->
            <div class="kpi-grid">
                <div class="kpi-card">
                    <div class="kpi-icon company"><i class="pi pi-building"></i></div>
                    <div class="kpi-body">
                        <span class="kpi-value text-ellipsis" :title="account.companyName">{{ account.companyName }}</span>
                        <span class="kpi-label">Partner Account</span>
                    </div>
                </div>
                <div class="kpi-card">
                    <div class="kpi-icon tenants"><i class="pi pi-users"></i></div>
                    <div class="kpi-body">
                        <span class="kpi-value">{{ activeTenantsCount }} / {{ account.maxTenants }}</span>
                        <span class="kpi-label">Deployed Tenants ({{ usagePercentage }}%)</span>
                    </div>
                </div>
                <div class="kpi-card">
                    <div class="kpi-icon sessions"><i class="pi pi-comments"></i></div>
                    <div class="kpi-body">
                        <span class="kpi-value">{{ formatNumber(totalSessions) }}</span>
                        <span class="kpi-label">Total Chats Combined</span>
                    </div>
                </div>
                <div class="kpi-card">
                    <div class="kpi-icon key"><i class="pi pi-key"></i></div>
                    <div class="kpi-body">
                        <span class="kpi-value">{{ account.masterKeyActive ? 'Active' : 'Revoked' }}</span>
                        <span class="kpi-label">Master API Credentials</span>
                    </div>
                </div>
            </div>

            <!-- Dashboard Details -->
            <div class="details-grid">
                <Card class="details-card">
                    <template #title><span class="card-title">Integration Guide</span></template>
                    <template #content>
                        <div class="guide-content">
                            <p>Integrate the AiChatBox system into your B2B multi-tenant applications programmatically. Follow these steps:</p>
                            <ol class="steps-list">
                                <li>
                                    <strong>Provision Tenants</strong>
                                    <p>When a new customer signs up or registers on your platform, invoke our provisioning API endpoint to create their chatbot instance instantly:</p>
                                    <pre class="code-snippet">POST /api/partner/tenants
Headers: { "X-Master-Key": "YOUR_MASTER_KEY" }
Body: { "TenantName": "Customer Co", "TenantIdentifier": "customer-co-subdomain" }</pre>
                                </li>
                                <li>
                                    <strong>Generate Scoped Embed Tokens</strong>
                                    <p>To let customers configure their specific chatbot settings via an iframe within your dashboard, generate a scoped token:</p>
                                    <pre class="code-snippet">POST /api/partner/tenants/{tenantProjectId}/token
Headers: { "X-Master-Key": "YOUR_MASTER_KEY" }</pre>
                                </li>
                                <li>
                                    <strong>Embed settings iframe</strong>
                                    <p>Embed our minimal, white-labeled dashboard inside an iframe, passing the generated token:</p>
                                    <pre class="code-snippet">&lt;iframe src="https://aichatbox.com/embed/{tenantProjectId}?token={token}"&gt;&lt;/iframe&gt;</pre>
                                </li>
                            </ol>
                            <div class="mt-4">
                                <router-link to="/developer/settings">
                                    <Button label="Manage API Credentials" icon="pi pi-key" severity="secondary" outlined size="small" />
                                </router-link>
                            </div>
                        </div>
                    </template>
                </Card>

                <Card class="details-card">
                    <template #title><span class="card-title">Integration Config Defaults</span></template>
                    <template #content>
                        <div class="defaults-list">
                            <div class="default-item">
                                <span class="label">Whitelisted Domains</span>
                                <span class="value font-mono">{{ account.allowedDomainPattern || 'No restriction (*)' }}</span>
                            </div>
                            <div class="default-item">
                                <span class="label">Default LLM Provider</span>
                                <span class="value font-mono">{{ account.defaultProvider || 'gemini' }}</span>
                            </div>
                            <div class="default-item">
                                <span class="label">Default LLM Model</span>
                                <span class="value font-mono">{{ account.defaultModel || 'gemini-3.1-flash-lite-preview' }}</span>
                            </div>
                            <div class="default-item">
                                <span class="label">System Prompt Template</span>
                                <span class="value prompt-preview">{{ account.defaultSystemPrompt || 'You are a helpful AI assistant.' }}</span>
                            </div>
                            <div class="default-item">
                                <span class="label">Theme Customization</span>
                                <span class="value font-mono">{{ account.defaultThemeSettingsJson ? 'Custom' : 'System Default' }}</span>
                            </div>
                            <div class="mt-4 text-right">
                                <router-link to="/developer/settings">
                                    <Button label="Modify Templates" icon="pi pi-sliders-h" severity="secondary" text size="small" />
                                </router-link>
                            </div>
                        </div>
                    </template>
                </Card>
            </div>
        </div>
    </div>
</template>

<style scoped>
.dev-dashboard {
    padding: 12px 0;
}
.header {
    display: flex;
    justify-content: space-between;
    align-items: flex-end;
    margin-bottom: 32px;
}
.subtitle {
    color: var(--p-surface-500);
    margin-top: 4px;
    font-size: 0.9rem;
}
.loading-state {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    padding: 64px 0;
    color: var(--p-surface-400);
}
.loading-state p {
    margin-top: 12px;
}

/* KPI */
.kpi-grid {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
    gap: 16px;
    margin-bottom: 32px;
}
.kpi-card {
    background: var(--p-surface-0);
    border: 1px solid var(--p-surface-200);
    border-radius: 12px;
    padding: 20px;
    display: flex;
    align-items: center;
    gap: 16px;
    transition: transform 0.15s, box-shadow 0.15s;
}
.kpi-card:hover {
    transform: translateY(-2px);
    box-shadow: 0 4px 12px rgba(0,0,0,0.05);
}
.kpi-icon {
    width: 44px;
    height: 44px;
    border-radius: 10px;
    display: flex;
    align-items: center;
    justify-content: center;
    font-size: 1.2rem;
    flex-shrink: 0;
}
.kpi-icon.company { background: color-mix(in srgb, var(--p-primary-500) 12%, transparent); color: var(--p-primary-600); }
.kpi-icon.tenants { background: color-mix(in srgb, var(--p-indigo-500) 12%, transparent); color: var(--p-indigo-600); }
.kpi-icon.sessions { background: color-mix(in srgb, var(--p-emerald-500) 12%, transparent); color: var(--p-emerald-600); }
.kpi-icon.key { background: color-mix(in srgb, var(--p-amber-500) 12%, transparent); color: var(--p-amber-600); }

.kpi-body {
    display: flex;
    flex-direction: column;
    min-width: 0;
}
.kpi-value {
    font-size: 1.3rem;
    font-weight: 700;
    color: var(--p-surface-900);
    line-height: 1.2;
}
.text-ellipsis {
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
}
.kpi-label {
    font-size: 0.75rem;
    color: var(--p-surface-500);
    margin-top: 2px;
}

/* Details Grid */
.details-grid {
    display: grid;
    grid-template-columns: 3fr 2fr;
    gap: 24px;
}
.details-card {
    background: var(--p-surface-0);
    border: 1px solid var(--p-surface-200);
}
.card-title {
    font-size: 1rem;
    font-weight: 600;
}
.steps-list {
    margin: 16px 0 0 0;
    padding-left: 20px;
}
.steps-list li {
    margin-bottom: 16px;
    font-size: 0.9rem;
    color: var(--p-surface-700);
}
.steps-list li p {
    margin: 4px 0;
    color: var(--p-surface-500);
    font-size: 0.85rem;
}
.code-snippet {
    background: var(--p-surface-900);
    color: var(--p-surface-100);
    padding: 8px 12px;
    border-radius: 6px;
    font-family: monospace;
    font-size: 0.75rem;
    overflow-x: auto;
    border: 1px solid var(--p-surface-800);
    margin: 8px 0;
}

.defaults-list {
    display: flex;
    flex-direction: column;
    gap: 16px;
    margin-top: 12px;
}
.default-item {
    display: flex;
    flex-direction: column;
    gap: 4px;
    border-bottom: 1px solid var(--p-surface-100);
    padding-bottom: 12px;
}
.default-item:last-child {
    border: none;
    padding: 0;
}
.default-item .label {
    font-size: 0.8rem;
    font-weight: 600;
    color: var(--p-surface-400);
}
.default-item .value {
    font-size: 0.85rem;
    color: var(--p-surface-800);
    word-break: break-all;
}
.font-mono {
    font-family: monospace;
    background: var(--p-surface-50);
    padding: 2px 6px;
    border-radius: 4px;
    border: 1px solid var(--p-surface-200);
    display: inline-block;
}
.prompt-preview {
    font-style: italic;
    color: var(--p-surface-600);
}

@media (max-width: 992px) {
    .details-grid {
        grid-template-columns: 1fr;
    }
}
</style>

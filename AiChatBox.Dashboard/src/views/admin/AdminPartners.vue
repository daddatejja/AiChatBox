<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { useApi } from '../../composables/useApi';
import DataTable from 'primevue/datatable';
import Column from 'primevue/column';
import Button from 'primevue/button';
import Dialog from 'primevue/dialog';
import Select from 'primevue/select';
import InputText from 'primevue/inputtext';
import InputNumber from 'primevue/inputnumber';
import Message from 'primevue/message';
import Tag from 'primevue/tag';

const { apiFetch } = useApi();

const partners = ref<any[]>([]);
const users = ref<any[]>([]);
const loading = ref(false);
const error = ref('');
const success = ref('');

// Dialogs
const showCreateDialog = ref(false);
const showEditDialog = ref(false);
const showKeyDialog = ref(false);

// Form States
const createForm = ref({
    userId: '',
    companyName: '',
    allowedDomainPattern: '',
    maxTenants: 100,
    creditLimit: 0
});

const editForm = ref({
    id: '',
    companyName: '',
    allowedDomainPattern: '',
    maxTenants: 100,
    creditLimit: 0
});

const newlyCreatedKey = ref('');

const loadData = async () => {
    loading.value = true;
    error.value = '';
    try {
        const [pRes, uRes] = await Promise.all([
            apiFetch('/api/admin/partners'),
            apiFetch('/api/admin/users')
        ]);
        if (pRes.ok) partners.value = await pRes.json();
        if (uRes.ok) {
            const allUsers = await uRes.json();
            // Only suggest users who are not already partners or admins
            users.value = allUsers.filter((u: any) => u.role === 'StandardUser');
        }
    } catch (e) {
        error.value = 'Failed to load partners or users list.';
        console.error(e);
    } finally {
        loading.value = false;
    }
};

const handleCreatePartner = async () => {
    error.value = '';
    success.value = '';
    if (!createForm.value.userId) {
        error.value = 'Please select a user to elevate.';
        return;
    }
    try {
        const res = await apiFetch('/api/admin/partners', {
            method: 'POST',
            body: JSON.stringify(createForm.value)
        });
        if (res.ok) {
            const data = await res.json();
            newlyCreatedKey.value = data.masterKey;
            showCreateDialog.value = false;
            showKeyDialog.value = true;
            success.value = `Partner elevated successfully.`;
            // Reset form
            createForm.value = {
                userId: '',
                companyName: '',
                allowedDomainPattern: '',
                maxTenants: 100,
                creditLimit: 0
            };
            await loadData();
        } else {
            const errData = await res.json();
            error.value = errData.message || 'Failed to elevate partner.';
        }
    } catch (e) {
        error.value = 'An error occurred during partner creation.';
    }
};

const openEditDialog = (partner: any) => {
    editForm.value = {
        id: partner.id,
        companyName: partner.companyName,
        allowedDomainPattern: partner.allowedDomainPattern || '',
        maxTenants: partner.maxTenants,
        creditLimit: partner.creditLimit
    };
    showEditDialog.value = true;
};

const handleUpdatePartner = async () => {
    error.value = '';
    success.value = '';
    try {
        const res = await apiFetch(`/api/admin/partners/${editForm.value.id}`, {
            method: 'PUT',
            body: JSON.stringify(editForm.value)
        });
        if (res.ok) {
            success.value = 'Partner account updated.';
            showEditDialog.value = false;
            await loadData();
        } else {
            error.value = 'Failed to update partner accounts.';
        }
    } catch (e) {
        error.value = 'An error occurred.';
    }
};

const handleDeletePartner = async (partnerId: string) => {
    if (!confirm('Are you sure you want to demote this partner back to a standard user? This will delete the partner account.')) return;
    error.value = '';
    success.value = '';
    try {
        const res = await apiFetch(`/api/admin/partners/${partnerId}`, {
            method: 'DELETE'
        });
        if (res.ok) {
            success.value = 'Partner demoted successfully.';
            await loadData();
        } else {
            error.value = 'Failed to demote partner.';
        }
    } catch (e) {
        error.value = 'An error occurred.';
    }
};

onMounted(() => {
    loadData();
});
</script>

<template>
    <div class="partners-view">
        <header class="header">
            <div>
                <h1>Partner Management</h1>
                <p class="subtitle">Elevate users to B2B Partners, adjust account quotas, and configure tenant limits.</p>
            </div>
            <Button label="Elevate Partner" icon="pi pi-plus" @click="showCreateDialog = true" />
        </header>

        <Message v-if="error" severity="error" variant="simple" class="mb-4">{{ error }}</Message>
        <Message v-if="success" severity="success" variant="simple" class="mb-4">{{ success }}</Message>

        <DataTable :value="partners" :loading="loading" class="p-datatable-lg" responsiveLayout="scroll">
            <Column field="companyName" header="Company" sortable />
            <Column field="ownerEmail" header="Owner" sortable />
            <Column field="tenantCount" header="Active Tenants">
                <template #body="{ data }">
                    <span class="tenant-count">{{ data.tenantCount }} / {{ data.maxTenants }}</span>
                </template>
            </Column>
            <Column field="creditLimit" header="Spend / Limit">
                <template #body="{ data }">
                    <span>${{ data.currentSpend.toFixed(2) }} / {{ data.creditLimit > 0 ? '$' + data.creditLimit.toFixed(2) : 'Unlimited' }}</span>
                </template>
            </Column>
            <Column field="allowedDomainPattern" header="Domains">
                <template #body="{ data }">
                    <code class="domain-badge" v-if="data.allowedDomainPattern">{{ data.allowedDomainPattern }}</code>
                    <span class="text-muted" v-else>None</span>
                </template>
            </Column>
            <Column field="masterKeyActive" header="API Key">
                <template #body="{ data }">
                    <Tag :severity="data.masterKeyActive ? 'success' : 'secondary'" :value="data.masterKeyActive ? 'Active' : 'Disabled'" />
                </template>
            </Column>
            <Column field="createdAt" header="Joined" sortable>
                <template #body="{ data }">
                    {{ new Date(data.createdAt).toLocaleDateString() }}
                </template>
            </Column>
            <Column header="Actions" headerStyle="width: 8rem; text-align: center" bodyStyle="text-align: center; overflow: visible">
                <template #body="{ data }">
                    <div class="actions-group">
                        <Button icon="pi pi-pencil" severity="secondary" text rounded @click="openEditDialog(data)" title="Edit Limits" />
                        <Button icon="pi pi-user-minus" severity="danger" text rounded @click="handleDeletePartner(data.id)" title="Demote Partner" />
                    </div>
                </template>
            </Column>
        </DataTable>

        <!-- Elevate Partner Dialog -->
        <Dialog v-model:visible="showCreateDialog" header="Elevate Partner Account" modal :style="{ width: '450px' }">
            <div class="dialog-form">
                <div class="form-field">
                    <label for="select-user">Select Developer User</label>
                    <Select id="select-user" v-model="createForm.userId" :options="users" optionLabel="email" optionValue="id" placeholder="Choose a registered user" class="w-full" filter />
                </div>
                <div class="form-field">
                    <label for="company-name">Company Name</label>
                    <InputText id="company-name" v-model="createForm.companyName" placeholder="e.g., Acme Corp" class="w-full" required />
                </div>
                <div class="form-field">
                    <label for="allowed-domains">Allowed Domain Pattern (Optional)</label>
                    <InputText id="allowed-domains" v-model="createForm.allowedDomainPattern" placeholder="e.g., *.acme.com" class="w-full" />
                </div>
                <div class="form-grid">
                    <div class="form-field">
                        <label for="max-tenants">Max Tenants</label>
                        <InputNumber id="max-tenants" v-model="createForm.maxTenants" :min="1" class="w-full" />
                    </div>
                    <div class="form-field">
                        <label for="credit-limit">Credit Limit ($)</label>
                        <InputNumber id="credit-limit" v-model="createForm.creditLimit" :min="0" suffix=" USD" class="w-full" placeholder="0 = Unlimited" />
                    </div>
                </div>
            </div>
            <template #footer>
                <Button label="Cancel" severity="secondary" text @click="showCreateDialog = false" />
                <Button label="Elevate" @click="handleCreatePartner" />
            </template>
        </Dialog>

        <!-- Edit Partner Dialog -->
        <Dialog v-model:visible="showEditDialog" header="Edit Partner Limits" modal :style="{ width: '450px' }">
            <div class="dialog-form">
                <div class="form-field">
                    <label for="edit-company-name">Company Name</label>
                    <InputText id="edit-company-name" v-model="editForm.companyName" class="w-full" />
                </div>
                <div class="form-field">
                    <label for="edit-allowed-domains">Allowed Domain Pattern</label>
                    <InputText id="edit-allowed-domains" v-model="editForm.allowedDomainPattern" class="w-full" />
                </div>
                <div class="form-grid">
                    <div class="form-field">
                        <label for="edit-max-tenants">Max Tenants</label>
                        <InputNumber id="edit-max-tenants" v-model="editForm.maxTenants" :min="1" class="w-full" />
                    </div>
                    <div class="form-field">
                        <label for="edit-credit-limit">Credit Limit ($)</label>
                        <InputNumber id="edit-credit-limit" v-model="editForm.creditLimit" :min="0" class="w-full" />
                    </div>
                </div>
            </div>
            <template #footer>
                <Button label="Cancel" severity="secondary" text @click="showEditDialog = false" />
                <Button label="Save Changes" @click="handleUpdatePartner" />
            </template>
        </Dialog>

        <!-- Master Key Dialog (Shown Once) -->
        <Dialog v-model:visible="showKeyDialog" header="Master API Key Generated" modal :closable="false" :style="{ width: '500px' }">
            <div class="key-display-content">
                <Message severity="warn" variant="simple" class="mb-4">Copy this master API key now. It will not be shown again.</Message>
                <div class="key-box">
                    <code>{{ newlyCreatedKey }}</code>
                </div>
            </div>
            <template #footer>
                <Button label="Done" @click="showKeyDialog = false; newlyCreatedKey = ''" />
            </template>
        </Dialog>
    </div>
</template>

<style scoped>
.partners-view {
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
.tenant-count {
    font-weight: 600;
    color: var(--p-surface-800);
}
.domain-badge {
    background: var(--p-surface-100);
    padding: 3px 6px;
    border-radius: 4px;
    font-family: monospace;
    font-size: 0.8rem;
    color: var(--p-surface-700);
}
.actions-group {
    display: flex;
    gap: 4px;
    justify-content: center;
}
.text-muted {
    color: var(--p-surface-400);
    font-size: 0.85rem;
}

/* Dialog Forms */
.dialog-form {
    display: flex;
    flex-direction: column;
    gap: 16px;
    padding-top: 8px;
}
.form-field {
    display: flex;
    flex-direction: column;
    gap: 6px;
}
.form-field label {
    font-size: 0.85rem;
    font-weight: 600;
    color: var(--p-surface-600);
}
.form-grid {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 12px;
}

/* Key Display */
.key-display-content {
    display: flex;
    flex-direction: column;
}
.key-box {
    background: var(--p-surface-900);
    color: var(--p-emerald-400);
    padding: 16px;
    border-radius: 8px;
    font-family: monospace;
    font-size: 1rem;
    word-break: break-all;
    text-align: center;
    border: 1px solid var(--p-surface-800);
    box-shadow: inset 0 2px 4px rgba(0,0,0,0.2);
}
</style>

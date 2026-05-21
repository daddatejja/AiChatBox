<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { useApi } from '../../composables/useApi';
import DataTable from 'primevue/datatable';
import Column from 'primevue/column';
import Button from 'primevue/button';
import Select from 'primevue/select';
import Message from 'primevue/message';
import Tag from 'primevue/tag';

const { apiFetch } = useApi();

const users = ref<any[]>([]);
const loading = ref(false);
const error = ref('');
const success = ref('');

const roleOptions = [
    { label: 'Standard User', value: 'StandardUser' },
    { label: 'B2B Partner Developer', value: 'PartnerDeveloper' },
    { label: 'System Admin', value: 'SystemAdmin' }
];

const loadUsers = async () => {
    loading.value = true;
    error.value = '';
    try {
        const res = await apiFetch('/api/admin/users');
        if (res.ok) {
            users.value = await res.json();
        } else {
            error.value = 'Failed to load users list.';
        }
    } catch (e) {
        error.value = 'An error occurred while loading users.';
        console.error(e);
    } finally {
        loading.value = false;
    }
};

const handleRoleChange = async (user: any, newRole: string) => {
    error.value = '';
    success.value = '';
    try {
        const res = await apiFetch(`/api/admin/users/${user.id}/role`, {
            method: 'PUT',
            body: JSON.stringify({ role: newRole })
        });
        if (res.ok) {
            success.value = `Updated role for ${user.email} to ${newRole}.`;
            await loadUsers();
        } else {
            error.value = 'Failed to update user role.';
        }
    } catch (e) {
        error.value = 'An error occurred during role change.';
    }
};

const handleImpersonate = async (user: any) => {
    error.value = '';
    success.value = '';
    try {
        const res = await apiFetch(`/api/admin/impersonate/${user.id}`, {
            method: 'POST'
        });
        if (res.ok) {
            const data = await res.json();
            success.value = `Impersonation token generated for ${user.email}. Opening session...`;
            // Open homepage in a new tab with the token query parameter to log in
            window.open(`/?token=${data.token}`, '_blank');
        } else {
            error.value = 'Failed to generate impersonation session.';
        }
    } catch (e) {
        error.value = 'An error occurred during impersonation request.';
    }
};

onMounted(() => {
    loadUsers();
});

const getRoleSeverity = (role: string) => {
    switch (role) {
        case 'SystemAdmin': return 'danger';
        case 'PartnerDeveloper': return 'warn';
        default: return 'info';
    }
};
</script>

<template>
    <div class="users-view">
        <header class="header">
            <div>
                <h1>User Management</h1>
                <p class="subtitle">Assign system access roles, view active projects count, and manage developer permissions.</p>
            </div>
        </header>

        <Message v-if="error" severity="error" variant="simple" class="mb-4">{{ error }}</Message>
        <Message v-if="success" severity="success" variant="simple" class="mb-4">{{ success }}</Message>

        <DataTable :value="users" :loading="loading" class="p-datatable-lg" responsiveLayout="scroll" paginator :rows="15">
            <Column field="username" header="Username" sortable />
            <Column field="email" header="Email" sortable />
            <Column field="role" header="Role" sortable>
                <template #body="{ data }">
                    <Tag :severity="getRoleSeverity(data.role)" :value="data.role" />
                </template>
            </Column>
            <Column field="projectCount" header="Owned Chatbots" sortable>
                <template #body="{ data }">
                    <span class="count-badge">{{ data.projectCount }}</span>
                </template>
            </Column>
            <Column field="createdAt" header="Registered" sortable>
                <template #body="{ data }">
                    {{ new Date(data.createdAt).toLocaleDateString() }}
                </template>
            </Column>
            <Column header="Change Access Role" headerStyle="width: 14rem">
                <template #body="{ data }">
                    <Select :modelValue="data.role" :options="roleOptions" optionLabel="label" optionValue="value" class="w-full select-role" @update:modelValue="(val) => handleRoleChange(data, val)" />
                </template>
            </Column>
            <Column header="Actions" headerStyle="width: 8rem; text-align: center" bodyStyle="text-align: center">
                <template #body="{ data }">
                    <Button label="Impersonate" icon="pi pi-user" severity="secondary" size="small" outlined @click="handleImpersonate(data)" title="Sign in as this user in a new tab" />
                </template>
            </Column>
        </DataTable>
    </div>
</template>

<style scoped>
.users-view {
    padding: 12px 0;
}
.header {
    margin-bottom: 32px;
}
.subtitle {
    color: var(--p-surface-500);
    margin-top: 4px;
    font-size: 0.9rem;
}
.count-badge {
    background: var(--p-surface-100);
    color: var(--p-surface-800);
    padding: 4px 8px;
    border-radius: 6px;
    font-weight: 600;
    font-size: 0.85rem;
}
.select-role {
    font-size: 0.85rem;
}
</style>

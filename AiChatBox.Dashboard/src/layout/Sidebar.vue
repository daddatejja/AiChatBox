<script setup lang="ts">
import { useRoute } from 'vue-router';
import { useApi } from '../composables/useApi';
import { useAuth } from '../composables/useAuth';

defineProps<{
    collapsed: boolean;
}>();

const emit = defineEmits<{
    (e: 'navigate'): void;
}>();

const route = useRoute();
const { apiFetch, API_BASE } = useApi();
const { isAdmin, isPartner } = useAuth();

const coreNavItems = [
    {
        id: 'projects',
        label: 'Projects',
        icon: 'pi-th-large',
        to: '/',
        exact: true
    },
    {
        id: 'logs',
        label: 'Logs',
        icon: 'pi-file',
        to: '/logs'
    },
    {
        id: 'analytics',
        label: 'Analytics',
        icon: 'pi-chart-bar',
        to: '/analytics'
    },
    {
        id: 'playground',
        label: 'Playground',
        icon: 'pi-box',
        to: '/playground'
    },
    {
        id: 'live-chat',
        label: 'Live Chat',
        icon: 'pi-headphones',
        to: '/live-chat'
    },
    {
        id: 'docs',
        label: 'Documentation',
        icon: 'pi-book',
        to: '/docs'
    }
];

const devNavItems = [
    {
        id: 'dev-dashboard',
        label: 'Developer Hub',
        icon: 'pi-code',
        to: '/developer'
    },
    {
        id: 'dev-tenants',
        label: 'Tenants',
        icon: 'pi-users',
        to: '/developer/tenants'
    },
    {
        id: 'dev-settings',
        label: 'Partner Settings',
        icon: 'pi-cog',
        to: '/developer/settings'
    }
];

const adminNavItems = [
    {
        id: 'admin-dashboard',
        label: 'Admin Panel',
        icon: 'pi-shield',
        to: '/admin'
    },
    {
        id: 'admin-partners',
        label: 'Partners',
        icon: 'pi-building',
        to: '/admin/partners'
    },
    {
        id: 'admin-users',
        label: 'Users',
        icon: 'pi-id-card',
        to: '/admin/users'
    }
];

function isActive(item: any): boolean {
    if (item.exact) {
        return route.path === item.to;
    }
    return route.path.startsWith(item.to);
}

function handleNavigate(item: any) {
    if (item.action) {
        item.action();
    } else {
        emit('navigate');
    }
}

async function openHangfire() {
    try {
        await apiFetch('/api/auth/hangfire-cookie', { credentials: 'include' });
        window.open(`${API_BASE}/hangfire`, '_blank');
    } catch (e) {
        console.error("Failed to authenticate for Hangfire", e);
    }
}

const externalItems = [
    {
        id: 'jobs',
        label: 'System Jobs',
        icon: 'pi-server',
        action: openHangfire
    }
];
</script>

<template>
    <aside :class="['sidebar', { collapsed }]">
        <!-- Logo -->
        <router-link to="/" class="logo-link">
            <div class="logo-icon">
                <svg width="20" height="20" viewBox="0 0 24 24" fill="none">
                    <path d="M19 9l1.25-2.75L23 5l-2.75-1.25L19 1l-1.25 2.75L15 5l2.75 1.25L19 9zm-7.5.5L9 4 6.5 9.5 1 12l5.5 2.5L9 20l2.5-5.5L17 12l-5.5-2.5z" fill="currentColor"/>
                </svg>
            </div>
            <Transition name="label-fade">
                <span v-if="!collapsed" class="logo-text">AiChatBox</span>
            </Transition>
        </router-link>

        <!-- Navigation -->
        <nav class="nav-section">
            <ul class="nav-menu">
                <li v-for="item in coreNavItems" :key="item.id" class="nav-item">
                    <router-link
                        :to="item.to"
                        :class="['nav-link', { active: isActive(item) }]"
                        :title="collapsed ? item.label : undefined"
                        @click="handleNavigate"
                    >
                        <i :class="['pi', item.icon, 'nav-icon']"></i>
                        <Transition name="label-fade">
                            <span v-if="!collapsed" class="nav-label">{{ item.label }}</span>
                        </Transition>
                    </router-link>
                </li>
            </ul>

            <!-- Developer section -->
            <template v-if="isPartner() || isAdmin()">
                <div class="nav-section-title" v-if="!collapsed">Developer Hub</div>
                <div class="nav-divider" v-else></div>
                <ul class="nav-menu">
                    <li v-for="item in devNavItems" :key="item.id" class="nav-item">
                        <router-link
                            :to="item.to"
                            :class="['nav-link', { active: isActive(item) }]"
                            :title="collapsed ? item.label : undefined"
                            @click="handleNavigate"
                        >
                            <i :class="['pi', item.icon, 'nav-icon']"></i>
                            <Transition name="label-fade">
                                <span v-if="!collapsed" class="nav-label">{{ item.label }}</span>
                            </Transition>
                        </router-link>
                    </li>
                </ul>
            </template>

            <!-- Admin section -->
            <template v-if="isAdmin()">
                <div class="nav-section-title" v-if="!collapsed">Administration</div>
                <div class="nav-divider" v-else></div>
                <ul class="nav-menu">
                    <li v-for="item in adminNavItems" :key="item.id" class="nav-item">
                        <router-link
                            :to="item.to"
                            :class="['nav-link', { active: isActive(item) }]"
                            :title="collapsed ? item.label : undefined"
                            @click="handleNavigate"
                        >
                            <i :class="['pi', item.icon, 'nav-icon']"></i>
                            <Transition name="label-fade">
                                <span v-if="!collapsed" class="nav-label">{{ item.label }}</span>
                            </Transition>
                        </router-link>
                    </li>
                </ul>
            </template>

            <!-- Jobs section (Admin only) -->
            <template v-if="isAdmin()">
                <div class="nav-divider"></div>
                <ul class="nav-menu">
                    <li v-for="item in externalItems" :key="item.id" class="nav-item">
                        <a
                            href="#"
                            :class="['nav-link']"
                            :title="collapsed ? item.label : undefined"
                            @click.prevent="handleNavigate(item)"
                        >
                            <i :class="['pi', item.icon, 'nav-icon']"></i>
                            <Transition name="label-fade">
                                <span v-if="!collapsed" class="nav-label">{{ item.label }} <i class="pi pi-external-link ml-1" style="font-size: 0.75rem"></i></span>
                            </Transition>
                        </a>
                    </li>
                </ul>
            </template>
        </nav>

        <!-- Bottom section -->
        <div class="sidebar-footer">
            <Transition name="label-fade">
                <div v-if="!collapsed" class="footer-badge">
                    <i class="pi pi-bolt"></i>
                    <span>v1.0</span>
                </div>
            </Transition>
            <div v-if="collapsed" class="footer-badge-collapsed">
                <i class="pi pi-bolt"></i>
            </div>
        </div>
    </aside>
</template>

<style scoped>
.sidebar {
    width: var(--sidebar-width);
    min-width: var(--sidebar-width);
    background-color: var(--p-surface-50);
    border-right: 1px solid var(--p-surface-200);
    padding: 20px 12px;
    display: flex;
    flex-direction: column;
    height: 100vh;
    box-sizing: border-box;
    transition: width var(--sidebar-transition), min-width var(--sidebar-transition), padding var(--sidebar-transition);
    overflow: hidden;
    position: sticky;
    top: 0;
    z-index: 110;
}

.sidebar.collapsed {
    width: var(--sidebar-collapsed-width);
    min-width: var(--sidebar-collapsed-width);
    padding: 20px 10px;
}

/* Logo */
.logo-link {
    display: flex;
    align-items: center;
    gap: 12px;
    text-decoration: none;
    color: var(--p-surface-900);
    padding: 8px 12px;
    margin-bottom: 32px;
    border-radius: 10px;
    transition: background-color 0.2s ease;
    white-space: nowrap;
    overflow: hidden;
}

.logo-link:hover {
    background-color: var(--p-surface-100);
    text-decoration: none;
}

.logo-icon {
    width: 32px;
    height: 32px;
    min-width: 32px;
    background: linear-gradient(135deg, var(--p-primary-500), var(--p-primary-700));
    border-radius: 10px;
    display: flex;
    align-items: center;
    justify-content: center;
    color: white;
}

.logo-text {
    font-size: 1.15rem;
    font-weight: 700;
    letter-spacing: -0.02em;
}

/* Navigation */
.nav-section {
    flex: 1;
    display: flex;
    flex-direction: column;
    overflow-y: auto;
}

.nav-menu {
    list-style: none;
    padding: 0;
    margin: 0;
    display: flex;
    flex-direction: column;
    gap: 4px;
}

.nav-link {
    display: flex;
    align-items: center;
    gap: 12px;
    padding: 10px 12px;
    border-radius: 10px;
    color: var(--p-surface-500);
    text-decoration: none;
    font-weight: 500;
    font-size: 0.9rem;
    transition: all 0.2s ease;
    white-space: nowrap;
    overflow: hidden;
    position: relative;
}

.nav-link:hover {
    background-color: var(--p-surface-100);
    color: var(--p-surface-900);
    text-decoration: none;
}

.nav-link.active {
    background-color: var(--p-primary-50);
    color: var(--p-primary-600);
    font-weight: 600;
}

.nav-link.active .nav-icon {
    color: var(--p-primary-500);
}

.nav-divider {
    height: 1px;
    background-color: var(--p-surface-200);
    margin: 12px 0;
    flex-shrink: 0;
}

.nav-section-title {
    font-size: 0.7rem;
    text-transform: uppercase;
    letter-spacing: 0.05em;
    color: var(--p-surface-400);
    margin: 16px 12px 6px 12px;
    font-weight: 700;
    flex-shrink: 0;
}

.ml-1 {
    margin-left: 0.25rem;
}

.nav-icon {
    font-size: 1.1rem;
    min-width: 20px;
    text-align: center;
    transition: color 0.2s ease;
}

.nav-label {
    line-height: 1;
}

/* Collapsed state — center icons */
.collapsed .nav-link {
    justify-content: center;
    padding: 12px;
}

.collapsed .logo-link {
    justify-content: center;
    padding: 8px;
}

/* Footer */
.sidebar-footer {
    padding-top: 16px;
    border-top: 1px solid var(--p-surface-200);
    display: flex;
    justify-content: center;
}

.footer-badge {
    display: flex;
    align-items: center;
    gap: 6px;
    padding: 6px 12px;
    border-radius: 20px;
    background-color: var(--p-surface-100);
    color: var(--p-surface-500);
    font-size: 0.75rem;
    font-weight: 600;
}

.footer-badge-collapsed {
    display: flex;
    align-items: center;
    justify-content: center;
    width: 32px;
    height: 32px;
    border-radius: 50%;
    background-color: var(--p-surface-100);
    color: var(--p-surface-400);
    font-size: 0.8rem;
}

/* Label fade transition */
.label-fade-enter-active {
    transition: opacity 0.2s ease 0.1s;
}
.label-fade-leave-active {
    transition: opacity 0.1s ease;
}
.label-fade-enter-from,
.label-fade-leave-to {
    opacity: 0;
}
</style>

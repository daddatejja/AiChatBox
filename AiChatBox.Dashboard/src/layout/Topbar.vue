<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { useRouter } from 'vue-router';
import { useApi } from '../composables/useApi';

defineProps<{
    collapsed: boolean;
}>();

const emit = defineEmits<{
    (e: 'toggle-sidebar'): void;
}>();

const router = useRouter();
const { apiFetch } = useApi();
const username = ref('');
const userEmail = ref('');
const showUserMenu = ref(false);
const currentThemeIcon = ref('pi-moon');

onMounted(async () => {
    // Theme
    const savedTheme = localStorage.getItem('acb_theme') || 'dark';
    document.documentElement.setAttribute('data-theme', savedTheme);
    currentThemeIcon.value = savedTheme === 'dark' ? 'pi-sun' : 'pi-moon';

    // Fetch real user info from API
    try {
        const res = await apiFetch('/api/auth/me');
        if (res.ok) {
            const data = await res.json();
            username.value = data.username || data.email || 'User';
            userEmail.value = data.email || '';
            localStorage.setItem('acb_username', username.value);
        } else {
            // Fallback to cached name
            username.value = localStorage.getItem('acb_username') || 'User';
        }
    } catch {
        username.value = localStorage.getItem('acb_username') || 'User';
    }
});

const toggleTheme = () => {
    const currentTheme = document.documentElement.getAttribute('data-theme') || 'dark';
    const newTheme = currentTheme === 'dark' ? 'light' : 'dark';
    document.documentElement.setAttribute('data-theme', newTheme);
    localStorage.setItem('acb_theme', newTheme);
    currentThemeIcon.value = newTheme === 'dark' ? 'pi-sun' : 'pi-moon';
};

const logout = () => {
    localStorage.removeItem('acb_token');
    localStorage.removeItem('acb_username');
    router.push('/login');
};

const toggleUserMenu = () => {
    showUserMenu.value = !showUserMenu.value;
};

const closeMenu = () => {
    showUserMenu.value = false;
};
</script>

<template>
    <header class="topbar" @click.self="closeMenu">
        <div class="topbar-left">
            <button class="topbar-toggle" @click="emit('toggle-sidebar')" title="Toggle sidebar">
                <i class="pi pi-bars"></i>
            </button>
        </div>

        <div class="topbar-right">
            <!-- Theme Toggle -->
            <button class="topbar-icon-btn" @click="toggleTheme" title="Toggle theme">
                <i :class="['pi', currentThemeIcon]"></i>
            </button>

            <!-- User Profile -->
            <div class="user-menu-wrapper">
                <button class="user-chip" @click="toggleUserMenu">
                    <div class="user-avatar">
                        {{ username ? username.charAt(0).toUpperCase() : 'U' }}
                    </div>
                    <span class="user-name">{{ username }}</span>
                    <i class="pi pi-chevron-down chevron-icon"></i>
                </button>

                <Transition name="menu-fade">
                    <div v-if="showUserMenu" class="user-dropdown">
                        <div class="dropdown-header" v-if="userEmail">
                            <div class="dropdown-user-name">{{ username }}</div>
                            <div class="dropdown-user-email">{{ userEmail }}</div>
                        </div>
                        <button class="dropdown-item" @click="logout">
                            <i class="pi pi-sign-out"></i>
                            <span>Sign Out</span>
                        </button>
                    </div>
                </Transition>
            </div>
        </div>
    </header>
</template>

<style scoped>
.topbar {
    height: var(--topbar-height);
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 0 24px;
    background-color: var(--p-surface-0);
    border-bottom: 1px solid var(--p-surface-200);
    position: sticky;
    top: 0;
    z-index: 100;
    box-sizing: border-box;
}

.topbar-left {
    display: flex;
    align-items: center;
    gap: 16px;
}

.topbar-toggle {
    width: 36px;
    height: 36px;
    display: flex;
    align-items: center;
    justify-content: center;
    background: transparent;
    border: none;
    border-radius: 8px;
    color: var(--p-surface-500);
    cursor: pointer;
    font-size: 1.1rem;
    transition: all 0.2s ease;
}

.topbar-toggle:hover {
    background-color: var(--p-surface-100);
    color: var(--p-surface-900);
}

.topbar-right {
    display: flex;
    align-items: center;
    gap: 8px;
}

.topbar-icon-btn {
    width: 36px;
    height: 36px;
    display: flex;
    align-items: center;
    justify-content: center;
    background: transparent;
    border: none;
    border-radius: 8px;
    color: var(--p-surface-500);
    cursor: pointer;
    font-size: 1rem;
    transition: all 0.2s ease;
}

.topbar-icon-btn:hover {
    background-color: var(--p-surface-100);
    color: var(--p-surface-900);
}

.user-menu-wrapper {
    position: relative;
}

.user-chip {
    display: flex;
    align-items: center;
    gap: 10px;
    padding: 6px 12px 6px 6px;
    background: transparent;
    border: 1px solid var(--p-surface-200);
    border-radius: 24px;
    cursor: pointer;
    transition: all 0.2s ease;
    color: var(--p-surface-700);
}

.user-chip:hover {
    background-color: var(--p-surface-100);
    border-color: var(--p-surface-300);
}

.user-avatar {
    width: 28px;
    height: 28px;
    border-radius: 50%;
    background: linear-gradient(135deg, var(--p-primary-500), var(--p-primary-700));
    display: flex;
    align-items: center;
    justify-content: center;
    color: white;
    font-size: 0.75rem;
    font-weight: 700;
}

.user-name {
    font-weight: 600;
    font-size: 0.85rem;
}

.chevron-icon {
    font-size: 0.7rem;
    opacity: 0.5;
}

.user-dropdown {
    position: absolute;
    top: calc(100% + 8px);
    right: 0;
    min-width: 220px;
    background-color: var(--p-surface-0);
    border: 1px solid var(--p-surface-200);
    border-radius: 12px;
    box-shadow: 0 8px 24px rgba(0, 0, 0, 0.12);
    overflow: hidden;
    z-index: 200;
}

.dropdown-header {
    padding: 14px 16px;
    border-bottom: 1px solid var(--p-surface-100);
}

.dropdown-user-name {
    font-weight: 700;
    font-size: 0.9rem;
    color: var(--p-surface-900);
}

.dropdown-user-email {
    font-size: 0.78rem;
    color: var(--p-surface-500);
    margin-top: 2px;
}

.dropdown-item {
    width: 100%;
    display: flex;
    align-items: center;
    gap: 10px;
    padding: 12px 16px;
    background: transparent;
    border: none;
    color: var(--p-surface-700);
    cursor: pointer;
    font-size: 0.9rem;
    transition: all 0.15s ease;
}

.dropdown-item:hover {
    background-color: var(--p-surface-100);
    color: var(--p-surface-900);
}

.dropdown-item .pi {
    font-size: 0.9rem;
    opacity: 0.7;
}

/* Transition */
.menu-fade-enter-active,
.menu-fade-leave-active {
    transition: opacity 0.15s ease, transform 0.15s ease;
}
.menu-fade-enter-from,
.menu-fade-leave-to {
    opacity: 0;
    transform: translateY(-4px);
}

/* ── Mobile Responsive ── */
@media (max-width: 768px) {
    .topbar {
        padding: 0 12px;
    }
    .user-name,
    .chevron-icon {
        display: none;
    }
    .user-chip {
        padding: 6px;
        border-radius: 50%;
    }
}
</style>

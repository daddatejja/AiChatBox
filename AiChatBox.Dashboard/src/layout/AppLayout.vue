<script setup lang="ts">
import { ref, onMounted, onUnmounted, computed } from 'vue';
import Sidebar from './Sidebar.vue';
import Topbar from './Topbar.vue';
import { useAuth } from '../composables/useAuth';

const sidebarCollapsed = ref(false);
const isMobile = ref(false);
const mobileMenuOpen = ref(false);

const { getUser, exitImpersonation } = useAuth();
const currentUser = computed(() => getUser());
const isImpersonating = computed(() => sessionStorage.getItem('acb_is_impersonating') === 'true');

function checkMobile() {
    isMobile.value = window.innerWidth < 769;
    if (isMobile.value) {
        sidebarCollapsed.value = true;
        mobileMenuOpen.value = false;
    }
}

onMounted(() => {
    const saved = localStorage.getItem('acb_sidebar_collapsed');
    if (saved === 'true') sidebarCollapsed.value = true;

    // Initialize theme
    const savedTheme = localStorage.getItem('acb_theme') || 'dark';
    document.documentElement.setAttribute('data-theme', savedTheme);

    checkMobile();
    window.addEventListener('resize', checkMobile);
});

onUnmounted(() => {
    window.removeEventListener('resize', checkMobile);
});

function toggleSidebar() {
    if (isMobile.value) {
        mobileMenuOpen.value = !mobileMenuOpen.value;
    } else {
        sidebarCollapsed.value = !sidebarCollapsed.value;
        localStorage.setItem('acb_sidebar_collapsed', String(sidebarCollapsed.value));
    }
}

function closeMobileMenu() {
    mobileMenuOpen.value = false;
}
</script>

<template>
    <div class="app-shell">
        <!-- Mobile overlay backdrop -->
        <Transition name="backdrop-fade">
            <div v-if="mobileMenuOpen" class="mobile-backdrop" @click="closeMobileMenu"></div>
        </Transition>

        <Sidebar 
            :collapsed="isMobile ? false : sidebarCollapsed" 
            :class="{ 'sidebar-mobile-open': mobileMenuOpen, 'sidebar-mobile-hidden': isMobile && !mobileMenuOpen }"
            @navigate="closeMobileMenu"
        />
        <div class="app-main">
            <div v-if="currentUser?.impersonatedBy || isImpersonating" class="impersonation-banner">
                <div class="banner-content">
                    <i class="pi pi-exclamation-triangle warning-icon"></i>
                    <span>
                        You are impersonating <strong>{{ currentUser?.username || currentUser?.email }}</strong>. 
                        Actions you take will affect their account.
                    </span>
                </div>
                <button @click="exitImpersonation" class="exit-button">
                    <i class="pi pi-sign-out"></i> Exit Impersonation
                </button>
            </div>
            <Topbar :collapsed="sidebarCollapsed" @toggle-sidebar="toggleSidebar" />
            <main class="main-content">
                <router-view />
            </main>
        </div>
    </div>
</template>

<style scoped>
.app-shell {
    display: flex;
    min-height: 100vh;
    width: 100%;
}

.app-main {
    flex: 1;
    display: flex;
    flex-direction: column;
    min-width: 0;
    height: 100vh;
}

.main-content {
    flex: 1;
    background-color: var(--p-surface-0);
    overflow-y: auto;
    box-sizing: border-box;
}

.main-content:not(:has(.flow-builder-root)){
    padding: 32px 40px;
}

/* Mobile backdrop overlay */
.mobile-backdrop {
    position: fixed;
    inset: 0;
    background: rgba(0, 0, 0, 0.4);
    z-index: 109;
    backdrop-filter: blur(2px);
}
.backdrop-fade-enter-active,
.backdrop-fade-leave-active {
    transition: opacity 0.25s ease;
}
.backdrop-fade-enter-from,
.backdrop-fade-leave-to {
    opacity: 0;
}

/* Mobile sidebar states */
.sidebar-mobile-hidden {
    position: fixed;
    left: 0;
    top: 0;
    transform: translateX(-100%);
    transition: transform var(--sidebar-transition);
    z-index: 115;
    height: 100vh;
}
.sidebar-mobile-open {
    position: fixed;
    left: 0;
    top: 0;
    transform: translateX(0);
    transition: transform var(--sidebar-transition);
    z-index: 115;
    height: 100vh;
}

/* ── Responsive ── */
@media (max-width: 768px) {
    .main-content {
        padding: 20px 16px;
    }
}

@media (max-width: 480px) {
    .main-content {
        padding: 16px 12px;
    }
}

.impersonation-banner {
    background: linear-gradient(135deg, #d97706 0%, #b45309 100%);
    color: #ffffff;
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding: 10px 24px;
    font-size: 0.9rem;
    font-weight: 500;
    box-shadow: 0 4px 12px rgba(180, 83, 9, 0.15);
    z-index: 1001;
    position: relative;
    border-bottom: 1px solid rgba(255, 255, 255, 0.1);
    animation: slideDown 0.3s ease;
}

@keyframes slideDown {
    from { transform: translateY(-100%); }
    to { transform: translateY(0); }
}

.banner-content {
    display: flex;
    align-items: center;
    gap: 12px;
}

.warning-icon {
    font-size: 1.1rem;
    color: #fef3c7;
    animation: pulse 2s infinite;
}

@keyframes pulse {
    0% { transform: scale(1); opacity: 1; }
    50% { transform: scale(1.15); opacity: 0.8; }
    100% { transform: scale(1); opacity: 1; }
}

.exit-button {
    background: rgba(255, 255, 255, 0.15);
    border: 1px solid rgba(255, 255, 255, 0.3);
    color: #ffffff;
    padding: 6px 14px;
    border-radius: 6px;
    font-weight: 600;
    font-size: 0.82rem;
    cursor: pointer;
    display: flex;
    align-items: center;
    gap: 8px;
    transition: all 0.2s ease;
}

.exit-button:hover {
    background: rgba(255, 255, 255, 0.25);
    transform: translateY(-1px);
    box-shadow: 0 2px 6px rgba(0, 0, 0, 0.1);
}

.exit-button:active {
    transform: translateY(0);
}
</style>

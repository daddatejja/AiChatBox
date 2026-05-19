<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue';
import Sidebar from './Sidebar.vue';
import Topbar from './Topbar.vue';

const sidebarCollapsed = ref(false);
const isMobile = ref(false);
const mobileMenuOpen = ref(false);

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
</style>

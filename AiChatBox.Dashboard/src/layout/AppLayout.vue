<script setup lang="ts">
import { ref, onMounted } from 'vue';
import Sidebar from './Sidebar.vue';
import Topbar from './Topbar.vue';

const sidebarCollapsed = ref(false);

onMounted(() => {
    const saved = localStorage.getItem('acb_sidebar_collapsed');
    if (saved === 'true') sidebarCollapsed.value = true;

    // Initialize theme
    const savedTheme = localStorage.getItem('acb_theme') || 'dark';
    document.documentElement.setAttribute('data-theme', savedTheme);
});

function toggleSidebar() {
    sidebarCollapsed.value = !sidebarCollapsed.value;
    localStorage.setItem('acb_sidebar_collapsed', String(sidebarCollapsed.value));
}
</script>

<template>
    <div class="app-shell">
        <Sidebar :collapsed="sidebarCollapsed" />
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
    padding: 32px 40px;
    background-color: var(--p-surface-0);
    overflow-y: auto;
    box-sizing: border-box;
}
</style>

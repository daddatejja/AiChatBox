<script setup lang="ts">
import Sidebar from './Sidebar.vue';
</script>

<template>
    <div class="app-container">
        <Sidebar />
        <main class="main-content">
            <router-view />
        </main>
    </div>
</template>

<style scoped>
.app-container {
    display: flex;
    min-height: 100vh;
    width: 100%;
}

.main-content {
    flex: 1;
    padding: 48px;
    background-color: var(--p-surface-50);
    overflow-y: auto;
    height: 100vh;
    box-sizing: border-box;
}
</style>

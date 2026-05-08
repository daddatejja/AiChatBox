<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { useRouter } from 'vue-router';

const router = useRouter();
const username = ref('Developer');

onMounted(() => {
    const savedName = localStorage.getItem('acb_username');
    if (savedName) username.value = savedName;

    // Initialize theme
    const savedTheme = localStorage.getItem('acb_theme') || 'dark';
    document.documentElement.setAttribute('data-theme', savedTheme);
});

const toggleTheme = () => {
    const currentTheme = document.documentElement.getAttribute('data-theme') || 'dark';
    const newTheme = currentTheme === 'dark' ? 'light' : 'dark';
    document.documentElement.setAttribute('data-theme', newTheme);
    localStorage.setItem('acb_theme', newTheme);
};

const logout = () => {
    localStorage.removeItem('acb_token');
    localStorage.removeItem('acb_username');
    router.push('/login');
};
</script>

<template>
    <aside class="sidebar">
        <a href="/" class="logo" @click.prevent="router.push('/')">
            <div class="logo-icon"></div>
            <span>AiChatBox</span>
        </a>
        <nav>
            <ul class="nav-menu">
                <li class="nav-item">
                    <router-link to="/" class="nav-link" exact-active-class="active">
                        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="3" y="3" width="7" height="7"></rect><rect x="14" y="3" width="7" height="7"></rect><rect x="14" y="14" width="7" height="7"></rect><rect x="3" y="14" width="7" height="7"></rect></svg>
                        Projects
                    </router-link>
                </li>
                <li class="nav-item">
                    <router-link to="/logs" class="nav-link" active-class="active">
                        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M14 2H6a2 2 0 00-2 2v16a2 2 0 002 2h12a2 2 0 002-2V8z"></path><polyline points="14 2 14 8 20 8"></polyline><line x1="16" y1="13" x2="8" y2="13"></line><line x1="16" y1="17" x2="8" y2="17"></line></svg>
                        Logs
                    </router-link>
                </li>
                <li class="nav-item">
                    <router-link to="/playground" class="nav-link" active-class="active">
                        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 16V8a2 2 0 00-1-1.73l-7-4a2 2 0 00-2 0l-7 4A2 2 0 003 8v8a2 2 0 001 1.73l7 4a2 2 0 002 0l7-4A2 2 0 0021 16z"></path><polyline points="3.27 6.96 12 12.01 20.73 6.96"></polyline><line x1="12" y1="22.08" x2="12" y2="12"></line></svg>
                        Playground
                    </router-link>
                </li>
                <li class="nav-item">
                    <router-link to="/docs" class="nav-link" active-class="active">
                        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M4 19.5A2.5 2.5 0 0 1 6.5 17H20"></path><path d="M6.5 2H20v20H6.5A2.5 2.5 0 0 1 4 19.5v-15A2.5 2.5 0 0 1 6.5 2z"></path></svg>
                        Documentation
                    </router-link>
                </li>
            </ul>
        </nav>
        <div class="theme-toggle">
            <button class="btn-theme" @click="toggleTheme">
                <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="5"></circle><line x1="12" y1="1" x2="12" y2="3"></line><line x1="12" y1="21" x2="12" y2="23"></line><line x1="4.22" y1="4.22" x2="5.64" y2="5.64"></line><line x1="18.36" y1="18.36" x2="19.78" y2="19.78"></line><line x1="1" y1="12" x2="3" y2="12"></line><line x1="21" y1="12" x2="23" y2="12"></line><line x1="4.22" y1="19.78" x2="5.64" y2="18.36"></line><line x1="18.36" y1="5.64" x2="19.78" y2="4.22"></line></svg>
                Toggle Theme
            </button>
        </div>
        <div class="user-profile">
            <div class="user-info">
                <div class="avatar"></div>
                <span class="username">{{ username }}</span>
            </div>
            <a href="#" @click.prevent="logout" class="sign-out">Sign Out</a>
        </div>
    </aside>
</template>

<style scoped>
.sidebar {
    width: 260px;
    background-color: var(--p-surface-100);
    border-right: 1px solid var(--p-surface-700);
    padding: 24px;
    display: flex;
    flex-direction: column;
    height: 100vh;
    box-sizing: border-box;
}

.logo {
    display: flex;
    align-items: center;
    gap: 12px;
    text-decoration: none;
    color: var(--p-surface-900);
    font-size: 1.25rem;
    font-weight: 700;
    margin-bottom: 40px;
}

.logo-icon {
    width: 24px;
    height: 24px;
    background-color: var(--p-primary-500);
    border-radius: 6px;
}

.nav-menu {
    list-style: none;
    padding: 0;
    margin: 0;
    display: flex;
    flex-direction: column;
    gap: 8px;
}

.nav-link {
    display: flex;
    align-items: center;
    gap: 12px;
    padding: 10px 16px;
    border-radius: 8px;
    color: var(--p-surface-400);
    text-decoration: none;
    font-weight: 500;
    transition: all 0.2s ease;
}

.nav-link:hover, .nav-link.active {
    background-color: var(--p-surface-200);
    color: var(--p-surface-900);
}

.theme-toggle {
    margin-top: auto;
    padding-bottom: 24px;
    border-bottom: 1px solid var(--p-surface-700);
    margin-bottom: 24px;
}

.btn-theme {
    width: 100%;
    display: flex;
    align-items: center;
    justify-content: center;
    gap: 8px;
    padding: 10px;
    background-color: transparent;
    border: 1px solid var(--p-surface-300);
    color: var(--p-surface-700);
    border-radius: 8px;
    cursor: pointer;
    font-weight: 500;
    transition: all 0.2s ease;
}

.btn-theme:hover {
    background-color: var(--p-surface-700);
    color: var(--p-surface-0);
}

.user-profile {
    display: flex;
    flex-direction: column;
}

.user-info {
    display: flex;
    align-items: center;
    gap: 12px;
}

.avatar {
    width: 32px;
    height: 32px;
    border-radius: 50%;
    background-color: var(--p-primary-500);
}

.username {
    font-weight: 600;
    font-size: 0.9rem;
    color: var(--p-surface-900);
}

.sign-out {
    color: var(--p-surface-400);
    font-size: 0.8rem;
    margin-top: 12px;
    text-decoration: none;
}
.sign-out:hover {
    color: var(--p-surface-200);
}
</style>

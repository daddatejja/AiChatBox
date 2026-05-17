<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue';
import DocsGettingStarted from '../components/docs/DocsGettingStarted.vue';
import DocsWidgetIntegration from '../components/docs/DocsWidgetIntegration.vue';
import DocsToolCalls from '../components/docs/DocsToolCalls.vue';
import DocsRestApi from '../components/docs/DocsRestApi.vue';
import DocsLiveVoice from '../components/docs/DocsLiveVoice.vue';
import DocsModels from '../components/docs/DocsModels.vue';
import DocsChangelog from '../components/docs/DocsChangelog.vue';

const activeSection = ref('getting-started');

const sections = [
    { id: 'getting-started', label: 'Getting Started', icon: 'pi-bolt' },
    { id: 'widget', label: 'Widget Integration', icon: 'pi-code' },
    { id: 'tools', label: 'Custom Tools', icon: 'pi-wrench' },
    { id: 'rest-api', label: 'REST API', icon: 'pi-server' },
    { id: 'live-voice', label: 'Live Voice', icon: 'pi-microphone' },
    { id: 'models', label: 'Models & Config', icon: 'pi-cog' },
    { id: 'changelog', label: 'Release Changelog', icon: 'pi-history' },
];

function scrollTo(id: string) {
    activeSection.value = id;
    const el = document.getElementById(id);
    if (el) el.scrollIntoView({ behavior: 'smooth', block: 'start' });
}

function handleScroll() {
    const container = document.querySelector('.docs-main');
    if (!container) return;
    for (const s of sections) {
        const el = document.getElementById(s.id);
        if (el) {
            const rect = el.getBoundingClientRect();
            if (rect.top <= 160) activeSection.value = s.id;
        }
    }
}

onMounted(() => {
    const main = document.querySelector('.main-content');
    if (main) main.addEventListener('scroll', handleScroll);
});

onUnmounted(() => {
    const main = document.querySelector('.main-content');
    if (main) main.removeEventListener('scroll', handleScroll);
});

const copiedId = ref('');
function copyCode(id: string) {
    const el = document.getElementById(id);
    if (el) {
        navigator.clipboard.writeText(el.textContent || '');
        copiedId.value = id;
        setTimeout(() => copiedId.value = '', 2000);
    }
}

// Provide copy function to children
import { provide } from 'vue';
provide('copyCode', copyCode);
provide('copiedId', copiedId);
</script>

<template>
    <div class="docs-layout">
        <main class="docs-main">
            <div class="docs-hero">
                <div class="hero-badge">Documentation</div>
                <h1>AiChatBox Developer Guide</h1>
                <p>Everything you need to integrate AI chat, voice, and custom tools into your application.</p>
            </div>

            <DocsGettingStarted />
            <DocsWidgetIntegration />
            <DocsToolCalls />
            <DocsRestApi />
            <DocsLiveVoice />
            <DocsModels />
            <DocsChangelog />
        </main>

        <aside class="docs-toc">
            <div class="toc-sticky">
                <h4 class="toc-title">On this page</h4>
                <nav>
                    <button
                        v-for="s in sections"
                        :key="s.id"
                        :class="['toc-item', { active: activeSection === s.id }]"
                        @click="scrollTo(s.id)"
                    >
                        <i :class="['pi', s.icon]"></i>
                        <span>{{ s.label }}</span>
                    </button>
                </nav>
            </div>
        </aside>
    </div>
</template>

<style scoped>
.docs-layout {
    display: flex;
    gap: 32px;
    max-width: 1200px;
    margin: 0 auto;
}

.docs-main {
    flex: 1;
    min-width: 0;
    padding-bottom: 120px;
}

.docs-hero {
    margin-bottom: 48px;
    padding-bottom: 32px;
    border-bottom: 1px solid var(--p-surface-200);
}

.hero-badge {
    display: inline-flex;
    align-items: center;
    padding: 4px 12px;
    background: linear-gradient(135deg, var(--p-primary-50), var(--p-primary-100));
    color: var(--p-primary-600);
    border-radius: 20px;
    font-size: 0.75rem;
    font-weight: 700;
    text-transform: uppercase;
    letter-spacing: 0.05em;
    margin-bottom: 16px;
}

.docs-hero h1 {
    font-size: 2.2rem;
    font-weight: 800;
    letter-spacing: -0.03em;
    margin-bottom: 12px;
    color: var(--p-surface-900);
}

.docs-hero p {
    color: var(--p-surface-500);
    font-size: 1.1rem;
    line-height: 1.6;
    margin: 0;
}

/* Table of Contents */
.docs-toc {
    width: 220px;
    flex-shrink: 0;
}

.toc-sticky {
    position: sticky;
    top: 24px;
}

.toc-title {
    font-size: 0.7rem;
    font-weight: 700;
    text-transform: uppercase;
    letter-spacing: 0.08em;
    color: var(--p-surface-400);
    margin-bottom: 12px;
}

.toc-item {
    display: flex;
    align-items: center;
    gap: 8px;
    width: 100%;
    padding: 8px 12px;
    background: transparent;
    border: none;
    border-left: 2px solid transparent;
    color: var(--p-surface-500);
    font-size: 0.82rem;
    font-weight: 500;
    cursor: pointer;
    transition: all 0.15s ease;
    text-align: left;
}

.toc-item:hover {
    color: var(--p-surface-800);
    background-color: var(--p-surface-50);
}

.toc-item.active {
    color: var(--p-primary-600);
    border-left-color: var(--p-primary-500);
    background-color: var(--p-primary-50);
    font-weight: 600;
}

.toc-item .pi {
    font-size: 0.85rem;
}

@media (max-width: 1100px) {
    .docs-toc { display: none; }
}
</style>

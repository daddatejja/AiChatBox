<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
import { useApi } from '../../composables/useApi';
import Card from 'primevue/card';
import Chart from 'primevue/chart';
import Message from 'primevue/message';

const { apiFetch } = useApi();

const overview = ref<any>(null);
const volumePoints = ref<any[]>([]);
const providerStats = ref<any[]>([]);
const loading = ref(false);
const error = ref('');

const loadData = async () => {
    loading.value = true;
    error.value = '';
    try {
        const res = await apiFetch('/api/admin/analytics?days=30');
        if (res.ok) {
            const data = await res.json();
            overview.value = data.overview;
            volumePoints.value = data.volume;
            providerStats.value = data.providers;
        } else {
            error.value = 'Failed to load platform analytics.';
        }
    } catch (e) {
        error.value = 'An error occurred while loading analytics.';
        console.error(e);
    } finally {
        loading.value = false;
    }
};

onMounted(() => {
    loadData();
});

const maxVolumeRequests = computed(() => Math.max(...volumePoints.value.map(v => v.requests), 1));

const pieData = computed(() => {
    return {
        labels: providerStats.value.map(p => p.provider),
        datasets: [
            {
                data: providerStats.value.map(p => p.requests),
                backgroundColor: [
                    '#6366f1', // Indigo
                    '#10b981', // Emerald
                    '#3b82f6', // Blue
                    '#f59e0b', // Amber
                    '#ef4444', // Red
                    '#8b5cf6', // Purple
                    '#ec4899', // Pink
                    '#14b8a6'  // Teal
                ]
            }
        ]
    };
});

const pieOptions = ref({
    plugins: {
        legend: {
            position: 'right',
            labels: {
                boxWidth: 12,
                font: {
                    size: 11
                }
            }
        }
    },
    responsive: true,
    maintainAspectRatio: false
});

function formatNumber(n: number): string {
    if (!n) return '0';
    if (n >= 1000000) return (n / 1000000).toFixed(1) + 'M';
    if (n >= 1000) return (n / 1000).toFixed(1) + 'K';
    return String(n);
}

function formatDate(d: string) {
    return new Date(d).toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
}
</script>

<template>
    <div class="admin-dashboard">
        <header class="header">
            <div>
                <h1>Admin Panel</h1>
                <p class="subtitle">Platform-wide statistics, health monitoring, and system metrics.</p>
            </div>
        </header>

        <Message v-if="error" severity="error" variant="simple" class="mb-4">{{ error }}</Message>

        <div v-if="loading && !overview" class="loading-state">
            <i class="pi pi-spin pi-spinner" style="font-size: 2rem"></i>
            <p>Loading platform analytics...</p>
        </div>

        <div v-else-if="overview">
            <!-- KPI Cards -->
            <div class="kpi-grid">
                <div class="kpi-card">
                    <div class="kpi-icon users"><i class="pi pi-id-card"></i></div>
                    <div class="kpi-body">
                        <span class="kpi-value">{{ formatNumber(overview.totalUsers) }}</span>
                        <span class="kpi-label">Total Users</span>
                    </div>
                </div>
                <div class="kpi-card">
                    <div class="kpi-icon partners"><i class="pi pi-building"></i></div>
                    <div class="kpi-body">
                        <span class="kpi-value">{{ formatNumber(overview.totalPartners) }}</span>
                        <span class="kpi-label">B2B Partners</span>
                    </div>
                </div>
                <div class="kpi-card">
                    <div class="kpi-icon projects"><i class="pi pi-folder"></i></div>
                    <div class="kpi-body">
                        <span class="kpi-value">{{ formatNumber(overview.totalProjects) }}</span>
                        <span class="kpi-label">Total Chatbots</span>
                    </div>
                </div>
                <div class="kpi-card">
                    <div class="kpi-icon requests"><i class="pi pi-share-alt"></i></div>
                    <div class="kpi-body">
                        <span class="kpi-value">{{ formatNumber(overview.totalRequests) }}</span>
                        <span class="kpi-label">Total API Calls</span>
                    </div>
                </div>
                <div class="kpi-card">
                    <div class="kpi-icon tokens"><i class="pi pi-bolt"></i></div>
                    <div class="kpi-body">
                        <span class="kpi-value">{{ formatNumber(overview.totalTokens) }}</span>
                        <span class="kpi-label">Tokens Processed</span>
                    </div>
                </div>
                <div class="kpi-card">
                    <div class="kpi-icon speed"><i class="pi pi-clock"></i></div>
                    <div class="kpi-body">
                        <span class="kpi-value">{{ overview.avgResponseMs }}ms</span>
                        <span class="kpi-label">Average Latency</span>
                    </div>
                </div>
                <div class="kpi-card" :class="{ warning: overview.errorRate > 5 }">
                    <div class="kpi-icon errors"><i class="pi pi-exclamation-triangle"></i></div>
                    <div class="kpi-body">
                        <span class="kpi-value">{{ overview.errorRate }}%</span>
                        <span class="kpi-label">Platform Error Rate</span>
                    </div>
                </div>
            </div>

            <!-- Charts Grid -->
            <div class="grid-2">
                <!-- Volume Chart -->
                <Card class="chart-card">
                    <template #title><span class="card-title">Daily Request Volume (Last 30 Days)</span></template>
                    <template #content>
                        <div class="bar-chart" v-if="volumePoints.length">
                            <div class="bar-row" v-for="v in volumePoints" :key="v.date">
                                <span class="bar-date">{{ formatDate(v.date) }}</span>
                                <div class="bar-track">
                                    <div class="bar-fill" :style="{ width: (v.requests / maxVolumeRequests * 100) + '%' }">
                                        <span class="bar-label" v-if="v.requests > 0">{{ v.requests }}</span>
                                    </div>
                                </div>
                                <span class="bar-sessions">{{ v.sessions }}s</span>
                            </div>
                        </div>
                        <div v-else class="empty-chart">No request logs recorded in this period.</div>
                    </template>
                </Card>

                <!-- Provider Distribution Chart -->
                <Card class="chart-card">
                    <template #title><span class="card-title">AI Provider Distribution</span></template>
                    <template #content>
                        <div class="pie-container" v-if="providerStats.length">
                            <Chart type="pie" :data="pieData" :options="pieOptions" class="pie-chart" />
                        </div>
                        <div v-else class="empty-chart">No model request logs available.</div>
                    </template>
                </Card>
            </div>
        </div>
    </div>
</template>

<style scoped>
.admin-dashboard {
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
.loading-state {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    padding: 64px 0;
    color: var(--p-surface-400);
}
.loading-state p {
    margin-top: 12px;
}

/* KPI Grid */
.kpi-grid {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
    gap: 16px;
    margin-bottom: 32px;
}
.kpi-card {
    background: var(--p-surface-0);
    border: 1px solid var(--p-surface-200);
    border-radius: 12px;
    padding: 20px;
    display: flex;
    align-items: center;
    gap: 16px;
    transition: transform 0.15s, box-shadow 0.15s;
}
.kpi-card:hover {
    transform: translateY(-2px);
    box-shadow: 0 4px 12px rgba(0,0,0,0.05);
}
.kpi-icon {
    width: 44px;
    height: 44px;
    border-radius: 10px;
    display: flex;
    align-items: center;
    justify-content: center;
    font-size: 1.2rem;
    flex-shrink: 0;
}
.kpi-icon.users { background: color-mix(in srgb, var(--p-cyan-500) 12%, transparent); color: var(--p-cyan-600); }
.kpi-icon.partners { background: color-mix(in srgb, var(--p-emerald-500) 12%, transparent); color: var(--p-emerald-600); }
.kpi-icon.projects { background: color-mix(in srgb, var(--p-indigo-500) 12%, transparent); color: var(--p-indigo-600); }
.kpi-icon.requests { background: color-mix(in srgb, var(--p-blue-500) 12%, transparent); color: var(--p-blue-600); }
.kpi-icon.tokens { background: color-mix(in srgb, var(--p-purple-500) 12%, transparent); color: var(--p-purple-600); }
.kpi-icon.speed { background: color-mix(in srgb, var(--p-orange-500) 12%, transparent); color: var(--p-orange-600); }
.kpi-icon.errors { background: color-mix(in srgb, var(--p-red-500) 12%, transparent); color: var(--p-red-600); }

.kpi-card.warning {
    border-color: var(--p-red-300);
    background-color: color-mix(in srgb, var(--p-red-50) 40%, transparent);
}

.kpi-body {
    display: flex;
    flex-direction: column;
    min-width: 0;
}
.kpi-value {
    font-size: 1.4rem;
    font-weight: 700;
    color: var(--p-surface-900);
    line-height: 1.2;
}
.kpi-label {
    font-size: 0.75rem;
    color: var(--p-surface-500);
    margin-top: 2px;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
}

/* Charts */
.grid-2 {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 24px;
}
.chart-card {
    background: var(--p-surface-0);
    border: 1px solid var(--p-surface-200);
}
.card-title {
    font-size: 1rem;
    font-weight: 600;
}
.bar-chart {
    max-height: 320px;
    overflow-y: auto;
}
.bar-row {
    display: flex;
    align-items: center;
    gap: 12px;
    padding: 4px 0;
}
.bar-date {
    font-size: 0.7rem;
    color: var(--p-surface-500);
    width: 50px;
    text-align: right;
    flex-shrink: 0;
}
.bar-track {
    flex: 1;
    background: var(--p-surface-100);
    border-radius: 4px;
    height: 20px;
    overflow: hidden;
}
.bar-fill {
    background: linear-gradient(90deg, var(--p-primary-400), var(--p-primary-600));
    height: 100%;
    border-radius: 4px;
    min-width: 0;
    display: flex;
    align-items: center;
    justify-content: flex-end;
    padding-right: 6px;
    transition: width 0.3s ease;
}
.bar-label {
    font-size: 0.65rem;
    color: white;
    font-weight: 600;
}
.bar-sessions {
    font-size: 0.7rem;
    color: var(--p-surface-400);
    width: 30px;
    flex-shrink: 0;
}
.empty-chart {
    text-align: center;
    padding: 48px;
    color: var(--p-surface-400);
    font-size: 0.9rem;
}
.pie-container {
    height: 320px;
    display: flex;
    align-items: center;
    justify-content: center;
}
.pie-chart {
    height: 100%;
    width: 100%;
}

@media (max-width: 1024px) {
    .grid-2 {
        grid-template-columns: 1fr;
    }
}
</style>

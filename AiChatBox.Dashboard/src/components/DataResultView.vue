<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import DataTable from 'primevue/datatable';
import Column from 'primevue/column';
import Chart from 'primevue/chart';
import Button from 'primevue/button';
import Tag from 'primevue/tag';
import SelectButton from 'primevue/selectbutton';

const props = defineProps<{
    result: {
        data: any[];
        sql: string;
        rowCount: number;
    };
    title?: string;
}>();

const viewMode = ref('table');
const viewOptions = ref([
    { label: 'Table', value: 'table', icon: 'pi pi-table' },
    { label: 'Chart', value: 'chart', icon: 'pi pi-chart-bar' }
]);

const columns = computed(() => {
    if (!props.result.data || props.result.data.length === 0) return [];
    return Object.keys(props.result.data[0]).map(key => ({
        field: key,
        header: key.charAt(0).toUpperCase() + key.slice(1).replace(/_/g, ' ')
    }));
});

// Chart logic
const chartType = ref('bar');
const chartTypes = [
    { label: 'Bar', value: 'bar' },
    { label: 'Line', value: 'line' },
    { label: 'Pie', value: 'pie' }
];

const chartData = computed(() => {
    if (!props.result.data || props.result.data.length === 0) return null;

    const labels = props.result.data.map(row => {
        // Try to find a sensible label (first string column or first column)
        const entries = Object.entries(row);
        const stringCol = entries.find(([_, v]) => typeof v === 'string');
        return stringCol ? stringCol[1] : entries[0][1];
    });

    const datasets = [];
    const numericCols = Object.keys(props.result.data[0]).filter(key => {
        const val = props.result.data[0][key];
        return typeof val === 'number';
    });

    const colors = ['#6366f1', '#10b981', '#f59e0b', '#ef4444', '#8b5cf6', '#ec4899'];

    numericCols.forEach((col, index) => {
        datasets.push({
            label: col,
            data: props.result.data.map(row => row[col]),
            backgroundColor: chartType.value === 'pie' ? colors : colors[index % colors.length],
            borderColor: colors[index % colors.length],
            borderWidth: 1
        });
    });

    return {
        labels,
        datasets
    };
});

const chartOptions = computed(() => {
    return {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
            legend: {
                position: 'bottom'
            }
        },
        scales: chartType.value === 'pie' ? {} : {
            y: {
                beginAtZero: true
            }
        }
    };
});

const isExporting = ref(false);

const exportData = async (format: 'pdf' | 'excel') => {
    isExporting.value = true;
    try {
        const response = await fetch(`/api/export/${format}`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                data: props.result.data,
                title: props.title || 'Database Report',
                fileName: `report_${new Date().getTime()}`
            })
        });

        if (response.ok) {
            const blob = await response.blob();
            const url = window.URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = `report_${new Date().getTime()}.${format === 'excel' ? 'xlsx' : 'pdf'}`;
            document.body.appendChild(a);
            a.click();
            a.remove();
        }
    } catch (err) {
        console.error('Export failed', err);
    } finally {
        isExporting.value = false;
    }
};

onMounted(() => {
    // If we have numeric data and few rows, default to chart
    if (props.result.data?.length > 0 && props.result.data.length < 20) {
        const hasNumeric = Object.values(props.result.data[0]).some(v => typeof v === 'number');
        if (hasNumeric) {
            // viewMode.value = 'chart';
        }
    }
});
</script>

<template>
    <div class="data-result-view card p-4 shadow-sm border rounded-xl bg-white dark:bg-gray-900 mb-4">
        <div class="flex flex-wrap justify-between items-center mb-4 gap-4">
            <div class="flex items-center gap-3">
                <i class="pi pi-database text-primary text-xl"></i>
                <h3 class="text-lg font-semibold m-0">{{ title || 'Query Result' }}</h3>
                <Tag :value="`${result.rowCount} rows`" severity="info" rounded />
            </div>
            
            <div class="flex items-center gap-2">
                <SelectButton v-model="viewMode" :options="viewOptions" optionLabel="label" optionValue="value" aria-labelledby="basic">
                    <template #option="slotProps">
                        <i :class="slotProps.option.icon" class="mr-2"></i>
                        <span>{{ slotProps.option.label }}</span>
                    </template>
                </SelectButton>

                <div class="h-8 w-px bg-gray-200 dark:bg-gray-700 mx-2"></div>

                <Button icon="pi pi-file-excel" severity="success" text rounded v-tooltip.top="'Export to Excel'" @click="exportData('excel')" :loading="isExporting" />
                <Button icon="pi pi-file-pdf" severity="danger" text rounded v-tooltip.top="'Export to PDF'" @click="exportData('pdf')" :loading="isExporting" />
            </div>
        </div>

        <div v-if="viewMode === 'table'" class="overflow-x-auto">
            <DataTable :value="result.data" stripedRows paginator :rows="10" size="small" class="p-datatable-sm">
                <Column v-for="col in columns" :key="col.field" :field="col.field" :header="col.header" sortable />
            </DataTable>
        </div>

        <div v-else class="chart-container relative" style="height: 400px">
            <div class="flex justify-end mb-2 gap-2">
                <SelectButton v-model="chartType" :options="chartTypes" optionLabel="label" optionValue="value" size="small" />
            </div>
            <Chart v-if="chartData" :type="chartType" :data="chartData" :options="chartOptions" class="h-full w-full" />
            <div v-else class="flex flex-col items-center justify-center h-full text-gray-500">
                <i class="pi pi-chart-line text-4xl mb-2"></i>
                <p>No numeric data available for charting</p>
            </div>
        </div>

        <div class="mt-4 pt-3 border-t border-gray-100 dark:border-gray-800">
            <details class="cursor-pointer">
                <summary class="text-xs text-gray-500 hover:text-primary transition-colors">Show SQL Query</summary>
                <pre class="mt-2 p-3 bg-gray-50 dark:bg-gray-800 rounded text-xs overflow-x-auto font-mono text-primary">{{ result.sql }}</pre>
            </details>
        </div>
    </div>
</template>

<style scoped>
.data-result-view :deep(.p-datatable-thead > tr > th) {
    background: transparent;
    font-weight: 600;
}

.data-result-view :deep(.p-selectbutton .p-button) {
    padding: 0.5rem 0.75rem;
    font-size: 0.875rem;
}
</style>

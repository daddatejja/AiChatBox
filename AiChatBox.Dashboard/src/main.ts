import { createApp } from 'vue'
import './style.css'
import App from './App.vue'
import router from './router'
import PrimeVue from 'primevue/config'
import Aura from '@primeuix/themes/aura'
import { definePreset } from '@primeuix/themes'
import Tooltip from 'primevue/tooltip'
import ToastService from 'primevue/toastservice'
import ConfirmationService from 'primevue/confirmationservice'

// Microsoft Clarity
const clarityId = import.meta.env.VITE_CLARITY_ID;
if (clarityId) {
    (function (c: any, l: any, a: any, r: any, i: any) {
        c[a] = c[a] || function () { (c[a].q = c[a].q || []).push(arguments) };
        const t = l.createElement(r); t.async = 1; t.src = "https://www.clarity.ms/tag/" + i;
        const y = l.getElementsByTagName(r)[0]; y.parentNode.insertBefore(t, y);
    })(window, document, "clarity", "script", clarityId);
}

const TerminalPrime = definePreset(Aura, {
    semantic: {
        primary: {
            50: '#eef2ff',
            100: '#e0e7ff',
            200: '#c7d2fe',
            300: '#a5b4fc',
            400: '#818cf8',
            500: '#6366f1',
            600: '#4f46e5',
            700: '#4338ca',
            800: '#3730a3',
            900: '#312e81',
            950: '#1e1b4b'
        },
        colorScheme: {
            light: {
                surface: {
                    0: '#ffffff',
                    50: '#f9fafb',
                    100: '#f3f4f6',
                    200: '#e5e7eb',
                    300: '#d1d5db',
                    400: '#9ca3af',
                    500: '#6b7280',
                    600: '#4b5563',
                    700: '#374151',
                    800: '#1f2937',
                    900: '#111827',
                    950: '#030712'
                }
            },
            dark: {
                surface: {
                    0: '#0b1326',
                    50: '#111827',
                    100: '#1f2937',
                    200: '#374151',
                    300: '#4b5563',
                    400: '#6b7280',
                    500: '#9ca3af',
                    600: '#d1d5db',
                    700: '#e5e7eb',
                    800: '#f3f4f6',
                    900: '#f9fafb',
                    950: '#ffffff'
                }
            }
        }
    }
});

const app = createApp(App)

app.use(router)
app.use(PrimeVue, {
    theme: {
        preset: TerminalPrime,
        options: {
            darkModeSelector: '[data-theme="dark"]'
        }
    }
})

app.use(ToastService)
app.use(ConfirmationService)
app.directive('tooltip', Tooltip)

app.mount('#app')

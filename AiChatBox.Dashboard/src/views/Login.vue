<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { useRouter, useRoute } from 'vue-router';
import { useApi } from '../composables/useApi';
import InputText from 'primevue/inputtext';
import Password from 'primevue/password';
import Button from 'primevue/button';
import Message from 'primevue/message';

const router = useRouter();
const route = useRoute();
const { apiFetch, API_BASE } = useApi();

const email = ref('');
const password = ref('');
const loading = ref(false);
const errorMsg = ref('');

onMounted(() => {
    // Check if we just returned from an OAuth callback
    const token = route.query.token as string;
    if (token) {
        localStorage.setItem('acb_token', token);
        // OAuth login doesn't currently return username in query, default to User
        localStorage.setItem('acb_username', 'User'); 
        
        // Remove token from URL and redirect
        router.replace('/');
    }
});

const login = async () => {
    loading.value = true;
    errorMsg.value = '';
    
    try {
        const res = await apiFetch('/api/auth/login', {
            method: 'POST',
            body: JSON.stringify({ email: email.value, password: password.value })
        });
        
        if (res.ok) {
            const data = await res.json();
            localStorage.setItem('acb_token', data.token);
            localStorage.setItem('acb_username', data.username);
            router.push('/');
        } else {
            errorMsg.value = 'Invalid email or password';
        }
    } catch (err) {
        console.error(err);
        errorMsg.value = 'Failed to connect to the server';
    } finally {
        loading.value = false;
    }
};

const oauthLogin = (provider: string) => {
    window.location.href = `${API_BASE}/api/auth/external-login/${provider}`;
};
</script>

<template>
    <div class="login-container">
        <div class="login-box">
            <div class="logo">
                <div class="logo-icon"></div>
                <h2>AiChatBox</h2>
            </div>
            
            <p class="subtitle">Sign in to your account</p>
            
            <form @submit.prevent="login" class="login-form">
                <Message v-if="errorMsg" severity="error" :closable="false" class="mb-4">{{ errorMsg }}</Message>
                
                <div class="form-group">
                    <label for="email">Email</label>
                    <InputText id="email" v-model="email" type="email" placeholder="name@example.com" required fluid />
                </div>
                
                <div class="form-group">
                    <label for="password">Password</label>
                    <Password id="password" v-model="password" :feedback="false" toggleMask required fluid />
                </div>
                
                <Button type="submit" label="Sign In" :loading="loading" class="submit-btn" />
            </form>
            
            <div class="divider">
                <span>OR</span>
            </div>

            <div class="oauth-buttons">
                <Button label="Sign in with Google" icon="pi pi-google" severity="secondary" outlined fluid @click="oauthLogin('Google')" />
                <Button label="Sign in with GitHub" icon="pi pi-github" severity="secondary" outlined fluid @click="oauthLogin('GitHub')" />
            </div>
            
            <div class="links">
                <router-link to="/docs" class="link">View Documentation</router-link>
            </div>
        </div>
    </div>
</template>

<style scoped>
.login-container {
    display: flex;
    justify-content: center;
    align-items: center;
    min-height: 100vh;
    background-color: var(--p-surface-50);
    padding: 24px;
}

.login-box {
    background-color: var(--p-surface-0);
    border: 1px solid var(--p-surface-200);
    border-radius: 12px;
    padding: 48px;
    width: 100%;
    max-width: 440px;
    box-shadow: 0 10px 25px rgba(0, 0, 0, 0.05);
}

.logo {
    display: flex;
    align-items: center;
    justify-content: center;
    gap: 12px;
    margin-bottom: 8px;
}

.logo h2 {
    color: var(--p-surface-900);
    margin: 0;
    font-size: 1.5rem;
}

.logo-icon {
    width: 28px;
    height: 28px;
    background-color: var(--p-primary-500);
    border-radius: 6px;
}

.subtitle {
    text-align: center;
    color: var(--p-surface-500);
    margin-bottom: 32px;
    font-size: 0.95rem;
}

.login-form {
    display: flex;
    flex-direction: column;
    gap: 20px;
}

.form-group {
    display: flex;
    flex-direction: column;
    gap: 8px;
}

.form-group label {
    font-weight: 500;
    font-size: 0.9rem;
    color: var(--p-surface-700);
}

.submit-btn {
    margin-top: 12px;
}

.mb-4 {
    margin-bottom: 16px;
}

:deep(.p-password-input) {
    width: 100%;
}

.links {
    margin-top: 24px;
    text-align: center;
}

.link {
    color: var(--p-primary-500);
    text-decoration: none;
    font-size: 0.85rem;
    font-weight: 500;
}

.link:hover {
    text-decoration: underline;
}

.divider {
    display: flex;
    align-items: center;
    text-align: center;
    margin: 24px 0;
    color: var(--p-surface-400);
    font-size: 0.85rem;
}

.divider::before,
.divider::after {
    content: '';
    flex: 1;
    border-bottom: 1px solid var(--p-surface-200);
}

.divider span {
    padding: 0 16px;
}

.oauth-buttons {
    display: flex;
    flex-direction: column;
    gap: 12px;
}
</style>

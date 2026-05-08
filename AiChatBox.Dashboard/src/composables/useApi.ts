export function useApi() {
    // Determine the backend API URL. If running in dev mode, default to the Blazor API port
    // In production, it might be the same origin.
    const API_BASE = import.meta.env.DEV ? 'https://localhost:44385' : window.location.origin;

    function getToken() {
        return localStorage.getItem('acb_token');
    }

    async function apiFetch(path: string, options: RequestInit = {}) {
        const token = getToken();
        const headers: Record<string, string> = {
            ...(options.headers as Record<string, string> || {}),
        };
        
        if (token) {
            headers['Authorization'] = `Bearer ${token}`;
        }
        
        if (options.body && !(options.body instanceof FormData)) {
            headers['Content-Type'] = 'application/json';
        }

        try {
            const res = await fetch(`${API_BASE}${path}`, { ...options, headers });
            if (res.status === 401) {
                localStorage.removeItem('acb_token');
                window.location.href = '/login'; 
            }
            return res;
        } catch (error) {
            console.error("API Fetch Error:", error);
            throw error;
        }
    }

    return { apiFetch, getToken, API_BASE };
}

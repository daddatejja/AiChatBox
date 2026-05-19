export function useApi() {
  const API_BASE = import.meta.env.DEV
    ? "https://localhost:44385"
    : import.meta.env.VITE_API_URL || window.location.origin;

  function getToken() {
    return localStorage.getItem("acb_token");
  }

  async function apiFetch(path: string, options: RequestInit = {}) {
    const token = getToken();
    const headers: Record<string, string> = {
      ...((options.headers as Record<string, string>) || {}),
    };

    if (token) {
      headers["Authorization"] = `Bearer ${token}`;
    }

    if (options.body && !(options.body instanceof FormData)) {
      headers["Content-Type"] = "application/json";
    }

    try {
      const res = await fetch(`${API_BASE}${path}`, { ...options, headers });
      if (res.status === 401) {
        localStorage.removeItem("acb_token");
        window.location.href = "/login";
      }
      return res;
    } catch (error) {
      console.error("API Fetch Error:", error);
      throw error;
    }
  }

  return { apiFetch, getToken, API_BASE };
}

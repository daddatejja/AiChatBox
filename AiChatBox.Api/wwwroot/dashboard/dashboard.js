const API_BASE = window.location.origin;

// Theme Management
const initTheme = () => {
    const savedTheme = localStorage.getItem('theme') || 'dark';
    document.documentElement.setAttribute('data-theme', savedTheme);
};

window.toggleTheme = () => {
    const currentTheme = document.documentElement.getAttribute('data-theme');
    const newTheme = currentTheme === 'dark' ? 'light' : 'dark';
    document.documentElement.setAttribute('data-theme', newTheme);
    localStorage.setItem('theme', newTheme);
};

initTheme();

// Handle token from URL (OAuth redirect)
{
    const urlParams = new URLSearchParams(window.location.search);
    const urlToken = urlParams.get('token');
    if (urlToken) {
        localStorage.setItem('acb_token', urlToken);
        // Clean up URL
        window.history.replaceState({}, document.title, window.location.pathname);
    }
}

const token = localStorage.getItem('acb_token');

if (!token && !window.location.pathname.includes('login.html')) {
    window.location.href = 'login.html';
}

document.addEventListener('DOMContentLoaded', () => {
    loadUser();
    if (document.getElementById('projects-grid')) {
        loadProjects();
    }

    const createBtn = document.getElementById('btn-create-project');
    if (createBtn) {
        createBtn.onclick = () => openModal('modal-create');
    }

    const createForm = document.getElementById('form-create-project');
    if (createForm) {
        createForm.onsubmit = handleCreateProject;
    }

    const logoutBtn = document.getElementById('btn-logout');
    if (logoutBtn) {
        logoutBtn.onclick = () => {
            localStorage.removeItem('acb_token');
            window.location.href = 'login.html';
        };
    }
});

async function loadUser() {
    const userNameEl = document.getElementById('user-name');
    if (!userNameEl) return;

    try {
        // Try to get user info from API if not in local storage
        const res = await fetch(`${API_BASE}/api/auth/me`, {
            headers: { 'Authorization': `Bearer ${token}` }
        });
        if (res.ok) {
            const user = await res.json();
            userNameEl.innerText = user.email.split('@')[0];
        }
    } catch (err) {
        userNameEl.innerText = 'Developer';
    }
}

async function loadProjects() {
    try {
        const response = await fetch(`${API_BASE}/api/project`, {
            headers: { 'Authorization': `Bearer ${token}` }
        });
        if (response.status === 401) window.location.href = 'login.html';
        
        const projects = await response.json();
        const grid = document.getElementById('projects-grid');
        grid.innerHTML = projects.map(p => `
            <div class="card animate-in">
                <div class="section-header" style="margin-bottom: 1rem;">
                    <h3 style="font-size: 1.1rem; color: var(--text-primary);">${p.name}</h3>
                    <span style="font-size: 0.7rem; font-weight: 700; color: var(--primary-indigo); background: var(--bg-surface-high); padding: 4px 10px; border-radius: 20px; text-transform: uppercase;">${p.provider}</span>
                </div>
                <p style="color: var(--text-secondary); font-size: 0.85rem; margin-bottom: 1.5rem; height: 2.6rem; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden;">
                    ${p.systemPrompt || 'No system prompt defined.'}
                </p>
                <div style="display: flex; gap: 1rem; font-size: 0.8rem; color: var(--text-secondary); margin-bottom: 1.5rem;">
                    <div style="display: flex; align-items: center; gap: 0.4rem;">
                        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="3" y="11" width="18" height="11" rx="2" ry="2"></rect><path d="M7 11V7a5 5 0 0 1 10 0v4"></path></svg>
                        ${p.apiKeyCount} Keys
                    </div>
                    <div style="display: flex; align-items: center; gap: 0.4rem;">
                        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="3" y="4" width="18" height="18" rx="2" ry="2"></rect><line x1="16" y1="2" x2="16" y2="6"></line><line x1="8" y1="2" x2="8" y2="6"></line><line x1="3" y1="10" x2="21" y2="10"></line></svg>
                        ${new Date(p.createdAt).toLocaleDateString()}
                    </div>
                </div>
                <div style="display: flex; gap: 0.75rem;">
                    <a href="project.html?id=${p.id}" class="btn btn-outline" style="flex: 1; font-size: 0.85rem;">
                        Manage
                    </a>
                    <button onclick="deleteProject('${p.id}')" class="btn btn-outline" style="color: var(--danger); padding: 0.6rem;">
                        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="3 6 5 6 21 6"></polyline><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"></path><line x1="10" y1="11" x2="10" y2="17"></line><line x1="14" y1="11" x2="14" y2="17"></line></svg>
                    </button>
                </div>
            </div>
        `).join('');
    } catch (err) {
        console.error('Failed to load projects', err);
    }
}

async function handleCreateProject(e) {
    e.preventDefault();
    const name = document.getElementById('proj-name').value;
    const provider = document.getElementById('proj-provider').value;
    const systemPrompt = document.getElementById('proj-prompt').value;

    try {
        const response = await fetch(`${API_BASE}/api/project`, {
            method: 'POST',
            headers: { 
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${token}`
            },
            body: JSON.stringify({ name, provider, systemPrompt })
        });

        if (response.ok) {
            closeModal('modal-create');
            loadProjects();
        }
    } catch (err) {
        console.error('Failed to create project', err);
    }
}

async function deleteProject(id) {
    if (!confirm('Are you sure you want to delete this project? All associated API keys and data will be lost.')) return;

    try {
        const response = await fetch(`${API_BASE}/api/project/${id}`, {
            method: 'DELETE',
            headers: { 'Authorization': `Bearer ${token}` }
        });
        if (response.ok) loadProjects();
    } catch (err) {
        console.error('Failed to delete project', err);
    }
}

// Modal helpers
function openModal(id) {
    document.getElementById(id).classList.add('active');
}

function closeModal(id) {
    document.getElementById(id).classList.remove('active');
}

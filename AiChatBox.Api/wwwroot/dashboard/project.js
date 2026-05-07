const projUrlParams = new URLSearchParams(window.location.search);
const projectId = projUrlParams.get('id');

if (!projectId) window.location.href = 'index.html';

document.addEventListener('DOMContentLoaded', () => {
    loadProjectDetails();
    loadApiKeys();
    loadTools();

    document.getElementById('btn-save-config').onclick = saveConfig;
    document.getElementById('btn-create-key').onclick = createApiKey;
});

async function loadProjectDetails() {
    const res = await fetch(`${API_BASE}/api/project/${projectId}`, {
        headers: { 'Authorization': `Bearer ${token}` }
    });
    const p = await res.json();
    
    document.getElementById('project-name-title').innerText = p.name;
    document.getElementById('edit-provider').value = p.provider;
    document.getElementById('edit-model').value = p.modelName;
    document.getElementById('edit-prompt').value = p.systemPrompt;
    document.getElementById('edit-webhook-url').value = p.webhookUrl || '';
}

async function saveConfig() {
    const btn = document.getElementById('btn-save-config');
    const originalHtml = btn.innerHTML;
    btn.innerHTML = 'Saving...';
    
    const payload = {
        name: document.getElementById('project-name-title').innerText,
        provider: document.getElementById('edit-provider').value,
        modelName: document.getElementById('edit-model').value,
        systemPrompt: document.getElementById('edit-prompt').value,
        webhookUrl: document.getElementById('edit-webhook-url').value,
        webhookSecret: document.getElementById('edit-webhook-secret').value
    };

    try {
        await fetch(`${API_BASE}/api/project/${projectId}`, {
            method: 'PUT',
            headers: { 
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${token}`
            },
            body: JSON.stringify(payload)
        });
        btn.innerHTML = 'Saved!';
        setTimeout(() => btn.innerHTML = originalHtml, 2000);
    } catch (err) {
        console.error(err);
        btn.innerHTML = 'Error';
        setTimeout(() => btn.innerHTML = originalHtml, 2000);
    }
}

async function loadApiKeys() {
    const res = await fetch(`${API_BASE}/api/project/${projectId}/keys`, {
        headers: { 'Authorization': `Bearer ${token}` }
    });
    const keys = await res.json();
    const list = document.getElementById('keys-list');
    
    list.innerHTML = keys.length ? keys.map(k => `
        <div class="card" style="display: flex; align-items: center; justify-content: space-between; padding: 1rem 1.5rem;">
            <div style="display: flex; flex-direction: column; gap: 0.25rem;">
                <span style="font-weight: 700; font-size: 0.85rem; color: var(--primary-indigo); text-transform: uppercase;">${k.label || 'API Key'}</span>
                <span style="font-family: var(--font-mono); font-size: 0.9rem; opacity: 0.5;">acb_••••••••••••••••••••••••</span>
            </div>
            <div style="display: flex; align-items: center; gap: 1.5rem;">
                <span style="font-size: 0.8rem; color: var(--text-secondary)">Created ${new Date(k.createdAt).toLocaleDateString()}</span>
                <button onclick="deleteKey('${k.id}')" class="btn btn-outline btn-sm" style="color: var(--danger); border-color: var(--danger)">Revoke</button>
            </div>
        </div>
    `).join('') : '<p style="color: var(--text-secondary)">No API access keys generated yet.</p>';
}

async function createApiKey() {
    const label = prompt('Key Label (e.g. Production, Website)');
    if (!label) return;

    const res = await fetch(`${API_BASE}/api/project/${projectId}/keys`, {
        method: 'POST',
        headers: { 
            'Content-Type': 'application/json',
            'Authorization': `Bearer ${token}`
        },
        body: JSON.stringify(label)
    });

    if (res.ok) {
        const data = await res.json();
        document.getElementById('new-key-value').innerText = data.key;
        openModal('modal-key');
        loadApiKeys();
        
        document.getElementById('btn-copy-key').onclick = () => {
            navigator.clipboard.writeText(data.key);
            document.getElementById('btn-copy-key').innerText = 'Copied!';
        };
    }
}

async function deleteKey(id) {
    if (!confirm('Are you sure you want to revoke this key?')) return;
    await fetch(`${API_BASE}/api/project/keys/${id}`, {
        method: 'DELETE',
        headers: { 'Authorization': `Bearer ${token}` }
    });
    loadApiKeys();
}

async function loadTools() {
    const res = await fetch(`${API_BASE}/api/tool/project/${projectId}`, {
        headers: { 'Authorization': `Bearer ${token}` }
    });
    const tools = await res.json();
    const list = document.getElementById('tools-list');
    
    list.innerHTML = tools.length ? tools.map(t => `
        <div class="card" style="margin-bottom: 1rem;">
            <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 1rem;">
                <h3 style="color: var(--electric-blue); font-family: var(--font-mono); font-size: 1rem;">${t.name}</h3>
                <button onclick="deleteTool('${t.id}')" class="btn btn-outline btn-sm" style="color: var(--danger)">Remove</button>
            </div>
            <p style="color: var(--text-secondary); font-size: 0.9rem; margin-bottom: 1rem;">${t.description}</p>
            <div class="code-block">${t.parametersJsonSchema}</div>
        </div>
    `).join('') : '<p style="color: var(--text-secondary)">No custom agent tools configured.</p>';
}

document.getElementById('btn-add-tool').onclick = async () => {
    const name = prompt('Tool Name (e.g. get_weather)');
    const description = prompt('Tool Description');
    if (!name) return;

    await fetch(`${API_BASE}/api/tool/project/${projectId}`, {
        method: 'POST',
        headers: { 
            'Content-Type': 'application/json',
            'Authorization': `Bearer ${token}`
        },
        body: JSON.stringify({ 
            name, 
            description, 
            parametersJsonSchema: '{"type": "object", "properties": {}}' 
        })
    });
    loadTools();
};

async function deleteTool(id) {
    await fetch(`${API_BASE}/api/tool/${id}`, {
        method: 'DELETE',
        headers: { 'Authorization': `Bearer ${token}` }
    });
    loadTools();
}

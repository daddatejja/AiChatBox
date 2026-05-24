export function getSessionStorageKey(chatbox) {
  const projectId = chatbox.projectId || chatbox.getAttribute("project-id");
  return projectId ? `ai_chat_session_id_${projectId}` : `ai_chat_session_id`;
}

export function getHeaders(chatbox) {
  const headers = { "X-User-Id": chatbox.userId };
  if (chatbox.apiKey) headers["X-Api-Key"] = chatbox.apiKey;
  if (chatbox.authToken) headers["Authorization"] = `Bearer ${chatbox.authToken}`;
  return headers;
}

export function getPageContext() {
  return {
    url: window.location.href,
    title: document.title,
    path: window.location.pathname
  };
}

export async function safeJson(response) {
  const contentType = response.headers.get("content-type");
  if (contentType && contentType.includes("application/json")) {
    return await response.json();
  }
  const text = await response.text();
  throw new Error(`Expected JSON but got ${contentType || 'unknown'}. Content: ${text.substring(0, 100)}...`);
}

export async function fetchConfig(chatbox) {
  if (!chatbox.apiKey) return;
  try {
    const response = await fetch(`${chatbox.apiUrl}/api/chat/config`, {
      headers: getHeaders(chatbox)
    });
    if (response.ok) {
      chatbox.config = await safeJson(response);
      if (chatbox.config.projectId) chatbox.projectId = chatbox.config.projectId;
      if (chatbox.config.defaultModel) chatbox.modelName = chatbox.config.defaultModel;
      if (chatbox.config.defaultProvider) chatbox.provider = chatbox.config.defaultProvider;
      if (chatbox.config.suggestions && Array.isArray(chatbox.config.suggestions) && chatbox.config.suggestions.length > 0) {
        chatbox.suggestions = chatbox.config.suggestions;
      }
      if (chatbox.config.theme) {
        chatbox.applyTheme(chatbox.config.theme);
      }
    }
  } catch (err) {
    console.error("Failed to fetch widget config:", err);
  }
}

export async function fetchCommands(chatbox) {
  if (!chatbox.projectId) return;
  try {
    const response = await fetch(`${chatbox.apiUrl}/api/rules/project/${chatbox.projectId}/commands`, {
      headers: getHeaders(chatbox)
    });
    if (response.ok) {
      chatbox.commands = await safeJson(response);
      console.log("Loaded commands:", chatbox.commands);
    }
  } catch (err) {
    console.error("Failed to fetch commands:", err);
  }
}

export async function uploadFile(chatbox, file) {
  const formData = new FormData();
  formData.append("file", file);
  const resp = await fetch(`${chatbox.apiUrl}/api/File/upload`, {
    method: "POST",
    headers: getHeaders(chatbox),
    body: formData,
  });
  if (!resp.ok) throw new Error("Upload failed");
  return await safeJson(resp);
}

export async function exportData(chatbox, format, data) {
  try {
    const rows = data.data || data.rows || (Array.isArray(data) ? data : []);
    
    const response = await fetch(`${chatbox.apiUrl}/api/export/${format}`, {
      method: 'POST',
      headers: { ...getHeaders(chatbox), 'Content-Type': 'application/json' },
      body: JSON.stringify({
        data: rows,
        title: "Data Report",
        fileName: `report_${new Date().getTime()}`
      })
    });
    if (response.ok) {
      const blob = await response.blob();
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `report.${format === 'excel' ? 'xlsx' : 'pdf'}`;
      document.body.appendChild(a);
      a.click();
      a.remove();
    } else {
      const error = await response.text();
      console.error('Export failed:', error);
    }
  } catch (err) {
    console.error('Export failed:', err);
  }
}

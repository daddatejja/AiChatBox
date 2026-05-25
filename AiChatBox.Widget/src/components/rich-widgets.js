import { icons } from '../icons.js';
import { getHeaders, safeJson, exportData } from '../services/api.js';

export function renderRichResponse(chatbox, ruleResponse, bubble, isHistoric = false) {
  try {
    const rawPayload = ruleResponse.responsePayload !== undefined ? ruleResponse.responsePayload 
      : (ruleResponse.ResponsePayload !== undefined ? ruleResponse.ResponsePayload 
      : (ruleResponse.payload !== undefined ? ruleResponse.payload : ruleResponse.Payload));

    const payload = typeof rawPayload === 'string' 
      ? JSON.parse(rawPayload) 
      : rawPayload;
    
    const type = (ruleResponse.responseType || ruleResponse.ResponseType || ruleResponse.type || ruleResponse.Type || '').toLowerCase();
    const textContent = bubble.querySelector(".message-text-content");
    const widgetContainer = bubble.querySelector(".message-widget-container");

    console.log("Rendering rich response type:", type, payload, "isHistoric:", isHistoric);

    if (type !== 'text') {
      bubble.dataset.hasRichResponse = "true";
      if (textContent && (textContent.innerHTML === "" || textContent.querySelector(".typing-indicator"))) {
        textContent.innerHTML = "";
      }
    }

    if (type === 'text') {
      if (textContent) {
        textContent.innerHTML = chatbox.formatMarkdown(payload.text || payload.content || payload.message || JSON.stringify(payload));
      }
    } else if (type === 'redirect') {
      const url = payload.url || payload.redirectUrl || payload.Url || payload.RedirectUrl;
      const delaySeconds = Number(payload.seconds || payload.redirectSeconds || payload.Seconds || payload.RedirectSeconds) || 5;
      const countdownTpl = payload.countdownText || payload.CountdownText || "Redirecting you in {seconds} seconds...";
      const countdownId = "countdown-" + Math.random().toString(36).substring(7);
      const text = isHistoric ? "Automated redirection executed" : countdownTpl.replace("{seconds}", delaySeconds);

      if (widgetContainer) {
        widgetContainer.innerHTML = `
          <div class="rich-redirect" style="cursor: pointer;" onclick="window.open('${url}', '_blank')">
            <div class="rich-redirect-icon">🔗</div>
            <div class="rich-redirect-content">
              <div class="rich-redirect-title">Redirect Link</div>
              <div class="rich-redirect-url" title="${url}">Click to visit: ${url}</div>
              <div id="${countdownId}" class="rich-redirect-countdown">${text}</div>
              ${!isHistoric ? `
                <div class="rich-redirect-progress">
                  <div class="rich-redirect-progress-bar" style="animation-duration: ${delaySeconds}s"></div>
                </div>
              ` : ''}
            </div>
          </div>
        `;
      }

      if (!isHistoric && url) {
        let currentSec = delaySeconds;
        const timer = setInterval(() => {
          currentSec--;
          const countdownEl = chatbox.shadowRoot.getElementById(countdownId);
          if (countdownEl) {
            countdownEl.textContent = countdownTpl.replace("{seconds}", currentSec);
          }
          if (currentSec <= 0) {
            clearInterval(timer);
            window.location.href = url;
          }
        }, 1000);
      }
    } else if (type === 'card') {
      if (widgetContainer) {
        widgetContainer.innerHTML = '';
        const cardEl = document.createElement("div");
        cardEl.className = "rich-card";
        
        const imgUrl = payload.imageUrl || payload.ImageUrl || payload.image || payload.Image;
        const imgHtml = imgUrl ? `<img src="${imgUrl}" class="rich-card-banner" alt="Banner" />` : "";
        
        const titleVal = payload.title || payload.Title;
        const title = titleVal ? `<div class="rich-card-title">${titleVal}</div>` : "";
        
        const bodyVal = payload.content || payload.body || payload.Content || payload.Body;
        const content = bodyVal ? `<div class="rich-card-body">${bodyVal}</div>` : "";
        
        let button = "";
        const btnText = payload.buttonLabel || payload.buttonText || payload.ButtonLabel || payload.ButtonText;
        const btnUrl = payload.buttonUrl || payload.url || payload.ButtonUrl || payload.Url;
        if (btnText && btnUrl) {
          button = `<a href="${btnUrl}" target="_blank" class="rich-card-btn">${btnText}</a>`;
        }
        
        cardEl.innerHTML = `${imgHtml}${title}${content}${button}`;
        widgetContainer.appendChild(cardEl);
      }
    } else if (type === 'file') {
      if (widgetContainer) {
        widgetContainer.innerHTML = '';
        const fileEl = document.createElement("div");
        fileEl.className = "rich-file";
        
        const name = payload.fileName || payload.name || payload.FileName || payload.Name || "download-file";
        const url = payload.fileUrl || payload.url || payload.FileUrl || payload.Url;
        const ext = name.split('.').pop().toLowerCase();
        
        let icon = icons.attach;
        if (ext === 'xlsx' || ext === 'xls') icon = icons.excel || icons.attach;
        if (ext === 'pdf') icon = icons.pdf || icons.attach;
        
        fileEl.innerHTML = `
          <div class="rich-file-info">
            <div class="rich-file-icon">${icon}</div>
            <div class="rich-file-name" title="${name}">${name}</div>
          </div>
          <a href="${url}" target="_blank" download="${name}" class="rich-file-btn">
            ${icons.download || '⬇️'} Download
          </a>
        `;
        widgetContainer.appendChild(fileEl);
      }
    } else if (type === 'form') {
      if (widgetContainer) {
        widgetContainer.innerHTML = '';
        const formEl = document.createElement("form");
        formEl.className = "rich-form";
        
        const title = payload.title ? `<div class="rich-form-title">${payload.title}</div>` : "";
        formEl.innerHTML = title;
        
        const fields = payload.fields || [];
        fields.forEach(field => {
          const group = document.createElement("div");
          group.className = "rich-form-group";
          
          const label = `<label class="rich-form-label">${field.label || field.name} ${field.required ? '<span style="color:var(--danger-color, #ef4444)">*</span>' : ''}</label>`;
          let input = "";
          
          if (field.type === 'textarea') {
            input = `<textarea name="${field.name}" class="rich-form-input" placeholder="${field.placeholder || ''}" ${field.required ? 'required' : ''} rows="2"></textarea>`;
          } else if (field.type === 'select') {
            let opts = field.options || [];
            if (typeof opts === 'string') {
              opts = opts.split(',').map(s => s.trim());
            }
            const options = opts.map(opt => `<option value="${opt.value || opt}">${opt.label || opt}</option>`).join('');
            input = `<select name="${field.name}" class="rich-form-select" ${field.required ? 'required' : ''}>${options}</select>`;
          } else if (field.type === 'checkbox') {
            let opts = field.options || [];
            if (typeof opts === 'string') {
              opts = opts.split(',').map(s => s.trim());
            }
            if (opts.length > 0) {
              let optionsHtml = "";
              opts.forEach((opt, idx) => {
                const optLabel = opt.label || opt.name || opt;
                const optVal = opt.value || opt;
                const id = `chk-${field.name}-${idx}-${Math.random().toString(36).substring(7)}`;
                optionsHtml += `
                  <div class="rich-form-check-option">
                    <input type="checkbox" id="${id}" name="${field.name}" value="${optVal}" class="rich-form-checkbox">
                    <label for="${id}" class="rich-form-check-label">${optLabel}</label>
                  </div>
                `;
              });
              input = `<div class="rich-form-check-group">${optionsHtml}</div>`;
            } else {
              const id = `chk-${field.name}-${Math.random().toString(36).substring(7)}`;
              input = `
                <div class="rich-form-check-option">
                  <input type="checkbox" id="${id}" name="${field.name}" value="true" class="rich-form-checkbox" ${field.required ? 'required' : ''}>
                  <label for="${id}" class="rich-form-check-label">${field.placeholder || 'Accept'}</label>
                </div>
              `;
            }
          } else if (field.type === 'radio') {
            let opts = field.options || [];
            if (typeof opts === 'string') {
              opts = opts.split(',').map(s => s.trim());
            }
            let optionsHtml = "";
            opts.forEach((opt, idx) => {
              const optLabel = opt.label || opt.name || opt;
              const optVal = opt.value || opt;
              const id = `rad-${field.name}-${idx}-${Math.random().toString(36).substring(7)}`;
              optionsHtml += `
                <div class="rich-form-check-option">
                  <input type="radio" id="${id}" name="${field.name}" value="${optVal}" class="rich-form-radio" ${field.required && idx === 0 ? 'required' : ''}>
                  <label for="${id}" class="rich-form-check-label">${optLabel}</label>
                </div>
              `;
            });
            input = `<div class="rich-form-check-group">${optionsHtml}</div>`;
          } else {
            input = `<input type="${field.type || 'text'}" name="${field.name}" class="rich-form-input" placeholder="${field.placeholder || ''}" ${field.required ? 'required' : ''}>`;
          }
          
          group.innerHTML = `${label}${input}`;
          formEl.appendChild(group);
        });
        
        const submitBtn = document.createElement("button");
        submitBtn.type = "submit";
        submitBtn.className = "rich-form-submit";
        submitBtn.textContent = payload.submitLabel || payload.submitText || "Submit";
        formEl.appendChild(submitBtn);
        
        formEl.onsubmit = async (e) => {
          e.preventDefault();
          submitBtn.disabled = true;
          submitBtn.textContent = "Submitting...";
          
          const formData = new FormData(formEl);
          const body = {};
          formData.forEach((val, key) => {
            if (body[key]) {
              if (Array.isArray(body[key])) {
                body[key].push(val);
              } else {
                body[key] = [body[key], val];
              }
            } else {
              body[key] = val;
            }
          });
          Object.keys(body).forEach(k => {
            if (Array.isArray(body[k])) {
              body[k] = body[k].join(", ");
            }
          });
          
          try {
            const res = await fetch(`${chatbox.apiUrl}/api/rules/form-submit`, {
              method: "POST",
              headers: { 
                ...getHeaders(chatbox),
                "Content-Type": "application/json" 
              },
              body: JSON.stringify({
                sessionId: chatbox.currentSessionId,
                projectId: chatbox.projectId,
                formTitle: payload.title,
                submitUrl: payload.submitUrl,
                data: body
              })
            });
            if (res.ok) {
              formEl.innerHTML = `<div style="color:var(--success-color, #10b981); font-weight:600; display:flex; align-items:center; gap:8px; padding: 12px; background: rgba(16, 185, 129, 0.1); border: 1px solid rgba(16, 185, 129, 0.2); border-radius: 8px;">
                ${icons.check || '✅'} Submission received successfully!
              </div>`;
            } else {
              throw new Error("Submit failed");
            }
          } catch(err) {
            console.error(err);
            submitBtn.disabled = false;
            submitBtn.textContent = "Retry Submit";
            const errEl = document.createElement("div");
            errEl.style.color = "var(--danger-color, #ef4444)";
            errEl.style.fontSize = "12px";
            errEl.style.marginTop = "8px";
            errEl.textContent = "Submission failed. Please try again.";
            formEl.appendChild(errEl);
          }
        };
        
        widgetContainer.appendChild(formEl);
      }
    } else if (type === 'buttons') {
      if (widgetContainer) {
        widgetContainer.innerHTML = '';
        const btnContainer = document.createElement("div");
        btnContainer.className = "rich-buttons-container";
        
        const buttons = payload.buttons || [];
        buttons.forEach(btn => {
          const buttonEl = document.createElement("button");
          buttonEl.className = "rich-action-button";
          buttonEl.textContent = btn.label || "Click";
          
          if (isHistoric) {
            buttonEl.disabled = true;
            buttonEl.classList.add("historic");
          } else {
            buttonEl.addEventListener("click", () => {
              btnContainer.querySelectorAll(".rich-action-button").forEach(b => {
                b.disabled = true;
                b.classList.add("historic");
              });

              const action = (btn.action || "next").toLowerCase();
              const value = btn.value || "";

              if (action === "url" && value) {
                window.open(value, "_blank");
              } else if (action === "next") {
                chatbox.sendUserActionMessage(btn.label);
              } else if (action === "postback") {
                chatbox.sendUserActionMessage(btn.label, value);
              }
            });
          }
          btnContainer.appendChild(buttonEl);
        });
        widgetContainer.appendChild(btnContainer);
      }
    } else if (type === 'tool_call') {
      if (!isHistoric && payload.toolName) {
        chatbox.handleToolCalls([{
          name: payload.toolName,
          arguments: payload.arguments || {},
          id: "tool-rule-" + Math.random().toString(36).substring(7)
        }], bubble);
      } else if (isHistoric && payload.toolName) {
        if (widgetContainer) {
          widgetContainer.innerHTML = '';
          const toolCallEl = document.createElement("div");
          toolCallEl.className = "rich-tool-badge";
          toolCallEl.innerHTML = `
            <div class="rich-tool-badge-header">
              <span class="rich-tool-badge-icon">🔧</span>
              <span class="rich-tool-badge-title">Automated Action Executed</span>
            </div>
            <div class="rich-tool-badge-body">
              <div class="rich-tool-badge-name"><strong>Command:</strong> <code>${payload.toolName}</code></div>
              ${payload.arguments && Object.keys(payload.arguments).length > 0 ? `
                <div class="rich-tool-badge-args">
                  <div class="rich-tool-badge-args-title">Parameters:</div>
                  <pre><code>${JSON.stringify(payload.arguments, null, 2)}</code></pre>
                </div>
              ` : ''}
            </div>
          `;
          widgetContainer.appendChild(toolCallEl);
        }
      }
    }
  } catch (err) {
    console.error("Failed to render rich response:", err);
  }
}

export function _hasChartableData(rows, columns) {
  if (!rows || rows.length < 2 || columns.length < 2) return false;
  const numericCols = columns.slice(1).filter(c => {
    const sample = rows.find(r => r[c] !== null && r[c] !== undefined);
    return sample && typeof sample[c] === 'number';
  });
  return numericCols.length > 0;
}

export function renderDataResult(chatbox, result, container) {
  if (!result) return;

  const widgetId = result.widgetId || result.WidgetId;
  if (widgetId && !result.data && !result.rows) {
    if (container.dataset.loadingWidgetId === widgetId) {
      return;
    }
    container.dataset.loadingWidgetId = widgetId;

    container.innerHTML = `
      <div class="data-loading-widget" style="background: rgba(99, 102, 241, 0.04); border: 1px dashed rgba(99, 102, 241, 0.25); border-radius: 12px; padding: 16px; margin-top: 8px; display: flex; flex-direction: column; gap: 12px;">
        <div style="display: flex; align-items: center; gap: 10px; color: var(--primary-color, #6366f1); font-size: 13px; font-weight: 500;">
          <span class="spin-animation" style="display: inline-block; animation: spin 1s linear infinite;">${icons.refresh}</span>
          <span>Fetching query results (${result.rowCount || 0} rows)...</span>
        </div>
        <div style="display: flex; flex-direction: column; gap: 6px;">
          <div class="data-skeleton-line" style="height: 10px; width: 90%; background: rgba(255,255,255,0.06); border-radius: 4px;"></div>
          <div class="data-skeleton-line" style="height: 10px; width: 70%; background: rgba(255,255,255,0.06); border-radius: 4px;"></div>
          <div class="data-skeleton-line" style="height: 10px; width: 45%; background: rgba(255,255,255,0.06); border-radius: 4px;"></div>
        </div>
      </div>`;

    fetch(`${chatbox.apiUrl}/api/chat/widgets/${widgetId}`, {
      headers: getHeaders(chatbox)
    })
    .then(res => {
      if (!res.ok) throw new Error(`HTTP error! status: ${res.status}`);
      return res.json();
    })
    .then(data => {
      delete container.dataset.loadingWidgetId;
      renderDataResult(chatbox, {
        data: data,
        rowCount: result.rowCount || (Array.isArray(data) ? data.length : 0),
        truncated: result.truncated
      }, container);
    })
    .catch(err => {
      console.error("Failed to load lazy widget data:", err);
      delete container.dataset.loadingWidgetId;
      container.innerHTML = `
        <div style="color: var(--danger-color); font-size: 13px; padding: 12px; border: 1px solid rgba(239, 68, 68, 0.2); border-radius: 12px; background: rgba(239, 68, 68, 0.05); display: flex; align-items: flex-start; gap: 10px; margin-top: 8px;">
          <div style="flex-shrink: 0; margin-top: 1px;">${icons.error}</div>
          <div style="flex: 1; font-weight: 500;">Failed to load query data: ${chatbox.escapeHtml(err.message)}</div>
        </div>`;
    });
    return;
  }

  if (typeof result === 'string') {
    const isWarning = result.includes("restricted") || result.includes("permitted") || result.includes("limit") || result.includes("not permitted");
    const alertBg = isWarning ? "rgba(245, 158, 11, 0.08)" : "rgba(239, 68, 68, 0.08)";
    const alertBorder = isWarning ? "rgba(245, 158, 11, 0.2)" : "rgba(239, 68, 68, 0.2)";
    const alertText = isWarning ? "#d97706" : "#ef4444";
    const icon = isWarning ? icons.lightbulb : icons.error;

    container.innerHTML = `
      <div class="data-alert-widget" style="background: ${alertBg}; border: 1px solid ${alertBorder}; border-radius: 12px; padding: 14px; color: ${alertText}; font-size: 13px; line-height: 1.5; margin-top: 8px; display: flex; align-items: flex-start; gap: 10px; animation: slideUp 0.2s ease;">
        <div style="flex-shrink: 0; margin-top: 1px;">${icon}</div>
        <div style="flex: 1; font-weight: 500;">${chatbox.escapeHtml(result)}</div>
      </div>`;
    return;
  }
  
  let rows = result.rows || result.data || (Array.isArray(result) ? result : []);
  let columns = result.columns || [];
  
  if (columns.length === 0 && rows.length > 0) {
    columns = Object.keys(rows[0]);
  }

  if (columns.length === 0 || rows.length === 0) {
    container.innerHTML = `
      <div class="data-empty-state">
        <div class="data-empty-icon">${icons.list}</div>
        <div class="data-empty-text">No data returned</div>
      </div>`;
    return;
  }

  if (rows.length === 1 && columns.length === 1) {
    const val = rows[0][columns[0]];
    const label = columns[0];
    container.innerHTML = `
      <div class="data-stat-pill">
        <span class="data-stat-label">${label}</span>
        <span class="data-stat-value">${val !== null && val !== undefined ? val : '—'}</span>
      </div>`;
    return;
  }

  const canChart = _hasChartableData(rows, columns);
  const id = 'data-' + Math.random().toString(36).substr(2, 9);
  const PAGE_SIZE = 10;
  let currentPage = 1;
  const totalPages = () => Math.ceil(rows.length / PAGE_SIZE);
  let isExpanded = false;

  const buildTableRows = (page) => {
    const start = (page - 1) * PAGE_SIZE;
    const pageRows = rows.slice(start, start + PAGE_SIZE);
    return pageRows.map(row =>
      `<tr>${columns.map(c => `<td>${row[c] !== null && row[c] !== undefined ? chatbox.escapeHtml(row[c]) : ''}</td>`).join('')}</tr>`
    ).join('');
  };

  const buildPagination = (page) => {
    if (rows.length <= PAGE_SIZE) return '';
    const tp = totalPages();
    const start = (page - 1) * PAGE_SIZE + 1;
    const end = Math.min(page * PAGE_SIZE, rows.length);

    let pageBtns = '';
    const maxBtns = 5;
    let startP = Math.max(1, page - Math.floor(maxBtns / 2));
    let endP = Math.min(tp, startP + maxBtns - 1);
    if (endP - startP < maxBtns - 1) startP = Math.max(1, endP - maxBtns + 1);

    if (startP > 1) pageBtns += `<button class="pg-btn pg-num" data-page="1">1</button>${startP > 2 ? '<span class="pg-ellipsis">…</span>' : ''}`;
    for (let p = startP; p <= endP; p++) {
      pageBtns += `<button class="pg-btn pg-num ${p === page ? 'pg-current' : ''}" data-page="${p}">${p}</button>`;
    }
    if (endP < tp) pageBtns += `${endP < tp - 1 ? '<span class="pg-ellipsis">…</span>' : ''}<button class="pg-btn pg-num" data-page="${tp}">${tp}</button>`;

    return `
      <div class="data-pagination">
        <span class="pg-info">Rows ${start}–${end} of ${rows.length}</span>
        <div class="pg-controls">
          <button class="pg-btn pg-arrow" data-page="${page - 1}" ${page === 1 ? 'disabled' : ''} title="Previous">&#8249;</button>
          ${pageBtns}
          <button class="pg-btn pg-arrow" data-page="${page + 1}" ${page === tp ? 'disabled' : ''} title="Next">&#8250;</button>
        </div>
      </div>`;
  };

  const isTruncated = result.truncated || result.Truncated || false;

  container.innerHTML = `
    <div class="data-result-widget" id="${id}">
      <div class="data-tabs">
        <button class="data-tab active" data-tab="table" title="View as Table">${icons.list} Table <span class="data-tab-badge">${rows.length}</span></button>
        ${canChart ? `<button class="data-tab" data-tab="chart" title="View as Chart">${icons.chart} Chart</button>` : ''}
        <div style="flex:1"></div>
        <div class="data-actions">
          <button class="data-action-btn data-expand-btn" data-action="expand" title="Expand View">${icons.expand}</button>
          <button class="data-action-btn" data-action="copy" title="Copy to CSV">${icons.copy}</button>
          <button class="data-action-btn" data-action="excel" title="Export Excel">${icons.excel}</button>
          <button class="data-action-btn" data-action="pdf" title="Export PDF">${icons.pdf}</button>
        </div>
      </div>
      <div class="data-content">
        ${isTruncated ? `
          <div class="data-truncated-banner" style="background: rgba(245, 158, 11, 0.08); border: 1px dashed rgba(245, 158, 11, 0.2); border-radius: 8px; padding: 8px 12px; margin: 12px 0; font-size: 12px; color: #d97706; display: flex; align-items: center; gap: 8px;">
            <span style="font-size: 14px; display: flex; align-items: center;">⚠️</span>
            <span>Showing first ${rows.length} rows. Results are truncated for performance.</span>
          </div>` : ''}
        <div class="data-panel active" data-panel="table">
          <div class="table-container">
            <table>
              <thead><tr>${columns.map(c => `<th>${c}</th>`).join('')}</tr></thead>
              <tbody class="data-tbody">${buildTableRows(1)}</tbody>
            </table>
          </div>
          ${buildPagination(1)}
        </div>
        ${canChart ? `
        <div class="data-panel" data-panel="chart">
          <div class="chart-controls">
            <button class="data-action-btn" data-action="download-chart" title="Download Chart">${icons.download || ''}</button>
            <select class="chart-type-select">
              <option value="bar">Bar</option>
              <option value="line">Line</option>
              <option value="pie">Pie</option>
              <option value="doughnut">Doughnut</option>
            </select>
          </div>
          <div class="chart-wrapper">
            <canvas class="data-chart-canvas"></canvas>
          </div>
        </div>` : ''}
      </div>
    </div>
  `;

  const widget = container.querySelector(`#${id}`);

  // Tooltip singleton setup
  (() => {
    let tip = chatbox.shadowRoot.getElementById("data-cell-tooltip-singleton");
    if (!tip) {
      tip = document.createElement("div");
      tip.id = "data-cell-tooltip-singleton";
      tip.className = "data-cell-tooltip";
      tip.innerHTML = "<span class='data-cell-tooltip-close' title='Close'>✕</span><span class='data-cell-tooltip-text'></span>";
      chatbox.shadowRoot.appendChild(tip);
      tip.querySelector(".data-cell-tooltip-close").addEventListener("click", () => {
        tip.classList.remove("visible");
      });
    }
    const tipText = tip.querySelector(".data-cell-tooltip-text");
    let hideTimer = null;
    let showTimer = null; 

    const showTip = (el, text) => {
      clearTimeout(hideTimer);
      tipText.textContent = text;
      tip.classList.add("visible");
      positionTip(el);
    };
    
    const hideTip = () => {
      hideTimer = setTimeout(() => tip.classList.remove("visible"), 120);
    };
    
    const positionTip = (el) => {
      const r = el.getBoundingClientRect();
      const tw = Math.min(320, window.innerWidth * 0.9);
      let left = r.left;
      let top = r.bottom + 8;
      if (left + tw > window.innerWidth - 8) left = window.innerWidth - tw - 8;
      if (left < 8) left = 8;
      if (top + 60 > window.innerHeight) top = r.top - 8 - 60;
      tip.style.left = left + "px";
      tip.style.top = top + "px";
    };

    tip.addEventListener("mouseenter", () => {
      clearTimeout(hideTimer);
    });

    tip.addEventListener("mouseleave", () => {
      hideTip();
    });

    widget.addEventListener("mouseover", (e) => {
      const td = e.target.closest("td, th");
      if (!td) return;
      if (td.scrollWidth > td.clientWidth + 2) {
        clearTimeout(hideTimer);
        clearTimeout(showTimer);
        
        showTimer = setTimeout(() => {
          showTip(td, td.textContent.trim());
        }, 400); 
      }
    });

    widget.addEventListener("mouseout", (e) => {
      const td = e.target.closest("td, th");
      if (td) {
        clearTimeout(showTimer);
        hideTip();
      }
    });

    widget.addEventListener("click", (e) => {
      const td = e.target.closest("td, th");
      if (!td) return;
      if (td.scrollWidth > td.clientWidth + 2) {
        clearTimeout(showTimer);
        showTip(td, td.textContent.trim());
      }
    });
  })();

  const tablePanel = widget.querySelector('[data-panel="table"]');
  const refreshPagination = () => {
    widget.querySelector('.data-tbody').innerHTML = buildTableRows(currentPage);
    const oldPg = tablePanel.querySelector('.data-pagination');
    if (oldPg) oldPg.remove();
    const pgHtml = buildPagination(currentPage);
    if (pgHtml) tablePanel.insertAdjacentHTML('beforeend', pgHtml);
    bindPagination();
  };
  
  const bindPagination = () => {
    tablePanel.querySelectorAll('.pg-btn[data-page]').forEach(btn => {
      btn.onclick = () => {
        const page = parseInt(btn.dataset.page);
        if (!isNaN(page) && page >= 1 && page <= totalPages() && page !== currentPage) {
          currentPage = page;
          refreshPagination();
          widget.querySelector('.table-container').scrollTop = 0;
        }
      };
    });
  };
  bindPagination();

  const tabs = widget.querySelectorAll('.data-tab');
  const panels = widget.querySelectorAll('.data-panel');
  tabs.forEach(tab => {
    tab.onclick = () => {
      tabs.forEach(t => t.classList.remove('active'));
      panels.forEach(p => p.classList.remove('active'));
      tab.classList.add('active');
      const panel = widget.querySelector(`[data-panel="${tab.dataset.tab}"]`);
      if (panel) panel.classList.add('active');
      if (tab.dataset.tab === 'chart' && canChart) initChart(widget, columns, rows);
    };
  });

  const expandBtn = widget.querySelector('.data-expand-btn');
  let widgetOriginalParent = null;
  let widgetNextSibling = null;

  const collapseExpand = () => {
    isExpanded = false;
    widget.classList.remove('data-widget-expanded', 'data-widget-expanded-mobile');

    if (widgetOriginalParent) {
      if (widgetNextSibling && widgetNextSibling.parentNode === widgetOriginalParent) {
        widgetOriginalParent.insertBefore(widget, widgetNextSibling);
      } else {
        widgetOriginalParent.appendChild(widget);
      }
      widgetOriginalParent = null;
      widgetNextSibling = null;
    }

    const backdrop = chatbox.shadowRoot.getElementById('data-expand-backdrop');
    if (backdrop) backdrop.remove();

    if (widget._rwCleanup) widget._rwCleanup();
    widget.style.width = '';
    widget.style.height = '';
    widget.style.left = '';
    widget.style.top = '';
    widget.style.transform = '';
    widget.style.removeProperty('--expanded-w');

    expandBtn.innerHTML = icons.expand;
    expandBtn.title = 'Expand View';
    document.removeEventListener('keydown', escHandler);

    const chartPanel = widget.querySelector('[data-panel="chart"]');
    if (canChart && chartPanel && chartPanel.classList.contains('active')) {
      setTimeout(() => initChart(widget, columns, rows), 350);
    }
  };

  const escHandler = (e) => { if (e.key === 'Escape') collapseExpand(); };

  widget.querySelectorAll('.data-action-btn').forEach(btn => {
    btn.onclick = () => {
      const action = btn.dataset.action;

      if (action === 'expand') {
        isExpanded = !isExpanded;
        if (isExpanded) {
          const isMobile = window.innerWidth < 768;

          widgetOriginalParent = widget.parentNode;
          widgetNextSibling = widget.nextSibling;

          chatbox.shadowRoot.appendChild(widget);

          const backdrop = document.createElement('div');
          backdrop.id = 'data-expand-backdrop';
          backdrop.className = 'data-expand-backdrop';
          backdrop.onclick = collapseExpand;
          chatbox.shadowRoot.insertBefore(backdrop, widget);

          widget.classList.add(isMobile ? 'data-widget-expanded-mobile' : 'data-widget-expanded');
          expandBtn.innerHTML = icons.collapse;
          expandBtn.title = 'Exit Fullscreen (Esc)';
          document.addEventListener('keydown', escHandler);

          if (!isMobile && !widget.querySelector('.data-widget-expanded-resize')) {
            const rh = document.createElement('div');
            rh.className = 'data-widget-expanded-resize';
            widget.appendChild(rh);

            let rwStartX, rwStartY, rwInitW, rwInitH, rwInitLeft, rwInitTop;
            const maxW = window.innerWidth * 0.95;

            const onRWMove = (ev) => {
              const newW = Math.min(maxW, Math.max(360, rwInitW + (ev.clientX - rwStartX)));
              widget.style.setProperty('--expanded-w', newW + 'px');
              const canvas = widget.querySelector('.data-chart-canvas');
              if (canvas && canvas._chart) canvas._chart.resize();
            };

            const onRWUp = (ev) => {
              rh.releasePointerCapture(ev.pointerId);
              document.body.style.userSelect = '';
              document.removeEventListener('pointermove', onRWMove);
              document.removeEventListener('pointerup', onRWUp);
            };

            rh.addEventListener('pointerdown', (ev) => {
              rwStartX = ev.clientX;
              rwStartY = ev.clientY;
              
              const wr = widget.getBoundingClientRect();
              rwInitW = wr.width;
              rwInitH = wr.height;
              rwInitLeft = wr.left;
              rwInitTop = wr.top;

              widget.style.transform = 'none';
              widget.style.left = rwInitLeft + 'px';
              widget.style.top = rwInitTop + 'px';

              document.body.style.userSelect = 'none';
              rh.setPointerCapture(ev.pointerId);

              document.addEventListener('pointermove', onRWMove);
              document.addEventListener('pointerup', onRWUp);

              ev.preventDefault();
              ev.stopPropagation();
            });

            widget._rwCleanup = () => {
              document.removeEventListener('pointermove', onRWMove);
              document.removeEventListener('pointerup', onRWUp);
              document.body.style.userSelect = '';
              rh.remove();
              delete widget._rwCleanup;
            };
          }

          const chartPanel = widget.querySelector('[data-panel="chart"]');
          if (canChart && chartPanel && chartPanel.classList.contains('active')) {
            setTimeout(() => initChart(widget, columns, rows), 350);
          }
        } else {
          collapseExpand();
        }
      } else if (action === 'copy') {
        const csv = [columns.join(','), ...rows.map(r => columns.map(c => r[c]).join(','))].join('\n');
        navigator.clipboard.writeText(csv);
        const original = btn.innerHTML;
        btn.innerHTML = icons.check;
        setTimeout(() => btn.innerHTML = original, 2000);
      } else if (action === 'download-chart') {
        const canvas = widget.querySelector('.data-chart-canvas');
        const link = document.createElement('a');
        link.download = 'chart.png';
        link.href = canvas.toDataURL('image/png');
        link.click();
      } else {
        exportData(chatbox, action, result);
      }
    };
  });

  const chartSelect = widget.querySelector('.chart-type-select');
  if (chartSelect && canChart) {
    chartSelect.onchange = () => initChart(widget, columns, rows);
  }
  chatbox.scrollToBottom();
}

export function initChart(widget, columns, rows) {
  const canvas = widget.querySelector('.data-chart-canvas');
  const type = widget.querySelector('.chart-type-select').value;
  if (!window.Chart) return;

  if (canvas._chart) canvas._chart.destroy();

  const labels = rows.map(r => r[columns[0]]?.toString() || '');
  const datasets = columns.slice(1).filter(c => typeof rows[0][c] === 'number').map((c, i) => ({
    label: c,
    data: rows.map(r => r[c]),
    backgroundColor: `hsla(${(i * 60) % 360}, 70%, 60%, 0.6)`,
    borderColor: `hsla(${(i * 60) % 360}, 70%, 50%, 1)`,
    borderWidth: 1
  }));

  const gridColor = 'rgba(255, 255, 255, 0.1)';
  const textColor = '#94a3b8';

  canvas._chart = new window.Chart(canvas, {
    type: type,
    data: { labels, datasets },
    options: { 
      responsive: true, 
      maintainAspectRatio: false,
      plugins: {
        legend: {
          display: type !== 'pie' && type !== 'doughnut',
          position: 'top',
          labels: { color: textColor, boxWidth: 12, padding: 10, font: { size: 11 } }
        },
        tooltip: {
          backgroundColor: 'rgba(15, 23, 42, 0.9)',
          titleColor: '#fff',
          bodyColor: '#cbd5e1',
          borderColor: 'rgba(99, 102, 241, 0.5)',
          borderWidth: 1,
          padding: 10,
          displayColors: true
        }
      },
      scales: (type === 'pie' || type === 'doughnut') ? {} : {
        x: {
          grid: { display: false },
          ticks: { 
            color: textColor, 
            font: { size: 10 },
            maxRotation: 45,
            minRotation: 45,
            callback: function(value) {
              const label = this.getLabelForValue(value);
              return label && label.length > 10 ? label.substr(0, 10) + '...' : label;
            }
          }
        },
        y: {
          grid: { color: gridColor },
          ticks: { color: textColor, font: { size: 10 } }
        }
      }
    }
  });
}

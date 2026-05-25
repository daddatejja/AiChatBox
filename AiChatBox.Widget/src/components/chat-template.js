export function getChatboxTemplate(chatbox) {
  const stylePath = chatbox.getAttribute("css-path") || `${chatbox.apiUrl}/widget/ai-chatbox.css`;
  return `
    <link rel="stylesheet" href="${stylePath}">
    <style>
      .message-text-content:empty, .message-widget-container:empty { display: none; }
      .message-text-content { margin-bottom: 8px; line-height: 1.5; }
      
      @keyframes dataPulse {
        0%, 100% { opacity: 0.4; }
        50% { opacity: 0.8; }
      }
      .data-skeleton-line {
        animation: dataPulse 1.5s infinite ease-in-out;
      }
      
      /* Command Autocomplete Dropdown Styling */
      .command-autocomplete-dropdown {
        position: absolute;
        bottom: calc(100% + 8px);
        left: 12px;
        right: 12px;
        background: rgba(30, 41, 59, 0.95);
        backdrop-filter: blur(16px);
        border: 1px solid rgba(255, 255, 255, 0.08);
        border-radius: 12px;
        box-shadow: 0 10px 25px -5px rgba(0, 0, 0, 0.4), 0 8px 10px -6px rgba(0, 0, 0, 0.4);
        max-height: 220px;
        overflow-y: auto;
        z-index: 1000;
        padding: 6px;
        display: none;
        flex-direction: column;
        gap: 2px;
        animation: slideUp 0.2s cubic-bezier(0.4, 0, 0.2, 1);
      }
      @keyframes slideUp {
        from { opacity: 0; transform: translateY(8px); }
        to { opacity: 1; transform: translateY(0); }
      }
      .command-item {
        display: flex;
        align-items: center;
        gap: 10px;
        padding: 8px 12px;
        border-radius: 8px;
        cursor: pointer;
        transition: all 0.15s ease;
      }
      .command-item-trigger {
        font-weight: 700;
        color: var(--primary-color, #6366f1);
        font-size: 14px;
        background: rgba(99, 102, 241, 0.15);
        padding: 2px 6px;
        border-radius: 4px;
      }
      .command-item-name {
        font-weight: 600;
        color: #fff;
        font-size: 13.5px;
      }
      .command-item-desc {
        color: var(--text-muted, #94a3b8);
        font-size: 12px;
        flex: 1;
        white-space: nowrap;
        overflow: hidden;
        text-overflow: ellipsis;
      }
      .command-item:hover, .command-item.active {
        background: rgba(255, 255, 255, 0.08);
      }

      /* Rich Response Styles */
      .rich-redirect {
        display: flex;
        align-items: center;
        gap: 14px;
        padding: 14px;
        background: rgba(57, 167, 185, 0.06);
        border: 1px solid rgba(57, 167, 185, 0.2);
        border-radius: 12px;
        color: var(--text-color, #1e293b);
        transition: all 0.2s ease;
      }
      .rich-redirect:hover {
        background: rgba(57, 167, 185, 0.1);
        border-color: rgba(57, 167, 185, 0.3);
      }
      .rich-redirect-icon {
        font-size: 22px;
        filter: drop-shadow(0 2px 4px rgba(57,167,185,0.2));
      }
      .rich-redirect-content {
        flex: 1;
        min-width: 0;
      }
      .rich-redirect-title {
        font-weight: 700;
        color: var(--primary-color, #39a7b9);
        font-size: 14px;
        margin-bottom: 2px;
      }
      .rich-redirect-url {
        font-size: 11px;
        color: var(--secondary-text, #64748b);
        white-space: nowrap;
        overflow: hidden;
        text-overflow: ellipsis;
      }
      .rich-redirect-countdown {
        font-size: 12px;
        font-weight: 600;
        color: var(--text-color, #1e293b);
        margin-top: 6px;
      }
      .rich-redirect-progress {
        width: 100%;
        height: 4px;
        background: var(--border-color, #e2e8f0);
        border-radius: 2px;
        margin-top: 8px;
        overflow: hidden;
      }
      .rich-redirect-progress-bar {
        height: 100%;
        background: var(--primary-color, #39a7b9);
        width: 100%;
        transform-origin: left;
        animation: redirectProgress linear forwards;
      }
      @keyframes redirectProgress {
        from { transform: scaleX(1); }
        to { transform: scaleX(0); }
      }

      .rich-card {
        background: var(--bg-color, #ffffff);
        border: 1px solid var(--border-color, #e2e8f0);
        border-radius: 14px;
        padding: 16px;
        box-shadow: 0 4px 20px rgba(0,0,0,0.04);
        margin-top: 8px;
        overflow: hidden;
        display: flex;
        flex-direction: column;
      }
      .rich-card-banner {
        width: calc(100% + 32px);
        margin: -16px -16px 12px -16px;
        height: 130px;
        object-fit: cover;
        border-top-left-radius: 12px;
        border-top-right-radius: 12px;
        border-bottom: 1px solid var(--border-color, #e2e8f0);
      }
      .rich-card-title {
        font-weight: 700;
        font-size: 15px;
        color: var(--text-color, #1e293b);
        margin-bottom: 8px;
      }
      .rich-card-body {
        font-size: 13px;
        line-height: 1.5;
        color: var(--secondary-text, #64748b);
        margin-bottom: 12px;
      }
      .rich-card-btn {
        display: inline-flex;
        align-items: center;
        justify-content: center;
        background: var(--primary-color, #39a7b9);
        color: #fff !important;
        border: none;
        padding: 10px 18px;
        border-radius: 8px;
        font-size: 13px;
        font-weight: 600;
        cursor: pointer;
        text-decoration: none;
        transition: all 0.2s ease;
        box-shadow: 0 2px 8px rgba(57, 167, 185, 0.2);
        text-align: center;
      }
      .rich-card-btn:hover {
        transform: translateY(-1px);
        box-shadow: 0 4px 14px rgba(57, 167, 185, 0.3);
      }

      .rich-file {
        display: flex;
        align-items: center;
        justify-content: space-between;
        background: var(--bg-color, #ffffff);
        border: 1px solid var(--border-color, #e2e8f0);
        border-radius: 12px;
        padding: 12px;
        margin-top: 8px;
        gap: 12px;
        box-shadow: 0 2px 10px rgba(0,0,0,0.02);
      }
      .rich-file-info {
        display: flex;
        align-items: center;
        gap: 10px;
        flex: 1;
        min-width: 0;
      }
      .rich-file-icon {
        width: 34px;
        height: 34px;
        background: rgba(57, 167, 185, 0.08);
        color: var(--primary-color, #39a7b9);
        border-radius: 8px;
        display: flex;
        align-items: center;
        justify-content: center;
        flex-shrink: 0;
      }
      .rich-file-icon svg {
        width: 18px;
        height: 18px;
      }
      .rich-file-name {
        font-size: 13px;
        font-weight: 600;
        color: var(--text-color, #1e293b);
        white-space: nowrap;
        overflow: hidden;
        text-overflow: ellipsis;
      }
      .rich-file-btn {
        background: var(--border-color, #e2e8f0);
        border: none;
        color: var(--text-color, #1e293b);
        padding: 6px 12px;
        border-radius: 6px;
        font-size: 12px;
        font-weight: 600;
        cursor: pointer;
        text-decoration: none;
        transition: all 0.2s;
        white-space: nowrap;
      }
      .rich-file-btn:hover {
        background: rgba(57, 167, 185, 0.1);
        color: var(--primary-color, #39a7b9);
      }

      .rich-form {
        background: rgba(247, 249, 252, 0.8);
        border: 1px solid var(--border-color, #e2e8f0);
        border-radius: 14px;
        padding: 16px;
        margin-top: 8px;
        display: flex;
        flex-direction: column;
        gap: 12px;
        box-shadow: 0 2px 12px rgba(0,0,0,0.02);
      }
      .rich-form-title {
        font-weight: 700;
        font-size: 14px;
        color: var(--text-color, #1e293b);
        margin-bottom: 4px;
      }
      .rich-form-group {
        display: flex;
        flex-direction: column;
        gap: 6px;
      }
      .rich-form-label {
        font-size: 11px;
        font-weight: 700;
        color: var(--secondary-text, #64748b);
        text-transform: uppercase;
        letter-spacing: 0.5px;
      }
      .rich-form-input, .rich-form-select {
        background: var(--bg-color, #ffffff);
        border: 1px solid var(--border-color, #e2e8f0);
        border-radius: 8px;
        padding: 8px 12px;
        color: var(--text-color, #1e293b);
        font-size: 13px;
        outline: none;
        transition: all 0.2s ease;
      }
      .rich-form-input:focus, .rich-form-select:focus {
        border-color: var(--primary-color, #39a7b9);
        box-shadow: 0 0 0 3px rgba(57, 167, 185, 0.1);
      }
      .rich-form-select option {
        background: var(--bg-color, #ffffff);
        color: var(--text-color, #1e293b);
      }
      .rich-form-check-group {
        display: flex;
        flex-direction: column;
        gap: 8px;
        margin-top: 4px;
      }
      .rich-form-check-option {
        display: flex;
        align-items: center;
        gap: 8px;
        cursor: pointer;
      }
      .rich-form-checkbox, .rich-form-radio {
        accent-color: var(--primary-color, #39a7b9);
        width: 16px;
        height: 16px;
        margin: 0;
        cursor: pointer;
      }
      .rich-form-check-label {
        font-size: 13px;
        color: var(--text-color, #1e293b);
        cursor: pointer;
        user-select: none;
      }
      .rich-form-submit {
        background: var(--primary-color, #39a7b9);
        color: #fff;
        border: none;
        padding: 10px 14px;
        border-radius: 8px;
        font-size: 13px;
        font-weight: 600;
        cursor: pointer;
        transition: all 0.2s ease;
        text-align: center;
      }
      .rich-form-submit:hover {
        background: var(--primary-gradient, #2d8a99);
        transform: translateY(-0.5px);
      }
      .rich-form-submit:disabled {
        background: var(--border-color, #e2e8f0);
        color: var(--secondary-text, #64748b);
        cursor: not-allowed;
        transform: none;
      }

      /* Historic Tool Call Badge Styles */
      .rich-tool-badge {
        background: rgba(241, 245, 249, 0.9);
        border: 1px solid var(--border-color, #e2e8f0);
        border-radius: 12px;
        padding: 12px;
        margin-top: 8px;
        box-shadow: 0 2px 8px rgba(0,0,0,0.02);
      }
      .rich-tool-badge-header {
        display: flex;
        align-items: center;
        gap: 6px;
        margin-bottom: 8px;
      }
      .rich-tool-badge-icon {
        font-size: 15px;
      }
      .rich-tool-badge-title {
        font-weight: 700;
        font-size: 12px;
        color: var(--text-color, #1e293b);
        text-transform: uppercase;
        letter-spacing: 0.5px;
      }
      .rich-tool-badge-body {
        display: flex;
        flex-direction: column;
        gap: 6px;
      }
      .rich-tool-badge-name {
        font-size: 13px;
        color: var(--secondary-text, #64748b);
      }
      .rich-tool-badge-name code {
        background: rgba(57, 167, 185, 0.08);
        color: var(--primary-color, #39a7b9);
        padding: 2px 6px;
        border-radius: 4px;
        font-weight: 600;
        font-family: var(--code-font, monospace);
      }
      .rich-tool-badge-args {
        display: flex;
        flex-direction: column;
        gap: 4px;
        margin-top: 4px;
      }
      .rich-tool-badge-args-title {
        font-size: 11px;
        font-weight: 700;
        color: var(--secondary-text, #64748b);
      }
      .rich-tool-badge-args pre {
        margin: 0;
        background: rgba(15, 23, 42, 0.03);
        border: 1px solid var(--border-color, #e2e8f0);
        border-radius: 8px;
        padding: 8px;
        overflow-x: auto;
      }
      .rich-tool-badge-args code {
        color: var(--text-color, #1e293b);
        font-size: 11px;
        font-family: var(--code-font, monospace);
      }
      
      .message-widget-container { width: 100%; overflow: hidden; margin-top: 10px; transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1); }
      .message-widget-container.expanded { width: calc(100% + 40px); margin-left: -20px; }
      
      .data-result-widget { 
        background: var(--bg-glass, rgba(255, 255, 255, 0.03)); 
        backdrop-filter: blur(12px);
        border-radius: 16px; 
        border: 1px solid var(--border-color, rgba(255, 255, 255, 0.1)); 
        overflow: hidden; 
        box-shadow: 0 8px 32px rgba(0,0,0,0.2);
        display: flex;
        flex-direction: column;
      }

      .data-tabs {
        display: flex;
        padding: 6px;
        background: rgba(0,0,0,0.2);
        gap: 4px;
        border-bottom: 1px solid rgba(255,255,255,0.05);
      }

      .data-tab {
        flex: 1;
        padding: 8px 12px;
        border: none;
        background: transparent;
        color: var(--text-muted, #94a3b8);
        font-size: 13px;
        font-weight: 500;
        cursor: pointer;
        border-radius: 8px;
        transition: all 0.2s ease;
        display: flex;
        align-items: center;
        justify-content: center;
        gap: 6px;
      }

      .data-tab svg { width: 14px; height: 14px; }
      .data-tab:hover { background: rgba(255,255,255,0.05); color: #fff; }
      .data-tab.active { background: var(--primary-color, #6366f1); color: #fff; box-shadow: 0 2px 8px rgba(99, 102, 241, 0.4); }

      .data-actions { display: flex; gap: 4px; padding-left: 8px; border-left: 1px solid rgba(255,255,255,0.1); }
      .data-action-btn {
        padding: 6px;
        background: transparent;
        border: none;
        color: var(--text-muted);
        cursor: pointer;
        border-radius: 6px;
        transition: all 0.2s;
        display: flex;
        align-items: center;
        justify-content: center;
      }
      .data-action-btn:hover { background: rgba(255,255,255,0.1); color: #fff; }
      .data-action-btn svg { width: 14px; height: 14px; }

      .data-content { padding: 12px; position: relative; min-height: 200px; }
      .data-panel { display: none; }
      .data-panel.active { display: block; animation: fadeIn 0.3s ease; }
      
      @keyframes fadeIn { from { opacity: 0; transform: translateY(5px); } to { opacity: 1; transform: translateY(0); } }

      .table-container { overflow-x: auto; border-radius: 8px; border: 1px solid rgba(255,255,255,0.05); }
      .data-result-widget table { width: 100%; border-collapse: collapse; font-size: 12px; color: var(--text-main); }
      .data-result-widget th { background: rgba(0,0,0,0.3); padding: 10px 12px; text-align: left; font-weight: 600; color: #fff; }
      .data-result-widget td { padding: 8px 12px; border-bottom: 1px solid rgba(255,255,255,0.05); white-space: nowrap; }
      .data-result-widget tr:nth-child(even) { background: rgba(255,255,255,0.02); }
      .data-result-widget tr:hover { background: rgba(99, 102, 241, 0.1); }

      .chart-controls { display: flex; justify-content: flex-end; margin-bottom: 10px; gap: 8px; }
      .chart-type-select {
        background: rgba(0,0,0,0.4);
        color: #fff;
        border: 1px solid rgba(255,255,255,0.1);
        border-radius: 6px;
        padding: 4px 8px;
        font-size: 12px;
        outline: none;
      }

      .data-chart-canvas { width: 100% !important; height: 220px !important; }
      
      /* Typing Indicator */
      .typing-indicator { display: flex; gap: 5px; padding: 8px 12px; }
      .typing-dot { width: 7px; height: 7px; border-radius: 50%; background: #94a3b8; animation: typingBounce 1.4s ease-in-out infinite; }
      .typing-dot:nth-child(2) { animation-delay: 0.15s; }
      .typing-dot:nth-child(3) { animation-delay: 0.3s; }
      @keyframes typingBounce {
        0%, 60%, 100% { transform: translateY(0); opacity: 0.4; }
        30% { transform: translateY(-6px); opacity: 1; }
      }

      .tool-calling-indicator {
        display: flex;
        align-items: center;
        gap: 10px;
        padding: 10px 14px;
        background: rgba(57, 167, 185, 0.05);
        border-radius: 10px;
        color: var(--primary-color);
        font-size: 13px;
        border: 1px dashed var(--primary-color);
        margin: 4px 0;
      }
      .spin-animation { animation: spin 1s linear infinite; display: inline-block; }
      @keyframes spin { 100% { transform: rotate(360deg); } }
      .live-widget-container { width: 100%; overflow: hidden; }
      .live-simple-result {
        font-size: 11px;
        opacity: 0.8;
        margin-top: 8px;
        overflow-x: auto;
        background: rgba(0,0,0,0.2);
        padding: 8px;
        border-radius: 8px;
        border: 1px solid rgba(255,255,255,0.1);
        color: #cbd5e1;
        max-height: 150px;
        white-space: pre-wrap;
      }
      .agent-side .message-bubble { background: var(--bot-msg-bg, var(--primary-color, #6366f1)); color: var(--bot-msg-text, white); border-radius: var(--bubble-border-radius, 20px); border-bottom-left-radius: 4px; }
      .agent-avatar { background: var(--header-bg, var(--primary-color, #6366f1)); color: var(--header-text, white); }
      .message-image-container { margin-bottom: 8px; border-radius: 8px; overflow: hidden; display: flex; justify-content: flex-start; background: rgba(0,0,0,0.02); }
      .message-image { max-width: 100%; max-height: 400px; height: auto; display: block; border-radius: 8px; object-fit: contain; }
    </style>
    
    <button class="chatbox-toggle-btn" id="fab-toggle" title="Open AI Assistant">
        ${chatbox.icons.awesome}
        <span class="toggle-pulse"></span>
    </button>

    <div class="chatbox-container" id="main-container">
        <div class="chatbox-header" id="drag-header">
            <div class="chatbox-title">
                ${chatbox.icons.awesome}
                <div class="chatbox-title-text-group">
                    <span class="chatbox-title-text">${chatbox.getAttribute("title") || chatbox.config?.theme?.title || chatbox.config?.projectName || "AI Assistant"}</span>
                    ${chatbox.config?.theme?.subtitle ? `<span class="chatbox-subtitle-text">${chatbox.config.theme.subtitle}</span>` : ''}
                </div>
            </div>
            <div class="chatbox-header-actions">
                ${chatbox.config?.liveVoiceEnabled !== false ? `<button class="header-action-btn" id="btn-live" title="Live Voice Mode">${chatbox.icons.voice}</button>` : ''}
                <button class="header-action-btn" id="btn-history" title="Chat history">${chatbox.icons.history}</button>
                <button class="header-action-btn" id="btn-retry" title="Retry last response">${chatbox.icons.refresh}</button>
                <button class="header-action-btn" id="btn-export" title="Copy chat transcript">${chatbox.icons.copy}</button>
                <button class="header-action-btn" id="btn-new" title="New chat">${chatbox.icons.newChat}</button>
                <button class="header-action-btn" id="btn-full" title="Fullscreen">${chatbox.icons.fullscreen}</button>
                <button class="header-action-btn" id="btn-minimize" title="Minimize">${chatbox.icons.minimize}</button>
                <button class="header-action-btn" id="btn-close" title="Close">${chatbox.icons.close}</button>
            </div>
        </div>

        <div class="chatbox-live-view" id="live-overlay">
            <div class="live-status-bar">
                <div class="live-status-left">
                    <div class="live-status-badge badge-connecting" id="live-badge">
                        <span class="live-status-dot"></span>
                        <span id="live-status-text">Connecting...</span>
                    </div>
                </div>
                <div class="live-timer">
                    ${chatbox.icons.timer}
                    <span id="live-timer-text">00:00</span>
                </div>
            </div>
            
            <div class="live-orb-section">
                <canvas id="live-orb-canvas"></canvas>
            </div>

            <div class="live-thinking-bar" id="live-thinking-bar" style="display:none">
                <div class="thinking-icon-anim">${chatbox.icons.lightbulb}</div>
                <span class="thinking-text" id="live-thought-text">Thinking...</span>
            </div>

            <div class="live-error-bar" id="live-error-bar" style="display:none">
                ${chatbox.icons.error}
                <span id="live-error-text" style="flex:1">An error occurred</span>
                <button class="live-reconnect-btn" id="live-reconnect-btn">Reconnect</button>
            </div>

            <div class="live-transcript-area" id="live-transcript">
                <div class="live-transcript-empty">
                    ${chatbox.icons.voice}
                    <p>Speak or type to start the conversation</p>
                </div>
            </div>

            <div class="live-controls-bar glass-controls">
                <div class="live-text-input-row">
                    <textarea class="live-text-field" id="live-text-input" placeholder="${chatbox.config?.theme?.placeholder || "Type a message..."}" rows="1"></textarea>
                    <button class="modern-action-btn live-send-btn" id="live-send-btn">${chatbox.icons.send}</button>
                </div>
                <div class="live-action-buttons">
                    <button class="live-ctrl-btn live-pill-btn live-mute-btn" id="live-mute-btn">
                        ${chatbox.icons.mic}
                        <span id="live-mute-text">Mute</span>
                    </button>
                    <button class="live-ctrl-btn live-pill-btn live-end-btn" id="live-end-btn">
                        ${chatbox.icons.callEnd}
                        <span>End Session</span>
                    </button>
                </div>
            </div>
        </div>

        <div class="chatbox-history-drawer" id="history-drawer">
            <div class="history-header">
                <h3 id="history-header-title">Chat History</h3>
                <button class="header-action-btn" id="btn-history-close" style="color:var(--secondary-text)">${chatbox.icons.close}</button>
            </div>
            <div class="history-tabs">
                <button class="history-tab history-tab-active" id="tab-chats">Chats</button>
                <button class="history-tab" id="tab-archived">Archived</button>
            </div>
            <div class="history-list" id="history-list">
                <!-- Sessions will be loaded here -->
            </div>
        </div>

        <div class="chatbox-messages" id="messages-container">
            <!-- Messages or Empty State will be loaded here -->
        </div>

        <button class="chatbox-scroll-down-btn" id="scroll-down-btn" title="Scroll to bottom" style="display:none">
            <svg width="20" height="20" viewBox="0 0 24 24" fill="currentColor"><path d="M7.41 8.59L12 13.17l4.59-4.58L18 10l-6 6-6-6 1.41-1.41z"/></svg>
        </button>

        <div class="chatbox-input-area">
            <div class="modern-input-wrapper" style="position: relative;">
                <div class="command-autocomplete-dropdown" id="command-dropdown" style="display:none"></div>
                <div class="attachments-row" id="attachments-container" style="display:none"></div>
                
                <div class="input-row">
                    <button class="modern-action-btn" id="btn-attach" title="Attach file">${chatbox.icons.attach}</button>
                    <input type="file" id="file-input" style="display:none">
                    
                    <textarea class="modern-chat-input" id="chat-input" placeholder="${chatbox.config?.theme?.placeholder || "Message AI Assistant..."}" rows="1"></textarea>
                    
                    <button class="modern-send-btn" id="btn-mic" title="Hold to talk">${chatbox.icons.mic}</button>
                    <button class="modern-send-btn" id="btn-send" title="Send message" disabled>${chatbox.icons.send}</button>
                    <button class="modern-send-btn stop-btn" id="btn-stop" style="display:none" title="Stop generation">${chatbox.icons.stop}</button>
                </div>

                <div class="input-footer">
                    <div class="model-selector-wrapper">
                        <select class="modern-model-select" id="model-select">
                            ${(chatbox.config?.enabledModels?.length > 0 ? chatbox.config.enabledModels : [{model: "gemini-3.1-flash-lite-preview", provider: "gemini"}, {model: "gemini-3-flash", provider: "gemini"}, {model: "gemini-2.5-flash-lite", provider: "gemini"}]).map(item => {
                                const name = typeof item === 'string' ? item : item.model;
                                const prov = typeof item === 'string' ? chatbox.provider : item.provider;
                                return `<option value="${name}" data-provider="${prov}" ${name === chatbox.modelName ? 'selected' : ''}>${name.split('-').map(w => w.charAt(0).toUpperCase() + w.slice(1)).join(' ')}</option>`;
                            }).join('')}
                        </select>
                        <select class="modern-model-select" id="voice-select">
                            <option value="Puck">Puck</option>
                            <option value="Charon">Charon</option>
                            <option value="Kore">Kore</option>
                            <option value="Fenrir">Fenrir</option>
                        </select>
                    </div>
                </div>
            </div>
            <div class="chatbox-resize-handle" id="chat-resize-handle"></div>
        </div>
    </div>

    <div class="chatbox-minimized-live" id="mini-live">
        <div class="mini-drag-handle" title="Drag to move" id="pill-drag">${chatbox.icons.drag}</div>
        <div class="mini-orb-container" id="mini-orb-expand" title="Expand Assistant">
            <canvas id="live-orb-canvas-mini" class="mini-orb-canvas"></canvas>
            <div class="mini-status-dot badge-connecting" id="mini-status-dot"></div>
        </div>
        <div class="mini-controls">
            <span class="mini-timer" id="mini-timer-text">00:00</span>
            <div class="mini-actions">
                <button class="mini-action-btn" id="mini-mute-btn" title="Mute">${chatbox.icons.mic}</button>
                <button class="mini-action-btn" id="mini-end-btn" title="End Session" style="color:var(--danger-color)">${chatbox.icons.callEnd}</button>
            </div>
        </div>
    </div>
  `;
}

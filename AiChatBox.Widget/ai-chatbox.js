(function() {
    class AiChatBox extends HTMLElement {
        constructor() {
            super();
            this.attachShadow({ mode: 'open' });
            this.isOpen = false;
            this.isHistoryOpen = false;
            this.isFullscreen = false;
            this.isTyping = false;
            this.sessions = [];
            this.attachments = [];
            this.pastedImage = null;
            this.isRecording = false;
            this.mediaRecorder = null;
            this.audioChunks = [];
            this.abortController = null;
            this.currentSessionId = localStorage.getItem('ai_chat_session_id') || null;
            this.apiUrl = this.getAttribute('api-url') || 'http://localhost:5180';
            this.userId = this.getAttribute('user-id') || 'standalone-user';
            this.provider = this.getAttribute('provider') || 'gemini';
            this.modelName = this.getAttribute('model') || 'gemini-3.1-flash-lite-preview';
            this.systemPrompt = this.getAttribute('system-prompt') || '';
            
            // Live Mode State
            this.isLive = false;
            this.isLiveMuted = false;
            this.liveConnection = null;
            this.audioContext = null;
            this.liveModel = "models/gemini-2.5-flash-native-audio-latest";
            this.isMinimized = false;
            this.audioNextStartTime = 0;
            this.isSignalRLoading = false;
        }

        connectedCallback() {
            this.render();
            if (this.currentSessionId) {
                this.loadSessionMessages(this.currentSessionId);
            }
            this.loadSessions();
        }

        static get observedAttributes() {
            return ['api-url', 'provider', 'model', 'user-id', 'system-prompt'];
        }

        attributeChangedCallback(name, oldValue, newValue) {
            if (name === 'api-url') this.apiUrl = newValue;
            if (name === 'provider') this.provider = newValue;
            if (name === 'model') this.modelName = newValue;
            if (name === 'user-id') this.userId = newValue;
            if (name === 'system-prompt') this.systemPrompt = newValue;
        }

        render() {
            const stylePath = this.getAttribute('css-path') || 'ai-chatbox.css';
            this.shadowRoot.innerHTML = `
                <link rel="stylesheet" href="${stylePath}">
                <div class="chatbox-launcher" id="launcher" title="Open AI Chat">
                    <svg width="28" height="28" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"></path></svg>
                </div>

                <div class="chatbox-container" id="container">
                    <div class="resize-handle" id="resize-handle"></div>
                    
                    <!-- History Drawer -->
                    <div class="history-drawer" id="history-drawer">
                        <div class="drawer-header">
                            <span>Chat History</span>
                            <button class="icon-btn" id="close-history" style="color:black">&times;</button>
                        </div>
                        <div class="history-tabs">
                            <button class="history-tab active" id="tab-active">Chats</button>
                            <button class="history-tab" id="tab-archived">Archived</button>
                        </div>
                        <div class="history-list" id="history-list">
                            <div style="padding:20px; text-align:center; color:#64748b; font-size:12px;">Loading history...</div>
                        </div>
                    </div>
                    <div class="drawer-overlay" id="drawer-overlay"></div>

                    <!-- Header -->
                    <div class="chatbox-header">
                        <div class="header-actions">
                            <button class="icon-btn" id="toggle-history-btn" title="History">
                                <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"></circle><polyline points="12 6 12 12 16 14"></polyline></svg>
                            </button>
                            <button class="icon-btn" id="new-chat-btn" title="New Chat">
                                <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><line x1="12" y1="5" x2="12" y2="19"></line><line x1="5" y1="12" x2="19" y2="12"></line></svg>
                            </button>
                        </div>
                        <div class="chatbox-title">AI Assistant</div>
                        <div class="header-actions">
                            <button class="icon-btn" id="go-live-btn" title="Start Live Voice">
                                <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M12 1a3 3 0 0 0-3 3v8a3 3 0 0 0 6 0V4a3 3 0 0 0-3-3z"></path><path d="M19 10v2a7 7 0 0 1-14 0v-2"></path><line x1="12" y1="19" x2="12" y2="23"></line><line x1="8" y1="23" x2="16" y2="23"></line></svg>
                            </button>
                            <button class="icon-btn" id="fullscreen-btn" title="Fullscreen">
                                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M8 3H5a2 2 0 0 0-2 2v3m18 0V5a2 2 0 0 0-2-2h-3m0 18h3a2 2 0 0 0 2-2v-3M3 16v3a2 2 0 0 0 2 2h3"></path></svg>
                            </button>
                            <button class="icon-btn" id="close-chatbox-btn" title="Close">
                                <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><line x1="18" y1="6" x2="6" y2="18"></line><line x1="6" y1="6" x2="18" y2="18"></line></svg>
                            </button>
                        </div>
                    </div>

                    <!-- Live Overlay -->
                    <div class="live-overlay" id="live-overlay">
                        <div class="live-header">
                            <div class="live-status">
                                <span class="live-indicator"></span>
                                <span id="live-status-text">Connecting...</span>
                            </div>
                            <button class="icon-btn" id="minimize-live-btn" title="Minimize">
                                <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M8 3H5a2 2 0 0 0-2 2v3m18 0V5a2 2 0 0 0-2-2h-3m0 18h3a2 2 0 0 0 2-2v-3M3 16v3a2 2 0 0 0 2 2h3"></path></svg>
                            </button>
                        </div>
                        
                        <div class="live-content">
                            <div class="live-orb-container">
                                <div class="live-orb" id="live-orb"></div>
                                <div class="live-orb-ripple"></div>
                                <div class="live-orb-ripple"></div>
                            </div>
                            <div class="live-transcript" id="live-transcript">
                                <div class="live-transcript-inner">Welcome to Live Mode! Talk to me...</div>
                            </div>
                        </div>

                        <div class="live-footer">
                            <button class="live-action-btn" id="live-mute-btn" title="Mute Microphone">
                                <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M12 1a3 3 0 0 0-3 3v8a3 3 0 0 0 6 0V4a3 3 0 0 0-3-3z"></path><path d="M19 10v2a7 7 0 0 1-14 0v-2"></path><line x1="12" y1="19" x2="12" y2="23"></line><line x1="8" y1="23" x2="16" y2="23"></line></svg>
                            </button>
                            <button class="live-action-btn end-btn" id="live-end-btn" title="End Live Session">
                                <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M10.68 13.31a16 16 0 0 0 3.41 2.6l1.27-1.27a2 2 0 0 1 2.11-.45 12.84 12.84 0 0 0 2.81.7 2 2 0 0 1 1.72 2v3a2 2 0 0 1-2.18 2 19.79 19.79 0 0 1-8.63-3.07 19.42 19.42 0 0 1-3.33-2.67m-2.67-3.34a19.79 19.79 0 0 1-3.07-8.63A2 2 0 0 1 3.11 2h3a2 2 0 0 1 2 1.72 12.84 12.84 0 0 0 .7 2.81 2 2 0 0 1-.45 2.11L7.09 9.91"></path><line x1="23" y1="1" x2="1" y2="23"></line></svg>
                            </button>
                        </div>
                    </div>

                    <!-- Minimized Pill -->
                    <div class="live-pill" id="live-pill">
                        <div class="live-pill-orb"></div>
                        <div class="live-pill-text">Live Session</div>
                        <button class="icon-btn" id="restore-live-btn" aria-label="Restore Live Session">
                            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="15 3 21 3 21 9"></polyline><polyline points="9 21 3 21 3 15"></polyline><line x1="21" y1="3" x2="14" y2="10"></line><line x1="3" y1="21" x2="10" y2="14"></line></svg>
                        </button>
                    </div>

                    <!-- Messages -->
                    <div class="chatbox-messages" id="messages-list">
                        <!-- Empty State Container -->
                        <div id="empty-state-container"></div>
                        <!-- Messages scroll to bottom btn -->
                        <button class="scroll-to-bottom" id="scroll-btn">
                            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="6 9 12 15 18 9"></polyline></svg>
                        </button>
                    </div>

                    <!-- Input Area -->
                    <div class="chatbox-input-area">
                        <div class="attachments-row" id="attachments-row"></div>
                        <div class="modern-input-row">
                            <button type="button" class="attach-btn" id="attach-btn" title="Attach file">
                                <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21.44 11.05l-9.19 9.19a6 6 0 0 1-8.49-8.49l9.19-9.19a4 4 0 0 1 5.66 5.66l-9.2 9.19a2 2 0 0 1-2.83-2.83l8.49-8.48"></path></svg>
                            </button>
                            <input type="file" id="file-input" style="display:none" multiple>
                            
                            <textarea class="chat-input" id="chat-input" placeholder="Type your message..." rows="1"></textarea>
                            
                            <button type="button" class="mic-btn" id="mic-btn" title="Voice Input (Hold to record)">
                                <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M12 1a3 3 0 0 0-3 3v8a3 3 0 0 0 6 0V4a3 3 0 0 0-3-3z"></path><path d="M19 10v2a7 7 0 0 1-14 0v-2"></path><line x1="12" y1="19" x2="12" y2="23"></line><line x1="8" y1="23" x2="16" y2="23"></line></svg>
                            </button>

                            <button type="button" class="send-btn" id="send-btn">
                                <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><line x1="22" y1="2" x2="11" y2="13"></line><polygon points="22 2 15 22 11 13 2 9 22 2"></polygon></svg>
                            </button>

                            <button type="button" class="stop-btn" id="stop-btn" style="display:none" title="Stop Generation">
                                <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="6" y="6" width="12" height="12"></rect></svg>
                            </button>
                        </div>
                        <div class="input-sub-controls">
                            <select class="select-control" id="provider-select">
                                <optgroup label="Gemini">
                                    <option value="gemini|gemini-3.1-flash-lite-preview" selected>Gemini 3.1 Flash Lite</option>
                                    <option value="gemini|gemini-1.5-flash">Gemini 1.5 Flash</option>
                                    <option value="gemini|gemini-1.5-pro">Gemini 1.5 Pro</option>
                                </optgroup>
                                <optgroup label="Groq">
                                    <option value="grok|llama-3.3-70b-versatile">Llama 3.3 70B</option>
                                    <option value="grok|llama-3.1-8b-instant">Llama 3.1 8B</option>
                                    <option value="grok|mixtral-8x7b-32768">Mixtral 8x7B</option>
                                </optgroup>
                            </select>
                            <span class="char-counter" id="char-counter" style="display:none">0/2000</span>
                        </div>
                    </div>
                </div>
            `;

            this.setupEventListeners();
            this.renderEmptyState();
        }

        setupEventListeners() {
            const shadow = this.shadowRoot;
            
            shadow.getElementById('launcher').onclick = (e) => { e.preventDefault(); e.stopPropagation(); this.toggleChat(); };
            shadow.getElementById('close-chatbox-btn').onclick = (e) => { e.preventDefault(); e.stopPropagation(); this.toggleChat(); };
            shadow.getElementById('toggle-history-btn').onclick = (e) => { e.preventDefault(); e.stopPropagation(); this.toggleHistory(); };
            shadow.getElementById('close-history').onclick = (e) => { e.preventDefault(); e.stopPropagation(); this.toggleHistory(); };
            shadow.getElementById('drawer-overlay').onclick = (e) => { e.preventDefault(); e.stopPropagation(); this.toggleHistory(); };
            shadow.getElementById('new-chat-btn').onclick = (e) => { e.preventDefault(); e.stopPropagation(); this.startNewChat(); };
            shadow.getElementById('fullscreen-btn').onclick = (e) => { e.preventDefault(); e.stopPropagation(); this.toggleFullscreen(); };
            
            // Live Mode Listeners
            shadow.getElementById('go-live-btn').onclick = (e) => { e.preventDefault(); e.stopPropagation(); this.toggleLive(); };
            shadow.getElementById('live-mute-btn').onclick = (e) => { e.preventDefault(); e.stopPropagation(); this.toggleLiveMute(); };
            shadow.getElementById('live-end-btn').onclick = (e) => { e.preventDefault(); e.stopPropagation(); this.stopLiveSession(); };
            shadow.getElementById('minimize-live-btn').onclick = (e) => { e.preventDefault(); e.stopPropagation(); this.toggleLiveMinimize(); };
            shadow.getElementById('restore-live-btn').onclick = (e) => { e.preventDefault(); e.stopPropagation(); this.toggleLiveMinimize(); };
            
            const messagesList = shadow.getElementById('messages-list');
            const scrollBtn = shadow.getElementById('scroll-btn');
            messagesList.onscroll = () => {
                const isNearBottom = messagesList.scrollHeight - messagesList.scrollTop - messagesList.clientHeight < 100;
                scrollBtn.classList.toggle('visible', !isNearBottom);
            };
            scrollBtn.onclick = () => {
                messagesList.scrollTo({ top: messagesList.scrollHeight, behavior: 'smooth' });
            };

            const input = shadow.getElementById('chat-input');
            const charCounter = shadow.getElementById('char-counter');
            input.onkeydown = (e) => {
                if (e.key === 'Enter' && !e.shiftKey) {
                    e.preventDefault();
                    e.stopPropagation();
                    this.sendMessage();
                }
            };
            input.oninput = () => {
                input.style.height = 'auto';
                input.style.height = input.scrollHeight + 'px';
                
                const len = input.value.length;
                if (len > 0) {
                    charCounter.style.display = 'inline';
                    charCounter.textContent = `${len}/2000`;
                    charCounter.classList.toggle('limit-reached', len > 2000);
                } else {
                    charCounter.style.display = 'none';
                }
            };

            shadow.getElementById('send-btn').onclick = (e) => {
                e.preventDefault();
                e.stopPropagation();
                this.sendMessage();
            };

            shadow.getElementById('stop-btn').onclick = (e) => {
                e.preventDefault();
                e.stopPropagation();
                this.stopGeneration();
            };

            const micBtn = shadow.getElementById('mic-btn');
            micBtn.onmousedown = (e) => { e.preventDefault(); this.startRecording(); };
            micBtn.onmouseup = (e) => { e.preventDefault(); this.stopRecording(); };
            micBtn.onmouseleave = (e) => { if (this.isRecording) this.stopRecording(); };
            // Touch support
            micBtn.ontouchstart = (e) => { e.preventDefault(); this.startRecording(); };
            micBtn.ontouchend = (e) => { e.preventDefault(); this.stopRecording(); };

            shadow.getElementById('provider-select').onchange = (e) => {
                const val = e.target.value;
                const [prov, model] = val.split('|');
                this.provider = prov;
                this.modelName = model;
            };

            // History Tabs
            shadow.getElementById('tab-active').onclick = () => {
                shadow.getElementById('tab-active').classList.add('active');
                shadow.getElementById('tab-archived').classList.remove('active');
                this.renderHistoryList(false);
            };
            shadow.getElementById('tab-archived').onclick = () => {
                shadow.getElementById('tab-active').classList.remove('active');
                shadow.getElementById('tab-archived').classList.add('active');
                this.renderHistoryList(true);
            };

            // Resize handle logic
            const container = shadow.getElementById('container');
            const handle = shadow.getElementById('resize-handle');
            let isResizing = false;
            
            handle.onmousedown = (e) => {
                isResizing = true;
                document.addEventListener('mousemove', handleMouseMove);
                document.addEventListener('mouseup', () => {
                    isResizing = false;
                    document.removeEventListener('mousemove', handleMouseMove);
                });
            };

            const handleMouseMove = (e) => {
                if (!isResizing || this.isFullscreen) return;
                const rect = container.getBoundingClientRect();
                const newWidth = rect.right - e.clientX;
                const newHeight = rect.bottom - e.clientY;
                if (newWidth > 340) container.style.width = newWidth + 'px';
                if (newHeight > 400) container.style.height = newHeight + 'px';
            };

            // File Uploads
            const fileInput = shadow.getElementById('file-input');
            shadow.getElementById('attach-btn').onclick = () => fileInput.click();
            fileInput.onchange = (e) => {
                const files = Array.from(e.target.files);
                files.forEach(file => this.uploadFile(file));
                fileInput.value = '';
            };

            // Image Paste
            input.onpaste = (e) => {
                const items = (e.clipboardData || e.originalEvent.clipboardData).items;
                for (const item of items) {
                    if (item.type.indexOf('image') !== -1) {
                        const blob = item.getAsFile();
                        const reader = new FileReader();
                        reader.onload = (event) => {
                            this.pastedImage = event.target.result;
                            this.renderAttachments();
                        };
                        reader.readAsDataURL(blob);
                    }
                }
            };
        }

        toggleChat() {
            this.isOpen = !this.isOpen;
            this.shadowRoot.getElementById('container').classList.toggle('open', this.isOpen);
            if (this.isOpen) {
                this.shadowRoot.getElementById('chat-input').focus();
                if (!this.currentSessionId) this.renderEmptyState();
            }
        }

        toggleHistory() {
            this.isHistoryOpen = !this.isHistoryOpen;
            this.shadowRoot.getElementById('history-drawer').classList.toggle('open', this.isHistoryOpen);
            this.shadowRoot.getElementById('drawer-overlay').classList.toggle('visible', this.isHistoryOpen);
            if (this.isHistoryOpen) this.loadSessions();
        }

        toggleFullscreen() {
            this.isFullscreen = !this.isFullscreen;
            this.shadowRoot.getElementById('container').classList.toggle('fullscreen', this.isFullscreen);
            const btn = this.shadowRoot.getElementById('fullscreen-btn');
            btn.innerHTML = this.isFullscreen 
                ? '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M8 3v3a2 2 0 0 1-2 2H3m18 0h-3a2 2 0 0 1-2-2V3m0 18v-3a2 2 0 0 1 2-2h3M3 16h3a2 2 0 0 1 2 2v3"></path></svg>'
                : '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M8 3H5a2 2 0 0 0-2 2v3m18 0V5a2 2 0 0 0-2-2h-3m0 18h3a2 2 0 0 0 2-2v-3M3 16v3a2 2 0 0 0 2 2h3"></path></svg>';
        }

        stopGeneration() {
            if (this.abortController) {
                this.abortController.abort();
                this.abortController = null;
                this.isTyping = false;
                this.updateSendButton();
            }
        }

        clearMessages(list) {
            if (!list) return;
            const children = [...list.children];
            children.forEach(c => {
                if (c.id !== 'empty-state-container' && c.id !== 'scroll-btn') c.remove();
            });
        }

        renderEmptyState() {
            let container = this.shadowRoot.getElementById('empty-state-container');
            const messagesList = this.shadowRoot.getElementById('messages-list');
            
            if (!messagesList) return;
            this.clearMessages(messagesList);

            if (!container) {
                container = document.createElement('div');
                container.id = 'empty-state-container';
                messagesList.appendChild(container);
            }

            container.style.display = 'flex';
            container.className = 'empty-state';
            container.innerHTML = `
                <div class="empty-state-icon">
                    <svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"></path></svg>
                </div>
                <h3>How can I help you?</h3>
                <p>Start a conversation with your AI assistant.</p>
                <div class="suggestion-chips" id="suggestion-chips"></div>
            `;

            const suggestions = JSON.parse(this.getAttribute('suggestions') || '["What can you do?", "Analyze my data", "Tell me a joke", "Write a summary"]');
            const chips = container.querySelector('#suggestion-chips');
            suggestions.forEach(text => {
                const chip = document.createElement('button');
                chip.className = 'suggestion-chip';
                chip.textContent = text;
                chip.onclick = () => {
                    this.shadowRoot.getElementById('chat-input').value = text;
                    this.sendMessage();
                };
                chips.appendChild(chip);
            });
        }

        startNewChat() {
            this.currentSessionId = null;
            localStorage.removeItem('ai_chat_session_id');
            this.renderEmptyState();
            if (this.isHistoryOpen) this.toggleHistory();
        }

        async loadSessions() {
            try {
                const isArchived = this.shadowRoot.getElementById('tab-archived').classList.contains('active');
                const endpoint = isArchived ? 'archived' : 'sessions';
                const response = await fetch(`${this.apiUrl}/api/chat/${endpoint}`, {
                    headers: { 'X-User-Id': this.userId }
                });
                if (!response.ok) return;
                this.sessions = await response.json();
                this.renderHistoryList(isArchived);
            } catch (err) {
                console.error("Failed to load sessions", err);
            }
        }

        renderHistoryList(isArchived = false) {
            const list = this.shadowRoot.getElementById('history-list');
            if (this.sessions.length === 0) {
                list.innerHTML = `<div style="padding:20px; text-align:center; color:#64748b; font-size:12px;">No ${isArchived ? 'archived ' : ''}chats found.</div>`;
                return;
            }

            list.innerHTML = this.sessions.map(s => {
                const id = s.Id || s.id;
                const title = s.Title || s.title || 'New Chat';
                const date = s.LastMessageAt || s.lastMessageAt;
                return `
                    <div class="history-item ${id === this.currentSessionId ? 'active' : ''}" data-id="${id}">
                        <div class="history-item-title">${title}</div>
                        <div class="history-item-date">${new Date(date).toLocaleDateString()}</div>
                        <button class="history-item-delete" data-id="${id}" title="${isArchived ? 'Delete Forever' : 'Archive'}">
                            ${isArchived ? '&times;' : '&#128230;'}
                        </button>
                    </div>
                `;
            }).join('');

            list.querySelectorAll('.history-item').forEach(item => {
                item.onclick = (e) => {
                    const id = item.dataset.id;
                    if (e.target.classList.contains('history-item-delete')) {
                        if (isArchived) this.hardDeleteSession(id);
                        else this.archiveSession(id);
                        return;
                    }
                    this.loadSessionMessages(id);
                    this.toggleHistory();
                };
            });
        }

        async archiveSession(id) {
            try {
                await fetch(`${this.apiUrl}/api/chat/sessions/${id}/archive`, { 
                    method: 'POST',
                    headers: { 'X-User-Id': this.userId }
                });
                if (this.currentSessionId === id) this.startNewChat();
                this.loadSessions();
            } catch (err) {
                console.error("Failed to archive", err);
            }
        }

        async hardDeleteSession(id) {
            if (!confirm("Delete this chat forever?")) return;
            try {
                await fetch(`${this.apiUrl}/api/chat/sessions/${id}/hard`, { 
                    method: 'DELETE',
                    headers: { 'X-User-Id': this.userId }
                });
                this.loadSessions();
            } catch (err) {
                console.error("Failed to delete", err);
            }
        }

        copyToClipboard(text, btn) {
            navigator.clipboard.writeText(text).then(() => {
                btn.classList.add('copied');
                const originalHtml = btn.innerHTML;
                btn.innerHTML = '<svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="20 6 9 17 4 12"></polyline></svg>';
                setTimeout(() => {
                    btn.classList.remove('copied');
                    btn.innerHTML = originalHtml;
                }, 2000);
            });
        }

        regenerateMessage(originalUserMessage) {
            // Find last user message if not provided
            this.shadowRoot.getElementById('chat-input').value = originalUserMessage;
            this.sendMessage();
        }

        async uploadFile(file) {
            const formData = new FormData();
            formData.append('file', file);

            try {
                const response = await fetch(`${this.apiUrl}/api/file/upload`, {
                    method: 'POST',
                    headers: { 'X-User-Id': this.userId },
                    body: formData
                });
                if (response.ok) {
                    const data = await response.json();
                    this.attachments.push(data);
                    this.renderAttachments();
                }
            } catch (err) {
                console.error("Upload failed", err);
            }
        }

        renderAttachments() {
            const row = this.shadowRoot.getElementById('attachments-row');
            row.innerHTML = '';

            if (this.pastedImage) {
                const pill = document.createElement('div');
                pill.className = 'attachment-pill';
                pill.innerHTML = `
                    <img src="${this.pastedImage}" class="attachment-image-thumb">
                    <span class="attachment-pill-name">Pasted Image</span>
                    <button class="attachment-remove">&times;</button>
                `;
                pill.querySelector('.attachment-remove').onclick = () => {
                    this.pastedImage = null;
                    this.renderAttachments();
                };
                row.appendChild(pill);
            }

            this.attachments.forEach((file, index) => {
                const pill = document.createElement('div');
                pill.className = 'attachment-pill';
                pill.innerHTML = `
                    <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M13 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V9z"></path><polyline points="13 2 13 9 20 9"></polyline></svg>
                    <span class="attachment-pill-name">${file.Name || file.name}</span>
                    <button class="attachment-remove">&times;</button>
                `;
                pill.querySelector('.attachment-remove').onclick = () => {
                    this.attachments.splice(index, 1);
                    this.renderAttachments();
                };
                row.appendChild(pill);
            });
        }

        async transcribeAudio(blob) {
            const formData = new FormData();
            formData.append('audio', blob, 'recording.wav');

            try {
                const response = await fetch(`${this.apiUrl}/api/audio/transcribe`, {
                    method: 'POST',
                    headers: { 'X-User-Id': this.userId },
                    body: formData
                });
                if (response.ok) {
                    const data = await response.json();
                    if (data.text) {
                        this.shadowRoot.getElementById('chat-input').value = data.text;
                        this.sendMessage();
                    }
                }
            } catch (err) {
                console.error("Transcription failed", err);
            }
        }

        async speakText(text, btn) {
            btn.classList.add('playing');
            try {
                const response = await fetch(`${this.apiUrl}/api/audio/tts`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json', 'X-User-Id': this.userId },
                    body: JSON.stringify({ text })
                });
                if (response.ok) {
                    const blob = await response.blob();
                    const url = URL.createObjectURL(blob);
                    const audio = new Audio(url);
                    audio.onended = () => btn.classList.remove('playing');
                    audio.play();
                } else {
                    btn.classList.remove('playing');
                }
            } catch (err) {
                console.error("TTS failed", err);
                btn.classList.remove('playing');
            }
        }

        async startRecording() {
            if (this.isRecording) return;
            try {
                const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
                this.mediaRecorder = new MediaRecorder(stream);
                this.audioChunks = [];
                this.mediaRecorder.ondataavailable = (e) => this.audioChunks.push(e.data);
                this.mediaRecorder.onstop = () => {
                    const blob = new Blob(this.audioChunks, { type: 'audio/wav' });
                    this.transcribeAudio(blob);
                };
                this.mediaRecorder.start();
                this.isRecording = true;
                this.shadowRoot.getElementById('container').classList.add('recording');
            } catch (err) {
                console.error("Microphone access denied", err);
            }
        }

        stopRecording() {
            if (!this.isRecording) return;
            this.mediaRecorder.stop();
            this.isRecording = false;
            this.shadowRoot.getElementById('container').classList.remove('recording');
        }

        async loadSessionMessages(sessionId) {
            this.currentSessionId = sessionId;
            localStorage.setItem('ai_chat_session_id', sessionId);
            
            const list = this.shadowRoot.getElementById('messages-list');
            if (!list) return;
            this.clearMessages(list);
            
            const loading = document.createElement('div');
            loading.style.cssText = "padding:20px; text-align:center; color:#64748b; font-size:12px;";
            loading.textContent = "Loading messages...";
            loading.className = "loading-placeholder";
            list.appendChild(loading);

            try {
                const response = await fetch(`${this.apiUrl}/api/chat/sessions/${sessionId}`);
                if (!response.ok) throw new Error("Session not found");
                const messages = await response.json();
                
                this.clearMessages(list);
                messages.forEach(m => {
                    const role = m.Role || m.role;
                    const content = m.Content || m.content;
                    this.addMessage(role, content, false);
                });
                this.scrollToBottom();
            } catch (err) {
                this.addMessage('ai', "Error loading chat history.");
            }
        }

        async sendMessage() {
            const input = this.shadowRoot.getElementById('chat-input');
            const text = input.value.trim();
            if (!text && !this.pastedImage && !this.attachments.length) return;
            if (this.isTyping) return;

            input.value = '';
            input.style.height = 'auto';
            const charCounter = this.shadowRoot.getElementById('char-counter');
            if (charCounter) charCounter.style.display = 'none';
            
            const attachedFileId = this.attachments.length > 0 ? this.attachments[0].id : null;
            const imageDataUrl = this.pastedImage;

            this.addMessage('user', text || (imageDataUrl ? '[Image]' : '[File]'));
            
            // Clear attachments
            this.attachments = [];
            this.pastedImage = null;
            this.renderAttachments();

            this.isTyping = true;
            this.updateSendButton();

            const aiWrapper = this.addMessage('ai', '<div class="typing-indicator"><span class="typing-dot"></span><span class="typing-dot"></span><span class="typing-dot"></span></div>');
            const bubble = aiWrapper.querySelector('.message-bubble');

            try {
                this.abortController = new AbortController();
                const response = await fetch(`${this.apiUrl}/api/chat`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json', 'X-User-Id': this.userId },
                    signal: this.abortController.signal,
                    body: JSON.stringify({
                        message: text,
                        sessionId: this.currentSessionId,
                        provider: this.provider,
                        modelName: this.modelName,
                        systemPrompt: this.systemPrompt,
                        attachedFileId: attachedFileId,
                        imageDataUrl: imageDataUrl
                    })
                }).catch(err => {
                    throw new Error(`Connection failed: ${err.message}. Check if API is running and CORS is enabled.`);
                });

                if (!response.ok) {
                    const errText = await response.text();
                    throw new Error(`API Error (${response.status}): ${errText || 'Unknown error'}`);
                }

                const reader = response.body.getReader();
                const decoder = new TextDecoder();
                let fullText = '';
                bubble.innerHTML = '';

                while (true) {
                    const { done, value } = await reader.read();
                    if (done) break;

                    const chunk = decoder.decode(value);
                    const lines = chunk.split('\n');

                    for (const line of lines) {
                        if (line.startsWith('data: ')) {
                            try {
                                const data = JSON.parse(line.substring(6));
                                const sid = data.SessionId || data.sessionId;
                                const text = data.Text || data.text;
                                const err = data.Error || data.error;

                                if (sid && !this.currentSessionId) {
                                    this.currentSessionId = sid;
                                    localStorage.setItem('ai_chat_session_id', this.currentSessionId);
                                }
                                if (text) {
                                    fullText += text;
                                    bubble.innerHTML = this.formatMarkdown(fullText);
                                    this.scrollToBottom();
                                }
                                if (err) {
                                    bubble.innerHTML = `<span style="color:red">${err}</span>`;
                                }
                            } catch (e) {}
                        }
                    }
                }
            } catch (err) {
                bubble.innerHTML = `<span style="color:red">Error: ${err.message}</span>`;
            } finally {
                this.isTyping = false;
                this.abortController = null;
                this.updateSendButton();
            }
        }

        addMessage(role, text, scroll = true) {
            const list = this.shadowRoot.getElementById('messages-list');
            const emptyState = this.shadowRoot.getElementById('empty-state-container');
            if (emptyState) emptyState.style.display = 'none';

            const wrapper = document.createElement('div');
            wrapper.className = `message-wrapper ${role === 'user' ? 'user' : 'ai'}`;
            
            const avatarSvg = role === 'user' 
                ? '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"></path><circle cx="12" cy="7" r="4"></circle></svg>'
                : '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polygon points="12 2 15.09 8.26 22 9.27 17 14.14 18.18 21.02 12 17.77 5.82 21.02 7 14.14 2 9.27 8.91 8.26 12 2"></polygon></svg>';

            const actions = role === 'ai' ? `
                <div class="message-actions">
                    <button class="msg-action-btn copy-btn" title="Copy message">
                        <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="9" y="9" width="13" height="13" rx="2" ry="2"></rect><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"></path></svg>
                    </button>
                    <button class="msg-action-btn tts-btn" title="Listen">
                        <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polygon points="11 5 6 9 2 9 2 15 6 15 11 19 11 5"></polygon><path d="M19.07 4.93a10 10 0 0 1 0 14.14M15.54 8.46a5 5 0 0 1 0 7.07"></path></svg>
                    </button>
                    <button class="msg-action-btn regen-btn" title="Regenerate">
                        <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="23 4 23 10 17 10"></polyline><path d="M20.49 15a9 9 0 1 1-2.12-9.36L23 10"></path></svg>
                    </button>
                </div>
            ` : '';

            wrapper.innerHTML = `
                <div class="message-avatar ${role}-avatar">${avatarSvg}</div>
                <div class="message-content-wrap">
                    <div class="message-bubble">${this.formatMarkdown(text)}</div>
                    ${actions}
                </div>
            `;
            
            list.appendChild(wrapper);

            if (role === 'ai') {
                const bubble = wrapper.querySelector('.message-bubble');
                wrapper.querySelector('.copy-btn').onclick = () => this.copyToClipboard(bubble.innerText, wrapper.querySelector('.copy-btn'));
                wrapper.querySelector('.tts-btn').onclick = () => this.speakText(bubble.innerText, wrapper.querySelector('.tts-btn'));
                wrapper.querySelector('.regen-btn').onclick = () => this.regenerateMessage(text);
            }

            if (scroll) this.scrollToBottom();
            return wrapper;
        }

        formatMarkdown(text) {
            if (!text) return '';
            let html = text;
            // Code blocks (``` ... ```)
            html = html.replace(/```(\w*)?\n?([\s\S]*?)```/g, (_, lang, code) =>
                `<pre><code>${code.replace(/</g,'&lt;').replace(/>/g,'&gt;').trim()}</code></pre>`);
            // Inline code
            html = html.replace(/`([^`]+)`/g, '<code>$1</code>');
            // Headers
            html = html.replace(/^#{3}\s+(.+)$/gm, '<h3>$1</h3>');
            html = html.replace(/^#{2}\s+(.+)$/gm, '<h2>$1</h2>');
            html = html.replace(/^#{1}\s+(.+)$/gm, '<h1>$1</h1>');
            // Bold & italic
            html = html.replace(/\*\*\*(.*?)\*\*\*/g, '<strong><em>$1</em></strong>');
            html = html.replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>');
            html = html.replace(/(?<![*])\*(?![*])(.*?)(?<![*])\*(?![*])/g, '<em>$1</em>');
            // Blockquotes
            html = html.replace(/^>\s+(.+)$/gm, '<blockquote>$1</blockquote>');
            // Horizontal rule
            html = html.replace(/^---$/gm, '<hr>');
            // Links
            html = html.replace(/\[([^\]]+)\]\(([^)]+)\)/g, '<a href="$2" target="_blank" rel="noopener">$1</a>');
            // Unordered lists
            html = html.replace(/^[\-\*]\s+(.+)$/gm, '<li>$1</li>');
            html = html.replace(/((?:<li>.*<\/li>\n?)+)/g, '<ul>$1</ul>');
            // Ordered lists
            html = html.replace(/^\d+\.\s+(.+)$/gm, '<li>$1</li>');
            // Tables
            html = html.replace(/^\|(.+)\|\s*\n\|[\s\-|]+\|\s*\n((?:\|.+\|\s*\n?)*)/gm, (_, header, body) => {
                const ths = header.split('|').filter(h => h.trim()).map(h => `<th>${h.trim()}</th>`).join('');
                const rows = body.trim().split('\n').map(row => {
                    const tds = row.replace(/^\||\|$/g,'').split('|').map(c => `<td>${c.trim()}</td>`).join('');
                    return `<tr>${tds}</tr>`;
                }).join('');
                return `<table><thead><tr>${ths}</tr></thead><tbody>${rows}</tbody></table>`;
            });
            // Line breaks (but not inside pre/table/ul/ol)
            html = html.replace(/\n/g, '<br>');
            // Clean up extra br after block elements
            html = html.replace(/<\/(pre|h[1-6]|ul|ol|table|blockquote|hr|li)><br>/g, '</$1>');
            html = html.replace(/<br><(pre|h[1-6]|ul|ol|table|blockquote|hr|li)/g, '<$1');
            return html;
        }

        scrollToBottom() {
            const list = this.shadowRoot.getElementById('messages-list');
            if (list) list.scrollTop = list.scrollHeight;
        }

        updateSendButton() {
            const sendBtn = this.shadowRoot.getElementById('send-btn');
            const stopBtn = this.shadowRoot.getElementById('stop-btn');
            const micBtn = this.shadowRoot.getElementById('mic-btn');
            const liveBtn = this.shadowRoot.getElementById('go-live-btn');
            
            if (this.isTyping) {
                sendBtn.style.display = 'none';
                stopBtn.style.display = 'flex';
                micBtn.style.display = 'none';
            } else {
                sendBtn.style.display = 'flex';
                stopBtn.style.display = 'none';
                micBtn.style.display = 'flex';
                sendBtn.disabled = false;
            }
            
            if (liveBtn) liveBtn.classList.toggle('active', this.isLive);
        }

        async toggleLive() {
            if (this.isLive) {
                this.stopLiveSession();
            } else {
                await this.startLiveSession();
            }
        }

        async startLiveSession() {
            try {
                this.isLive = true;
                this.isMinimized = false;
                this.shadowRoot.getElementById('container').classList.add('live-active');
                this.shadowRoot.getElementById('live-status-text').textContent = "Connecting...";
                this.updateSendButton();

                // 1. Ensure SignalR is loaded
                if (typeof signalR === 'undefined' && !this.isSignalRLoading) {
                    this.isSignalRLoading = true;
                    await this.loadScript('https://cdnjs.cloudflare.com/ajax/libs/microsoft-signalr/8.0.0/signalr.min.js');
                    this.isSignalRLoading = false;
                }

                // 2. Setup Connection
                if (this.liveConnection) await this.liveConnection.stop();

                this.liveConnection = new signalR.HubConnectionBuilder()
                    .withUrl(`${this.apiUrl}/liveAudioHub`)
                    .withAutomaticReconnect()
                    .build();

                this.liveConnection.on("ReceiveAudioChunk", (data) => this.handleLiveAudioChunk(data));
                this.liveConnection.on("ReceiveTextChunk", (text) => this.handleLiveTextChunk(text));
                this.liveConnection.on("StopAudio", () => {
                    this.audioNextStartTime = 0;
                    // Note: We don't stop the current buffer easily here without keeping refs
                });
                this.liveConnection.on("ReceiveError", (msg) => {
                    console.error("Live Error:", msg);
                    this.shadowRoot.getElementById('live-status-text').textContent = "Error: " + msg;
                });

                await this.liveConnection.start();
                await this.liveConnection.invoke("StartLive", this.userId, this.liveModel);
                
                this.shadowRoot.getElementById('live-status-text').textContent = "Live";

                // 3. Setup Audio
                await this.setupLiveAudio();

            } catch (err) {
                console.error("Live Session failed", err);
                this.stopLiveSession();
            }
        }

        async stopLiveSession() {
            this.isLive = false;
            this.isMinimized = false;
            const container = this.shadowRoot.getElementById('container');
            if (container) {
                container.classList.remove('live-active');
                container.classList.remove('live-minimized');
            }
            
            if (this.liveConnection) {
                await this.liveConnection.stop();
                this.liveConnection = null;
            }
            
            if (this.audioContext) {
                await this.audioContext.close();
                this.audioContext = null;
            }
            
            this.updateSendButton();
        }

        async setupLiveAudio() {
            this.audioContext = new (window.AudioContext || window.webkitAudioContext)({ sampleRate: 16000 });
            
            // AudioWorklet for recording
            await this.audioContext.audioWorklet.addModule(`${this.apiUrl}/audio-processor.js`);
            
            const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
            const source = this.audioContext.createMediaStreamSource(stream);
            const processor = new AudioWorkletNode(this.audioContext, 'audio-processor');

            processor.port.onmessage = (e) => {
                if (this.isLive && !this.isLiveMuted && this.liveConnection) {
                    this.liveConnection.invoke("SendAudio", e.data);
                }
            };

            source.connect(processor);
            processor.connect(this.audioContext.destination);
        }

        handleLiveAudioChunk(base64Data) {
            // Playback AI response
            if (!this.audioContext || !this.isLive) return;
            
            const bytes = typeof base64Data === 'string' ? Uint8Array.from(atob(base64Data), c => c.charCodeAt(0)) : new Uint8Array(base64Data);
            const floatData = new Float32Array(bytes.length / 2);
            const view = new DataView(bytes.buffer);
            for (let i = 0; i < floatData.length; i++) {
                floatData[i] = view.getInt16(i * 2, true) / 32768.0;
            }

            const buffer = this.audioContext.createBuffer(1, floatData.length, 16000);
            buffer.getChannelData(0).set(floatData);
            
            const source = this.audioContext.createBufferSource();
            source.buffer = buffer;
            source.connect(this.audioContext.destination);

            // Jitter buffer / precise scheduling
            const now = this.audioContext.currentTime;
            if (this.audioNextStartTime < now) {
                this.audioNextStartTime = now + 0.1; // 100ms buffer
            }
            
            source.start(this.audioNextStartTime);
            this.audioNextStartTime += buffer.duration;

            // Animate Orb
            this.animateOrb(true);
        }

        handleLiveTextChunk(text) {
            const inner = this.shadowRoot.getElementById('live-transcript-inner');
            if (inner.textContent.includes("Welcome to Live Mode")) inner.textContent = "";
            inner.textContent += text;
            const transcript = this.shadowRoot.getElementById('live-transcript');
            transcript.scrollTop = transcript.scrollHeight;
        }

        toggleLiveMute() {
            this.isLiveMuted = !this.isLiveMuted;
            const btn = this.shadowRoot.getElementById('live-mute-btn');
            btn.classList.toggle('muted', this.isLiveMuted);
            btn.innerHTML = this.isLiveMuted ? 
                '<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><line x1="1" y1="1" x2="23" y2="23"></line><path d="M9 9v3a3 3 0 0 0 5.12 2.12M15 9.34V4a3 3 0 0 0-5.94-.6"></path><path d="M17 16.95A7 7 0 0 1 5 12v-2m14 0v2a7 7 0 0 1-.11 1.23"></path><line x1="12" y1="19" x2="12" y2="23"></line><line x1="8" y1="23" x2="16" y2="23"></line></svg>' :
                '<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M12 1a3 3 0 0 0-3 3v8a3 3 0 0 0 6 0V4a3 3 0 0 0-3-3z"></path><path d="M19 10v2a7 7 0 0 1-14 0v-2"></path><line x1="12" y1="19" x2="12" y2="23"></line><line x1="8" y1="23" x2="16" y2="23"></line></svg>';
        }

        toggleLiveMinimize() {
            this.isMinimized = !this.isMinimized;
            const container = this.shadowRoot.getElementById('container');
            container.classList.toggle('live-minimized', this.isMinimized);
        }

        animateOrb(isSpeaking) {
            const orb = this.shadowRoot.getElementById('live-orb');
            if (isSpeaking) {
                orb.classList.add('speaking');
                setTimeout(() => orb.classList.remove('speaking'), 200);
            }
        }

        loadScript(url) {
            return new Promise((resolve, reject) => {
                const script = document.createElement('script');
                script.src = url;
                script.onload = resolve;
                script.onerror = reject;
                document.head.appendChild(script);
            });
        }
    }

    customElements.define('ai-chatbox', AiChatBox);
})();

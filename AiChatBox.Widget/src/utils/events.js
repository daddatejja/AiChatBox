export function setupEventListeners(chatbox) {
  const root = chatbox.shadowRoot;

  // Toggle Chatbox
  root.getElementById("fab-toggle").onclick = () => chatbox.toggleChat();
  root.getElementById("btn-close").onclick = () => chatbox.toggleChat();

  // Header Actions
  root.getElementById("btn-full").onclick = () => chatbox.toggleFullscreen();
  root.getElementById("btn-minimize").onclick = () => chatbox.toggleChat();
  root.getElementById("btn-history").onclick = () => chatbox.toggleHistory();
  root.getElementById("btn-history-close").onclick = () => chatbox.toggleHistory();
  root.getElementById("btn-new").onclick = () => chatbox.startNewChat();
  
  const liveBtn = root.getElementById("btn-live");
  if (liveBtn) liveBtn.onclick = () => chatbox.toggleLiveMode();

  // Input Actions
  root.getElementById("btn-send").onclick = () => chatbox.sendMessage();
  root.getElementById("btn-stop").onclick = () => chatbox.stopGeneration();
  
  const chatInput = root.getElementById("chat-input");

  // Click away hides autocomplete
  const onDocumentClick = (e) => {
    const dropdown = root.getElementById("command-dropdown");
    if (dropdown && !e.target.closest(".modern-input-wrapper")) {
      chatbox.hideAutocompleteDropdown();
    }
  };
  document.addEventListener("click", onDocumentClick);

  chatInput.onkeydown = (e) => {
    const dropdown = root.getElementById("command-dropdown");
    const isDropdownVisible = dropdown && dropdown.style.display === "flex";
    
    if (isDropdownVisible) {
      const items = dropdown.querySelectorAll(".command-item");
      if (e.key === "ArrowDown") {
        e.preventDefault();
        chatbox.activeCommandIndex = (chatbox.activeCommandIndex + 1) % items.length;
        chatbox.updateAutocompleteActiveItem(items);
      } else if (e.key === "ArrowUp") {
        e.preventDefault();
        chatbox.activeCommandIndex = (chatbox.activeCommandIndex - 1 + items.length) % items.length;
        chatbox.updateAutocompleteActiveItem(items);
      } else if (e.key === "Enter" || e.key === "Tab") {
        e.preventDefault();
        const activeItem = dropdown.querySelector(".command-item.active");
        if (activeItem) {
          const name = activeItem.getAttribute("data-name");
          chatbox.selectCommand(name);
        } else if (items.length > 0) {
          const name = items[0].getAttribute("data-name");
          chatbox.selectCommand(name);
        }
      } else if (e.key === "Escape") {
        e.preventDefault();
        chatbox.hideAutocompleteDropdown();
      }
    } else {
      if (e.key === "Enter" && !e.shiftKey) {
        e.preventDefault();
        chatbox.sendMessage();
      }
    }
  };

  const onPaste = (e) => {
    const items = (e.clipboardData || e.originalEvent.clipboardData).items;
    for (let index in items) {
      const item = items[index];
      if (item.kind === 'file' && item.type.startsWith('image/')) {
        const blob = item.getAsFile();
        const reader = new FileReader();
        reader.onload = (event) => {
          chatbox.attachments = [];
          chatbox.pastedImage = {
            data: event.target.result,
            type: item.type
          };
          chatbox.renderAttachments();
        };
        reader.readAsDataURL(blob);
        e.preventDefault();
        break;
      }
    }
  };
  chatInput.addEventListener('paste', onPaste);

  chatInput.oninput = (e) => {
    chatbox.adjustTextAreaHeight(e.target);
    chatbox.updateSendButtonState();
    chatbox.handleUserTyping();

    const value = e.target.value;
    const cursorPosition = e.target.selectionStart || value.length;
    const textBeforeCursor = value.substring(0, cursorPosition);
    
    const match = textBeforeCursor.match(/(?:^|\s)([\/@#])([a-zA-Z0-9_-]*)$/);
    
    if (match) {
      const triggerChar = match[1];
      const filterText = match[2].toLowerCase();
      
      const matchIndex = textBeforeCursor.length - match[0].length + (match[0].startsWith(' ') || match[0].startsWith('\n') ? 1 : 0);
      const matchLength = textBeforeCursor.length - matchIndex;

      chatbox.lastCommandMatch = {
        index: matchIndex,
        length: matchLength,
        triggerChar: triggerChar
      };

      const matched = (chatbox.commands || []).filter(c => 
        (c.commandTriggerChar || '/').trim() === triggerChar && 
        c.commandName.toLowerCase().startsWith(filterText)
      );

      if (matched.length > 0) {
        chatbox.renderAutocompleteDropdown(matched, triggerChar);
      } else {
        chatbox.hideAutocompleteDropdown();
      }
    } else {
      chatbox.hideAutocompleteDropdown();
    }
  };

  // Attachment Actions
  root.getElementById("btn-attach").onclick = () => root.getElementById("file-input").click();
  root.getElementById("file-input").onchange = (e) => chatbox.handleFileSelection(e);

  // Mic Button (Hold to talk)
  const micBtn = root.getElementById("btn-mic");
  micBtn.onmousedown = () => chatbox.voiceRecorder.start();
  micBtn.onmouseup = () => chatbox.voiceRecorder.stop();
  micBtn.onmouseleave = () => chatbox.voiceRecorder.cancel();

  // Live View Actions
  root.getElementById("live-send-btn").onclick = () => chatbox.sendLiveTextMessage();
  const liveTextInput = root.getElementById("live-text-input");
  liveTextInput.onkeydown = (e) => {
    if (e.key === "Enter" && !e.shiftKey) {
      e.preventDefault();
      chatbox.sendLiveTextMessage();
    }
  };
  liveTextInput.oninput = (e) => chatbox.adjustTextAreaHeight(e.target);

  root.getElementById("live-mute-btn").onclick = () => chatbox.toggleLiveMute();
  root.getElementById("live-end-btn").onclick = () => chatbox.stopLiveSession();
  root.getElementById("live-reconnect-btn").onclick = () => chatbox.reconnectLiveSession();

  // Minimized Live Actions
  root.getElementById("mini-orb-expand").onclick = () => chatbox.toggleChat();
  root.getElementById("mini-mute-btn").onclick = () => chatbox.toggleLiveMute();
  root.getElementById("mini-end-btn").onclick = () => chatbox.stopLiveSession();

  // History Tabs
  root.getElementById("tab-chats").onclick = () => chatbox.switchHistoryTab("chats");
  root.getElementById("tab-archived").onclick = () => chatbox.switchHistoryTab("archived");

  // Scroll Down Button
  const messagesContainer = root.getElementById("messages-container");
  const onMessagesScroll = () => chatbox.handleMessagesScroll();
  messagesContainer.onscroll = onMessagesScroll;
  root.getElementById("scroll-down-btn").onclick = () => chatbox.scrollToBottom();

  // Return cleanup function to clean up document-level listener and dynamic listeners
  return () => {
    document.removeEventListener("click", onDocumentClick);
    if (chatInput) {
      chatInput.removeEventListener('paste', onPaste);
    }
  };
}

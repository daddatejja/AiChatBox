export function setupDraggable(chatbox) {
  const root = chatbox.shadowRoot;
  const container = root.getElementById("main-container");
  const header = root.getElementById("drag-header");
  const miniPill = root.getElementById("mini-live");
  const miniHandle = root.getElementById("pill-drag");

  let isDragging = false, startX, startY, initTop, initLeft;

  header.onmousedown = (e) => {
    if (e.target.closest("button") || chatbox.isFullscreen) return;
    isDragging = true;
    const rect = container.getBoundingClientRect();
    container.style.bottom = "auto";
    container.style.right = "auto";
    container.style.top = rect.top + "px";
    container.style.left = rect.left + "px";
    startX = e.clientX;
    startY = e.clientY;
    initTop = rect.top;
    initLeft = rect.left;
    e.preventDefault();
  };

  miniHandle.onmousedown = (e) => {
    isDragging = "mini";
    const rect = miniPill.getBoundingClientRect();
    miniPill.style.bottom = "auto";
    miniPill.style.right = "auto";
    miniPill.style.top = rect.top + "px";
    miniPill.style.left = rect.left + "px";
    startX = e.clientX;
    startY = e.clientY;
    initTop = rect.top;
    initLeft = rect.left;
    e.preventDefault();
  };

  document.addEventListener("mousemove", (e) => {
    if (!isDragging) return;
    const dx = e.clientX - startX;
    const dy = e.clientY - startY;

    if (isDragging === true) {
      container.style.left = Math.max(0, Math.min(initLeft + dx, window.innerWidth - container.offsetWidth)) + "px";
      container.style.top = Math.max(0, Math.min(initTop + dy, window.innerHeight - container.offsetHeight)) + "px";
    } else if (isDragging === "mini") {
      miniPill.style.left = Math.max(0, Math.min(initLeft + dx, window.innerWidth - miniPill.offsetWidth)) + "px";
      miniPill.style.top = Math.max(0, Math.min(initTop + dy, window.innerHeight - miniPill.offsetHeight)) + "px";
    }
  });

  document.addEventListener("mouseup", () => (isDragging = false));

  // Resize Logic
  const resizeHandle = root.getElementById("chat-resize-handle");
  let isResizing = false;
  let initWidth, initHeight;

  resizeHandle.onmousedown = (e) => {
    isResizing = true;
    startX = e.clientX;
    startY = e.clientY;
    const rect = container.getBoundingClientRect();
    initWidth = rect.width;
    initHeight = rect.height;
    e.preventDefault();
  };

  document.addEventListener("mousemove", (e) => {
    if (isResizing) {
      const dx = e.clientX - startX;
      const dy = e.clientY - startY;
      container.style.width = Math.max(320, initWidth + dx) + "px";
      container.style.height = Math.max(400, initHeight + dy) + "px";
    }
  });

  document.addEventListener("mouseup", () => (isResizing = false));
}

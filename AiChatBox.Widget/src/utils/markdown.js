import { marked } from 'marked';
import hljs from 'highlight.js';
import DOMPurify from 'dompurify';
import { icons } from '../icons.js';

// Configure marked with highlight.js
marked.setOptions({
  highlight: (code, lang) => {
    if (hljs) {
      if (lang && hljs.getLanguage(lang)) {
        return hljs.highlight(code, { language: lang }).value;
      }
      return hljs.highlightAuto(code).value;
    }
    return code;
  },
  breaks: true,
  gfm: true,
  headerIds: false,
  mangle: false
});

export function formatMarkdown(text) {
  if (!text) return "";
  
  try {
    let html = marked.parse(text);
    
    // Code block copy wrapper logic
    const tempDiv = document.createElement('div');
    tempDiv.innerHTML = html;
    
    tempDiv.querySelectorAll('pre code').forEach(codeEl => {
      const pre = codeEl.parentElement;
      const code = codeEl.innerText;
      const id = 'code-' + Math.random().toString(36).substr(2, 9);
      const langMatch = codeEl.className.match(/language-(\w+)/);
      const lang = langMatch ? langMatch[1] : 'code';
      
      const wrapper = document.createElement('div');
      wrapper.className = 'code-block-wrapper';
      wrapper.innerHTML = `
        <div class="code-header">
          <span>${lang.toUpperCase()}</span>
          <button class="copy-code-btn" data-code-id="${id}">${icons.copy} Copy</button>
        </div>
        <pre><code id="${id}" class="${codeEl.className}">${codeEl.innerHTML}</code></pre>
      `;
      pre.parentNode.replaceChild(wrapper, pre);
    });
    
    return sanitizeHtml(tempDiv.innerHTML);
  } catch (e) {
    console.warn("Markdown parsing failed, falling back to basic rendering", e);
  }

  // Fallback to basic rendering if marked is not available or fails
  let html = text.replace(/\*\*(.*?)\*\*/g, "<strong>$1</strong>")
                 .replace(/`([^`]+)`/g, "<code>$1</code>")
                 .replace(/\n/g, "<br>");
  
  html = html.replace(/<pre><code>([\s\S]*?)<\/code><\/pre>|```([\s\S]*?)```/g, (match, p1, p2) => {
    const code = (p1 || p2 || "").replace(/<br>/g, "\n");
    const id = 'code-' + Math.random().toString(36).substr(2, 9);
    return `
      <div class="code-block-wrapper">
        <div class="code-header">
          <span>Code</span>
          <button class="copy-code-btn" data-code-id="${id}">${icons.copy} Copy</button>
        </div>
        <pre><code id="${id}">${code}</code></pre>
      </div>
    `;
  });

  return sanitizeHtml(html);
}

export function sanitizeHtml(html) {
  if (!html) return "";
  
  // Use DOMPurify for industry-standard sanitization
  const clean = DOMPurify.sanitize(html, {
    ADD_TAGS: ['use', 'svg'], // Ensure SVGs in widgets and codes are preserved
    ADD_ATTR: ['target', 'download', 'onclick'] // Retain critical custom element widget behavior
  });

  // Preserve the existing DOMParser secondary scrub for extra custom script mitigation
  const parser = new DOMParser();
  const doc = parser.parseFromString(clean, "text/html");
  const body = doc.body;
  
  const scripts = body.querySelectorAll("script");
  scripts.forEach(s => s.remove());
  
  const allElements = body.querySelectorAll("*");
  allElements.forEach(el => {
    const attrs = Array.from(el.attributes);
    attrs.forEach(attr => {
      if (attr.name.toLowerCase().startsWith("on") && attr.name !== "onclick") {
        el.removeAttribute(attr.name);
      }
      if (["href", "src", "action"].includes(attr.name.toLowerCase())) {
        const val = attr.value.trim().toLowerCase();
        if (val.startsWith("javascript:") || val.startsWith("data:text/html")) {
          el.removeAttribute(attr.name);
        }
      }
    });

    const name = el.tagName.toLowerCase();
    if (["iframe", "object", "embed", "frame", "frameset", "base"].includes(name)) {
      el.remove();
    }
  });

  return body.innerHTML;
}

export function escapeHtml(str) {
  if (str === null || str === undefined) return "";
  if (typeof str !== "string") {
    return String(str);
  }
  return str
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&#039;");
}

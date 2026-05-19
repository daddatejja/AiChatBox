<script setup lang="ts">
import { ref, onMounted, watch, onUnmounted } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { VueFlow, useVueFlow, Handle, Position } from '@vue-flow/core';
import { Background } from '@vue-flow/background';
import { Controls } from '@vue-flow/controls';
import { MiniMap } from '@vue-flow/minimap';
import dagre from 'dagre';
import '@vue-flow/core/dist/style.css';
import '@vue-flow/core/dist/theme-default.css';
import '@vue-flow/controls/dist/style.css';
import '@vue-flow/minimap/dist/style.css';
import { useApi } from '../../composables/useApi';
import './FlowBuilder.css';
import {
  PALETTE_ITEMS,
  getDefaultConfig,
  getNodeLabel,
  type NodeType,
  type SimLog,
  type SimMessage,
  type HistorySnapshot,
  type ReplayStep,
} from './flowBuilder.types';

// ── Router & API ───────────────────────────────────────────────────────────────
const route      = useRoute();
const router     = useRouter();
const { apiFetch } = useApi();
const projectId  = route.params.projectId as string;
const { addNodes, addEdges, onConnect, nodes, edges, onNodeDragStop } = useVueFlow();

// ── Flow Meta ──────────────────────────────────────────────────────────────────
const flowId      = ref<string | null>(route.params.flowId as string || null);
const flowName    = ref('New Flow');
const flowTrigger = ref('');
const isActive    = ref(true);

// ── Selection ──────────────────────────────────────────────────────────────────
const selectedNode = ref<any>(null);
const selectedEdge = ref<any>(null);

// ── History (Undo / Redo) ──────────────────────────────────────────────────────
const historyStack = ref<HistorySnapshot[]>([]);
const redoStack    = ref<HistorySnapshot[]>([]);
let isRestoring    = false;

const takeSnapshot = () => {
  if (isRestoring) return;
  const snapshot: HistorySnapshot = {
    nodes: JSON.stringify(nodes.value.map(n => ({
      id: n.id, type: n.type,
      position: { ...n.position },
      data: JSON.parse(JSON.stringify(n.data)),
    }))),
    edges: JSON.stringify(edges.value.map(e => ({
      id: e.id, source: e.source, target: e.target,
      label: e.label, sourceHandle: e.sourceHandle,
    }))),
  };
  if (historyStack.value.length > 0) {
    const top = historyStack.value[historyStack.value.length - 1];
    if (top.nodes === snapshot.nodes && top.edges === snapshot.edges) return;
  }
  historyStack.value.push(snapshot);
  if (historyStack.value.length > 50) historyStack.value.shift();
  redoStack.value = [];
};

const restoreFromSnapshot = (state: HistorySnapshot) => {
  isRestoring = true;
  try {
    const restoredNodes = JSON.parse(state.nodes);
    const restoredEdges = JSON.parse(state.edges);
    
    // Clear and re-add nodes to preserve VueFlow metadata
    nodes.value.forEach(n => {
      const restored = restoredNodes.find((rn: any) => rn.id === n.id);
      if (restored) {
        n.position = restored.position;
        n.type = restored.type;
        n.data = restored.data;
      }
    });
    
    // Remove nodes that shouldn't exist
    nodes.value = nodes.value.filter(n => 
      restoredNodes.some((rn: any) => rn.id === n.id)
    );
    
    // Add new nodes
    const newNodes = restoredNodes.filter((rn: any) => 
      !nodes.value.some(n => n.id === rn.id)
    );
    if (newNodes.length > 0) {
      addNodes(newNodes);
    }
    
    // Restore edges
    edges.value = restoredEdges;
    
    selectedNode.value = null;
    selectedEdge.value = null;
  } catch (e) {
    console.error('Failed to restore snapshot:', e);
  }
  setTimeout(() => { isRestoring = false; }, 100);
};

const undo = () => {
  if (historyStack.value.length <= 1) return;
  const current = historyStack.value.pop();
  if (current) redoStack.value.push(current);
  const previous = historyStack.value[historyStack.value.length - 1];
  if (previous) restoreFromSnapshot(previous);
};

const redo = () => {
  const next = redoStack.value.pop();
  if (next) { historyStack.value.push(next); restoreFromSnapshot(next); }
};

const handleKeyDown = (e: KeyboardEvent) => {
  const active = document.activeElement;
  if (active && (active.tagName === 'INPUT' || active.tagName === 'TEXTAREA' || active.getAttribute('contenteditable') === 'true')) return;
  if (e.ctrlKey && e.key.toLowerCase() === 'z') {
    e.preventDefault();
    e.shiftKey ? redo() : undo();
  } else if (e.ctrlKey && e.key.toLowerCase() === 'y') {
    e.preventDefault();
    redo();
  }
};

let changeTimeout: any = null;
watch(() => nodes.value.map(n => JSON.stringify(n.data)), () => {
  if (isRestoring) return;
  if (changeTimeout) clearTimeout(changeTimeout);
  changeTimeout = setTimeout(takeSnapshot, 800);
}, { deep: true });

onNodeDragStop(() => takeSnapshot());

// ── Auto-layout ────────────────────────────────────────────────────────────────
const autoLayout = () => {
  const g = new dagre.graphlib.Graph();
  g.setGraph({ rankdir: 'LR', align: 'DL', nodesep: 60, edgesep: 30, ranksep: 100 });
  g.setDefaultEdgeLabel(() => ({}));
  nodes.value.forEach(n => g.setNode(n.id, { width: 220, height: 130 }));
  edges.value.forEach(e => g.setEdge(e.source, e.target));
  dagre.layout(g);
  nodes.value.forEach(n => {
    const pos = g.node(n.id);
    if (pos) n.position = { x: Math.round(pos.x - 110), y: Math.round(pos.y - 65) };
  });
  takeSnapshot();
};

const removeNode = (nodeId: string) => {
  nodes.value = nodes.value.filter(n => n.id !== nodeId);
  edges.value = edges.value.filter(e => e.source !== nodeId && e.target !== nodeId);
  selectedNode.value = null;
  takeSnapshot();
};

// ── Simulator ──────────────────────────────────────────────────────────────────
const showSimulator     = ref(false);
const simMessages       = ref<SimMessage[]>([]);
const simInput          = ref('');
const isSimulating      = ref(false);
const activeSimNodeId   = ref<string | null>(null);
const activeSimEdgeId   = ref<string | null>(null);
const simVariables      = ref<Record<string, string>>({});
const simLogs           = ref<SimLog[]>([]);

watch(activeSimEdgeId, (newId) => {
  edges.value.forEach(e => {
    if (e.id === newId) { e.class = 'active-sim-edge'; e.animated = true; }
    else                { e.class = '';                e.animated = false; }
  });
});

const addSimLog = (type: SimLog['type'], message: string) => {
  simLogs.value.push({ time: new Date().toLocaleTimeString(), type, message });
};

const toggleSimulator = () => {
  showSimulator.value = !showSimulator.value;
  if (showSimulator.value && simMessages.value.length === 0) resetSimulator();
};

const resetSimulator = () => {
  activeSimNodeId.value = null;
  activeSimEdgeId.value = null;
  simVariables.value = {};
  simLogs.value = [];
  simMessages.value = [
    { role: 'ai', content: `Simulator ready — type a message matching keyword "${flowTrigger.value || '[empty]'}" to start.` },
  ];
  addSimLog('info', 'Simulator initialized. Awaiting trigger keyword match.');
};

const executeNodeStep = async (nodeId: string, incomingMsg: string) => {
  isSimulating.value = true;
  activeSimNodeId.value = nodeId;
  const node = nodes.value.find(n => n.id === nodeId);
  if (!node) { addSimLog('error', `Node ${nodeId} not found`); isSimulating.value = false; return; }

  addSimLog('info', `Active: "${node.data.label}"`);

  if (node.type === 'trigger') {
    addSimLog('success', `Triggered: [${(node.data.config?.triggerType || 'keyword').toUpperCase()}]`);
    await transitionToNext(node.id, incomingMsg);
  } else if (node.type === 'richresponse') {
    const resType = node.data.config?.responseType || 'card';
    await new Promise(r => setTimeout(r, 800));
    let contentStr = '';
    if (resType === 'card')     contentStr = `🎴 [Card] ${node.data.config?.title || 'Card Title'} — ${node.data.config?.body || ''}`;
    else if (resType === 'redirect') contentStr = `🔗 [Redirect] ${node.data.config?.url || ''} (${node.data.config?.seconds || 5}s)`;
    else if (resType === 'file')     contentStr = `📁 [File] ${node.data.config?.fileName || 'file'}`;
    else if (resType === 'form')     contentStr = `📝 [Form] ${node.data.config?.title || 'Form'}`;
    else if (resType === 'buttons')  contentStr = `🔘 [Quick Reply Buttons]: ` + (node.data.config?.buttons || []).map((b: any) => `[${b.label}]`).join(' ');
    simMessages.value.push({ role: 'ai', content: contentStr });
    addSimLog('success', `Rich Response sent: ${resType}`);
    await transitionToNext(node.id, incomingMsg);
  } else if (node.type === 'message') {
    let text = node.data.config?.text || 'Hello!';
    for (const [k, v] of Object.entries(simVariables.value)) text = text.replace(new RegExp(`\\{\\{\\s*${k}\\s*\\}\\}`, 'gi'), v);
    await new Promise(r => setTimeout(r, 800));
    simMessages.value.push({ role: 'ai', content: text });
    addSimLog('success', `Sent: "${text}"`);
    await transitionToNext(node.id, incomingMsg);
  } else if (node.type === 'input') {
    if (incomingMsg !== '') {
      const varName = node.data.config?.variableName || 'user_input';
      simVariables.value[varName] = incomingMsg;
      addSimLog('success', `Saved {{${varName}}} = "${incomingMsg}"`);
      await transitionToNext(node.id, incomingMsg);
    } else {
      const promptText = node.data.config?.promptText || '';
      if (promptText) {
        let text = promptText;
        for (const [k, v] of Object.entries(simVariables.value)) {
          text = text.replace(new RegExp(`\\{\\{\\s*${k}\\s*\\}\\}`, 'gi'), v);
        }
        simMessages.value.push({ role: 'ai', content: text });
        addSimLog('info', `Sent inline prompt: "${text}"`);
      }
      addSimLog('pending', `Waiting for user input → {{${node.data.config?.variableName || 'variable'}}}`);
      isSimulating.value = false;
    }
  } else if (node.type === 'ai') {
    let prompt = node.data.config?.prompt || 'You are an AI.';
    for (const [k, v] of Object.entries(simVariables.value)) prompt = prompt.replace(new RegExp(`\\{\\{\\s*${k}\\s*\\}\\}`, 'gi'), v);
    addSimLog('info', `LLM inference with prompt: "${prompt}"`);
    await new Promise(r => setTimeout(r, 1400));
    const mockReply = `Resolved prompt successfully (Classified or Extracted value).`;
    if (node.data.config?.runInBackground) {
      const storeVar = node.data.config?.storeVariableName || 'ai_result';
      simVariables.value[storeVar] = mockReply;
      addSimLog('success', `AI background execution completed. Saved {{${storeVar}}} = "${mockReply}"`);
    } else {
      simMessages.value.push({ role: 'ai', content: `🤖 [Simulated AI Result]: ${mockReply}` });
      addSimLog('success', 'AI generation complete.');
    }
    await transitionToNext(node.id, incomingMsg);
  } else if (node.type === 'webhook') {
    const url = node.data.config?.url || 'https://api.example.com';
    addSimLog('info', `POST → ${url}`);
    await new Promise(r => setTimeout(r, 1000));
    addSimLog('success', 'POST 200 OK');
    await transitionToNext(node.id, incomingMsg);
  } else if (node.type === 'switch') {
    const varName    = node.data.config?.variableName || 'user_input';
    const currentVal = simVariables.value[varName] || '';
    addSimLog('info', `Evaluating Multi-way Switch on {{${varName}}} (value: "${currentVal}")`);
    const outgoingEdges = edges.value.filter(e => e.source === node.id);
    let matchedEdge: any = null;
    for (const edge of outgoingEdges) {
      const cond = edge.label?.toString() || '';
      if (!cond || cond.toLowerCase() === 'default') continue;
      const parts = cond.split(':');
      const op = parts[0]?.trim().toLowerCase() || 'equals';
      const val = parts.slice(1).join(':')?.trim().toLowerCase() || parts[0]?.trim().toLowerCase() || '';
      let match = false;
      if (op === 'equals') match = currentVal.toLowerCase() === val.toLowerCase();
      else if (op === 'contains') match = currentVal.toLowerCase().includes(val.toLowerCase());
      else if (op === 'regex') { try { match = new RegExp(val, 'i').test(currentVal); } catch (e) {} }
      else match = currentVal.toLowerCase() === cond.toLowerCase();
      if (match) { matchedEdge = edge; break; }
    }
    if (!matchedEdge) {
      matchedEdge = outgoingEdges.find(e => {
        const cond = e.label?.toString().toLowerCase() || '';
        return !cond || cond === 'default';
      });
    }
    if (matchedEdge) {
      activeSimEdgeId.value = matchedEdge.id;
      addSimLog('success', `Switch matched route: [${matchedEdge.label || 'default'}]`);
      await new Promise(r => setTimeout(r, 800));
      await executeNodeStep(matchedEdge.target, incomingMsg);
    } else {
      addSimLog('error', 'No outgoing switch route matched');
      isSimulating.value = false;
    }
  } else if (node.type === 'condition') {
    const varName    = node.data.config?.variableName || 'user_input';
    const op         = node.data.config?.operator || 'equals';
    const val        = node.data.config?.value || '';
    const currentVal = simVariables.value[varName] || '';
    addSimLog('info', `Evaluating: {{${varName}}} ${op} "${val}"`);
    let isTrue = false;
    if      (op === 'equals')      isTrue = currentVal.toLowerCase() === val.toLowerCase();
    else if (op === 'contains')    isTrue = currentVal.toLowerCase().includes(val.toLowerCase());
    else if (op === 'exists')      isTrue = currentVal.trim() !== '';
    else if (op === 'greaterthan') isTrue = parseFloat(currentVal) > parseFloat(val);
    else if (op === 'lessthan')    isTrue = parseFloat(currentVal) < parseFloat(val);
    else if (op === 'regex')       { try { isTrue = new RegExp(val, 'i').test(currentVal); } catch { addSimLog('error', `Invalid regex: ${val}`); } }
    addSimLog('success', `Result: ${isTrue ? 'TRUE' : 'FALSE'}`);
    const outgoing   = edges.value.filter(e => e.source === node.id);
    const target     = isTrue ? 'true' : 'false';
    const matched    = outgoing.find(e => e.sourceHandle === target || e.label?.toString().toLowerCase() === target) || outgoing[0];
    if (matched) { activeSimEdgeId.value = matched.id; await new Promise(r => setTimeout(r, 800)); await executeNodeStep(matched.target, incomingMsg); }
    else         { addSimLog('error', `No branch for [${target.toUpperCase()}]`); isSimulating.value = false; }
  }
};

const transitionToNext = async (sourceId: string, currentMsg: string) => {
  const outgoing = edges.value.filter(e => e.source === sourceId);
  if (outgoing.length === 0) {
    addSimLog('success', 'Flow execution complete.');
    activeSimNodeId.value = null; activeSimEdgeId.value = null; isSimulating.value = false;
    return;
  }
  let matched: any = null;
  for (const edge of outgoing) {
    const cond = edge.label?.toString() || '';
    if (!cond) { matched = edge; continue; }
    const parts = cond.split(':');
    if (parts.length < 2) continue;
    const op  = parts[0].trim().toLowerCase();
    const val = parts.slice(1).join(':').trim().toLowerCase();
    const inp = currentMsg.trim().toLowerCase();
    if (op === 'equals' && inp === val)   { matched = edge; break; }
    if (op === 'contains' && inp.includes(val)) { matched = edge; break; }
    if (op === 'regex') { try { if (new RegExp(val,'i').test(currentMsg)) { matched = edge; break; } } catch { addSimLog('error', `Invalid regex: ${val}`); } }
  }
  if (!matched && outgoing.length > 0) matched = outgoing[0];
  if (matched) {
    activeSimEdgeId.value = matched.id;
    addSimLog('info', `Following: [${matched.label || 'default'}]`);
    await new Promise(r => setTimeout(r, 600));
    await executeNodeStep(matched.target, '');
  } else {
    addSimLog('success', 'Path ended.');
    activeSimNodeId.value = null; activeSimEdgeId.value = null; isSimulating.value = false;
  }
};

const sendSimMessage = () => {
  if (!simInput.value.trim()) return;
  const msg = simInput.value;
  simMessages.value.push({ role: 'user', content: msg });
  simInput.value = '';
  if (activeSimNodeId.value) {
    executeNodeStep(activeSimNodeId.value, msg);
  } else {
    const triggerNode = nodes.value.find(n => n.type === 'trigger');
    if (!triggerNode) {
      addSimLog('error', 'No Trigger node on canvas.');
      return;
    }
    
    const triggerType = triggerNode.data.config?.triggerType || 'keyword';
    const normalized  = msg.toLowerCase();
    let shouldTrigger = false;
    
    if (triggerType === 'onstart') {
      shouldTrigger = true;
    } else if (triggerType === 'keyword') {
      const keyword = (triggerNode.data.config?.keyword || flowTrigger.value || '').toLowerCase();
      if (keyword === '') {
        // Empty keyword matches any message
        shouldTrigger = true;
      } else {
        shouldTrigger = normalized.includes(keyword);
      }
    } else if (triggerType === 'command') {
      const command = (triggerNode.data.config?.command || '').toLowerCase();
      shouldTrigger = command && normalized.startsWith(command);
    }
    
    if (shouldTrigger) {
      executeNodeStep(triggerNode.id, msg);
    } else {
      simMessages.value.push({ role: 'ai', content: `No trigger match. Expected: "${triggerNode.data.config?.keyword || flowTrigger.value || 'any message'}"` });
    }
  }
};

// ── Replay ─────────────────────────────────────────────────────────────────────
const replayLogId        = route.query.replayLogId as string || null;
const isReplayMode       = ref(false);
const replaySteps        = ref<ReplayStep[]>([]);
const currentReplayIndex = ref(-1);
const activeReplayNodeId = ref<string | null>(null);
const isPlayingReplay    = ref(false);
let   replayInterval: any = null;

const loadReplayLog = async () => {
  if (!replayLogId) return;
  try {
    const res = await apiFetch(`/api/projects/${projectId}/flows/execution-logs/${replayLogId}`);
    if (res.ok) {
      const data = await res.json();
      replaySteps.value = JSON.parse(data.stepsJson || '[]');
      isReplayMode.value = true;
      if (replaySteps.value.length > 0) selectReplayStep(0);
    }
  } catch (e) { console.error('Failed to load replay', e); }
};

const selectReplayStep = (index: number) => {
  if (index < 0 || index >= replaySteps.value.length) return;
  currentReplayIndex.value = index;
  activeReplayNodeId.value = replaySteps.value[index].NodeId;
};

const nextReplayStep = () => {
  if (currentReplayIndex.value < replaySteps.value.length - 1) selectReplayStep(currentReplayIndex.value + 1);
  else { isPlayingReplay.value = false; if (replayInterval) { clearInterval(replayInterval); replayInterval = null; } }
};

const prevReplayStep = () => { if (currentReplayIndex.value > 0) selectReplayStep(currentReplayIndex.value - 1); };

const togglePlayReplay = () => {
  isPlayingReplay.value = !isPlayingReplay.value;
  if (isPlayingReplay.value) {
    if (currentReplayIndex.value >= replaySteps.value.length - 1) selectReplayStep(0);
    replayInterval = setInterval(nextReplayStep, 2000);
  } else {
    if (replayInterval) { clearInterval(replayInterval); replayInterval = null; }
  }
};

const exitReplayMode = () => {
  isReplayMode.value = false; isPlayingReplay.value = false; activeReplayNodeId.value = null;
  if (replayInterval) { clearInterval(replayInterval); replayInterval = null; }
  router.replace({ query: {} });
};

// ── DnD ───────────────────────────────────────────────────────────────────────
const onDragStart = (event: DragEvent, nodeType: string) => {
  if (event.dataTransfer) { event.dataTransfer.setData('application/vueflow', nodeType); event.dataTransfer.effectAllowed = 'move'; }
};
const onDragOver = (event: DragEvent) => { event.preventDefault(); if (event.dataTransfer) event.dataTransfer.dropEffect = 'move'; };

const onDrop = (event: DragEvent) => {
  const type = event.dataTransfer?.getData('application/vueflow') as NodeType | undefined;
  if (!type) return;
  const position = { x: event.clientX - 280, y: event.clientY - 120 };
  const id = `node_${Date.now()}`;
  addNodes([{ id, type, position, data: { customType: type, label: getNodeLabel(type), config: getDefaultConfig(type) } }]);
};

// ── Edges & Clicks ─────────────────────────────────────────────────────────────
onConnect((params) => {
  const p = params as any;
  let label = p.label || '';
  if (p.sourceHandle === 'true' || p.sourceHandle === 'false') label = p.sourceHandle;
  addEdges([{ ...p, id: `edge_${Date.now()}`, label }]);
});

const onNodeClick  = (e: any) => { selectedNode.value = e.node; selectedEdge.value = null; };
const onEdgeClick  = (e: any) => { selectedEdge.value = e.edge; selectedNode.value = null; };
const onPaneClick  = ()        => { selectedNode.value = null;   selectedEdge.value = null; };

// ── Load / Save Flow ───────────────────────────────────────────────────────────
const loadFlow = async () => {
  if (!flowId.value) return;
  try {
    const res = await apiFetch(`/api/projects/${projectId}/flows/${flowId.value}`);
    if (!res.ok) return;
    const detail = await res.json();
    flowName.value    = detail.name;
    flowTrigger.value = detail.triggerKeyword || '';
    isActive.value    = detail.isActive;
    nodes.value = [];
    edges.value = [];
    addNodes(detail.nodes.map((n: any) => {
      const parsed = JSON.parse(n.dataJson);
      if (n.type === 'trigger') {
        parsed.config = parsed.config || { triggerType: 'keyword', keyword: detail.triggerKeyword || '' };
        if (parsed.config.triggerType === 'keyword' && !parsed.config.keyword) parsed.config.keyword = detail.triggerKeyword || '';
      }
      return { id: n.id, position: { x: n.positionX, y: n.positionY }, data: parsed, type: parsed.customType || 'default' };
    }));
    addEdges(detail.edges.map((e: any) => ({
      id: e.id, source: e.sourceNodeId, target: e.targetNodeId,
      label: e.condition || '',
      sourceHandle: (e.condition === 'true' || e.condition === 'false') ? e.condition : null,
    })));
    takeSnapshot();
  } catch (e) { console.error('Failed to load flow', e); }
};

const saveFlow = async () => {
  const triggerNode = nodes.value.find(n => n.type === 'trigger');
  let saveTriggerVal = flowTrigger.value;
  if (triggerNode?.data.config) {
    const tType = triggerNode.data.config.triggerType || 'keyword';
    if      (tType === 'keyword') saveTriggerVal = triggerNode.data.config.keyword  || saveTriggerVal;
    else if (tType === 'command') saveTriggerVal = triggerNode.data.config.command  || saveTriggerVal;
    else if (tType === 'onstart') saveTriggerVal = 'onStart';
  }
  const flowData = {
    name: flowName.value,
    triggerKeyword: saveTriggerVal,
    isActive: isActive.value,
    nodes: nodes.value.map(n => ({ id: n.id, type: n.data.customType || 'default', dataJson: JSON.stringify(n.data), positionX: n.position.x, positionY: n.position.y })),
    edges: edges.value.map(e => ({ id: e.id, sourceNodeId: e.source, targetNodeId: e.target, condition: e.sourceHandle || e.label?.toString() || '' })),
  };
  const url    = flowId.value ? `/api/projects/${projectId}/flows/${flowId.value}` : `/api/projects/${projectId}/flows`;
  const method = flowId.value ? 'PUT' : 'POST';
  try {
    const res = await apiFetch(url, { method, body: JSON.stringify(flowData) });
    if (res.ok && !flowId.value) { const created = await res.json(); flowId.value = created.id; }
    alert('Flow saved!');
  } catch (e) { console.error('Save failed', e); alert('Error saving flow.'); }
};

// ── Sync trigger node ↔ flowTrigger ───────────────────────────────────────────
watch(flowTrigger, (newVal) => {
  const triggerNode = nodes.value.find(n => n.type === 'trigger');
  if (triggerNode?.data?.config) {
    const tType = triggerNode.data.config.triggerType || 'keyword';
    if      (tType === 'keyword') triggerNode.data.config.keyword = newVal;
    else if (tType === 'command') triggerNode.data.config.command = newVal;
  }
});

// ── Lifecycle ──────────────────────────────────────────────────────────────────
onMounted(async () => {
  await loadFlow();
  if (replayLogId) await loadReplayLog();
  window.addEventListener('keydown', handleKeyDown);
});
onUnmounted(() => window.removeEventListener('keydown', handleKeyDown));

// ── Palette grouping helper ────────────────────────────────────────────────────
const CATEGORIES = ['Foundations', 'Interactivity', 'Logic'] as const;
</script>

<template>
  <div class="flow-builder-root">

    <!-- ═══════════════════════════════════════════════
         LEFT PALETTE SIDEBAR
         ═══════════════════════════════════════════════ -->
    <aside class="palette-sidebar">
      <div class="palette-header">
        <span class="palette-title">Node Palette</span>
      </div>

      <div v-for="cat in CATEGORIES" :key="cat" class="palette-section">
        <div class="palette-category">{{ cat }}</div>
        <div
          v-for="item in PALETTE_ITEMS.filter(p => p.category === cat)"
          :key="item.type"
          class="palette-item"
          :data-type="item.type"
          draggable="true"
          @dragstart="onDragStart($event, item.type)"
        >
          <span class="item-icon"><i :class="item.icon"></i></span>
          {{ item.label }}
        </div>
      </div>
    </aside>

    <!-- ═══════════════════════════════════════════════
         MAIN CONTENT
         ═══════════════════════════════════════════════ -->
    <div class="main-column">

      <!-- ── Topbar ───────────────────────────────── -->
      <header class="topbar">
        <!-- Flow name -->
        <div class="topbar-field">
          <i class="pi pi-sitemap" style="color:var(--text-accent)"></i>
          <input v-model="flowName" placeholder="Flow Name" class="topbar-input" />
        </div>

        <!-- Trigger keyword -->
        <div class="topbar-field">
          <i class="pi pi-key"></i>
          <input v-model="flowTrigger" placeholder="Trigger keyword" class="topbar-input" style="min-width:130px" />
        </div>

        <div class="topbar-divider"></div>

        <!-- Undo / Redo -->
        <button
          class="tb-btn tb-btn-history"
          :disabled="historyStack.length <= 1"
          @click="undo"
          title="Undo (Ctrl+Z)"
        ><i class="pi pi-undo"></i></button>
        <button
          class="tb-btn tb-btn-history"
          :disabled="redoStack.length === 0"
          @click="redo"
          title="Redo (Ctrl+Y)"
        ><i class="pi pi-refresh"></i></button>

        <div class="topbar-divider"></div>

        <!-- Auto-layout -->
        <button class="tb-btn" @click="autoLayout" title="Auto-arrange nodes">
          <i class="pi pi-sitemap"></i> Layout
        </button>

        <div class="topbar-spacer"></div>

        <!-- Active toggle -->
        <div class="status-toggle" @click="isActive = !isActive">
          <span class="status-label-draft" :style="{ opacity: isActive ? 0.4 : 1 }">Draft</span>
          <div class="toggle-track" :class="isActive ? 'on' : 'off'">
            <div class="toggle-thumb" :class="isActive ? 'on' : 'off'"></div>
          </div>
          <span class="status-label-active" :style="{ opacity: isActive ? 1 : 0.4 }">Active</span>
        </div>

        <!-- Save -->
        <button class="tb-btn tb-btn-save" @click="saveFlow">
          <i class="pi pi-save"></i> Save
        </button>
      </header>

      <!-- ── Vue-Flow Canvas ──────────────────────── -->
      <div class="canvas-area" @drop="onDrop" @dragover="onDragOver">
        <VueFlow
          @nodeClick="onNodeClick"
          @edgeClick="onEdgeClick"
          @paneClick="onPaneClick"
          :snap-to-grid="true"
          :snap-grid="[16, 16]"
        >
          <Background pattern-color="#1a2236" :gap="20" />
          <Controls />
          <MiniMap pannable zoomable />

          <!-- ── Trigger Node ─── -->
          <template #node-trigger="props">
            <div
              class="fb-node trigger-node"
              :class="{
                'active-sim-node':    activeSimNodeId === props.id,
                'active-replay-node': isReplayMode && activeReplayNodeId === props.id,
              }"
            >
              <Handle type="source" :position="Position.Right" />
              <div class="fb-node-header">
                <i class="pi pi-bolt"></i>
                <span>{{ props.data.label }}</span>
                <span class="node-active-led" v-if="activeSimNodeId === props.id"></span>
              </div>
              <div class="fb-node-body">
                <span class="node-chip yellow">{{ props.data.config?.triggerType || 'keyword' }}</span>
                <span style="font-weight:600;color:var(--text-primary);font-size:0.78rem;">
                  <template v-if="(props.data.config?.triggerType||'keyword')==='keyword'">{{ props.data.config?.keyword || flowTrigger || 'keyword' }}</template>
                  <template v-else-if="props.data.config?.triggerType==='command'">{{ props.data.config?.command || '/help' }}</template>
                  <template v-else>Runs on widget open</template>
                </span>
              </div>
            </div>
          </template>

          <!-- ── Rich Response Node ─── -->
          <template #node-richresponse="props">
            <div
              class="fb-node richresponse-node"
              :class="{
                'active-sim-node':    activeSimNodeId === props.id,
                'active-replay-node': isReplayMode && activeReplayNodeId === props.id,
              }"
            >
              <Handle type="target" :position="Position.Left" />
              <Handle type="source" :position="Position.Right" />
              <div class="fb-node-header">
                <i class="pi pi-images"></i>
                <span>{{ props.data.label }}</span>
                <span class="node-active-led" v-if="activeSimNodeId === props.id"></span>
              </div>
              <div class="fb-node-body">
                <span class="node-chip cyan">{{ props.data.config?.responseType || 'card' }}</span>
                <div class="node-preview">
                  <template v-if="props.data.config?.responseType==='card'">{{ props.data.config?.title || 'Card Title' }}</template>
                  <template v-else-if="props.data.config?.responseType==='redirect'">{{ props.data.config?.url || 'Redirect URL' }}</template>
                  <template v-else-if="props.data.config?.responseType==='file'">{{ props.data.config?.fileName || 'File Download' }}</template>
                  <template v-else-if="props.data.config?.responseType==='buttons'">Action Buttons ({{ props.data.config?.buttons?.length || 0 }})</template>
                  <template v-else>{{ props.data.config?.title || 'Form Submission' }}</template>
                </div>
              </div>
            </div>
          </template>

          <!-- ── Message Node ─── -->
          <template #node-message="props">
            <div
              class="fb-node message-node"
              :class="{
                'active-sim-node':    activeSimNodeId === props.id,
                'active-replay-node': isReplayMode && activeReplayNodeId === props.id,
              }"
            >
              <Handle type="target" :position="Position.Left" />
              <Handle type="source" :position="Position.Right" />
              <div class="fb-node-header">
                <i class="pi pi-comment"></i>
                <span>{{ props.data.label }}</span>
                <span class="node-active-led" v-if="activeSimNodeId === props.id"></span>
              </div>
              <div class="fb-node-body">
                <div class="node-preview">{{ props.data.config?.text || 'Empty message' }}</div>
              </div>
            </div>
          </template>

          <!-- ── Input Node ─── -->
          <template #node-input="props">
            <div
              class="fb-node input-node"
              :class="{
                'active-sim-node':    activeSimNodeId === props.id,
                'active-replay-node': isReplayMode && activeReplayNodeId === props.id,
              }"
            >
              <Handle type="target" :position="Position.Left" />
              <Handle type="source" :position="Position.Right" />
              <div class="fb-node-header">
                <i class="pi pi-user-edit"></i>
                <span>{{ props.data.label }}</span>
                <span class="node-active-led" v-if="activeSimNodeId === props.id"></span>
              </div>
              <div class="fb-node-body">
                <div class="node-preview mono">{{ props.data.config?.variableName || 'user_input' }}</div>
              </div>
            </div>
          </template>

          <!-- ── AI Node ─── -->
          <template #node-ai="props">
            <div
              class="fb-node ai-node"
              :class="{
                'active-sim-node':    activeSimNodeId === props.id,
                'active-replay-node': isReplayMode && activeReplayNodeId === props.id,
              }"
            >
              <Handle type="target" :position="Position.Left" />
              <Handle type="source" :position="Position.Right" />
              <div class="fb-node-header">
                <i class="pi pi-microchip"></i>
                <span>{{ props.data.label }}</span>
                <span class="node-active-led" v-if="activeSimNodeId === props.id"></span>
              </div>
              <div class="fb-node-body">
                <div class="node-preview">{{ props.data.config?.prompt || 'No system prompt' }}</div>
              </div>
            </div>
          </template>

          <!-- ── Webhook Node ─── -->
          <template #node-webhook="props">
            <div
              class="fb-node webhook-node"
              :class="{
                'active-sim-node':    activeSimNodeId === props.id,
                'active-replay-node': isReplayMode && activeReplayNodeId === props.id,
              }"
            >
              <Handle type="target" :position="Position.Left" />
              <Handle type="source" :position="Position.Right" />
              <div class="fb-node-header">
                <i class="pi pi-cloud-upload"></i>
                <span>{{ props.data.label }}</span>
                <span class="node-active-led" v-if="activeSimNodeId === props.id"></span>
              </div>
              <div class="fb-node-body">
                <div class="node-preview url">{{ props.data.config?.url || 'No URL set' }}</div>
              </div>
            </div>
          </template>

          <!-- ── Condition Node ─── -->
          <template #node-condition="props">
            <div
              class="fb-node condition-node"
              :class="{
                'active-sim-node':    activeSimNodeId === props.id,
                'active-replay-node': isReplayMode && activeReplayNodeId === props.id,
              }"
            >
              <Handle type="target" :position="Position.Left" />
              <Handle type="source" :position="Position.Right" id="true" class="handle-true" />
              <Handle type="source" :position="Position.Bottom" id="false" class="handle-false" />
              <div class="fb-node-header">
                <i class="pi pi-question-circle"></i>
                <span>{{ props.data.label }}</span>
                <span class="node-active-led" v-if="activeSimNodeId === props.id"></span>
              </div>
              <div class="fb-node-body">
                <div class="node-preview mono" style="color:var(--accent-ai)">IF {{ props.data.config?.variableName || 'user_input' }}</div>
                <div class="node-preview" style="font-size:0.7rem;">{{ props.data.config?.operator }} "{{ props.data.config?.value }}"</div>
                <div class="condition-handles-row">
                  <span style="color:var(--success)">True →</span>
                  <span style="color:var(--danger)">→ False</span>
                </div>
              </div>
            </div>
          </template>

          <!-- ── Switch Node ─── -->
          <template #node-switch="props">
            <div
              class="fb-node switch-node"
              :class="{
                'active-sim-node':    activeSimNodeId === props.id,
                'active-replay-node': isReplayMode && activeReplayNodeId === props.id,
              }"
            >
              <Handle type="target" :position="Position.Left" />
              <Handle type="source" :position="Position.Right" />
              <div class="fb-node-header">
                <i class="pi pi-sitemap"></i>
                <span>{{ props.data.label }}</span>
                <span class="node-active-led" v-if="activeSimNodeId === props.id"></span>
              </div>
              <div class="fb-node-body">
                <div class="node-preview mono" style="color:var(--accent-ai)">SWITCH {{ props.data.config?.variableName || 'user_input' }}</div>
                <div class="node-preview" style="font-size:0.7rem;">Multi-path matching</div>
              </div>
            </div>
          </template>
        </VueFlow>

        <!-- ── Replay Floating Control ─────────────────────── -->
        <div v-if="isReplayMode && replaySteps.length > 0" class="replay-floater">
          <div class="replay-floater-header">
            <div class="replay-title">
              <span class="live-dot"></span>
              Flow Telemetry Playback
            </div>
            <button class="replay-exit-btn" @click="exitReplayMode" title="Exit replay">
              <i class="pi pi-times"></i>
            </button>
          </div>

          <div class="replay-controls">
            <button class="replay-ctrl-btn" @click="prevReplayStep" :disabled="currentReplayIndex <= 0">
              <i class="pi pi-chevron-left"></i>
            </button>
            <button
              class="replay-play-btn"
              :class="isPlayingReplay ? 'playing' : 'paused'"
              @click="togglePlayReplay"
            >
              <i :class="isPlayingReplay ? 'pi pi-pause' : 'pi pi-play'"></i>
            </button>
            <button class="replay-ctrl-btn" @click="nextReplayStep" :disabled="currentReplayIndex >= replaySteps.length - 1">
              <i class="pi pi-chevron-right"></i>
            </button>
          </div>

          <div class="replay-progress-row">
            <span class="replay-step-info">Step {{ currentReplayIndex + 1 }} / {{ replaySteps.length }}</span>
            <span class="replay-duration">
              {{ replaySteps[currentReplayIndex]?.DurationMs ? replaySteps[currentReplayIndex].DurationMs.toFixed(1) + 'ms' : '0.0ms' }}
            </span>
          </div>
          <div class="replay-track">
            <div class="replay-fill" :style="{ width: `${((currentReplayIndex + 1) / replaySteps.length) * 100}%` }"></div>
          </div>

          <div class="replay-step-detail" v-if="replaySteps[currentReplayIndex]">
            <div class="replay-node-row">
              <span :class="['type-chip', replaySteps[currentReplayIndex]?.NodeType]">{{ replaySteps[currentReplayIndex]?.NodeType }}</span>
              <span>{{ replaySteps[currentReplayIndex]?.NodeLabel }}</span>
            </div>
            <div v-if="replaySteps[currentReplayIndex]?.InputMessage" class="replay-io-row">
              <span class="replay-io-label">User input</span>
              "{{ replaySteps[currentReplayIndex]?.InputMessage }}"
            </div>
            <div v-if="replaySteps[currentReplayIndex]?.OutputMessage" class="replay-io-row output">
              <span class="replay-io-label">Response</span>
              {{ replaySteps[currentReplayIndex]?.OutputMessage }}
            </div>
          </div>
        </div>

        <!-- ── Simulator Tab Trigger ──────────────────────── -->
        <button
          class="sim-tab-trigger"
          :style="showSimulator ? 'right: calc(360px - 1px)' : ''"
          @click="toggleSimulator"
          :title="showSimulator ? 'Close Simulator' : 'Open Simulator'"
        >
          <i :class="showSimulator ? 'pi pi-times' : 'pi pi-play-circle'"></i>
          <span v-if="!showSimulator">Dry-Run</span>
        </button>

        <!-- ── Simulator Panel ────────────────────────────── -->
        <div class="sim-panel" :class="{ open: showSimulator }">
          <div class="sim-header">
            <div class="sim-header-left">
              <span class="live-dot"></span>
              <span class="sim-header-title">Simulator</span>
            </div>
            <button class="sim-reset-btn" @click="resetSimulator">
              <i class="pi pi-refresh"></i> Reset
            </button>
          </div>

          <div class="sim-chat-wrap">
            <div class="sim-messages-scroll">
              <div v-for="(msg, i) in simMessages" :key="i" class="sim-msg" :class="msg.role">
                <div class="sim-bubble-col">
                  <span class="sim-role-label">{{ msg.role }}</span>
                  <div class="sim-bubble">{{ msg.content }}</div>
                </div>
              </div>
            </div>
            <div class="sim-input-row">
              <input
                v-model="simInput"
                @keyup.enter="sendSimMessage"
                placeholder="Send a message…"
                class="sim-text-input"
              />
              <button class="sim-send-btn" @click="sendSimMessage">
                <i class="pi pi-send"></i>
              </button>
            </div>
          </div>

          <!-- Debug inspector -->
          <div class="sim-inspector">
            <div class="inspector-section-label">
              <i class="pi pi-eye" style="color:var(--text-accent)"></i> Live Variables
            </div>
            <div class="variables-table">
              <div v-if="Object.keys(simVariables).length === 0" class="no-data-note">
                No variables yet
              </div>
              <table v-else>
                <thead>
                  <tr>
                    <th>Key</th>
                    <th>Value</th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="(val, key) in simVariables" :key="key">
                    <td><code>{{ key }}</code></td>
                    <td>{{ val }}</td>
                  </tr>
                </tbody>
              </table>
            </div>

            <div class="inspector-section-label">
              <i class="pi pi-terminal" style="color:var(--text-accent)"></i> Execution Trace
            </div>
            <div class="console-scroll">
              <div v-if="simLogs.length === 0" class="no-data-note">No executions yet</div>
              <div
                v-for="(log, idx) in simLogs"
                :key="idx"
                class="console-entry"
                :class="log.type"
              >
                <span class="console-time">{{ log.time }}</span>
                <span class="console-badge">{{ log.type }}</span>
                <span>{{ log.message }}</span>
              </div>
            </div>
          </div>
        </div>
      </div><!-- /canvas-area -->
    </div><!-- /main-column -->

    <!-- ═══════════════════════════════════════════════
         RIGHT PROPERTIES PANEL
         ═══════════════════════════════════════════════ -->

    <!-- Node panel -->
    <aside class="props-panel" v-if="selectedNode">
      <div class="props-panel-header">
        <div style="display:flex;flex-direction:column;gap:6px">
          <span class="props-title">Node Config</span>
          <span :class="['type-chip', selectedNode.data.customType]">{{ selectedNode.data.customType }}</span>
        </div>
        <button class="delete-node-btn" @click="removeNode(selectedNode.id)">
          <i class="pi pi-trash"></i> Delete
        </button>
      </div>

      <div class="props-body">
        <!-- Label -->
        <div class="prop-group">
          <label class="prop-label">Node Title</label>
          <input class="prop-input" v-model="selectedNode.data.label" />
        </div>

        <!-- Trigger -->
        <template v-if="selectedNode.data.customType === 'trigger'">
          <div class="prop-group">
            <label class="prop-label">Trigger Type</label>
            <select class="prop-select" v-model="selectedNode.data.config.triggerType">
              <option value="keyword">Keyword Match</option>
              <option value="command">Slash Command (/)</option>
              <option value="onstart">On Start (Widget Load)</option>
            </select>
          </div>
          <div class="prop-group" v-if="(selectedNode.data.config.triggerType||'keyword')==='keyword'">
            <label class="prop-label">Keyword</label>
            <input class="prop-input" v-model="selectedNode.data.config.keyword" @input="flowTrigger = selectedNode.data.config.keyword" placeholder="e.g. support" />
          </div>
          <div class="prop-group" v-if="selectedNode.data.config.triggerType==='command'">
            <label class="prop-label">Slash Command</label>
            <input class="prop-input" v-model="selectedNode.data.config.command" @input="flowTrigger = selectedNode.data.config.command" placeholder="/help" />
          </div>
        </template>

        <!-- Message -->
        <div class="prop-group" v-if="selectedNode.data.customType === 'message'">
          <label class="prop-label">Message Text</label>
          <textarea class="prop-textarea" v-model="selectedNode.data.config.text" placeholder="Type response… Supports {{variable}}"></textarea>
        </div>

        <!-- Rich Response -->
        <template v-if="selectedNode.data.customType === 'richresponse'">
          <div class="prop-group">
            <label class="prop-label">Response Type</label>
            <select class="prop-select" v-model="selectedNode.data.config.responseType">
              <option value="card">Card / Banner</option>
              <option value="redirect">Auto-Redirect</option>
              <option value="file">File Download</option>
              <option value="form">Interactive Form</option>
              <option value="buttons">Action Buttons (Quick Replies)</option>
            </select>
          </div>
          <template v-if="selectedNode.data.config.responseType==='card'">
            <div class="prop-group"><label class="prop-label">Card Title</label><input class="prop-input" v-model="selectedNode.data.config.title" /></div>
            <div class="prop-group"><label class="prop-label">Body Text</label><textarea class="prop-textarea" v-model="selectedNode.data.config.body"></textarea></div>
            <div class="prop-group"><label class="prop-label">Image URL</label><input class="prop-input" v-model="selectedNode.data.config.imageUrl" placeholder="https://…" /></div>
            <div class="prop-group"><label class="prop-label">Button Label</label><input class="prop-input" v-model="selectedNode.data.config.buttonLabel" /></div>
            <div class="prop-group"><label class="prop-label">Button URL</label><input class="prop-input" v-model="selectedNode.data.config.buttonUrl" placeholder="https://…" /></div>
          </template>
          <template v-if="selectedNode.data.config.responseType==='redirect'">
            <div class="prop-group"><label class="prop-label">Target URL</label><input class="prop-input" v-model="selectedNode.data.config.url" placeholder="https://…" /></div>
            <div class="prop-group"><label class="prop-label">Delay (seconds)</label><input class="prop-input" type="number" v-model.number="selectedNode.data.config.seconds" /></div>
            <div class="prop-group"><label class="prop-label">Countdown Text</label><input class="prop-input" v-model="selectedNode.data.config.countdownText" placeholder="Redirecting in {seconds}s…" /></div>
          </template>
          <template v-if="selectedNode.data.config.responseType==='file'">
            <div class="prop-group"><label class="prop-label">File URL</label><input class="prop-input" v-model="selectedNode.data.config.fileUrl" placeholder="https://…" /></div>
            <div class="prop-group"><label class="prop-label">File Name</label><input class="prop-input" v-model="selectedNode.data.config.fileName" placeholder="document.pdf" /></div>
          </template>
          <template v-if="selectedNode.data.config.responseType==='form'">
            <div class="prop-group"><label class="prop-label">Form Title</label><input class="prop-input" v-model="selectedNode.data.config.title" /></div>
            <div class="prop-group">
              <label class="prop-label">Fields (JSON)</label>
              <textarea class="prop-textarea" style="min-height:120px;font-family:var(--font-mono);font-size:0.72rem;"
                :value="JSON.stringify(selectedNode.data.config.fields || [], null, 2)"
                @input="(e: any) => { try { selectedNode.data.config.fields = JSON.parse(e.target.value); } catch {} }"
                placeholder='[{"label":"Name","name":"name","type":"text","required":true}]'
              ></textarea>
              <span class="prop-hint">Supports label, name, type, placeholder, required, options.</span>
            </div>
          </template>
          <template v-if="selectedNode.data.config.responseType==='buttons'">
            <label class="prop-label">Manage Buttons</label>
            <div v-for="(btn, idx) in selectedNode.data.config.buttons || []" :key="idx" class="prop-button-item" style="border: 1px solid var(--border-subtle); padding: 8px; border-radius: 8px; margin-bottom: 8px; display: flex; flex-direction: column; gap: 6px; background: rgba(255,255,255,0.02)">
              <div style="display: flex; justify-content: space-between; align-items: center;">
                <span class="prop-hint" style="font-weight: bold; color: var(--text-primary)">Button #{{ Number(idx) + 1 }}</span>
                <button class="danger-text-btn" style="color: var(--danger); font-size: 10px; background: transparent; border: none; cursor: pointer; font-weight: bold; text-transform: uppercase;" @click="selectedNode.data.config.buttons.splice(Number(idx), 1)">Remove</button>
              </div>
              <input class="prop-input" v-model="btn.label" placeholder="Button Label" style="margin-bottom: 0;" />
              <div style="display: flex; gap: 8px;">
                <select class="prop-select" v-model="btn.action" style="flex: 1; margin-bottom: 0;">
                  <option value="next">Next (Adv Flow)</option>
                  <option value="url">URL Link</option>
                  <option value="postback">Postback Value</option>
                </select>
                <input class="prop-input" v-model="btn.value" placeholder="URL or Value" style="flex: 1; margin-bottom: 0;" />
              </div>
            </div>
            <button class="prop-btn" style="background: rgba(99,102,241,0.15); border: 1px dashed var(--accent-rich); color: var(--accent-rich); width: 100%; padding: 6px; border-radius: 6px; cursor: pointer; font-weight: bold; font-size: 11px; text-transform: uppercase;" @click="() => { selectedNode.data.config.buttons = selectedNode.data.config.buttons || []; selectedNode.data.config.buttons.push({ label: 'New Button', action: 'next', value: '' }) }">Add Button</button>
          </template>
        </template>

        <!-- AI -->
        <div class="prop-group" v-if="selectedNode.data.customType === 'ai'">
          <label class="prop-label">System Prompt</label>
          <textarea class="prop-textarea" v-model="selectedNode.data.config.prompt" placeholder="Instruct the AI at this point…"></textarea>
          
          <div style="display: flex; align-items: center; gap: 8px; margin-top: 8px;">
            <input type="checkbox" id="run-bg-chk" v-model="selectedNode.data.config.runInBackground" />
            <label for="run-bg-chk" style="font-size: 0.75rem; font-weight: 600; cursor: pointer; color: var(--text-primary)">Run in Background (AI Task)</label>
          </div>
          <div v-if="selectedNode.data.config.runInBackground" style="margin-top: 8px;">
            <label class="prop-label">Store Output Variable</label>
            <input class="prop-input" v-model="selectedNode.data.config.storeVariableName" placeholder="e.g. issue_category" style="margin-bottom: 4px;" />
            <span class="prop-hint">Stores response silently without sending to user.</span>
          </div>
        </div>

        <!-- Input -->
        <div class="prop-group" v-if="selectedNode.data.customType === 'input'">
          <label class="prop-label">Variable Name</label>
          <input class="prop-input" v-model="selectedNode.data.config.variableName" placeholder="e.g. user_email" style="margin-bottom: 8px;" />
          
          <label class="prop-label">Inline Prompt Message (Optional)</label>
          <textarea class="prop-textarea" v-model="selectedNode.data.config.promptText" placeholder="e.g. What is your email address?" style="min-height: 60px;"></textarea>
          <span class="prop-hint">If specified, the bot speaks this message and pauses for input in a single step. User reply is saved to <code>{{selectedNode.data.config.variableName}}</code>.</span>
        </div>

        <!-- Webhook -->
        <div class="prop-group" v-if="selectedNode.data.customType === 'webhook'">
          <label class="prop-label">Webhook URL</label>
          <input class="prop-input" v-model="selectedNode.data.config.url" placeholder="https://…" />
          <span class="prop-hint">All session variables are POSTed as JSON.</span>
        </div>

        <!-- Condition -->
        <template v-if="selectedNode.data.customType === 'condition'">
          <div class="prop-group">
            <label class="prop-label">Variable</label>
            <input class="prop-input" v-model="selectedNode.data.config.variableName" placeholder="e.g. user_input" />
          </div>
          <div class="prop-group">
            <label class="prop-label">Operator</label>
            <select class="prop-select" v-model="selectedNode.data.config.operator">
              <option value="equals">Equals</option>
              <option value="contains">Contains</option>
              <option value="regex">Matches Regex</option>
              <option value="exists">Exists / Not Empty</option>
              <option value="greaterthan">Greater Than</option>
              <option value="lessthan">Less Than</option>
            </select>
          </div>
          <div class="prop-group" v-if="selectedNode.data.config.operator !== 'exists'">
            <label class="prop-label">Value</label>
            <input class="prop-input" v-model="selectedNode.data.config.value" placeholder="e.g. yes" />
          </div>
          <span class="prop-hint">
            True path → <strong style="color:var(--success)">Right handle</strong><br/>
            False path → <strong style="color:var(--danger)">Bottom handle</strong>
          </span>
        </template>

        <!-- Switch -->
        <template v-if="selectedNode.data.customType === 'switch'">
          <div class="prop-group">
            <label class="prop-label">Variable to Evaluate</label>
            <input class="prop-input" v-model="selectedNode.data.config.variableName" placeholder="e.g. user_input" />
            <span class="prop-hint">The switch node routes the session depending on the value of this variable.</span>
          </div>
          <span class="prop-hint">
            <strong>Edge Label Matchers:</strong><br/>
            Create connections from this node and edit edge labels:<br/>
            · <code>equals:value</code><br/>
            · <code>contains:value</code><br/>
            · <code>regex:pattern</code><br/>
            · <code>default</code> or blank for default path.
          </span>
        </template>
      </div>
    </aside>

    <!-- Edge panel -->
    <aside class="props-panel" v-if="selectedEdge">
      <div class="props-panel-header">
        <span class="props-title">Connection</span>
        <span class="type-chip" style="background:rgba(99,102,241,0.12);color:var(--text-accent);border:1px solid rgba(99,102,241,0.25);">EDGE</span>
      </div>
      <div class="props-body">
        <div class="prop-group">
          <label class="prop-label">Route Matcher</label>
          <input class="prop-input" v-model="selectedEdge.label" placeholder="e.g. equals:yes" />
          <span class="prop-hint">
            <code>equals:text</code> · <code>contains:text</code> · <code>regex:[0-9]</code><br/>
            Leave blank for immediate default path.
          </span>
        </div>
      </div>
    </aside>

  </div>
</template>

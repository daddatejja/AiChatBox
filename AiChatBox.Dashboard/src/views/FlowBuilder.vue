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
import { useApi } from '../composables/useApi';

const route = useRoute();
const router = useRouter();
const { apiFetch } = useApi();
const projectId = route.params.projectId as string;
const { addNodes, addEdges, onConnect, nodes, edges, onNodeDragStop } = useVueFlow();

const flowId = ref<string | null>(route.params.flowId as string || null);
const flowName = ref('New Flow');
const flowTrigger = ref('');
const isActive = ref(true);

const selectedNode = ref<any>(null);
const selectedEdge = ref<any>(null);

// History Stack Management (Undo / Redo)
const historyStack = ref<Array<{ nodes: string; edges: string }>>([]);
const redoStack = ref<Array<{ nodes: string; edges: string }>>([]);
let isRestoring = false;

const takeSnapshot = () => {
  if (isRestoring) return;
  const snapshot = {
    nodes: JSON.stringify(nodes.value.map(n => ({
      id: n.id,
      type: n.type,
      position: { ...n.position },
      data: JSON.parse(JSON.stringify(n.data))
    }))),
    edges: JSON.stringify(edges.value.map(e => ({
      id: e.id,
      source: e.source,
      target: e.target,
      label: e.label,
      sourceHandle: e.sourceHandle
    })))
  };

  // Skip pushing if identical to top of the stack
  if (historyStack.value.length > 0) {
    const top = historyStack.value[historyStack.value.length - 1];
    if (top.nodes === snapshot.nodes && top.edges === snapshot.edges) {
      return;
    }
  }

  historyStack.value.push(snapshot);
  if (historyStack.value.length > 50) {
    historyStack.value.shift();
  }
  // Clear redo stack on new action
  redoStack.value = [];
};

const undo = () => {
  if (historyStack.value.length <= 1) return; // Retain at least initial load state

  const current = historyStack.value.pop();
  if (current) {
    redoStack.value.push(current);
  }

  const previous = historyStack.value[historyStack.value.length - 1];
  if (previous) {
    restoreFromSnapshot(previous);
  }
};

const redo = () => {
  const next = redoStack.value.pop();
  if (next) {
    historyStack.value.push(next);
    restoreFromSnapshot(next);
  }
};

const restoreFromSnapshot = (state: { nodes: string; edges: string }) => {
  isRestoring = true;
  const parsedNodes = JSON.parse(state.nodes);
  const parsedEdges = JSON.parse(state.edges);

  nodes.value = parsedNodes;
  edges.value = parsedEdges;

  selectedNode.value = null;
  selectedEdge.value = null;

  // Wrap in timeout to prevent triggering our data changes watcher
  setTimeout(() => {
    isRestoring = false;
  }, 100);
};

// Listen for keys (Ctrl+Z, Ctrl+Shift+Z, Ctrl+Y)
const handleKeyDown = (e: KeyboardEvent) => {
  const activeEl = document.activeElement;
  if (activeEl && (activeEl.tagName === 'INPUT' || activeEl.tagName === 'TEXTAREA' || activeEl.getAttribute('contenteditable') === 'true')) {
    return;
  }

  if (e.ctrlKey && e.key.toLowerCase() === 'z') {
    e.preventDefault();
    if (e.shiftKey) {
      redo();
    } else {
      undo();
    }
  } else if (e.ctrlKey && e.key.toLowerCase() === 'y') {
    e.preventDefault();
    redo();
  }
};

// Automatic snapshot on configuration changes (debounced deep watcher)
let changeTimeout: any = null;
watch(
  () => nodes.value.map(n => JSON.stringify(n.data)),
  () => {
    if (isRestoring) return;
    if (changeTimeout) clearTimeout(changeTimeout);
    changeTimeout = setTimeout(() => {
      takeSnapshot();
    }, 800);
  },
  { deep: true }
);

// Capture snapshots when node dragging stops
onNodeDragStop(() => {
  takeSnapshot();
});

// Auto-Layout calculation
const autoLayout = () => {
  const g = new dagre.graphlib.Graph();
  g.setGraph({ rankdir: 'LR', align: 'DL', nodesep: 60, edgesep: 30, ranksep: 100 });
  g.setDefaultEdgeLabel(() => ({}));

  nodes.value.forEach(node => {
    g.setNode(node.id, { width: 220, height: 130 });
  });

  edges.value.forEach(edge => {
    g.setEdge(edge.source, edge.target);
  });

  dagre.layout(g);

  nodes.value.forEach(node => {
    const pos = g.node(node.id);
    if (pos) {
      node.position = {
        x: Math.round(pos.x - 110),
        y: Math.round(pos.y - 65)
      };
    }
  });

  takeSnapshot();
};

const removeNode = (nodeId: string) => {
  nodes.value = nodes.value.filter(n => n.id !== nodeId);
  edges.value = edges.value.filter(e => e.source !== nodeId && e.target !== nodeId);
  selectedNode.value = null;
  takeSnapshot();
};


// Interactive dry-run Simulator State
const showSimulator = ref(false);
const simMessages = ref<Array<{ role: string; content: string }>>([]);
const simInput = ref('');
const isSimulating = ref(false);
const activeSimNodeId = ref<string | null>(null);
const activeSimEdgeId = ref<string | null>(null);
const simVariables = ref<Record<string, string>>({});
const simLogs = ref<Array<{ time: string; type: string; message: string }>>([]);

// Sync animated glowing styles for dry-run tracing
watch(activeSimEdgeId, (newId) => {
  edges.value.forEach(e => {
    if (e.id === newId) {
      e.class = 'active-sim-edge';
      e.animated = true;
    } else {
      e.class = '';
      e.animated = false;
    }
  });
});

const onDragStart = (event: DragEvent, nodeType: string) => {
  if (event.dataTransfer) {
    event.dataTransfer.setData('application/vueflow', nodeType);
    event.dataTransfer.effectAllowed = 'move';
  }
};

const onDragOver = (event: DragEvent) => {
  event.preventDefault();
  if (event.dataTransfer) {
    event.dataTransfer.dropEffect = 'move';
  }
};

const onDrop = (event: DragEvent) => {
  const type = event.dataTransfer?.getData('application/vueflow');
  if (!type) return;

  const position = { x: event.clientX - 280, y: event.clientY - 120 };
  const id = `node_${Date.now()}`;
  const newNode = {
    id: id,
    type: type,
    position,
    data: { 
      customType: type,
      label: type === 'richresponse' ? 'Rich Response Node' : type === 'switch' ? 'Multi Switch Node' : `${type.charAt(0).toUpperCase() + type.slice(1)} Node`,
      config: type === 'message' ? { text: 'Hello! How can I help you today?' } : 
              type === 'ai' ? { prompt: 'You are a helpful assistant answering inquiries based on collected context.', runInBackground: false, storeVariableName: '' } : 
              type === 'webhook' ? { url: 'https://api.example.com/v1/webhook' } : 
              type === 'input' ? { variableName: 'user_input', promptText: '' } : 
              type === 'condition' ? { variableName: 'user_input', operator: 'equals', value: 'yes' } :
              type === 'switch' ? { variableName: 'user_input' } :
              type === 'richresponse' ? { responseType: 'card', title: 'Special Offer', body: 'Get 20% off your purchase.', imageUrl: 'https://picsum.photos/400/200', buttonLabel: 'Claim Offer', buttonUrl: 'https://example.com/claim', seconds: 5, countdownText: 'Redirecting you in {seconds} seconds...', fileUrl: 'https://example.com/doc.pdf', fileName: 'document.pdf', fields: [], buttons: [] } : 
              type === 'trigger' ? { triggerType: 'keyword', keyword: '' } : {}
    },
  };
  addNodes([newNode]);
};

onConnect((params) => {
  const id = `edge_${Date.now()}`;
  const p = params as any;
  // If the source is a condition node, default to setting label/condition to the source handle's name
  let label = p.label || '';
  if (p.sourceHandle === 'true' || p.sourceHandle === 'false') {
    label = p.sourceHandle;
  }
  addEdges([{ ...p, id, label }]);
});

const onNodeClick = (event: any) => {
  selectedNode.value = event.node;
  selectedEdge.value = null;
};

const onEdgeClick = (event: any) => {
  selectedEdge.value = event.edge;
  selectedNode.value = null;
};

const onPaneClick = () => {
  selectedNode.value = null;
  selectedEdge.value = null;
};

const loadFlow = async () => {
  if (!flowId.value) return;
  try {
    const resDetail = await apiFetch(`/api/projects/${projectId}/flows/${flowId.value}`);
    if (resDetail.ok) {
      const detail = await resDetail.json();
      flowName.value = detail.name;
      flowTrigger.value = detail.triggerKeyword || '';
      isActive.value = detail.isActive; // Load active state
      
      // Clear current
      nodes.value = [];
      edges.value = [];
      
      addNodes(detail.nodes.map((n: any) => {
        const parsed = JSON.parse(n.dataJson);
        if (n.type === 'trigger') {
          parsed.config = parsed.config || { triggerType: 'keyword', keyword: detail.triggerKeyword || '' };
          if (parsed.config.triggerType === 'keyword' && !parsed.config.keyword) {
            parsed.config.keyword = detail.triggerKeyword || '';
          }
        }
        return {
          id: n.id,
          position: { x: n.positionX, y: n.positionY },
          data: parsed,
          type: parsed.customType || 'default'
        };
      }));
      addEdges(detail.edges.map((e: any) => ({
        id: e.id,
        source: e.sourceNodeId,
        target: e.targetNodeId,
        label: e.condition || '',
        sourceHandle: (e.condition === 'true' || e.condition === 'false') ? e.condition : null
      })));
      
      // Capture initial load snapshot
      takeSnapshot();
    }
  } catch (error) {
    console.error("Failed to load flow", error);
  }
};

// --- Trace Step Replay System ---
const replayLogId = route.query.replayLogId as string || null;
const isReplayMode = ref(false);
const replaySteps = ref<any[]>([]);
const currentReplayIndex = ref(-1);
const activeReplayNodeId = ref<string | null>(null);
const isPlayingReplay = ref(false);
let replayInterval: any = null;

const loadReplayLog = async () => {
  if (!replayLogId) return;
  try {
    const res = await apiFetch(`/api/projects/${projectId}/flows/execution-logs/${replayLogId}`);
    if (res.ok) {
      const data = await res.json();
      replaySteps.value = JSON.parse(data.stepsJson || '[]');
      isReplayMode.value = true;
      if (replaySteps.value.length > 0) {
        selectReplayStep(0);
      }
    }
  } catch (error) {
    console.error("Failed to load replay log", error);
  }
};

const selectReplayStep = (index: number) => {
  if (index < 0 || index >= replaySteps.value.length) return;
  currentReplayIndex.value = index;
  const step = replaySteps.value[index];
  activeReplayNodeId.value = step.NodeId;
};

const nextReplayStep = () => {
  if (currentReplayIndex.value < replaySteps.value.length - 1) {
    selectReplayStep(currentReplayIndex.value + 1);
  } else {
    isPlayingReplay.value = false;
    if (replayInterval) {
      clearInterval(replayInterval);
      replayInterval = null;
    }
  }
};

const prevReplayStep = () => {
  if (currentReplayIndex.value > 0) {
    selectReplayStep(currentReplayIndex.value - 1);
  }
};

const togglePlayReplay = () => {
  isPlayingReplay.value = !isPlayingReplay.value;
  if (isPlayingReplay.value) {
    if (currentReplayIndex.value >= replaySteps.value.length - 1) {
      selectReplayStep(0);
    }
    replayInterval = setInterval(() => {
      nextReplayStep();
    }, 2000);
  } else {
    if (replayInterval) {
      clearInterval(replayInterval);
      replayInterval = null;
    }
  }
};

const exitReplayMode = () => {
  isReplayMode.value = false;
  isPlayingReplay.value = false;
  activeReplayNodeId.value = null;
  if (replayInterval) {
    clearInterval(replayInterval);
    replayInterval = null;
  }
  router.replace({ query: {} });
};

const saveFlow = async () => {
  const triggerNode = nodes.value.find(n => n.type === 'trigger');
  let saveTriggerVal = flowTrigger.value;
  if (triggerNode && triggerNode.data.config) {
    const tType = triggerNode.data.config.triggerType || 'keyword';
    if (tType === 'keyword') {
      saveTriggerVal = triggerNode.data.config.keyword || saveTriggerVal;
    } else if (tType === 'command') {
      saveTriggerVal = triggerNode.data.config.command || saveTriggerVal;
    } else if (tType === 'onstart') {
      saveTriggerVal = 'onStart';
    }
  }

  const flowData = {
    name: flowName.value,
    triggerKeyword: saveTriggerVal,
    isActive: isActive.value, // Bind to toggle!
    nodes: nodes.value.map(n => ({
      id: n.id,
      type: n.data.customType || 'default',
      dataJson: JSON.stringify(n.data),
      positionX: n.position.x,
      positionY: n.position.y
    })),
    edges: edges.value.map(e => ({
      id: e.id,
      sourceNodeId: e.source,
      targetNodeId: e.target,
      condition: e.sourceHandle || e.label?.toString() || ''
    }))
  };

  const url = flowId.value ? `/api/projects/${projectId}/flows/${flowId.value}` : `/api/projects/${projectId}/flows`;
  const method = flowId.value ? 'PUT' : 'POST';

  try {
    const res = await apiFetch(url, {
      method,
      body: JSON.stringify(flowData)
    });
    
    if (res.ok && !flowId.value) {
      const created = await res.json();
      flowId.value = created.id;
    }
    alert('Flow saved successfully!');
  } catch (error) {
    console.error('Failed to save flow', error);
    alert('Error saving flow.');
  }
};

const addSimLog = (type: string, message: string) => {
  simLogs.value.push({
    time: new Date().toLocaleTimeString(),
    type,
    message
  });
};

const toggleSimulator = () => {
  showSimulator.value = !showSimulator.value;
  if (showSimulator.value && simMessages.value.length === 0) {
    resetSimulator();
  }
};

const resetSimulator = () => {
  activeSimNodeId.value = null;
  activeSimEdgeId.value = null;
  simVariables.value = {};
  simLogs.value = [];
  simMessages.value = [
    { role: 'ai', content: `💬 Debug Simulator started. Type a message matching keyword "${flowTrigger.value || '[empty]'}" to execute!` }
  ];
  addSimLog('info', 'Simulator initialized. Awaiting trigger keyword match.');
};

const executeNodeStep = async (nodeId: string, incomingMsg: string) => {
  isSimulating.value = true;
  activeSimNodeId.value = nodeId;
  
  const node = nodes.value.find(n => n.id === nodeId);
  if (!node) {
    addSimLog('error', `Node ${nodeId} not found in the graph`);
    isSimulating.value = false;
    return;
  }

  addSimLog('info', `Active: Node "${node.data.label}"`);

  // Synchronize flowTrigger and trigger node configuration
  watch(flowTrigger, (newVal) => {
    const triggerNode = nodes.value.find(n => n.type === 'trigger');
    if (triggerNode && triggerNode.data && triggerNode.data.config) {
      const tType = triggerNode.data.config.triggerType || 'keyword';
      if (tType === 'keyword') {
        triggerNode.data.config.keyword = newVal;
      } else if (tType === 'command') {
        triggerNode.data.config.command = newVal;
      }
    }
  });

  if (node.type === 'trigger') {
    const tType = node.data.config?.triggerType || 'keyword';
    addSimLog('success', `Flow triggered: entry point of type [${tType.toUpperCase()}] matches`);
    await transitionToNext(node.id, incomingMsg);
  }
  else if (node.type === 'richresponse') {
    const resType = node.data.config?.responseType || 'card';
    addSimLog('info', `Rendering Rich Response bubble: [${resType.toUpperCase()}]`);
    await new Promise(resolve => setTimeout(resolve, 800));
    
    let contentStr = '';
    if (resType === 'card') {
      contentStr = `🎴 [Card Banner] Title: ${node.data.config?.title || 'No Title'} | ${node.data.config?.body || 'No Body'}`;
    } else if (resType === 'redirect') {
      contentStr = `🔗 [Auto-Redirect] Target URL: ${node.data.config?.url || 'No URL'} (Wait ${node.data.config?.seconds || 5}s)`;
    } else if (resType === 'file') {
      contentStr = `📁 [File Download] Name: ${node.data.config?.fileName || 'file'} | URL: ${node.data.config?.fileUrl || ''}`;
    } else if (resType === 'form') {
      contentStr = `📝 [Dynamic Form] Title: ${node.data.config?.title || 'Form'} | Fields: ${JSON.stringify(node.data.config?.fields || [])}`;
    } else if (resType === 'buttons') {
      contentStr = `🔘 [Quick Reply Buttons]: ` + (node.data.config?.buttons || []).map((b: any) => `[${b.label}]`).join(' ');
    }
    
    simMessages.value.push({ role: 'ai', content: contentStr });
    addSimLog('success', `Sent Rich Response payload: ${resType}`);
    
    await transitionToNext(node.id, incomingMsg);
  }
  else if (node.type === 'message') {
    let text = node.data.config?.text || 'Hello!';
    for (const [k, v] of Object.entries(simVariables.value)) {
      text = text.replace(new RegExp(`\\{\\{\\s*${k}\\s*\\}\\}`, 'gi'), v);
    }
    
    addSimLog('info', `Rendering static text bubble response`);
    await new Promise(resolve => setTimeout(resolve, 800)); // Natural typing gap
    
    simMessages.value.push({ role: 'ai', content: text });
    addSimLog('success', `Sent text payload: "${text}"`);
    
    await transitionToNext(node.id, incomingMsg);
  }
  else if (node.type === 'input') {
    if (incomingMsg !== '') {
      const varName = node.data.config?.variableName || 'user_input';
      simVariables.value[varName] = incomingMsg;
      addSimLog('success', `Saved Context variable: "${varName}" = "${incomingMsg}"`);
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
      addSimLog('pending', `Paused: Waiting for user reply to save into variable "{{${node.data.config?.variableName || 'variable'}}}"`);
      isSimulating.value = false;
    }
  }
  else if (node.type === 'ai') {
    let prompt = node.data.config?.prompt || 'You are an AI.';
    for (const [k, v] of Object.entries(simVariables.value)) {
      prompt = prompt.replace(new RegExp(`\\{\\{\\s*${k}\\s*\\}\\}`, 'gi'), v);
    }
    
    addSimLog('info', `Processing LLM inference with prompt instructions: "${prompt}"`);
    await new Promise(resolve => setTimeout(resolve, 1400)); // Simulated LLM latency
    
    const mockReply = `Resolved prompt successfully (Classified or Extracted value).`;
    
    if (node.data.config?.runInBackground) {
      const storeVar = node.data.config?.storeVariableName || 'ai_result';
      simVariables.value[storeVar] = mockReply;
      addSimLog('success', `AI background execution completed. Saved Context variable: "${storeVar}" = "${mockReply}"`);
    } else {
      simMessages.value.push({ role: 'ai', content: `🤖 [Simulated AI Result]: ${mockReply}` });
      addSimLog('success', `AI generation completed successfully.`);
    }
    
    await transitionToNext(node.id, incomingMsg);
  }
  else if (node.type === 'webhook') {
    const url = node.data.config?.url || 'https://api.example.com';
    addSimLog('info', `Dispatching dynamic variables POST call to ${url}...`);
    
    await new Promise(resolve => setTimeout(resolve, 1000));
    addSimLog('success', `POST 200 OK — webhook delivered successfully.`);
    
    await transitionToNext(node.id, incomingMsg);
  }
  else if (node.type === 'switch') {
    const varName = node.data.config?.variableName || 'user_input';
    const currentVal = simVariables.value[varName] || '';
    addSimLog('info', `Evaluating Multi-way Switch on {{${varName}}} (value: "${currentVal}")`);
    
    const outgoingEdges = edges.value.filter(e => e.source === node.id);
    let matchedEdge = null;
    for (const edge of outgoingEdges) {
      const cond = edge.label?.toString() || '';
      if (!cond || cond.toLowerCase() === 'default') {
        continue;
      }
      
      const parts = cond.split(':');
      const op = parts[0]?.trim().toLowerCase() || 'equals';
      const val = parts.slice(1).join(':')?.trim().toLowerCase() || parts[0]?.trim().toLowerCase() || '';
      
      let match = false;
      if (op === 'equals') {
        match = currentVal.toLowerCase() === val.toLowerCase();
      } else if (op === 'contains') {
        match = currentVal.toLowerCase().includes(val.toLowerCase());
      } else if (op === 'regex') {
        try {
          match = new RegExp(val, 'i').test(currentVal);
        } catch (e) {}
      } else {
        // Assume direct equals comparison if no colon is present
        match = currentVal.toLowerCase() === cond.toLowerCase();
      }
      
      if (match) {
        matchedEdge = edge;
        break;
      }
    }
    
    if (!matchedEdge) {
      // Find fallback default or empty labeled edge
      matchedEdge = outgoingEdges.find(e => {
        const cond = e.label?.toString().toLowerCase() || '';
        return !cond || cond === 'default';
      });
    }
    
    if (matchedEdge) {
      activeSimEdgeId.value = matchedEdge.id;
      addSimLog('success', `Switch matched route: [${matchedEdge.label || 'default'}]`);
      await new Promise(resolve => setTimeout(resolve, 800));
      await executeNodeStep(matchedEdge.target, incomingMsg);
    } else {
      addSimLog('error', `No outgoing switch route matched`);
      isSimulating.value = false;
    }
  }
  else if (node.type === 'condition') {
    const varName = node.data.config?.variableName || 'user_input';
    const op = node.data.config?.operator || 'equals';
    const val = node.data.config?.value || '';
    const currentVal = simVariables.value[varName] || '';
    
    addSimLog('info', `Evaluating Condition: {{${varName}}} (${currentVal}) ${op} "${val}"`);
    
    let isTrue = false;
    if (op === 'equals') {
      isTrue = currentVal.toLowerCase() === val.toLowerCase();
    } else if (op === 'contains') {
      isTrue = currentVal.toLowerCase().includes(val.toLowerCase());
    } else if (op === 'regex') {
      try {
        isTrue = new RegExp(val, 'i').test(currentVal);
      } catch (e) {
        addSimLog('error', `Invalid Regex rule: ${val}`);
      }
    } else if (op === 'exists' || op === 'notempty') {
      isTrue = currentVal.trim() !== '';
    } else if (op === 'greaterthan') {
      isTrue = parseFloat(currentVal) > parseFloat(val);
    } else if (op === 'lessthan') {
      isTrue = parseFloat(currentVal) < parseFloat(val);
    }
    
    addSimLog('success', `Condition resolved to: ${isTrue.toString().toUpperCase()}`);
    
    const outgoingEdges = edges.value.filter(e => e.source === node.id);
    const targetCond = isTrue ? 'true' : 'false';
    const matchedEdge = outgoingEdges.find(e => e.sourceHandle === targetCond || e.label?.toString().toLowerCase() === targetCond) || outgoingEdges[0];
    
    if (matchedEdge) {
      activeSimEdgeId.value = matchedEdge.id;
      addSimLog('info', `Advancing path along route: [${targetCond.toUpperCase()}]`);
      await new Promise(resolve => setTimeout(resolve, 800));
      await executeNodeStep(matchedEdge.target, incomingMsg);
    } else {
      addSimLog('error', `No outgoing connection found for branch [${targetCond.toUpperCase()}]`);
      isSimulating.value = false;
    }
  }
};

const transitionToNext = async (sourceId: string, currentMsg: string) => {
  const outgoingEdges = edges.value.filter(e => e.source === sourceId);
  
  if (outgoingEdges.length === 0) {
    addSimLog('success', 'Reached leaf terminal node. State machine execution completed.');
    activeSimNodeId.value = null;
    activeSimEdgeId.value = null;
    isSimulating.value = false;
    return;
  }

  let matchedEdge = null;
  for (const edge of outgoingEdges) {
    const cond = edge.label?.toString() || '';
    if (!cond) {
      matchedEdge = edge; // Default fallback branch
      continue;
    }

    const parts = cond.split(':');
    if (parts.length < 2) continue;
    
    const op = parts[0].trim().toLowerCase();
    const val = parts.slice(1).join(':').trim().toLowerCase();
    const input = currentMsg.trim().toLowerCase();

    if (op === 'equals' && input === val) {
      matchedEdge = edge;
      break;
    } else if (op === 'contains' && input.includes(val)) {
      matchedEdge = edge;
      break;
    } else if (op === 'regex') {
      try {
        if (new RegExp(val, 'i').test(currentMsg)) {
          matchedEdge = edge;
          break;
        }
      } catch (e) {
        addSimLog('error', `Invalid Regex rule: ${val}`);
      }
    }
  }

  if (!matchedEdge && outgoingEdges.length > 0) {
    matchedEdge = outgoingEdges[0];
  }

  if (matchedEdge) {
    activeSimEdgeId.value = matchedEdge.id;
    addSimLog('info', `Advancing path along route: [${matchedEdge.label || 'default'}]`);
    await new Promise(resolve => setTimeout(resolve, 600));
    
    await executeNodeStep(matchedEdge.target, '');
  } else {
    addSimLog('success', 'Execution path ended.');
    activeSimNodeId.value = null;
    activeSimEdgeId.value = null;
    isSimulating.value = false;
  }
};

const sendSimMessage = () => {
  if (!simInput.value.trim()) return;
  const msg = simInput.value;
  simMessages.value.push({ role: 'user', content: msg });
  simInput.value = '';

  if (activeSimNodeId.value) {
    // If waiting at an input block, resume execution using response payload
    executeNodeStep(activeSimNodeId.value, msg);
  } else {
    // Attempt trigger matching
    const normalized = msg.toLowerCase();
    const triggerStr = (flowTrigger.value || '').toLowerCase();
    if (triggerStr && normalized.includes(triggerStr)) {
      const triggerNode = nodes.value.find(n => n.type === 'trigger');
      if (triggerNode) {
        executeNodeStep(triggerNode.id, msg);
      } else {
        addSimLog('error', 'Missing Trigger Node. Ensure trigger node is added.');
      }
    } else {
      simMessages.value.push({ role: 'ai', content: `No matching rule trigger! Match the trigger word: "${flowTrigger.value}" to begin.` });
    }
  }
};

onMounted(async () => {
  await loadFlow();
  if (replayLogId) {
    await loadReplayLog();
  }
  window.addEventListener('keydown', handleKeyDown);
});

onUnmounted(() => {
  window.removeEventListener('keydown', handleKeyDown);
});
</script>

<template>
  <div class="flow-builder-container">
    <!-- Floating DnD Node Palette -->
    <div class="sidebar glassmorphic">
      <h3>Draggable Logic Palette</h3>
      <div class="node-category">Foundations</div>
      <div class="dndnode node-trigger-btn" draggable="true" @dragstart="onDragStart($event, 'trigger')">
        <i class="pi pi-bolt"></i> Trigger Node
      </div>
      <div class="dndnode node-message-btn" draggable="true" @dragstart="onDragStart($event, 'message')">
        <i class="pi pi-comment"></i> Text Bubble
      </div>
      <div class="dndnode node-richresponse-btn" draggable="true" @dragstart="onDragStart($event, 'richresponse')">
        <i class="pi pi-images"></i> Rich Response
      </div>
      
      <div class="node-category mt-4">Interactivity</div>
      <div class="dndnode node-input-btn" draggable="true" @dragstart="onDragStart($event, 'input')">
        <i class="pi pi-user-edit"></i> Wait & Capture
      </div>
      <div class="dndnode node-ai-btn" draggable="true" @dragstart="onDragStart($event, 'ai')">
        <i class="pi pi-microchip"></i> AI Prompt Block
      </div>
      <div class="dndnode node-webhook-btn" draggable="true" @dragstart="onDragStart($event, 'webhook')">
        <i class="pi pi-cloud-upload"></i> POST Webhook
      </div>

      <div class="node-category mt-4">Logic & Branching</div>
      <div class="dndnode node-condition-btn" draggable="true" @dragstart="onDragStart($event, 'condition')">
        <i class="pi pi-question-circle"></i> Conditional Branch
      </div>
      <div class="dndnode node-switch-btn" draggable="true" @dragstart="onDragStart($event, 'switch')">
        <i class="pi pi-sitemap"></i> Multi Switch
      </div>
    </div>
    
    <div class="main-content">
      <div class="header glassmorphic">
        <div class="title-section">
          <i class="pi pi-sitemap text-indigo-400 text-lg"></i>
          <input v-model="flowName" placeholder="Flow Name" class="flow-input" />
        </div>
        
        <div class="trigger-section">
          <i class="pi pi-key text-surface-400"></i>
          <input v-model="flowTrigger" placeholder="Trigger keyword (e.g. support)" class="flow-input w-48" />
        </div>
        
        <!-- Active/Draft status toggle, Auto-Layout & Save Design actions -->
        <div class="actions-section flex items-center gap-3">
          <div class="status-toggle-container flex items-center gap-2 bg-slate-900/60 border border-slate-700/80 rounded-full px-3 py-1.5 text-[11px] select-none">
            <span :class="!isActive ? 'text-rose-400 font-bold' : 'text-slate-400 font-semibold'">Draft</span>
            <div 
              class="w-8 h-4 rounded-full cursor-pointer relative transition-colors duration-300"
              :class="isActive ? 'bg-emerald-500' : 'bg-slate-700'"
              @click="isActive = !isActive"
            >
              <div 
                class="w-3.5 h-3.5 rounded-full bg-white absolute top-[1px] left-[1px] transition-transform duration-300 shadow"
                :style="{ transform: isActive ? 'translateX(14px)' : 'translateX(0)' }"
              ></div>
            </div>
            <span :class="isActive ? 'text-emerald-400 font-bold' : 'text-slate-400 font-semibold'">Active</span>
          </div>

          <button class="layout-btn flex items-center gap-1.5 font-semibold text-xs text-slate-300 bg-slate-900/60 hover:bg-slate-800 border border-slate-700/80 hover:border-slate-600 px-3 py-1.5 rounded transition" @click="autoLayout" title="Organize elements Left-to-Right automatically">
            <i class="pi pi-refresh"></i> Auto-Layout
          </button>

          <button class="save-btn font-semibold" @click="saveFlow">
            <i class="pi pi-save mr-1"></i> Save Design
          </button>
        </div>
      </div>

      <div class="vue-flow-wrapper" @drop="onDrop" @dragover="onDragOver">
        <VueFlow 
          @nodeClick="onNodeClick" 
          @edgeClick="onEdgeClick"
          @paneClick="onPaneClick"
          :snap-to-grid="true"
          :snap-grid="[16, 16]"
        >
          <Background pattern-color="#2a2e3f" :gap="16" />
          <Controls />
          <MiniMap pannable zoomable />

          <!-- Custom Trigger Node -->
          <template #node-trigger="props">
            <div class="custom-node trigger-node" :class="{ 'active-sim-node': activeSimNodeId === props.id, 'active-replay-node': isReplayMode && activeReplayNodeId === props.id }">
              <Handle type="source" :position="Position.Right" />
              <div class="node-header">
                <i class="pi pi-bolt"></i>
                <span>{{ props.data.label }}</span>
                <span class="active-led" v-if="activeSimNodeId === props.id"></span>
              </div>
              <div class="node-body">
                <span class="badge yellow uppercase">{{ props.data.config?.triggerType || 'keyword' }}</span>
                <span class="trigger-label">
                  <template v-if="(props.data.config?.triggerType || 'keyword') === 'keyword'">
                    {{ props.data.config?.keyword || flowTrigger || 'Keyword' }}
                  </template>
                  <template v-else-if="props.data.config?.triggerType === 'command'">
                    {{ props.data.config?.command || '/help' }}
                  </template>
                  <template v-else-if="props.data.config?.triggerType === 'onstart'">
                    Runs on widget open
                  </template>
                  <template v-else>
                    {{ flowTrigger }}
                  </template>
                </span>
              </div>
            </div>
          </template>

          <!-- Custom Rich Response Node -->
          <template #node-richresponse="props">
            <div class="custom-node richresponse-node" :class="{ 'active-sim-node': activeSimNodeId === props.id, 'active-replay-node': isReplayMode && activeReplayNodeId === props.id }">
              <Handle type="target" :position="Position.Left" />
              <Handle type="source" :position="Position.Right" />
              <div class="node-header">
                <i class="pi pi-images"></i>
                <span>{{ props.data.label }}</span>
                <span class="active-led" v-if="activeSimNodeId === props.id"></span>
              </div>
              <div class="node-body">
                <span class="badge yellow uppercase">{{ props.data.config?.responseType || 'card' }}</span>
                <div class="node-preview" v-if="props.data.config?.responseType === 'card'">{{ props.data.config?.title || 'Card Title' }}</div>
                <div class="node-preview" v-else-if="props.data.config?.responseType === 'redirect'">{{ props.data.config?.url || 'Redirect URL' }}</div>
                <div class="node-preview" v-else-if="props.data.config?.responseType === 'file'">{{ props.data.config?.fileName || 'File Download' }}</div>
                <div class="node-preview" v-else-if="props.data.config?.responseType === 'form'">{{ props.data.config?.title || 'Form Submission' }}</div>
              </div>
            </div>
          </template>

          <!-- Custom Message Node -->
          <template #node-message="props">
            <div class="custom-node message-node" :class="{ 'active-sim-node': activeSimNodeId === props.id, 'active-replay-node': isReplayMode && activeReplayNodeId === props.id }">
              <Handle type="target" :position="Position.Left" />
              <Handle type="source" :position="Position.Right" />
              <div class="node-header">
                <i class="pi pi-comment"></i>
                <span>{{ props.data.label }}</span>
                <span class="active-led" v-if="activeSimNodeId === props.id"></span>
              </div>
              <div class="node-body">
                <div class="node-preview">{{ props.data.config?.text || 'Empty message body' }}</div>
              </div>
            </div>
          </template>

          <!-- Custom Input Node -->
          <template #node-input="props">
            <div class="custom-node input-node" :class="{ 'active-sim-node': activeSimNodeId === props.id, 'active-replay-node': isReplayMode && activeReplayNodeId === props.id }">
              <Handle type="target" :position="Position.Left" />
              <Handle type="source" :position="Position.Right" />
              <div class="node-header">
                <i class="pi pi-user-edit"></i>
                <span>{{ props.data.label }}</span>
                <span class="active-led" v-if="activeSimNodeId === props.id"></span>
              </div>
              <div class="node-body">
                <div class="var-badge">
                  <i class="pi pi-save text-emerald-400 mr-1"></i>
                  <code>{{ props.data.config?.variableName || 'user_input' }}</code>
                </div>
              </div>
            </div>
          </template>

          <!-- Custom AI Node -->
          <template #node-ai="props">
            <div class="custom-node ai-node" :class="{ 'active-sim-node': activeSimNodeId === props.id, 'active-replay-node': isReplayMode && activeReplayNodeId === props.id }">
              <Handle type="target" :position="Position.Left" />
              <Handle type="source" :position="Position.Right" />
              <div class="node-header">
                <i class="pi pi-microchip"></i>
                <span>{{ props.data.label }}</span>
                <span class="active-led text-violet-400" v-if="activeSimNodeId === props.id"></span>
              </div>
              <div class="node-body">
                <div class="node-preview prompt-preview">{{ props.data.config?.prompt || 'No system prompt' }}</div>
              </div>
            </div>
          </template>

          <!-- Custom Webhook Node -->
          <template #node-webhook="props">
            <div class="custom-node webhook-node" :class="{ 'active-sim-node': activeSimNodeId === props.id, 'active-replay-node': isReplayMode && activeReplayNodeId === props.id }">
              <Handle type="target" :position="Position.Left" />
              <Handle type="source" :position="Position.Right" />
              <div class="node-header">
                <i class="pi pi-cloud-upload"></i>
                <span>{{ props.data.label }}</span>
                <span class="active-led" v-if="activeSimNodeId === props.id"></span>
              </div>
              <div class="node-body">
                <div class="node-preview url-preview">{{ props.data.config?.url || 'No URL set' }}</div>
              </div>
            </div>
          </template>

          <!-- Custom Condition Node -->
          <template #node-condition="props">
            <div class="custom-node condition-node" :class="{ 'active-sim-node': activeSimNodeId === props.id, 'active-replay-node': isReplayMode && activeReplayNodeId === props.id }">
              <Handle type="target" :position="Position.Left" />
              <Handle type="source" :position="Position.Right" id="true" class="handle-true" />
              <Handle type="source" :position="Position.Bottom" id="false" class="handle-false" />
              <div class="node-header">
                <i class="pi pi-question-circle text-amber-400"></i>
                <span>{{ props.data.label }}</span>
                <span class="active-led" v-if="activeSimNodeId === props.id"></span>
              </div>
              <div class="node-body">
                <div class="var-badge">
                  <code>IF {{ props.data.config?.variableName || 'user_input' }}</code>
                </div>
                <div class="node-preview mt-1 text-slate-400 text-xs font-semibold">
                  {{ props.data.config?.operator }} "{{ props.data.config?.value }}"
                </div>
                <div class="handle-labels flex justify-between items-center text-[9px] font-bold text-slate-500 mt-2 px-1">
                  <span class="text-emerald-400/90">True ➜</span>
                  <span class="text-rose-400/90">➜ False</span>
                </div>
              </div>
            </div>
          </template>

          <!-- Custom Switch Node -->
          <template #node-switch="props">
            <div class="custom-node switch-node" :class="{ 'active-sim-node': activeSimNodeId === props.id, 'active-replay-node': isReplayMode && activeReplayNodeId === props.id }">
              <Handle type="target" :position="Position.Left" />
              <Handle type="source" :position="Position.Right" />
              <div class="node-header">
                <i class="pi pi-sitemap text-indigo-400"></i>
                <span>{{ props.data.label }}</span>
                <span class="active-led text-indigo-400" v-if="activeSimNodeId === props.id"></span>
              </div>
              <div class="node-body">
                <div class="var-badge">
                  <code>SWITCH ON {{ props.data.config?.variableName || 'user_input' }}</code>
                </div>
                <div class="node-preview mt-1 text-slate-400 text-xs font-semibold">
                  Multi-way Routing
                </div>
              </div>
            </div>
          </template>
        </VueFlow>

        <!-- Replay Playback Floating Control Center -->
        <div v-if="isReplayMode && replaySteps.length > 0" class="replay-control-center glassmorphic shadow-2xl">
          <div class="replay-header">
            <div class="flex items-center gap-2">
              <span class="pulse-green-led"></span>
              <span class="text-[11px] font-bold uppercase tracking-wider text-emerald-400">Flow Telemetry Playback</span>
            </div>
            <button class="exit-replay-btn" @click="exitReplayMode" title="Exit Replay mode">
              <i class="pi pi-times"></i>
            </button>
          </div>

          <div class="replay-main flex flex-col gap-2 mt-2">
            <div class="playback-actions flex items-center justify-center gap-4">
              <button class="icon-control-btn" @click="prevReplayStep" :disabled="currentReplayIndex <= 0">
                <i class="pi pi-chevron-left"></i>
              </button>
              
              <button class="play-pause-btn" :class="isPlayingReplay ? 'warn' : 'success'" @click="togglePlayReplay">
                <i :class="isPlayingReplay ? 'pi pi-pause' : 'pi pi-play'"></i>
              </button>
              
              <button class="icon-control-btn" @click="nextReplayStep" :disabled="currentReplayIndex >= replaySteps.length - 1">
                <i class="pi pi-chevron-right"></i>
              </button>
            </div>

            <div class="playback-progress-info mt-1">
              <div class="flex items-center justify-between text-[11px] mb-1">
                <span class="font-bold text-slate-200">Step {{ currentReplayIndex + 1 }} of {{ replaySteps.length }}</span>
                <span class="mono-text text-emerald-400 font-bold bg-emerald-950/60 border border-emerald-800/40 rounded px-1.5 py-0.5">
                  {{ replaySteps[currentReplayIndex]?.DurationMs ? replaySteps[currentReplayIndex].DurationMs.toFixed(1) + 'ms' : '0.0ms' }}
                </span>
              </div>
              <div class="progress-track">
                <div class="progress-fill" :style="{ width: `${((currentReplayIndex + 1) / replaySteps.length) * 100}%` }"></div>
              </div>
            </div>
          </div>

          <!-- Selected Step Diagnostics -->
          <div class="replay-diagnostics mt-3 p-3 bg-slate-950/80 border border-slate-800/60 rounded-lg">
            <div class="flex items-center gap-2 mb-2">
              <span class="badge uppercase" :class="replaySteps[currentReplayIndex]?.NodeType || 'info'">
                {{ replaySteps[currentReplayIndex]?.NodeType || 'step' }}
              </span>
              <span class="text-xs font-bold text-slate-100">{{ replaySteps[currentReplayIndex]?.NodeLabel }}</span>
            </div>

            <!-- Input/Output text previews -->
            <div v-if="replaySteps[currentReplayIndex]?.InputMessage" class="text-xs text-slate-300 bg-slate-900 p-2 rounded mb-2 border-l-2 border-slate-500">
              <strong class="text-[10px] text-slate-400 block mb-0.5">USER INPUT:</strong>
              "{{ replaySteps[currentReplayIndex]?.InputMessage }}"
            </div>

            <div v-if="replaySteps[currentReplayIndex]?.OutputMessage" class="text-xs text-emerald-300 bg-slate-900 p-2 rounded mb-2 border-l-2 border-emerald-500">
              <strong class="text-[10px] text-emerald-400 block mb-0.5">OUTPUT RESPONSE:</strong>
              {{ replaySteps[currentReplayIndex]?.OutputMessage }}
            </div>

            <!-- Variables panel -->
            <div v-if="replaySteps[currentReplayIndex]?.VariablesSnapshotJson && replaySteps[currentReplayIndex]?.VariablesSnapshotJson !== '{}'" class="mt-2">
              <span class="text-[10px] text-slate-400 font-bold block mb-1">VARIABLES SNAPSHOT:</span>
              <div class="flex flex-wrap gap-1.5 max-h-24 overflow-y-auto pr-1">
                <div v-for="(val, key) in JSON.parse(replaySteps[currentReplayIndex]?.VariablesSnapshotJson)" :key="key" 
                     class="flex items-center gap-1 bg-slate-800/80 border border-slate-700/50 px-2 py-0.5 rounded text-[10px]">
                  <span class="text-slate-400 font-mono">{{ key }}:</span>
                  <strong class="text-emerald-400 font-mono">"{{ val }}"</strong>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Properties Panel (Sidebar Right Glassmorphic) -->
    <div class="properties-panel glassmorphic" v-if="selectedNode">
      <div class="flex justify-between items-center mb-3">
        <h3 class="m-0">Node Configuration</h3>
        <button class="delete-node-btn bg-slate-900/40 hover:bg-rose-500/20 text-rose-400 hover:text-rose-300 border border-rose-500/30 hover:border-rose-500/50 px-2 py-1 rounded text-[10px] font-bold uppercase transition flex items-center gap-1" @click="removeNode(selectedNode.id)" title="Delete Node from canvas">
          <i class="pi pi-trash text-[9px]"></i> Delete Node
        </button>
      </div>
      <div class="prop-type-badge mb-4">
        <i class="pi pi-cog text-indigo-400"></i>
        <span>{{ selectedNode.data.customType.toUpperCase() }} NODE</span>
      </div>
      
      <div class="prop-group">
        <label>Custom Node Title</label>
        <input v-model="selectedNode.data.label" />
      </div>

      <!-- Selected Trigger Properties -->
      <div v-if="selectedNode.data.customType === 'trigger'" class="prop-group flex flex-col gap-3">
        <div>
          <label class="block text-xs font-semibold text-slate-400 mb-1">Trigger Type</label>
          <select v-model="selectedNode.data.config.triggerType" @change="flowTrigger = selectedNode.data.config.triggerType === 'keyword' ? (selectedNode.data.config.keyword || '') : selectedNode.data.config.triggerType === 'command' ? (selectedNode.data.config.command || '') : 'onStart'" class="w-full bg-slate-900/60 border border-slate-700/80 rounded p-2 text-sm text-slate-200">
            <option value="keyword">Keyword Match</option>
            <option value="command">Slash Command (/)</option>
            <option value="onstart">On Start (Widget Load)</option>
          </select>
        </div>

        <div v-if="(selectedNode.data.config.triggerType || 'keyword') === 'keyword'">
          <label class="block text-xs font-semibold text-slate-400 mb-1">Trigger Keyword</label>
          <input v-model="selectedNode.data.config.keyword" @input="flowTrigger = selectedNode.data.config.keyword" placeholder="e.g. support" class="w-full bg-slate-900/60 border border-slate-700/80 rounded p-2 text-sm text-slate-200" />
        </div>

        <div v-if="selectedNode.data.config.triggerType === 'command'">
          <label class="block text-xs font-semibold text-slate-400 mb-1">Slash Command Key</label>
          <input v-model="selectedNode.data.config.command" @input="flowTrigger = selectedNode.data.config.command" placeholder="e.g. /help" class="w-full bg-slate-900/60 border border-slate-700/80 rounded p-2 text-sm text-slate-200" />
        </div>
      </div>

      <div v-if="selectedNode.data.customType === 'message'" class="prop-group">
        <label>Message Content Payload</label>
        <textarea v-model="selectedNode.data.config.text" placeholder="Type response payload... Supports dynamic keys like {{email}}"></textarea>
      </div>

      <!-- Selected Rich Response Properties -->
      <div v-if="selectedNode.data.customType === 'richresponse'" class="prop-group flex flex-col gap-3">
        <div>
          <label class="block text-xs font-semibold text-slate-400 mb-1">Response Type</label>
          <select v-model="selectedNode.data.config.responseType" class="w-full bg-slate-900/60 border border-slate-700/80 rounded p-2 text-sm text-slate-200">
            <option value="card">Card / Banner</option>
            <option value="redirect">Auto-Redirect</option>
            <option value="file">File Download Link</option>
            <option value="form">Interactive Form</option>
            <option value="buttons">Quick Reply Buttons</option>
          </select>
        </div>

        <!-- Card fields -->
        <div v-if="selectedNode.data.config.responseType === 'card'" class="flex flex-col gap-2">
          <div>
            <label class="block text-xs font-semibold text-slate-400 mb-1">Card Title</label>
            <input v-model="selectedNode.data.config.title" placeholder="e.g. Welcome Deal!" class="w-full bg-slate-900/60 border border-slate-700/80 rounded p-2 text-sm text-slate-200" />
          </div>
          <div>
            <label class="block text-xs font-semibold text-slate-400 mb-1">Card Body text</label>
            <textarea v-model="selectedNode.data.config.body" placeholder="Deal descriptions, details..." class="w-full bg-slate-900/60 border border-slate-700/80 rounded p-2 text-sm text-slate-200 h-20"></textarea>
          </div>
          <div>
            <label class="block text-xs font-semibold text-slate-400 mb-1">Banner Image URL</label>
            <input v-model="selectedNode.data.config.imageUrl" placeholder="https://..." class="w-full bg-slate-900/60 border border-slate-700/80 rounded p-2 text-sm text-slate-200" />
          </div>
          <div>
            <label class="block text-xs font-semibold text-slate-400 mb-1">Action Button Label</label>
            <input v-model="selectedNode.data.config.buttonLabel" placeholder="e.g. Claim Now" class="w-full bg-slate-900/60 border border-slate-700/80 rounded p-2 text-sm text-slate-200" />
          </div>
          <div>
            <label class="block text-xs font-semibold text-slate-400 mb-1">Action Button Redirect URL</label>
            <input v-model="selectedNode.data.config.buttonUrl" placeholder="https://..." class="w-full bg-slate-900/60 border border-slate-700/80 rounded p-2 text-sm text-slate-200" />
          </div>
        </div>

        <!-- Redirect fields -->
        <div v-if="selectedNode.data.config.responseType === 'redirect'" class="flex flex-col gap-2">
          <div>
            <label class="block text-xs font-semibold text-slate-400 mb-1">Target Redirect URL</label>
            <input v-model="selectedNode.data.config.url" placeholder="https://..." class="w-full bg-slate-900/60 border border-slate-700/80 rounded p-2 text-sm text-slate-200" />
          </div>
          <div>
            <label class="block text-xs font-semibold text-slate-400 mb-1">Delay (Seconds)</label>
            <input type="number" v-model.number="selectedNode.data.config.seconds" placeholder="5" class="w-full bg-slate-900/60 border border-slate-700/80 rounded p-2 text-sm text-slate-200" />
          </div>
          <div>
            <label class="block text-xs font-semibold text-slate-400 mb-1">Countdown Custom Text</label>
            <input v-model="selectedNode.data.config.countdownText" placeholder="Redirecting you in {seconds} seconds..." class="w-full bg-slate-900/60 border border-slate-700/80 rounded p-2 text-sm text-slate-200" />
          </div>
        </div>

        <!-- File fields -->
        <div v-if="selectedNode.data.config.responseType === 'file'" class="flex flex-col gap-2">
          <div>
            <label class="block text-xs font-semibold text-slate-400 mb-1">Downloadable File URL</label>
            <input v-model="selectedNode.data.config.fileUrl" placeholder="https://..." class="w-full bg-slate-900/60 border border-slate-700/80 rounded p-2 text-sm text-slate-200" />
          </div>
          <div>
            <label class="block text-xs font-semibold text-slate-400 mb-1">Visible File Name</label>
            <input v-model="selectedNode.data.config.fileName" placeholder="document.pdf" class="w-full bg-slate-900/60 border border-slate-700/80 rounded p-2 text-sm text-slate-200" />
          </div>
        </div>

        <!-- Form fields -->
        <div v-if="selectedNode.data.config.responseType === 'form'" class="flex flex-col gap-2">
          <div>
            <label class="block text-xs font-semibold text-slate-400 mb-1">Form Title</label>
            <input v-model="selectedNode.data.config.title" placeholder="Customer Feedback Form" class="w-full bg-slate-900/60 border border-slate-700/80 rounded p-2 text-sm text-slate-200" />
          </div>
          <div>
            <label class="block text-xs font-semibold text-slate-400 mb-1">Form Fields (JSON array)</label>
            <textarea :value="JSON.stringify(selectedNode.data.config.fields || [], null, 2)" @input="(e: any) => { try { selectedNode.data.config.fields = JSON.parse(e.target.value); } catch(err) {} }" class="w-full bg-slate-900/60 border border-slate-700/80 rounded p-2 text-xs font-mono text-slate-200 h-40" placeholder='[{"label": "Name", "name": "name", "type": "text", "required": true}]'></textarea>
            <small class="tip">Supports label, name, type (text, textarea, select, checkbox, radio), placeholder, required, and comma-separated options.</small>
          </div>
        </div>

        <!-- Buttons fields -->
        <div v-if="selectedNode.data.config.responseType === 'buttons'" class="flex flex-col gap-2">
          <label class="block text-xs font-semibold text-slate-400">Manage Buttons</label>
          <div v-for="(btn, idx) in selectedNode.data.config.buttons || []" :key="idx" class="flex flex-col gap-1.5 p-2 bg-slate-800/40 border border-slate-700/60 rounded">
            <div class="flex items-center justify-between">
              <span class="text-xs font-bold text-slate-300">Button #{{ Number(idx) + 1 }}</span>
              <button class="text-rose-400 hover:text-rose-300 text-[10px] uppercase font-bold" @click="selectedNode.data.config.buttons.splice(Number(idx), 1)">Remove</button>
            </div>
            <div>
              <input v-model="btn.label" placeholder="Label (e.g. Yes)" class="w-full bg-slate-900/60 border border-slate-700/80 rounded p-1.5 text-xs text-slate-200" />
            </div>
            <div class="flex gap-2">
              <select v-model="btn.action" class="w-1/2 bg-slate-900/60 border border-slate-700/80 rounded p-1.5 text-xs text-slate-200">
                <option value="next">Next (Adv Flow)</option>
                <option value="url">URL Link</option>
                <option value="postback">Postback Value</option>
              </select>
              <input v-model="btn.value" placeholder="URL or Value" class="w-1/2 bg-slate-900/60 border border-slate-700/80 rounded p-1.5 text-xs text-slate-200" />
            </div>
          </div>
          <button @click="() => { selectedNode.data.config.buttons = selectedNode.data.config.buttons || []; selectedNode.data.config.buttons.push({ label: 'New Button', action: 'next', value: '' }) }" class="bg-indigo-600/30 hover:bg-indigo-600/50 text-indigo-200 border border-indigo-500/30 p-1.5 rounded text-xs font-bold uppercase transition">
            + Add Button
          </button>
        </div>
      </div>

      <!-- Selected AI Properties -->
      <div v-if="selectedNode.data.customType === 'ai'" class="prop-group flex flex-col gap-3">
        <div>
          <label>Prompt Context Override</label>
          <textarea v-model="selectedNode.data.config.prompt" placeholder="Instruct the agent context during this state..." class="w-full bg-slate-900/60 border border-slate-700/80 rounded p-2 text-sm text-slate-200 h-28"></textarea>
        </div>
        <div class="flex items-center gap-2 mt-1">
          <input type="checkbox" id="run-bg-chk" v-model="selectedNode.data.config.runInBackground" class="rounded border-slate-700 bg-slate-900" />
          <label for="run-bg-chk" class="text-xs font-semibold text-slate-300 cursor-pointer m-0">Run in Background</label>
        </div>
        <div v-if="selectedNode.data.config.runInBackground">
          <label class="block text-xs font-semibold text-slate-400 mb-1">Store Result in Variable</label>
          <input v-model="selectedNode.data.config.storeVariableName" placeholder="e.g. issue_category" class="w-full bg-slate-900/60 border border-slate-700/80 rounded p-2 text-sm text-slate-200" />
          <small class="tip">Stores the LLM's classification or response payload directly to this state session key.</small>
        </div>
      </div>

      <!-- Selected Input Properties -->
      <div v-if="selectedNode.data.customType === 'input'" class="prop-group flex flex-col gap-3">
        <div>
          <label>Session Variable Storage</label>
          <input v-model="selectedNode.data.config.variableName" placeholder="e.g. user_email" class="w-full bg-slate-900/60 border border-slate-700/80 rounded p-2 text-sm text-slate-200" />
          <small class="tip">Saves user response payload directly to this state session key.</small>
        </div>
        <div>
          <label class="block text-xs font-semibold text-slate-400 mb-1">Optional Inline Speak/Prompt Text</label>
          <input v-model="selectedNode.data.config.promptText" placeholder="e.g. Please describe your issue:" class="w-full bg-slate-900/60 border border-slate-700/80 rounded p-2 text-sm text-slate-200" />
          <small class="tip">If provided, the bot will display this message chunk just before pausing to capture user input.</small>
        </div>
      </div>

      <!-- Selected Webhook Properties -->
      <div v-if="selectedNode.data.customType === 'webhook'" class="prop-group">
        <label>Target Webhook URL</label>
        <input v-model="selectedNode.data.config.url" placeholder="https://..." />
        <small class="tip">Sends dynamic variable payload in POST JSON object.</small>
      </div>

      <!-- Selected Switch Properties -->
      <div v-if="selectedNode.data.customType === 'switch'" class="prop-group flex flex-col gap-3">
        <div>
          <label class="block text-xs font-semibold text-slate-400 mb-1">Variable to Switch On</label>
          <input v-model="selectedNode.data.config.variableName" placeholder="e.g. issue_category" class="w-full bg-slate-900/60 border border-slate-700/80 rounded p-2 text-sm text-slate-200" />
        </div>
        <small class="tip text-xs text-indigo-300">
          This evaluates the checked variable. Draw outgoing connections off this node and label them with match conditions like: <code>equals:Technical</code>, <code>equals:Billing</code>, or leave them empty/set to <code>default</code> to catch all others!
        </small>
      </div>

      <!-- Selected Condition Properties -->
      <div v-if="selectedNode.data.customType === 'condition'" class="prop-group flex flex-col gap-3">
        <div>
          <label class="block text-xs font-semibold text-slate-400 mb-1">Variable to Check</label>
          <input v-model="selectedNode.data.config.variableName" placeholder="e.g. user_email" class="w-full bg-slate-900/60 border border-slate-700/80 rounded p-2 text-sm text-slate-200" />
        </div>
        <div>
          <label class="block text-xs font-semibold text-slate-400 mb-1">Operator</label>
          <select v-model="selectedNode.data.config.operator" class="w-full bg-slate-900/60 border border-slate-700/80 rounded p-2 text-sm text-slate-200">
            <option value="equals">Equals</option>
            <option value="contains">Contains</option>
            <option value="regex">Matches Regex</option>
            <option value="exists">Exists / Not Empty</option>
            <option value="greaterthan">Greater Than (&gt;)</option>
            <option value="lessthan">Less Than (&lt;)</option>
          </select>
        </div>
        <div v-if="selectedNode.data.config.operator !== 'exists'">
          <label class="block text-xs font-semibold text-slate-400 mb-1">Comparison Value</label>
          <input v-model="selectedNode.data.config.value" placeholder="e.g. yes" class="w-full bg-slate-900/60 border border-slate-700/80 rounded p-2 text-sm text-slate-200" />
        </div>
        <small class="tip text-xs text-indigo-300">
          This evaluates the checked variable. Connected edges from this node must start at either <strong>True (Green, Right)</strong> or <strong>False (Red, Bottom)</strong>.
        </small>
      </div>
    </div>

    <div class="properties-panel glassmorphic" v-if="selectedEdge">
      <h3>Connection Router</h3>
      <div class="prop-type-badge mb-4">
        <i class="pi pi-link text-indigo-400"></i>
        <span>CONDITIONAL EDGE</span>
      </div>
      
      <div class="prop-group">
        <label>Route Matcher Rule</label>
        <input v-model="selectedEdge.label" placeholder="e.g. equals:yes" />
        <small class="tip">
          <strong>Syntax patterns:</strong><br/>
          • <code>equals:text</code> (exact matches)<br/>
          • <code>contains:text</code> (substring matched)<br/>
          • <code>regex:[0-9]</code> (regular expression)<br/>
          • Leave blank for immediate default path
        </small>
      </div>
    </div>

    <!-- HIGH-FIDELITY SIMULATOR PLAYGROUND (SLIDE UP / SLIDE LEFT SIDEBAR) -->
    <div class="simulator-sidebar glassmorphic" :class="{ 'sim-open': showSimulator }">
      <div class="sim-toggle" @click="toggleSimulator">
        <i class="pi pi-play-circle animate-pulse" v-if="!showSimulator"></i>
        <i class="pi pi-times-circle" v-else></i>
        <span v-if="!showSimulator">Interactive Dry-Run</span>
      </div>
      <div class="sim-content animate-fade-in" v-if="showSimulator">
        <div class="sim-header flex justify-between items-center border-b border-slate-700/60 pb-3">
          <div class="flex items-center gap-2">
            <span class="pulsing-heartbeat-led"></span>
            <h3>Dry-Run Sandbox</h3>
          </div>
          <button @click="resetSimulator" class="reset-sim-btn font-semibold text-xs flex items-center gap-1">
            <i class="pi pi-refresh"></i> Reset States
          </button>
        </div>

        <div class="sim-chat-area flex-1">
          <div class="sim-messages">
            <div v-for="(msg, i) in simMessages" :key="i" class="sim-msg" :class="msg.role">
              <div class="sim-bubble-wrapper">
                <span class="role-lbl">{{ msg.role.toUpperCase() }}</span>
                <div class="sim-bubble">{{ msg.content }}</div>
              </div>
            </div>
          </div>
          
          <div class="sim-input-area">
            <input v-model="simInput" @keyup.enter="sendSimMessage" placeholder="Reply to logic flows..." />
            <button @click="sendSimMessage" class="send-sim-action"><i class="pi pi-send"></i></button>
          </div>
        </div>

        <!-- DRY RUN DEBUGGER INSPECTOR PANEL -->
        <div class="sim-debug-inspector">
          <div class="inspector-tabs flex border-t border-slate-700/60">
            <div class="tab-label font-bold text-xs uppercase p-2 border-r border-slate-700/60 flex-1 text-center">
              <i class="pi pi-info-circle mr-1"></i> Live Variables Watcher
            </div>
          </div>
          <div class="inspector-body p-2 flex flex-col gap-2 max-h-36 overflow-y-auto">
            <div v-if="Object.keys(simVariables).length === 0" class="no-vars font-semibold text-xs text-slate-500 text-center py-4">
              Awaiting variables collection...
            </div>
            <table v-else class="var-watcher-table w-full text-xs text-left">
              <thead>
                <tr>
                  <th class="text-indigo-400">Context Key</th>
                  <th class="text-emerald-400">Extracted Payload</th>
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

          <!-- Trace Console Log Logs -->
          <div class="console-label font-bold text-xs uppercase p-2 border-t border-b border-slate-700/60 text-slate-400 flex items-center gap-1">
            <i class="pi pi-terminal text-indigo-400"></i> Execution Trace Console
          </div>
          <div class="inspector-console p-2 max-h-36 overflow-y-auto font-mono text-xs flex flex-col gap-1">
            <div v-if="simLogs.length === 0" class="no-console font-semibold text-slate-500 text-center py-2">
              No executions yet.
            </div>
            <div v-for="(log, idx) in simLogs" :key="idx" class="console-log flex gap-2" :class="log.type">
              <span class="log-time text-slate-500">{{ log.time }}</span>
              <span class="log-badge font-bold uppercase">{{ log.type }}</span>
              <span class="log-msg">{{ log.message }}</span>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.flow-builder-container {
  display: flex;
  height: calc(100vh - 64px);
  background: var(--p-surface-0);
  color: var(--p-text-color);
  font-family: 'Outfit', 'Inter', sans-serif;
  overflow: hidden;
  position: relative;
}

/* Glassmorphism sidebar & headers */
.glassmorphic {
  background: var(--p-surface-card) !important;
  backdrop-filter: blur(20px) saturate(180%);
  -webkit-backdrop-filter: blur(20px) saturate(180%);
  border: 1px solid var(--p-content-border-color) !important;
  box-shadow: 0 8px 32px 0 rgba(0, 0, 0, 0.15);
}

.sidebar {
  width: 260px;
  padding: 1.25rem;
  display: flex;
  flex-direction: column;
  gap: 12px;
  z-index: 10;
  height: 100%;
}

.sidebar h3 {
  margin: 0 0 1rem 0;
  font-size: 1rem;
  font-weight: 700;
  color: #818cf8; /* indigo 400 */
  letter-spacing: 0.5px;
}

.node-category {
  font-size: 0.7rem;
  font-weight: 800;
  text-transform: uppercase;
  color: #64748b;
  letter-spacing: 1px;
  margin-top: 5px;
}

.dndnode {
  padding: 12px;
  border-radius: 8px;
  cursor: grab;
  font-weight: 600;
  font-size: 0.85rem;
  color: #e2e8f0;
  display: flex;
  align-items: center;
  gap: 10px;
  transition: all 0.25s cubic-bezier(0.4, 0, 0.2, 1);
  background: rgba(30, 41, 59, 0.6);
  border: 1px solid rgba(255, 255, 255, 0.05);
}

.dndnode i {
  font-size: 1rem;
}

.dndnode:hover {
  transform: translateY(-2px);
  background: rgba(59, 130, 246, 0.15);
  border-color: rgba(59, 130, 246, 0.4);
  box-shadow: 0 4px 15px rgba(59, 130, 246, 0.2);
}

/* Custom coloring for specific palette buttons */
.node-trigger-btn:hover { border-color: rgba(234, 179, 8, 0.4); background: rgba(234, 179, 8, 0.12); }
.node-message-btn:hover { border-color: rgba(59, 130, 246, 0.4); background: rgba(59, 130, 246, 0.12); }
.node-input-btn:hover { border-color: rgba(16, 185, 129, 0.4); background: rgba(16, 185, 129, 0.12); }
.node-ai-btn:hover { border-color: rgba(139, 92, 246, 0.4); background: rgba(139, 92, 246, 0.12); }
.node-webhook-btn:hover { border-color: rgba(244, 63, 94, 0.4); background: rgba(244, 63, 94, 0.12); }

.main-content {
  flex: 1;
  display: flex;
  flex-direction: column;
  position: relative;
  height: 100%;
}

.header {
  height: 70px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 1.5rem;
  z-index: 10;
  border-radius: 0;
  border-top: none;
  border-left: none;
  border-right: none;
}

.title-section, .trigger-section {
  display: flex;
  align-items: center;
  gap: 10px;
  background: var(--p-surface-section);
  border: 1px solid var(--p-content-border-color);
  padding: 6px 12px;
  border-radius: 8px;
}

.flow-input {
  background: transparent;
  border: none;
  color: var(--p-text-color);
  font-weight: 600;
  font-size: 0.9rem;
  outline: none;
  width: 180px;
}

.flow-input::placeholder {
  color: var(--p-text-muted-color);
}

.save-btn {
  background: linear-gradient(135deg, #4f46e5 0%, #3b82f6 100%);
  color: white;
  border: none;
  padding: 8px 18px;
  border-radius: 8px;
  cursor: pointer;
  font-size: 0.85rem;
  display: flex;
  align-items: center;
  gap: 6px;
  box-shadow: 0 4px 15px rgba(59, 130, 246, 0.35);
  transition: all 0.25s ease;
}

.save-btn:hover {
  transform: translateY(-1px);
  box-shadow: 0 6px 20px rgba(59, 130, 246, 0.5);
  filter: brightness(1.1);
}

.vue-flow-wrapper {
  flex: 1;
  position: relative;
  background: var(--p-surface-50);
}

.properties-panel {
  width: 320px;
  padding: 1.5rem;
  height: 100%;
  overflow-y: auto;
  border-top: none;
  border-bottom: none;
  border-right: none;
  z-index: 10;
}

.properties-panel h3 {
  margin: 0;
  font-size: 1.05rem;
  font-weight: 700;
  color: #fff;
  border-bottom: 1px solid rgba(255, 255, 255, 0.08);
  padding-bottom: 8px;
}

.prop-type-badge {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 4px 10px;
  border-radius: 20px;
  background: rgba(99, 102, 241, 0.12);
  border: 1px solid rgba(99, 102, 241, 0.25);
  font-size: 0.7rem;
  font-weight: 800;
  color: #818cf8;
}

.prop-group {
  margin-top: 1.25rem;
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.prop-group label {
  font-size: 0.75rem;
  font-weight: 700;
  color: var(--p-text-muted-color);
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.prop-group input, .prop-group textarea, .prop-group select {
  background: var(--p-surface-section);
  border: 1px solid var(--p-content-border-color);
  border-radius: 6px;
  padding: 8px 12px;
  color: var(--p-text-color);
  font-size: 0.85rem;
  outline: none;
  transition: all 0.2s;
}

.prop-group input:focus, .prop-group textarea:focus, .prop-group select:focus {
  border-color: var(--p-primary-color);
  background: var(--p-surface-card);
  box-shadow: 0 0 0 3px color-mix(in srgb, var(--p-primary-color) 20%, transparent);
}

.prop-group textarea {
  min-height: 100px;
  resize: vertical;
}

.tip {
  font-size: 0.7rem;
  color: var(--p-text-muted-color);
  line-height: 1.35;
}

/* Stunning Custom Nodes Design */
.custom-node {
  background: var(--p-surface-card);
  border-radius: 12px;
  border: 1.5px solid var(--p-content-border-color);
  min-width: 180px;
  max-width: 240px;
  box-shadow: 0 10px 30px rgba(0, 0, 0, 0.1);
  overflow: hidden;
  transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
}

.custom-node:hover {
  transform: translateY(-2px);
  border-color: rgba(255, 255, 255, 0.15);
  box-shadow: 0 15px 35px rgba(0, 0, 0, 0.6);
}

/* Neon Active Simulator Glow Effect */
.active-sim-node {
  transform: scale(1.05) !important;
  box-shadow: 0 0 30px rgba(59, 130, 246, 0.45) !important;
  border-color: #3b82f6 !important;
}

.node-header {
  padding: 10px 14px;
  font-weight: 700;
  font-size: 0.8rem;
  display: flex;
  align-items: center;
  gap: 8px;
  color: #fff;
  border-bottom: 1px solid rgba(255, 255, 255, 0.05);
}

.node-header i { font-size: 0.9rem; }

.node-body {
  padding: 14px;
  font-size: 0.75rem;
  color: #94a3b8;
  background: rgba(30, 41, 59, 0.25);
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.badge {
  display: inline-flex;
  align-items: center;
  padding: 2px 8px;
  border-radius: 4px;
  font-size: 0.6rem;
  font-weight: 800;
  width: fit-content;
}
.badge.yellow { background: rgba(234, 179, 8, 0.12); color: #facc15; border: 1px solid rgba(234, 179, 8, 0.25); }

.trigger-label {
  font-weight: 700;
  color: #fff;
  font-size: 0.8rem;
}

.node-preview {
  font-size: 0.7rem;
  color: #cbd5e1;
  white-space: nowrap;
  text-overflow: ellipsis;
  overflow: hidden;
  max-width: 100%;
}

.var-badge {
  display: flex;
  align-items: center;
  background: rgba(16, 185, 129, 0.1);
  border: 1px solid rgba(16, 185, 129, 0.2);
  color: #34d399;
  padding: 4px 8px;
  border-radius: 6px;
  font-family: monospace;
  font-size: 0.7rem;
}

/* LEDs for active simulation steps */
.active-led {
  width: 8px;
  height: 8px;
  background-color: #3b82f6;
  border-radius: 50%;
  margin-left: auto;
  box-shadow: 0 0 10px #3b82f6, 0 0 20px #3b82f6;
  animation: pulse-led 1s infinite alternate;
}

@keyframes pulse-led {
  from { opacity: 0.4; }
  to { opacity: 1; }
}

/* Gradient themes for custom node headers */
.trigger-node .node-header { background: linear-gradient(135deg, #eab308 0%, #ca8a04 100%); }
.message-node .node-header { background: linear-gradient(135deg, #3b82f6 0%, #1d4ed8 100%); }
.richresponse-node .node-header { background: linear-gradient(135deg, #06b6d4 0%, #0891b2 100%); }
.input-node .node-header { background: linear-gradient(135deg, #10b981 0%, #047857 100%); }
.ai-node .node-header { background: linear-gradient(135deg, #8b5cf6 0%, #6d28d9 100%); }
.webhook-node .node-header { background: linear-gradient(135deg, #f43f5e 0%, #be123c 100%); }
.condition-node .node-header { background: linear-gradient(135deg, #f97316 0%, #c2410c 100%); }

.handle-true { background-color: #10b981 !important; width: 10px !important; height: 10px !important; border: 2px solid #fff !important; }
.handle-false { background-color: #ef4444 !important; width: 10px !important; height: 10px !important; border: 2px solid #fff !important; }
.node-condition-btn:hover { border-color: rgba(249, 115, 22, 0.4); background: rgba(249, 115, 22, 0.12); }
.node-richresponse-btn:hover { border-color: rgba(6, 182, 212, 0.4); background: rgba(6, 182, 212, 0.12); }

/* Dynamic connector animation for active path dry runs */
:deep(.active-sim-edge .vue-flow__edge-path) {
  stroke: #3b82f6 !important;
  stroke-width: 3.5px !important;
  stroke-dasharray: 6;
  animation: flow-run-pulse 1s linear infinite;
}

@keyframes flow-run-pulse {
  from { stroke-dashoffset: 24; }
  to { stroke-dashoffset: 0; }
}

/* Styled Edge Labels as elegant theme-aware chips */
:deep(.vue-flow__edge-text) {
  fill: var(--p-text-color) !important;
  font-size: 9px !important;
  font-weight: 800 !important;
  text-transform: uppercase !important;
  letter-spacing: 0.05em !important;
}

:deep(.vue-flow__edge-textbg) {
  fill: var(--p-surface-card) !important;
  stroke: var(--p-content-border-color) !important;
  stroke-width: 1px !important;
  rx: 6px !important; /* Rounded corners */
  ry: 6px !important;
  filter: drop-shadow(0 2px 4px rgba(0, 0, 0, 0.12)) !important;
}

/* HIGH FIDELITY SIMULATOR DESIGN */
.simulator-sidebar {
  position: absolute;
  right: 0;
  top: 0;
  bottom: 0;
  width: 0;
  transition: width 0.4s cubic-bezier(0.4, 0, 0.2, 1);
  display: flex;
  flex-direction: column;
  z-index: 100;
  border-top: none;
  border-bottom: none;
  border-right: none;
}

.simulator-sidebar.sim-open {
  width: 360px;
}

.sim-toggle {
  position: absolute;
  left: -128px;
  top: 20px;
  background: linear-gradient(135deg, #4f46e5 0%, #3b82f6 100%);
  color: white;
  padding: 10px 18px;
  border-radius: 20px 0 0 20px;
  cursor: pointer;
  display: flex;
  align-items: center;
  gap: 8px;
  font-weight: 700;
  font-size: 0.8rem;
  box-shadow: -4px 4px 15px rgba(0,0,0,0.25);
  transition: all 0.25s ease;
  border: 1px solid rgba(255,255,255,0.1);
  border-right: none;
}

.sim-open .sim-toggle {
  left: -40px;
  border-radius: 50%;
  padding: 12px;
}

.sim-toggle:hover {
  filter: brightness(1.1);
  transform: scale(1.02);
}

.sim-content {
  display: flex;
  flex-direction: column;
  height: 100%;
}

.pulsing-heartbeat-led {
  width: 10px;
  height: 10px;
  background-color: #10b981;
  border-radius: 50%;
  box-shadow: 0 0 8px #10b981;
  animation: pulse-green 1.2s infinite alternate;
}

@keyframes pulse-green {
  from { transform: scale(0.8); opacity: 0.5; }
  to { transform: scale(1.2); opacity: 1; }
}

.sim-header h3 {
  margin: 0;
  font-size: 0.95rem;
  font-weight: 700;
  color: #fff;
}

.reset-sim-btn {
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.08);
  color: #94a3b8;
  padding: 4px 8px;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s;
}
.reset-sim-btn:hover {
  background: rgba(239, 68, 68, 0.15);
  border-color: rgba(239, 68, 68, 0.35);
  color: #ef4444;
}

.sim-chat-area {
  display: flex;
  flex-direction: column;
  height: 45%;
  border-bottom: 1.5px solid rgba(255, 255, 255, 0.06);
}

.sim-messages {
  flex: 1;
  overflow-y: auto;
  padding: 1.25rem;
  display: flex;
  flex-direction: column;
  gap: 14px;
  background: rgba(9, 12, 21, 0.7);
}

.sim-msg {
  display: flex;
}

.sim-msg.user { justify-content: flex-end; }

.sim-bubble-wrapper {
  max-width: 85%;
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.sim-msg.user .sim-bubble-wrapper { align-items: flex-end; }

.role-lbl {
  font-size: 0.6rem;
  font-weight: 800;
  color: #64748b;
  letter-spacing: 0.5px;
}

.sim-bubble {
  padding: 10px 14px;
  border-radius: 12px;
  font-size: 0.8rem;
  line-height: 1.4;
}

.sim-msg.user .sim-bubble {
  background: linear-gradient(135deg, #4f46e5 0%, #3b82f6 100%);
  color: white;
  border-bottom-right-radius: 2px;
  box-shadow: 0 4px 12px rgba(59, 130, 246, 0.2);
}

.sim-msg.ai .sim-bubble {
  background: rgba(30, 41, 59, 0.85);
  color: #e2e8f0;
  border-bottom-left-radius: 2px;
  box-shadow: 0 4px 10px rgba(0,0,0,0.15);
  border: 1px solid rgba(255, 255, 255, 0.05);
}

.sim-input-area {
  padding: 10px 14px;
  background: rgba(15, 23, 42, 0.9);
  display: flex;
  gap: 8px;
  border-top: 1px solid rgba(255, 255, 255, 0.05);
}

.sim-input-area input {
  flex: 1;
  background: rgba(30, 41, 59, 0.6);
  border: 1px solid rgba(255, 255, 255, 0.08);
  padding: 8px 14px;
  border-radius: 20px;
  color: #fff;
  font-size: 0.8rem;
  outline: none;
}

.sim-input-area input:focus {
  border-color: #3b82f6;
  background: rgba(30, 41, 59, 0.8);
}

.send-sim-action {
  background: #3b82f6;
  color: white;
  border: none;
  width: 34px;
  height: 34px;
  border-radius: 50%;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.2s;
}
.send-sim-action:hover {
  background: #2563eb;
  transform: scale(1.05);
}

/* DRY RUN DIAGNOSTICS & TRACE PANELS */
.sim-debug-inspector {
  flex: 1.2;
  display: flex;
  flex-direction: column;
  background: rgba(10, 14, 23, 0.95);
}

.inspector-tabs {
  background: rgba(15, 23, 42, 0.8);
}

.tab-label {
  color: #818cf8;
}

.var-watcher-table th {
  padding-bottom: 6px;
  border-bottom: 1.5px solid rgba(255,255,255,0.06);
}

.var-watcher-table td {
  padding: 6px 0;
  border-bottom: 1px solid rgba(255,255,255,0.03);
}

.var-watcher-table code {
  background: rgba(16, 185, 129, 0.1);
  color: #10b981;
  padding: 2px 6px;
  border-radius: 4px;
  font-family: monospace;
}

.inspector-console {
  background: #05070c;
  flex: 1;
}

.console-log {
  padding: 4px 6px;
  border-radius: 4px;
  line-height: 1.35;
}

.console-log.info { background: rgba(59, 130, 246, 0.05); color: #93c5fd; }
.console-log.success { background: rgba(16, 185, 129, 0.05); color: #6ee7b7; }
.console-log.pending { background: rgba(234, 179, 8, 0.05); color: #fef08a; }
.console-log.error { background: rgba(239, 68, 68, 0.05); color: #fca5a5; }

.log-badge {
  font-size: 0.6rem;
  padding: 1px 4px;
  border-radius: 3px;
  height: fit-content;
  border: 1px solid currentColor;
}

/* Replay visualization glow styles */
.active-replay-node {
  border: 3px solid #10b981 !important;
  box-shadow: 0 0 15px rgba(16, 185, 129, 0.75), 0 0 30px rgba(16, 185, 129, 0.4) !important;
  animation: glow-pulse-replay 1.5s infinite alternate !important;
}

@keyframes glow-pulse-replay {
  from {
    box-shadow: 0 0 10px rgba(16, 185, 129, 0.5), 0 0 20px rgba(16, 185, 129, 0.2);
  }
  to {
    box-shadow: 0 0 20px rgba(16, 185, 129, 0.9), 0 0 40px rgba(16, 185, 129, 0.5);
  }
}

/* Replay Telemetry Floating Player */
.replay-control-center {
  position: absolute;
  bottom: 24px;
  left: 50%;
  transform: translateX(-50%);
  z-index: 1000;
  width: 420px;
  background: rgba(15, 23, 42, 0.85);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 16px;
  padding: 16px;
  box-shadow: 0 10px 30px rgba(0, 0, 0, 0.5);
  backdrop-filter: blur(16px);
  color: #fff;
  transition: all 0.3s ease;
}

.replay-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  border-bottom: 1px solid rgba(255, 255, 255, 0.1);
  padding-bottom: 8px;
  margin-bottom: 12px;
}

.exit-replay-btn {
  background: transparent;
  border: none;
  color: #94a3b8;
  cursor: pointer;
  transition: color 0.2s;
  font-size: 1rem;
}
.exit-replay-btn:hover {
  color: #ef4444;
}

.pulse-green-led {
  width: 10px;
  height: 10px;
  background-color: #10b981;
  border-radius: 50%;
  box-shadow: 0 0 10px #10b981, 0 0 20px #10b981;
  animation: pulse-led 1s infinite alternate;
}

.playback-actions {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 16px;
}

.icon-control-btn {
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.1);
  color: #cbd5e1;
  width: 32px;
  height: 32px;
  border-radius: 50%;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.2s;
}
.icon-control-btn:hover:not(:disabled) {
  background: rgba(255, 255, 255, 0.15);
  color: #fff;
  border-color: rgba(255, 255, 255, 0.2);
}
.icon-control-btn:disabled {
  opacity: 0.4;
  cursor: not-allowed;
}

.play-pause-btn {
  width: 40px;
  height: 40px;
  border-radius: 50%;
  border: none;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  color: #fff;
  font-size: 1rem;
  transition: all 0.2s;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.3);
}
.play-pause-btn.success {
  background: linear-gradient(135deg, #10b981 0%, #059669 100%);
}
.play-pause-btn.success:hover {
  filter: brightness(1.1);
  transform: scale(1.05);
}
.play-pause-btn.warn {
  background: linear-gradient(135deg, #f59e0b 0%, #d97706 100%);
}
.play-pause-btn.warn:hover {
  filter: brightness(1.1);
  transform: scale(1.05);
}

.progress-track {
  width: 100%;
  height: 6px;
  background: rgba(255, 255, 255, 0.1);
  border-radius: 3px;
  overflow: hidden;
  margin-top: 6px;
}

.progress-fill {
  height: 100%;
  background: #10b981;
  border-radius: 3px;
  transition: width 0.3s ease;
}

.replay-diagnostics {
  transition: all 0.3s ease;
}

.replay-diagnostics .badge {
  font-size: 0.65rem;
  font-weight: 800;
  padding: 2px 6px;
  border-radius: 4px;
}
.replay-diagnostics .badge.trigger {
  background: rgba(234, 179, 8, 0.15);
  color: #fbbf24;
}
.replay-diagnostics .badge.richresponse {
  background: rgba(6, 182, 212, 0.15);
  color: #22d3ee;
}
.replay-diagnostics .badge.message {
  background: rgba(59, 130, 246, 0.15);
  color: #60a5fa;
}
.replay-diagnostics .badge.input {
  background: rgba(16, 185, 129, 0.15);
  color: #34d399;
}
.replay-diagnostics .badge.ai {
  background: rgba(139, 92, 246, 0.15);
  color: #a78bfa;
}
.replay-diagnostics .badge.webhook {
  background: rgba(244, 63, 94, 0.15);
  color: #f43f5e;
}
.replay-diagnostics .badge.condition {
  background: rgba(249, 115, 22, 0.15);
  color: #fdba74;
}
</style>

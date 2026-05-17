<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { useRoute } from 'vue-router';
import { VueFlow, useVueFlow, Handle, Position } from '@vue-flow/core';
import { Background } from '@vue-flow/background';
import { Controls } from '@vue-flow/controls';
import { MiniMap } from '@vue-flow/minimap';
import '@vue-flow/core/dist/style.css';
import '@vue-flow/core/dist/theme-default.css';
import '@vue-flow/controls/dist/style.css';
import '@vue-flow/minimap/dist/style.css';

const route = useRoute();
const projectId = route.params.projectId as string;
const { addNodes, addEdges, onConnect, nodes, edges } = useVueFlow();

const flowId = ref<string | null>(null);
const flowName = ref('New Flow');
const flowTrigger = ref('');

const selectedNode = ref<any>(null);
const selectedEdge = ref<any>(null);

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

  const position = { x: event.clientX - 250, y: event.clientY - 50 }; // Adjust for sidebar offset
  const newNode = {
    id: `node_${Date.now()}`,
    type: type, // Use custom type
    position,
    data: { 
      customType: type,
      label: `${type.charAt(0).toUpperCase() + type.slice(1)} Node`,
      config: type === 'message' ? { text: 'Hello!' } : 
              type === 'ai' ? { prompt: 'You are an AI.' } : 
              type === 'webhook' ? { url: 'https://api.example.com' } : {}
    },
  };
  addNodes([newNode]);
};

onConnect((params) => {
  addEdges([params]);
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
  try {
    const res = await fetch(`/api/projects/${projectId}/flows`, {
      headers: { 'Authorization': `Bearer ${localStorage.getItem('acb_token')}` }
    });
    if (res.ok) {
      const data = await res.json();
      if (data.length > 0) {
        const flow = data[0]; // Load first flow for simplicity
        flowId.value = flow.id;
        flowName.value = flow.name;
        flowTrigger.value = flow.triggerKeyword || '';
        
        const resDetail = await fetch(`/api/projects/${projectId}/flows/${flow.id}`, {
          headers: { 'Authorization': `Bearer ${localStorage.getItem('acb_token')}` }
        });
        if (resDetail.ok) {
          const detail = await resDetail.json();
          addNodes(detail.nodes.map((n: any) => ({
            id: n.id,
            position: { x: n.positionX, y: n.positionY },
            data: JSON.parse(n.dataJson),
            type: JSON.parse(n.dataJson).customType || 'default'
          })));
          addEdges(detail.edges.map((e: any) => ({
            id: e.id,
            source: e.sourceNodeId,
            target: e.targetNodeId,
            label: e.condition || ''
          })));
        }
      }
    }
  } catch (error) {
    console.error("Failed to load flow", error);
  }
};

const saveFlow = async () => {
  const flowData = {
    name: flowName.value,
    triggerKeyword: flowTrigger.value,
    isActive: true,
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
      condition: e.label?.toString() || ''
    }))
  };

  const url = flowId.value ? `/api/projects/${projectId}/flows/${flowId.value}` : `/api/projects/${projectId}/flows`;
  const method = flowId.value ? 'PUT' : 'POST';

  try {
    const res = await fetch(url, {
      method,
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${localStorage.getItem('acb_token')}`
      },
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

const showSimulator = ref(false);
const simMessages = ref([{ role: 'ai', content: 'Simulation started. Type to test.' }]);
const simInput = ref('');

const toggleSimulator = () => {
  showSimulator.value = !showSimulator.value;
};

const sendSimMessage = () => {
  if (!simInput.value.trim()) return;
  simMessages.value.push({ role: 'user', content: simInput.value });
  simInput.value = '';
  
  // Placeholder: In real implementation, this would call a test API endpoint
  setTimeout(() => {
    simMessages.value.push({ role: 'ai', content: 'This is a simulation placeholder response.' });
  }, 1000);
};

onMounted(() => {
  loadFlow();
});
</script>

<template>
  <div class="flow-builder-container">
    <div class="sidebar">
      <h3>Nodes</h3>
      <div class="dndnode input" draggable="true" @dragstart="onDragStart($event, 'trigger')">Trigger</div>
      <div class="dndnode" draggable="true" @dragstart="onDragStart($event, 'message')">Message</div>
      <div class="dndnode" draggable="true" @dragstart="onDragStart($event, 'input')">User Input</div>
      <div class="dndnode" draggable="true" @dragstart="onDragStart($event, 'ai')">AI Prompt</div>
      <div class="dndnode output" draggable="true" @dragstart="onDragStart($event, 'webhook')">Webhook</div>
    </div>
    
    <div class="main-content">
      <div class="header">
        <input v-model="flowName" placeholder="Flow Name" class="flow-input" />
        <input v-model="flowTrigger" placeholder="Trigger Keyword (e.g. support)" class="flow-input" />
        <button class="save-btn" @click="saveFlow">Save Flow</button>
      </div>

      <div class="vue-flow-wrapper" @drop="onDrop" @dragover="onDragOver">
        <VueFlow 
          @nodeClick="onNodeClick" 
          @edgeClick="onEdgeClick"
          @paneClick="onPaneClick"
          :snap-to-grid="true"
          :snap-grid="[16, 16]"
        >
          <Background pattern-color="#aaa" :gap="16" />
          <Controls />
          <MiniMap pannable zoomable />

          <!-- Custom Trigger Node -->
          <template #node-trigger="props">
            <div class="custom-node trigger-node">
              <Handle type="source" :position="Position.Right" />
              <div class="node-header"><i class="pi pi-bolt"></i> {{ props.data.label }}</div>
              <div class="node-body">Entry Point</div>
            </div>
          </template>

          <!-- Custom Message Node -->
          <template #node-message="props">
            <div class="custom-node message-node">
              <Handle type="target" :position="Position.Left" />
              <Handle type="source" :position="Position.Right" />
              <div class="node-header"><i class="pi pi-comment"></i> {{ props.data.label }}</div>
              <div class="node-body">{{ (props.data.config?.text || '').substring(0, 30) }}...</div>
            </div>
          </template>

          <!-- Custom Input Node -->
          <template #node-input="props">
            <div class="custom-node input-node">
              <Handle type="target" :position="Position.Left" />
              <Handle type="source" :position="Position.Right" />
              <div class="node-header"><i class="pi pi-user-edit"></i> {{ props.data.label }}</div>
              <div class="node-body">Wait for user reply</div>
            </div>
          </template>

          <!-- Custom AI Node -->
          <template #node-ai="props">
            <div class="custom-node ai-node">
              <Handle type="target" :position="Position.Left" />
              <Handle type="source" :position="Position.Right" />
              <div class="node-header"><i class="pi pi-microchip"></i> {{ props.data.label }}</div>
              <div class="node-body">{{ (props.data.config?.prompt || '').substring(0, 30) }}...</div>
            </div>
          </template>

          <!-- Custom Webhook Node -->
          <template #node-webhook="props">
            <div class="custom-node webhook-node">
              <Handle type="target" :position="Position.Left" />
              <Handle type="source" :position="Position.Right" />
              <div class="node-header"><i class="pi pi-cloud-upload"></i> {{ props.data.label }}</div>
              <div class="node-body">{{ props.data.config?.url || 'No URL set' }}</div>
            </div>
          </template>
        </VueFlow>
      </div>
    </div>

    <div class="properties-panel" v-if="selectedNode">
      <h3>Node Properties</h3>
      <p><strong>Type:</strong> {{ selectedNode.data.customType }}</p>
      
      <div class="prop-group">
        <label>Label</label>
        <input v-model="selectedNode.data.label" />
      </div>

      <div v-if="selectedNode.data.customType === 'message'" class="prop-group">
        <label>Message Text</label>
        <textarea v-model="selectedNode.data.config.text"></textarea>
      </div>

      <div v-if="selectedNode.data.customType === 'ai'" class="prop-group">
        <label>System Prompt</label>
        <textarea v-model="selectedNode.data.config.prompt"></textarea>
      </div>

      <div v-if="selectedNode.data.customType === 'input'" class="prop-group">
        <label>Variable Name</label>
        <input v-model="selectedNode.data.config.variableName" placeholder="e.g. user_email" />
        <small>Saves user reply to this variable.</small>
      </div>

      <div v-if="selectedNode.data.customType === 'webhook'" class="prop-group">
        <label>Webhook URL</label>
        <input v-model="selectedNode.data.config.url" />
      </div>
    </div>

    <div class="properties-panel" v-if="selectedEdge">
      <h3>Edge Properties</h3>
      
      <div class="prop-group">
        <label>Condition</label>
        <input v-model="selectedEdge.label" placeholder="e.g. equals:yes" />
        <small>Formats: <code>equals:value</code>, <code>contains:value</code>, <code>regex:pattern</code></small>
      </div>
    </div>

    <!-- Simulator Sidebar -->
    <div class="simulator-sidebar" :class="{ 'sim-open': showSimulator }">
      <div class="sim-toggle" @click="toggleSimulator">
        <i class="pi pi-play" v-if="!showSimulator"></i>
        <i class="pi pi-times" v-else></i>
        <span v-if="!showSimulator">Test Flow</span>
      </div>
      <div class="sim-content" v-if="showSimulator">
        <div class="sim-header">
          <h3>Test Simulator</h3>
        </div>
        <div class="sim-messages">
          <div v-for="(msg, i) in simMessages" :key="i" class="sim-msg" :class="msg.role">
            <div class="sim-bubble">{{ msg.content }}</div>
          </div>
        </div>
        <div class="sim-input-area">
          <input v-model="simInput" @keyup.enter="sendSimMessage" placeholder="Type a message..." />
          <button @click="sendSimMessage"><i class="pi pi-send"></i></button>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.flow-builder-container {
  display: flex;
  height: calc(100vh - 64px); /* assuming header is 64px */
  background: #f8fafc;
}

.sidebar {
  width: 250px;
  background: white;
  border-right: 1px solid #e2e8f0;
  padding: 1rem;
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.sidebar h3 {
  margin-top: 0;
  margin-bottom: 1rem;
  font-size: 1.1rem;
  color: #1e293b;
}

.dndnode {
  padding: 10px;
  border: 1px solid #cbd5e1;
  border-radius: 4px;
  background: #f1f5f9;
  cursor: grab;
  text-align: center;
  font-weight: 500;
  color: #334155;
  transition: all 0.2s;
}
.dndnode:hover {
  background: #e2e8f0;
}

.main-content {
  flex: 1;
  display: flex;
  flex-direction: column;
  position: relative;
}

.header {
  height: 60px;
  background: white;
  border-bottom: 1px solid #e2e8f0;
  display: flex;
  align-items: center;
  padding: 0 1rem;
  gap: 1rem;
}

.flow-input {
  padding: 0.5rem;
  border: 1px solid #cbd5e1;
  border-radius: 4px;
  outline: none;
}
.flow-input:focus {
  border-color: #3b82f6;
}

.save-btn {
  margin-left: auto;
  background: #3b82f6;
  color: white;
  border: none;
  padding: 0.5rem 1rem;
  border-radius: 4px;
  cursor: pointer;
  font-weight: 500;
}
.save-btn:hover {
  background: #2563eb;
}

.vue-flow-wrapper {
  flex: 1;
  position: relative;
}

.properties-panel {
  width: 300px;
  background: white;
  border-left: 1px solid #e2e8f0;
  padding: 1rem;
  overflow-y: auto;
}

.properties-panel h3 {
  margin-top: 0;
  border-bottom: 1px solid #e2e8f0;
  padding-bottom: 0.5rem;
}

.prop-group {
  margin-top: 1rem;
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}
.prop-group label {
  font-size: 0.85rem;
  font-weight: 600;
  color: #475569;
}
.prop-group input, .prop-group textarea {
  padding: 0.5rem;
  border: 1px solid #cbd5e1;
  border-radius: 4px;
  outline: none;
}
.prop-group textarea {
  resize: vertical;
  min-height: 80px;
}

/* Custom Node Styling */
.custom-node {
  background: white;
  border-radius: 8px;
  box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.1), 0 2px 4px -1px rgba(0, 0, 0, 0.06);
  min-width: 150px;
  overflow: hidden;
  border: 1px solid #e2e8f0;
}
.node-header {
  padding: 8px 12px;
  font-weight: 600;
  font-size: 0.85rem;
  display: flex;
  align-items: center;
  gap: 6px;
  color: white;
}
.node-body {
  padding: 12px;
  font-size: 0.8rem;
  color: #475569;
  background: white;
}

.trigger-node .node-header { background: #eab308; }
.message-node .node-header { background: #3b82f6; }
.input-node .node-header { background: #10b981; }
.ai-node .node-header { background: #8b5cf6; }
.webhook-node .node-header { background: #f43f5e; }

/* Simulator Styling */
.simulator-sidebar {
  position: absolute;
  right: 0;
  top: 0;
  bottom: 0;
  width: 0;
  background: white;
  border-left: 1px solid #e2e8f0;
  transition: width 0.3s;
  display: flex;
  flex-direction: column;
  z-index: 100;
}
.simulator-sidebar.sim-open {
  width: 320px;
}
.sim-toggle {
  position: absolute;
  left: -120px;
  top: 20px;
  background: #3b82f6;
  color: white;
  padding: 8px 16px;
  border-radius: 20px 0 0 20px;
  cursor: pointer;
  display: flex;
  align-items: center;
  gap: 8px;
  font-weight: 600;
  box-shadow: -2px 2px 5px rgba(0,0,0,0.1);
  transition: all 0.2s;
}
.sim-open .sim-toggle {
  left: -40px;
  border-radius: 50%;
  padding: 12px;
}
.sim-toggle:hover {
  background: #2563eb;
}
.sim-content {
  display: flex;
  flex-direction: column;
  height: 100%;
}
.sim-header {
  padding: 1rem;
  border-bottom: 1px solid #e2e8f0;
  background: #f8fafc;
}
.sim-header h3 { margin: 0; font-size: 1.1rem; }
.sim-messages {
  flex: 1;
  overflow-y: auto;
  padding: 1rem;
  display: flex;
  flex-direction: column;
  gap: 12px;
  background: #f1f5f9;
}
.sim-msg {
  display: flex;
}
.sim-msg.user { justify-content: flex-end; }
.sim-bubble {
  max-width: 85%;
  padding: 8px 12px;
  border-radius: 12px;
  font-size: 0.9rem;
}
.sim-msg.user .sim-bubble {
  background: #3b82f6;
  color: white;
  border-bottom-right-radius: 2px;
}
.sim-msg.ai .sim-bubble {
  background: white;
  color: #334155;
  border-bottom-left-radius: 2px;
  box-shadow: 0 1px 2px rgba(0,0,0,0.05);
}
.sim-input-area {
  padding: 1rem;
  background: white;
  border-top: 1px solid #e2e8f0;
  display: flex;
  gap: 8px;
}
.sim-input-area input {
  flex: 1;
  padding: 8px 12px;
  border: 1px solid #cbd5e1;
  border-radius: 20px;
  outline: none;
}
.sim-input-area button {
  background: #3b82f6;
  color: white;
  border: none;
  width: 36px;
  height: 36px;
  border-radius: 50%;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
}
</style>

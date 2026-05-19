// ─── FlowBuilder Types & Constants ────────────────────────────────────────────

export interface QuickReplyButton {
  label: string;
  action: 'next' | 'url' | 'postback';
  value: string;
}

export interface NodeConfig {
  // Trigger
  triggerType?: 'keyword' | 'command' | 'onstart';
  keyword?: string;
  command?: string;
  // Message
  text?: string;
  // AI
  prompt?: string;
  runInBackground?: boolean;
  storeVariableName?: string;
  // Webhook
  url?: string;
  // Input
  variableName?: string;
  promptText?: string;
  // Condition / Switch
  operator?: ConditionOperator;
  value?: string;
  // Rich Response
  responseType?: 'card' | 'redirect' | 'file' | 'form' | 'buttons';
  title?: string;
  body?: string;
  imageUrl?: string;
  buttonLabel?: string;
  buttonUrl?: string;
  seconds?: number;
  countdownText?: string;
  fileUrl?: string;
  fileName?: string;
  fields?: FormField[];
  buttons?: QuickReplyButton[];
}

export type ConditionOperator =
  | 'equals'
  | 'contains'
  | 'regex'
  | 'exists'
  | 'greaterthan'
  | 'lessthan';

export interface FormField {
  label: string;
  name: string;
  type: 'text' | 'textarea' | 'select' | 'checkbox' | 'radio';
  placeholder?: string;
  required?: boolean;
  options?: string;
}

export type NodeType =
  | 'trigger'
  | 'message'
  | 'richresponse'
  | 'input'
  | 'ai'
  | 'webhook'
  | 'condition'
  | 'switch';

export interface FlowNodeData {
  customType: NodeType;
  label: string;
  config: NodeConfig;
}

export interface SimLog {
  time: string;
  type: 'info' | 'success' | 'pending' | 'error';
  message: string;
}

export interface SimMessage {
  role: 'user' | 'ai';
  content: string;
}

export interface HistorySnapshot {
  nodes: string;
  edges: string;
}

export interface ReplayStep {
  NodeId: string;
  NodeType: NodeType;
  NodeLabel: string;
  DurationMs: number;
  InputMessage?: string;
  OutputMessage?: string;
  VariablesSnapshotJson?: string;
}

// ─── Palette Items ─────────────────────────────────────────────────────────────

export interface PaletteItem {
  type: NodeType;
  label: string;
  icon: string;
  category: 'Foundations' | 'Interactivity' | 'Logic';
  accentVar: string;
}

export const PALETTE_ITEMS: PaletteItem[] = [
  // Foundations
  { type: 'trigger',      label: 'Trigger',         icon: 'pi pi-bolt',         category: 'Foundations',   accentVar: '--accent-trigger' },
  { type: 'message',      label: 'Text Bubble',      icon: 'pi pi-comment',      category: 'Foundations',   accentVar: '--accent-message' },
  { type: 'richresponse', label: 'Rich Response',    icon: 'pi pi-images',       category: 'Foundations',   accentVar: '--accent-rich' },
  // Interactivity
  { type: 'input',        label: 'Wait & Capture',   icon: 'pi pi-user-edit',    category: 'Interactivity', accentVar: '--accent-input' },
  { type: 'ai',           label: 'AI Prompt',        icon: 'pi pi-microchip',    category: 'Interactivity', accentVar: '--accent-ai' },
  { type: 'webhook',      label: 'POST Webhook',     icon: 'pi pi-cloud-upload', category: 'Interactivity', accentVar: '--accent-webhook' },
  // Logic
  { type: 'condition',    label: 'Branch Condition', icon: 'pi pi-question-circle', category: 'Logic',      accentVar: '--accent-condition' },
  { type: 'switch',       label: 'Multi Switch',     icon: 'pi pi-sitemap',           category: 'Logic',      accentVar: '--accent-switch' },
];

// ─── Default Node Configs ──────────────────────────────────────────────────────

export function getDefaultConfig(type: NodeType): NodeConfig {
  switch (type) {
    case 'message':      return { text: 'Hello! How can I help you today?' };
    case 'ai':           return { prompt: 'You are a helpful assistant answering inquiries based on collected context.', runInBackground: false, storeVariableName: '' };
    case 'webhook':      return { url: 'https://api.example.com/v1/webhook' };
    case 'input':        return { variableName: 'user_input', promptText: '' };
    case 'condition':    return { variableName: 'user_input', operator: 'equals', value: 'yes' };
    case 'switch':       return { variableName: 'user_input' };
    case 'richresponse': return {
      responseType: 'card',
      title: 'Special Offer',
      body: 'Get 20% off your purchase.',
      imageUrl: 'https://picsum.photos/400/200',
      buttonLabel: 'Claim Offer',
      buttonUrl: 'https://example.com/claim',
      seconds: 5,
      countdownText: 'Redirecting you in {seconds} seconds...',
      fileUrl: 'https://example.com/doc.pdf',
      fileName: 'document.pdf',
      fields: [],
      buttons: [],
    };
    case 'trigger': return { triggerType: 'keyword', keyword: '' };
    default:        return {};
  }
}

export function getNodeLabel(type: NodeType): string {
  return PALETTE_ITEMS.find(p => p.type === type)?.label ?? type;
}

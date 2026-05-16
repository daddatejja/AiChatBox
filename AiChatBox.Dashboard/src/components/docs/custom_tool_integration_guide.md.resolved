# Custom Tools & Webhook Integration Guide

Custom Tools allow the AI to interact with your own application's APIs in real-time. This guide explains how to configure, implement, and secure a custom tool using webhooks.

## 1. Dashboard Configuration

To enable a custom tool, you must configure two parts: the **Project Webhook** and the **Tool Definition**.

### A. Project Webhook Settings
In your Project Details, under **Project Settings**, configure the following:

- **Webhook URL**: The full endpoint in your application that will handle tool requests (e.g., `https://api.myapp.com/webhooks/aichat`).
- **Webhook Secret**: (Optional) A secret string used to sign requests. If provided, AiChatBox will send an `X-Hub-Signature` header containing an HMAC-SHA256 hash of the payload.

### B. Tool Definition
Under the **Custom Tools** section, click **New Tool**:

- **Tool Name**: `check_inventory` (Use lowercase and underscores, no spaces).
- **Description**: `Checks the available stock for a specific product SKU.`
- **Parameters JSON Schema**:
  ```json
  {
    "type": "object",
    "properties": {
      "sku": {
        "type": "string",
        "description": "The unique product SKU to check."
      }
    },
    "required": ["sku"]
  }
  ```

---

## 2. Webhook Implementation Example

When the AI decides to call your tool, AiChatBox sends a `POST` request to your Webhook URL.

### Request Payload
```json
{
  "ProjectName": "E-commerce Bot",
  "Tool": "check_inventory",
  "Arguments": {
    "sku": "LAP-102"
  }
}
```

### Server-Side Implementation (Node.js/Express)

```javascript
const express = require('express');
const crypto = require('crypto');
const app = express();

app.use(express.json());

const WEBHOOK_SECRET = 'your-secure-secret-from-dashboard';

// Security Helper: Verify Signature
function verifySignature(req) {
    const signature = req.headers['x-hub-signature'];
    if (!signature) return false;

    const hmac = crypto.createHmac('sha256', WEBHOOK_SECRET);
    const digest = hmac.update(JSON.stringify(req.body)).digest('hex');
    return signature === digest;
}

app.post('/webhooks/aichat', (req, res) => {
    // 1. Verify security (if secret is configured)
    if (WEBHOOK_SECRET && !verifySignature(req)) {
        return res.status(401).send('Invalid signature');
    }

    const { Tool, Arguments } = req.body;

    // 2. Route the tool call
    if (Tool === 'check_inventory') {
        const sku = Arguments.sku;
        
        // Mock database lookup
        const stock = { 'LAP-102': 15, 'PHN-05': 0 }[sku] ?? 0;

        // 3. Return the result as a JSON string or object
        return res.json({
            sku: sku,
            available: stock > 0,
            quantity: stock,
            message: stock > 0 ? "In Stock" : "Out of Stock"
        });
    }

    res.status(404).send('Tool not implemented');
});

app.listen(3000, () => console.log('Webhook server running on port 3000'));
```

---

## 3. How it Works (Flow)

1. **User asks**: "Is the laptop LAP-102 in stock?"
2. **AI identifies**: Needs to call `check_inventory` with `sku: "LAP-102"`.
3. **AiChatBox Backend**: Sends `POST` to your `Webhook URL`.
4. **Your App**: Processes the request, checks your DB, and returns JSON.
5. **AI receives**: The JSON result (e.g., `{ quantity: 15 }`).
6. **AI responds**: "Yes, we have 15 units of LAP-102 in stock!"

---

## 4. Key Fields Summary

| Field | Location | Description |
| :--- | :--- | :--- |
| **Webhook URL** | Project Settings | Where requests are sent. Must be publicly accessible. |
| **Webhook Secret** | Project Settings | Used for `X-Hub-Signature` verification. |
| **Tool Name** | Custom Tool | Unique identifier sent in the `Tool` field of the payload. |
| **Parameters Schema** | Custom Tool | Tells the AI what inputs are required (JSON Schema format). |

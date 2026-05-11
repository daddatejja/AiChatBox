<template>
    <section id="tools" class="doc-section">
        <h2 class="section-title"><i class="pi pi-wrench"></i> Custom Tools (Function Calling)</h2>
        <p class="section-intro">Tools let the AI call functions in your application. The AI decides when to call a tool based on the user's message, executes it, and uses the result to form its response.</p>

        <h3 class="sub-heading">How Tool Calls Work</h3>
        <div class="flow-steps">
            <div class="flow-step">
                <div class="flow-num">1</div>
                <div><strong>Define</strong> the tool schema in the Dashboard (name, description, parameters)</div>
            </div>
            <div class="flow-step">
                <div class="flow-num">2</div>
                <div><strong>Register</strong> a handler in your frontend JavaScript</div>
            </div>
            <div class="flow-step">
                <div class="flow-num">3</div>
                <div>The AI <strong>automatically calls</strong> your tool when relevant and uses the result</div>
            </div>
        </div>

        <h3 class="sub-heading">Step 1: Define the Tool in Dashboard</h3>
        <p class="desc">Go to your Project → <strong>Custom Tools</strong> → <strong>New Tool</strong>. Fill in the name, description, and a JSON Schema for parameters.</p>
        <div class="code-block">
            <div class="code-header">Example — Weather Lookup Tool</div>
            <pre><code>Name: get_weather
Description: Get the current weather for a given city

Parameters JSON Schema:
{
  "type": "object",
  "properties": {
    "city": {
      "type": "string",
      "description": "The city name, e.g. 'London'"
    },
    "units": {
      "type": "string",
      "enum": ["celsius", "fahrenheit"],
      "description": "Temperature units"
    }
  },
  "required": ["city"]
}</code></pre>
        </div>

        <h3 class="sub-heading">Step 2: Register the Handler</h3>
        <p class="desc">Use <code>registerTool()</code> to handle the tool call in your frontend. The handler receives the arguments and must return a result.</p>
        <div class="code-block">
            <div class="code-header">JavaScript — registerTool() API</div>
            <pre><code>const widget = document.querySelector('ai-chatbox');

// Register handler for "get_weather" tool
widget.registerTool('get_weather', async (args) => {
    // args = { city: "London", units: "celsius" }
    const response = await fetch(
        `https://api.weather.com/v1/current?city=${args.city}&units=${args.units}`
    );
    const data = await response.json();

    // Return the result — AI will use this to form its response
    return {
        temperature: data.temp,
        condition: data.condition,
        humidity: data.humidity
    };
});</code></pre>
        </div>

        <h3 class="sub-heading">Alternative: Event-Based Handling</h3>
        <p class="desc">If you prefer not to use <code>registerTool()</code>, listen for the <code>tool-call</code> event and respond with <code>submitToolResult()</code>:</p>
        <div class="code-block">
            <div class="code-header">JavaScript — Event-Based Approach</div>
            <pre><code>const widget = document.querySelector('ai-chatbox');

widget.addEventListener('tool-call', async (event) => {
    const { name, args, callId } = event.detail;

    if (name === 'check_order_status') {
        const order = await fetch(`/api/orders/${args.orderId}`);
        const data = await order.json();

        // Submit the result back to the widget
        widget.submitToolResult(callId, {
            orderId: args.orderId,
            status: data.status,
            estimatedDelivery: data.eta
        });
    }
});</code></pre>
        </div>

        <h3 class="sub-heading">Complete Example: Inventory Checker</h3>
        <p class="desc">Here's a full working example combining the dashboard tool definition with frontend handling:</p>
        <div class="code-block">
            <div class="code-header">Dashboard Tool Definition</div>
            <pre><code>Name: check_inventory
Description: Check if a product is in stock and get the current price

Parameters JSON Schema:
{
  "type": "object",
  "properties": {
    "productId": {
      "type": "string",
      "description": "The product SKU or ID"
    },
    "warehouse": {
      "type": "string",
      "enum": ["US-EAST", "US-WEST", "EU"],
      "description": "Warehouse region to check"
    }
  },
  "required": ["productId"]
}</code></pre>
        </div>
        <div class="code-block">
            <div class="code-header">Frontend Handler</div>
            <pre><code>widget.registerTool('check_inventory', async ({ productId, warehouse }) => {
    const res = await fetch(`/api/inventory/${productId}?region=${warehouse || 'US-EAST'}`);

    if (!res.ok) return { error: 'Product not found' };

    const data = await res.json();
    return {
        productId,
        name: data.name,
        inStock: data.quantity > 0,
        quantity: data.quantity,
        price: `$${data.price.toFixed(2)}`,
        warehouse: warehouse || 'US-EAST'
    };
});</code></pre>
        </div>

        <div class="tip-box">
            <i class="pi pi-lightbulb"></i>
            <div>
                <strong>Tip:</strong> The AI will automatically include tool results in its response. For example, after calling <code>check_inventory</code>, the AI might say: <em>"The Nike Air Max (SKU-1234) is in stock with 47 units available at $129.99 in the US-EAST warehouse."</em>
            </div>
        </div>
    </section>
</template>

<style scoped>
.doc-section { margin-bottom: 64px; }
.section-title { display: flex; align-items: center; gap: 10px; font-size: 1.6rem; font-weight: 700; color: var(--p-surface-900); margin-bottom: 12px; }
.section-title .pi { color: var(--p-primary-500); font-size: 1.3rem; }
.section-intro { color: var(--p-surface-500); font-size: 1.05rem; line-height: 1.7; margin-bottom: 32px; max-width: 720px; }
.sub-heading { font-size: 1.15rem; font-weight: 700; color: var(--p-surface-900); margin: 32px 0 8px 0; padding-top: 16px; border-top: 1px solid var(--p-surface-100); }
.desc { color: var(--p-surface-600); font-size: 0.92rem; line-height: 1.6; margin-bottom: 16px; }
.desc code { background: var(--p-surface-100); padding: 2px 6px; border-radius: 4px; font-size: 0.82rem; color: var(--p-primary-600); }

.code-block { border: 1px solid var(--p-surface-200); border-radius: 10px; overflow: hidden; margin-bottom: 24px; }
.code-header { padding: 6px 14px; background: var(--p-surface-100); font-size: 0.72rem; font-weight: 600; color: var(--p-surface-500); text-transform: uppercase; letter-spacing: 0.04em; border-bottom: 1px solid var(--p-surface-200); }
.code-block pre { margin: 0; padding: 14px; background: var(--p-surface-900); overflow-x: auto; }
.code-block code { color: var(--p-primary-300); font-size: 0.82rem; }

.flow-steps { display: flex; gap: 16px; margin-bottom: 32px; flex-wrap: wrap; }
.flow-step { flex: 1; min-width: 180px; display: flex; gap: 12px; align-items: flex-start; padding: 16px; background: var(--p-surface-50); border: 1px solid var(--p-surface-200); border-radius: 10px; font-size: 0.9rem; color: var(--p-surface-700); line-height: 1.5; }
.flow-num { width: 24px; height: 24px; min-width: 24px; background: var(--p-primary-500); color: white; border-radius: 50%; display: flex; align-items: center; justify-content: center; font-size: 0.75rem; font-weight: 700; margin-top: 2px; }

.tip-box { display: flex; gap: 10px; padding: 16px; background: var(--p-primary-50); border: 1px solid var(--p-primary-200); border-radius: 10px; font-size: 0.88rem; color: var(--p-primary-800); line-height: 1.6; margin-top: 16px; }
.tip-box .pi { font-size: 1.1rem; margin-top: 2px; color: var(--p-primary-500); }
.tip-box code { background: var(--p-primary-100); padding: 2px 5px; border-radius: 4px; font-size: 0.8rem; }
</style>

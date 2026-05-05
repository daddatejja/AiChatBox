class AudioProcessor extends AudioWorkletProcessor {
    constructor() {
        super();
        this.buffer = new Int16Array(4800); // 300ms at 16khz
        this.ptr = 0;
    }

    process(inputs, outputs, parameters) {
        const input = inputs[0];
        if (input.length > 0) {
            const channel = input[0];
            for (let i = 0; i < channel.length; i++) {
                // Convert float32 to int16 (PCM)
                const sample = Math.max(-1, Math.min(1, channel[i]));
                this.buffer[this.ptr++] = sample < 0 ? sample * 0x8000 : sample * 0x7FFF;

                if (this.ptr >= this.buffer.length) {
                    this.port.postMessage(this.buffer);
                    this.buffer = new Int16Array(4800);
                    this.ptr = 0;
                }
            }
        }
        return true;
    }
}

registerProcessor('audio-processor', AudioProcessor);

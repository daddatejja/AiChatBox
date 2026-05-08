class AudioProcessor extends AudioWorkletProcessor {
    constructor() {
        super();
        this.bufferSize = 4096;
        this.buffer = new Float32Array(this.bufferSize);
        this.bufferIndex = 0;
    }

    process(inputs, outputs, parameters) {
        const input = inputs[0];
        if (!input || !input[0]) return true;

        const inputChannel = input[0];
        for (let i = 0; i < inputChannel.length; i++) {
            this.buffer[this.bufferIndex++] = inputChannel[i];

            if (this.bufferIndex >= this.bufferSize) {
                this.sendBuffer();
                this.bufferIndex = 0;
            }
        }
        return true;
    }

    sendBuffer() {
        let sum = 0;
        for (let i = 0; i < this.buffer.length; i++) {
            sum += this.buffer[i] * this.buffer[i];
        }
        const volume = Math.sqrt(sum / this.buffer.length);

        const pcm16 = new Int16Array(this.buffer.length);
        for (let i = 0; i < this.buffer.length; i++) {
            let s = Math.max(-1, Math.min(1, this.buffer[i]));
            pcm16[i] = s < 0 ? s * 0x8000 : s * 0x7FFF;
        }

        this.port.postMessage({
            pcm16: pcm16,
            volume: volume
        });
    }
}

registerProcessor('audio-processor', AudioProcessor);

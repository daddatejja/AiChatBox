export class VoiceRecorder {
  constructor(chatbox) {
    this.chatbox = chatbox;
    this.mediaRecorder = null;
    this.audioChunks = [];
  }

  start() {
    this.chatbox.isRecording = true;
    const micBtn = this.chatbox.shadowRoot.getElementById("btn-mic");
    if (micBtn) micBtn.classList.add("recording");

    navigator.mediaDevices.getUserMedia({ audio: true }).then((stream) => {
      this.mediaRecorder = new MediaRecorder(stream, { mimeType: "audio/webm;codecs=opus" });
      this.audioChunks = [];
      this.mediaRecorder.ondataavailable = (e) => {
        if (e.data.size > 0) this.audioChunks.push(e.data);
      };
      this.mediaRecorder.start();
    }).catch((err) => {
      console.error("Microphone access denied:", err);
      this.cancel();
    });
  }

  async stop() {
    if (!this.chatbox.isRecording) return;
    this.chatbox.isRecording = false;
    const micBtn = this.chatbox.shadowRoot.getElementById("btn-mic");
    if (micBtn) micBtn.classList.remove("recording");

    if (!this.mediaRecorder || this.audioChunks.length === 0) return;

    this.mediaRecorder.onstop = async () => {
      this.mediaRecorder.stream.getTracks().forEach((t) => t.stop());

      const blob = new Blob(this.audioChunks, { type: "audio/webm" });
      const formData = new FormData();
      formData.append("audio", blob, "recording.webm");
      formData.append("language", "auto");

      try {
        const res = await fetch(`${this.chatbox.apiUrl}/api/audio/transcribe`, {
          method: "POST",
          headers: this.chatbox.getHeaders(),
          body: formData
        });
        if (res.ok) {
          const data = await this.chatbox.safeJson(res);
          const input = this.chatbox.shadowRoot.getElementById("chat-input");
          if (input) {
            input.value = data.text || "";
            this.chatbox.updateSendButtonState();
            if (data.text) this.chatbox.sendMessage();
          }
        }
      } catch (err) {
        console.error("Transcription failed:", err);
      }
    };

    this.mediaRecorder.stop();
  }

  cancel() {
    this.chatbox.isRecording = false;
    const micBtn = this.chatbox.shadowRoot.getElementById("btn-mic");
    if (micBtn) micBtn.classList.remove("recording");
    if (this.mediaRecorder && this.mediaRecorder.state !== "inactive") {
      this.mediaRecorder.stream.getTracks().forEach((t) => t.stop());
    }
  }
}

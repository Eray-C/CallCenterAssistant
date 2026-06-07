// WAV Recorder class using Web Audio API to output 16kHz mono WAV Blob
class WAVRecorder {
    constructor() {
        this.audioContext = null;
        this.processor = null;
        this.input = null;
        this.stream = null;
        this.leftchannel = [];
        this.recordingLength = 0;
        this.sampleRate = 16000;
    }

    async start(onSilence = null, customStream = null) {
        this.leftchannel = [];
        this.recordingLength = 0;

        this.stream = customStream || await navigator.mediaDevices.getUserMedia({ audio: true });

        const AudioContext = window.AudioContext || window.webkitAudioContext;
        this.audioContext = new AudioContext({ sampleRate: this.sampleRate });

        this.input = this.audioContext.createMediaStreamSource(this.stream);

        // 2048 buffer size, 1 input channel, 1 output channel
        this.processor = this.audioContext.createScriptProcessor(2048, 1, 1);

        let hasSpoken = false;
        let silenceStart = null;
        const silenceThreshold = 0.015; // Noise gate
        const silenceDelay = 1500; // 1.5 seconds

        this.processor.onaudioprocess = (e) => {
            const left = e.inputBuffer.getChannelData(0);
            this.leftchannel.push(new Float32Array(left));
            this.recordingLength += left.length;

            if (onSilence) {
                let sum = 0;
                for (let i = 0; i < left.length; i++) {
                    sum += left[i] * left[i];
                }
                const rms = Math.sqrt(sum / left.length);

                if (rms > silenceThreshold) {
                    hasSpoken = true;
                    silenceStart = null;
                } else if (hasSpoken) {
                    if (silenceStart === null) {
                        silenceStart = Date.now();
                    } else if (Date.now() - silenceStart > silenceDelay) {
                        hasSpoken = false;
                        silenceStart = null;
                        onSilence();
                    }
                }
            }
        };

        this.input.connect(this.processor);
        this.processor.connect(this.audioContext.destination);
    }

    stop() {
        if (!this.processor) return null;

        this.processor.disconnect();
        this.input.disconnect();
        if (this.audioContext && this.audioContext.state !== 'closed') {
            this.audioContext.close();
        }
        if (this.stream) {
            this.stream.getTracks().forEach(track => track.stop());
        }

        // Flatten the left channel buffers
        const samples = new Float32Array(this.recordingLength);
        let offset = 0;
        for (let i = 0; i < this.leftchannel.length; i++) {
            samples.set(this.leftchannel[i], offset);
            offset += this.leftchannel[i].length;
        }

        // Create WAV blob
        const buffer = new ArrayBuffer(44 + samples.length * 2);
        const view = new DataView(buffer);

        /* RIFF identifier */
        this.writeString(view, 0, 'RIFF');
        /* file length */
        view.setUint32(4, 36 + samples.length * 2, true);
        /* WAVE identifier */
        this.writeString(view, 8, 'WAVE');
        /* format chunk identifier */
        this.writeString(view, 12, 'fmt ');
        /* format chunk length */
        view.setUint32(16, 16, true);
        /* sample format (PCM) */
        view.setUint16(20, 1, true);
        /* channel count */
        view.setUint16(22, 1, true);
        /* sample rate */
        view.setUint32(24, this.sampleRate, true);
        /* byte rate (sample rate * block align) */
        view.setUint32(28, this.sampleRate * 2, true);
        /* block align (channel count * bytes per sample) */
        view.setUint16(32, 2, true);
        /* bits per sample */
        view.setUint16(34, 16, true);
        /* data chunk identifier */
        this.writeString(view, 36, 'data');
        /* data chunk length */
        view.setUint32(40, samples.length * 2, true);

        // Write PCM audio samples (Float32 to Int16)
        let index = 44;
        for (let i = 0; i < samples.length; i++) {
            let s = Math.max(-1, Math.min(1, samples[i]));
            view.setInt16(index, s < 0 ? s * 0x8000 : s * 0x7FFF, true);
            index += 2;
        }

        return new Blob([view], { type: 'audio/wav' });
    }

    writeString(view, offset, string) {
        for (let i = 0; i < string.length; i++) {
            view.setUint8(offset + i, string.charCodeAt(i));
        }
    }
}

// DOM Elements
const messagesContainer = document.getElementById('messages-container');
const chatInput = document.getElementById('chat-input');
const sendBtn = document.getElementById('send-btn');
const micBtn = document.getElementById('mic-btn');
const clearChatBtn = document.getElementById('clear-chat');
const typingIndicator = document.getElementById('typing-indicator');
const recordingOverlay = document.getElementById('recording-overlay');
const recordingTimeEl = document.getElementById('recording-time');
const ttsAudio = document.getElementById('tts-audio');

// Config Toggles
const ttsAutoPlayToggle = document.getElementById('tts-auto-play');
const sttDirectSendToggle = document.getElementById('stt-direct-send');

// Call Modal DOM Elements
const callModal = document.getElementById('call-modal');
const callModalTimerEl = document.getElementById('call-modal-timer');
const modalTranscript = document.getElementById('modal-transcript');
const modalHangupBtn = document.getElementById('modal-hangup-btn');

// Status Dots
const statusOllamaDot = document.querySelector('#status-ollama .status-dot');
const statusOllamaText = document.querySelector('#status-ollama .status-name');

// State Variables
let isRecording = false;
let recorder = null;
let recordingTimer = null;
let recordingSeconds = 0;
let currentlyPlayingBtn = null;

// Initial check for Ollama status
async function checkOllamaStatus() {
    try {
        const response = await fetch('http://localhost:11434/api/tags');
        if (response.ok) {
            statusOllamaDot.className = 'status-dot green';
            statusOllamaDot.title = 'Ollama Servisi Calisiyor ve Ulasılabilir';
        } else {
            statusOllamaDot.className = 'status-dot orange';
        }
    } catch {
        statusOllamaDot.className = 'status-dot orange';
    }
}

// Format seconds to MM:SS
function formatTime(secs) {
    const m = Math.floor(secs / 60).toString().padStart(2, '0');
    const s = (secs % 60).toString().padStart(2, '0');
    return `${m}:${s}`;
}

// Auto-scroll messages container
function scrollToBottom() {
    messagesContainer.scrollTop = messagesContainer.scrollHeight;
}

// Append message to UI
function appendMessage(text, sender, textToRead = null) {
    const messageDiv = document.createElement('div');
    messageDiv.className = `message ${sender}`;

    const contentDiv = document.createElement('div');
    contentDiv.className = 'message-content';

    const textPara = document.createElement('p');
    textPara.innerText = text;
    contentDiv.appendChild(textPara);

    // If assistant message, add a play button for Speech Synthesis (TTS)
    if (sender === 'assistant' && textToRead) {
        const playBtn = document.createElement('button');
        playBtn.className = 'play-btn';
        playBtn.title = 'Seslendir (TTS)';
        playBtn.innerHTML = '<i class="fa-solid fa-play"></i>';

        playBtn.addEventListener('click', () => {
            playTts(textToRead, playBtn);
        });

        contentDiv.appendChild(playBtn);
    }

    messageDiv.appendChild(contentDiv);
    messagesContainer.appendChild(messageDiv);
    scrollToBottom();
}

// Show/Hide typing indicator
function setTyping(isTyping) {
    typingIndicator.style.display = isTyping ? 'flex' : 'none';
    scrollToBottom();
}

// Send Text Message to API
async function sendTextMessage(text) {
    if (!text || text.trim() === '') return;

    appendMessage(text, 'user');
    chatInput.value = '';
    setTyping(true);

    try {
        const response = await fetch('/api/chat', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ message: text })
        });

        if (!response.ok) {
            const errText = await response.text();
            throw new Error(errText || 'Sunucu hatasi olustu.');
        }

        const data = await response.json();
        const botResponse = data.response || 'Bir hata olustu veya bos cevap dondu.';

        setTyping(false);
        appendMessage(botResponse, 'assistant', botResponse);

        // Ollama connected successfully
        statusOllamaDot.className = 'status-dot green';

        // Auto play TTS if enabled
        if (ttsAutoPlayToggle.checked) {
            const playBtns = messagesContainer.querySelectorAll('.message.assistant .play-btn');
            if (playBtns.length > 0) {
                const lastPlayBtn = playBtns[playBtns.length - 1];
                playTts(botResponse, lastPlayBtn);
            }
        }
    } catch (error) {
        setTyping(false);
        appendMessage(`Hata: ${error.message}`, 'system');
        statusOllamaDot.className = 'status-dot red';
    }
}

// Play TTS Audio
async function playTts(text, buttonElement) {
    // If already playing this message, stop it
    if (currentlyPlayingBtn === buttonElement && !ttsAudio.paused) {
        ttsAudio.pause();
        buttonElement.innerHTML = '<i class="fa-solid fa-play"></i>';
        buttonElement.classList.remove('playing');
        currentlyPlayingBtn = null;
        return;
    }

    // Stop currently playing audio
    if (currentlyPlayingBtn) {
        ttsAudio.pause();
        currentlyPlayingBtn.innerHTML = '<i class="fa-solid fa-play"></i>';
        currentlyPlayingBtn.classList.remove('playing');
    }

    buttonElement.innerHTML = '<i class="fa-solid fa-spinner fa-spin"></i>';
    buttonElement.classList.add('playing');
    currentlyPlayingBtn = buttonElement;

    try {
        const response = await fetch('/api/speech/synthesize', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ text: text })
        });

        if (!response.ok) {
            throw new Error('Ses sentezlenemedi.');
        }

        const audioBlob = await response.blob();
        const audioUrl = URL.createObjectURL(audioBlob);

        ttsAudio.src = audioUrl;

        ttsAudio.onplay = () => {
            buttonElement.innerHTML = '<i class="fa-solid fa-volume-high"></i>';
        };

        ttsAudio.onended = () => {
            buttonElement.innerHTML = '<i class="fa-solid fa-play"></i>';
            buttonElement.classList.remove('playing');
            currentlyPlayingBtn = null;
        };

        ttsAudio.onerror = () => {
            buttonElement.innerHTML = '<i class="fa-solid fa-triangle-exclamation"></i>';
            buttonElement.classList.remove('playing');
            currentlyPlayingBtn = null;
        };

        await ttsAudio.play();
    } catch (error) {
        console.error(error);
        buttonElement.innerHTML = '<i class="fa-solid fa-triangle-exclamation"></i>';
        buttonElement.classList.remove('playing');
        currentlyPlayingBtn = null;
    }
}

// Microphone / STT voice recording
async function toggleRecording() {
    if (isRecording) {
        // Stop recording
        isRecording = false;
        micBtn.classList.remove('recording');
        micBtn.title = 'Sesli Konus (STT)';
        recordingOverlay.style.display = 'none';

        clearInterval(recordingTimer);

        if (recorder) {
            const audioBlob = recorder.stop();
            if (audioBlob) {
                await uploadAndTranscribe(audioBlob);
            }
        }
    } else {
        // Start recording
        try {
            if (!recorder) {
                recorder = new WAVRecorder();
            }

            await recorder.start();

            isRecording = true;
            micBtn.classList.add('recording');
            micBtn.title = 'Kaydi Durdur ve Gonder';
            recordingOverlay.style.display = 'flex';

            recordingSeconds = 0;
            recordingTimeEl.innerText = formatTime(recordingSeconds);

            recordingTimer = setInterval(() => {
                recordingSeconds++;
                recordingTimeEl.innerText = formatTime(recordingSeconds);
                if (recordingSeconds >= 120) { // Max 2 minutes safety limit
                    toggleRecording();
                }
            }, 1000);

        } catch (error) {
            console.error(error);
            alert('Mikrofona erisilmedi: ' + error.message);
        }
    }
}

// Upload WAV blob and Transcribe (STT)
async function uploadAndTranscribe(wavBlob) {
    setTyping(true);

    const formData = new FormData();
    formData.append('file', wavBlob, 'recorded_audio.wav');

    try {
        const response = await fetch('/api/speech/transcribe', {
            method: 'POST',
            body: formData
        });

        if (!response.ok) {
            const errText = await response.text();
            throw new Error(errText || 'Transkripsiyon basarisiz.');
        }

        const data = await response.json();
        const transcribedText = data.text;

        setTyping(false);

        if (transcribedText && transcribedText.trim() !== '') {
            if (sttDirectSendToggle.checked) {
                sendTextMessage(transcribedText);
            } else {
                chatInput.value = transcribedText;
                chatInput.focus();
            }
        } else {
            appendMessage('Ses algilanamadi veya anlasılamadi. Lutfen tekrar deneyin.', 'system');
        }
    } catch (error) {
        setTyping(false);
        appendMessage(`Ses cozumleme hatasi: ${error.message}`, 'system');
    }
}

// Event Listeners
sendBtn.addEventListener('click', () => {
    const text = chatInput.value;
    sendTextMessage(text);
});

chatInput.addEventListener('keydown', (e) => {
    if (e.key === 'Enter' && !e.shiftKey) {
        e.preventDefault();
        const text = chatInput.value;
        sendTextMessage(text);
    }
});

micBtn.addEventListener('click', () => {
    toggleRecording();
});

clearChatBtn.addEventListener('click', () => {
    messagesContainer.innerHTML = '';
    appendMessage('Sohbet gecmisi temizlendi.', 'system');
    if (currentlyPlayingBtn) {
        ttsAudio.pause();
        currentlyPlayingBtn = null;
    }
});

// Auto grow input textarea height
chatInput.addEventListener('input', () => {
    chatInput.style.height = 'auto';
    chatInput.style.height = chatInput.scrollHeight + 'px';
});

// Run check on startup
checkOllamaStatus();
// Re-check Ollama status every 20 seconds
setInterval(checkOllamaStatus, 20000);

// =============================================================================
// Ara Butonu - Backend AMI Originate (/api/asterisk/originate)
// JsSIP/WebRTC yerine backend uzerinden Asterisk AMI ile arama yapilir.
// =============================================================================

const callBtn = document.getElementById('call-btn');
const callBtnText = document.getElementById('call-btn-text');
const callBanner = document.getElementById('call-banner');
const callBannerText = document.getElementById('call-banner-text');
const callTimerEl = document.getElementById('call-timer');
const hangupBtn = document.getElementById('hangup-btn');

let callTimerInterval = null;
let callSeconds = 0;
let isCallActive = false;

function formatCallTime(secs) {
    const m = Math.floor(secs / 60).toString().padStart(2, '0');
    const s = (secs % 60).toString().padStart(2, '0');
    return `${m}:${s}`;
}

function startCallTimer() {
    callSeconds = 0;
    callTimerEl.textContent = '00:00';
    if (callModalTimerEl) callModalTimerEl.textContent = '00:00';
    callTimerInterval = setInterval(() => {
        callSeconds++;
        const t = formatCallTime(callSeconds);
        callTimerEl.textContent = t;
        if (callModalTimerEl) callModalTimerEl.textContent = t;
    }, 1000);
}

function stopCallTimer() {
    clearInterval(callTimerInterval);
    callTimerInterval = null;
}

function appendModalMessage(text, sender) {
    if (!modalTranscript) return;
    const div = document.createElement('div');
    div.className = `modal-message ${sender}`;
    div.innerHTML = `<p>${text}</p>`;
    modalTranscript.appendChild(div);
    const body = document.getElementById('call-modal-body');
    if (body) body.scrollTop = body.scrollHeight;
}

function setCallState(state) {
    callBtn.className = 'btn btn-call';
    callBanner.classList.remove('active');

    switch (state) {
        case 'connecting':
            callBtn.classList.add('calling');
            callBtnText.textContent = 'Bağlanıyor...';
            callBtn.disabled = true;
            callBannerText.textContent = 'Bağlanıyor...';
            callBanner.classList.add('active');
            if (callModal) {
                callModal.classList.add('show');
                if (modalTranscript) modalTranscript.innerHTML = '<div class="modal-message system"><p>Arama başlatılıyor, lütfen bekleyin...</p></div>';
            }
            break;
        case 'in-call':
            callBtn.classList.add('in-call');
            callBtnText.textContent = 'Görüşmede';
            callBtn.disabled = false;
            callBannerText.textContent = 'Görüşme aktif';
            callBanner.classList.add('active');
            isCallActive = true;
            startCallTimer();
            if (callModal) callModal.classList.add('show');
            appendModalMessage('Asistan bağlantıda. Konuşmaya başlayabilirsiniz...', 'system');
            startVoiceLoop();
            break;
        case 'ending':
        case 'idle':
        default:
            callBtn.disabled = false;
            callBtnText.textContent = 'Ara';
            if (isCallActive) {
                isCallActive = false;
                if (recorder) {
                    try { recorder.stop(); } catch(e){}
                }
                ttsAudio.pause();
                appendModalMessage(`Görüşme sonlandı. Süre: ${formatCallTime(callSeconds)}`, 'system');
                setTimeout(() => {
                    if (callModal && !isCallActive) callModal.classList.remove('show');
                }, 3000);
            } else {
                if (callModal) callModal.classList.remove('show');
            }
            break;
    }
}

let isVoiceLoopActive = false;

async function startVoiceLoop() {
    if (!isCallActive) return;
    isVoiceLoopActive = true;
    try {
        if (!recorder) {
            recorder = new WAVRecorder();
        }
        appendModalMessage("Sizi dinliyorum...", "system");
        await recorder.start(async () => {
            if (!isCallActive) return;
            await handleModalVoiceInput();
        });
    } catch (e) {
        appendModalMessage("Mikrofon başlatılamadı: " + e.message, "system");
    }
}

async function handleModalVoiceInput() {
    if (!isCallActive) return;
    isVoiceLoopActive = false;
    appendModalMessage("Sesiniz çözümleniyor...", "system");
    
    let wavBlob = null;
    if (recorder) {
        wavBlob = recorder.stop();
    }
    
    if (!wavBlob) {
        if (isCallActive) startVoiceLoop();
        return;
    }

    const formData = new FormData();
    formData.append('file', wavBlob, 'recorded_audio.wav');

    try {
        // 1. Sesi Metne Dönüştür (STT)
        const sttResponse = await fetch('/api/speech/transcribe', {
            method: 'POST',
            body: formData
        });

        if (!sttResponse.ok) throw new Error("Ses metne dönüştürülemedi (STT Hatası)");
        const sttData = await sttResponse.json();
        const userText = sttData.text;

        if (!userText || userText.trim() === '') {
            appendModalMessage("Ses algılanamadı, tekrar dinleniyor...", "system");
            if (isCallActive) startVoiceLoop();
            return;
        }

        // Kullanıcı mesajını ekle
        appendModalMessage(userText, "user");
        appendModalMessage("Asistan düşünüyor...", "system");

        // 2. Yapay Zekadan Cevap Al (LLM)
        const chatResponse = await fetch('/api/chat', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ message: userText })
        });

        if (!chatResponse.ok) throw new Error("Yapay zekadan cevap alınamadı");
        const chatData = await chatResponse.json();
        const botText = chatData.response;

        // Asistan mesajını ekle
        appendModalMessage(botText, "assistant");

        // 3. Cevabı Sese Dönüştür ve Oynat (TTS)
        const ttsResponse = await fetch('/api/speech/synthesize', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ text: botText })
        });

        if (!ttsResponse.ok) throw new Error("Ses sentezlenemedi (TTS Hatası)");
        const audioBlob = await ttsResponse.blob();
        const audioUrl = URL.createObjectURL(audioBlob);

        ttsAudio.src = audioUrl;
        ttsAudio.onended = () => {
            if (isCallActive) startVoiceLoop();
        };
        ttsAudio.onerror = () => {
            if (isCallActive) startVoiceLoop();
        };
        await ttsAudio.play();

    } catch (error) {
        appendModalMessage("Hata: " + error.message, "system");
        if (isCallActive) {
            setTimeout(() => {
                if (isCallActive) startVoiceLoop();
            }, 3000);
        }
    }
}

let ua = null;
let currentSession = null;

function initSIP() {
    console.log("Initializing JsSIP...");
    const socket = new JsSIP.WebSocketInterface('ws://localhost:8088/ws');
    const configuration = {
        sockets: [socket],
        uri: 'sip:webrtc-user@localhost',
        password: 'webrtc1234',
        register: true
    };

    ua = new JsSIP.UA(configuration);

    ua.on('registered', () => {
        console.log('JsSIP: webrtc-user registered successfully.');
    });

    ua.on('registrationFailed', (e) => {
        console.error('JsSIP Registration failed:', e.cause);
    });

    ua.on('newRTCSession', (data) => {
        const session = data.session;
        console.log('New RTC Session:', session.direction);

        if (session.direction === 'incoming') {
            currentSession = session;
            
            // Auto-answer incoming call from Asterisk
            session.answer({
                mediaConstraints: { audio: true, video: false }
            });

            session.on('accepted', () => {
                console.log('Call accepted');
                setCallState('in-call');
            });

            session.on('peerconnection', (data) => {
                const pc = data.peerconnection;
                pc.addEventListener('track', (e) => {
                    const remoteAudio = document.getElementById('webrtc-audio');
                    if (remoteAudio) {
                        remoteAudio.srcObject = e.streams[0];
                        remoteAudio.play().catch(err => console.error("WebRTC Audio play error:", err));
                    }
                });
            });

            session.on('failed', (e) => {
                console.log('Call failed:', e.cause);
                setCallState('idle');
                currentSession = null;
            });

            session.on('ended', () => {
                console.log('Call ended');
                setCallState('idle');
                currentSession = null;
            });
        }
    });

    ua.start();
}

// Dynamically load JsSIP to prevent caching/build sync issues
function loadJsSIP(callback) {
    if (typeof JsSIP !== 'undefined') {
        callback();
        return;
    }
    console.log("Dynamically loading local JsSIP library...");
    const script = document.createElement('script');
    script.src = 'jssip.min.js';
    script.type = 'text/javascript';
    script.onload = () => {
        console.log("JsSIP library loaded successfully.");
        callback();
    };
    script.onerror = () => {
        console.error("Failed to load JsSIP library from CDN.");
    };
    document.head.appendChild(script);
}

// Initialize SIP on page load after dynamically loading JsSIP
window.addEventListener('DOMContentLoaded', () => {
    loadJsSIP(() => {
        initSIP();
    });
});

async function startCall() {
    setCallState('connecting');
    try {
        const response = await fetch('/api/asterisk/originate', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                channel: 'PJSIP/webrtc-user',
                exten: '8000',
                context: 'from-internal',
                priority: 1,
                callerId: 'Asistan <1000>',
                timeout: 30000
            })
        });

        if (!response.ok) {
            const err = await response.text();
            appendMessage(err || 'Arama başlatılamadı.', 'system');
            setCallState('idle');
        }
    } catch (error) {
        appendMessage(`Bağlantı hatası: ${error.message}`, 'system');
        setCallState('idle');
    }
}

function endCall() {
    if (currentSession) {
        try {
            currentSession.terminate();
        } catch (e) {
            console.error("Session termination error:", e);
        }
    }
    setCallState('ending');
}

callBtn.addEventListener('click', () => {
    if (isCallActive) {
        endCall();
    } else {
        startCall();
    }
});

hangupBtn.addEventListener('click', () => {
    endCall();
});

if (modalHangupBtn) {
    modalHangupBtn.addEventListener('click', () => {
        endCall();
    });
}
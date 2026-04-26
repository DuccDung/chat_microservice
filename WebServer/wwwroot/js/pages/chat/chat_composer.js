import { chatService } from "../../services/chatService.js";

const msgInput = document.getElementById("msgInput");
const sendBtn = document.getElementById("sendBtn");
const selectImgBtn = document.getElementById("btn-select-img-sender");
const selectGifBtn = document.getElementById("btn-select-gif-sender");
const inputWrap = document.querySelector(".input-wrap");

let selectedImageFile = null;

// Tạo file input ẩn
const fileInput = document.createElement("input");
fileInput.type = "file";
fileInput.accept = "image/*";
fileInput.style.display = "none";
document.body.appendChild(fileInput);

console.log("composer-ui loaded");

if (sendBtn && msgInput) {
    sendBtn.addEventListener("click", async (e) => {
        e.preventDefault();

        if (recordedBlob) {
            await handleSendAudio();
            return;
        }

        if (selectedImageFile) {
            await handleSendImage();
            return;
        }

        await handleSendText();
    });

    msgInput.addEventListener("keydown", async (e) => {
        if (e.key === "Enter" && !e.shiftKey) {
            e.preventDefault();

            if (selectedImageFile) {
                handleSendImage();
            } else {
                await handleSendText();
            }
        }
    });
}

// ===============================
// CHỌN ẢNH
// ===============================

if (selectImgBtn) {
    selectImgBtn.addEventListener("click", () => {
        fileInput.click();
    });
}
if (selectGifBtn) {
    selectGifBtn.addEventListener("click", () => {
        fileInput.click();
    });
}

fileInput.addEventListener("change", (e) => {
    const file = e.target.files[0];
    if (!file) return;

    selectedImageFile = file;
    showImagePreview(file);
});

// ===============================
// PREVIEW ẢNH
// ===============================

function showImagePreview(file) {
    if (!inputWrap) return;

    const reader = new FileReader();

    reader.onload = function (event) {
        const imageUrl = event.target.result;

        // Ẩn input text
        msgInput.style.display = "none";
        msgInput.value = "";
        msgInput.disabled = true;

        // Xoá preview cũ nếu có
        const oldPreview = inputWrap.querySelector(".image-preview-container");
        if (oldPreview) oldPreview.remove();

        const container = document.createElement("div");
        container.className = "image-preview-container";

        container.innerHTML = `
            <div class="image-preview">
                <img src="${imageUrl}" />
                <button type="button" class="remove-image-btn">×</button>
            </div>
        `;

        inputWrap.appendChild(container);

        // Xử lý nút xoá
        container.querySelector(".remove-image-btn").addEventListener("click", () => {
            clearImagePreview();
        });
    };

    reader.readAsDataURL(file);
}

function clearImagePreview() {
    selectedImageFile = null;

    const preview = inputWrap.querySelector(".image-preview-container");
    if (preview) preview.remove();

    msgInput.style.display = "block";
    msgInput.disabled = false;
    msgInput.focus();

    fileInput.value = "";
}

// ===============================
// GỬI TEXT
// ===============================

function getActiveConversationId() {
    const active = document.querySelector(".thread-item.active");
    return active?.dataset?.id ? parseInt(active.dataset.id, 10) : null;
}

async function handleSendText() {
    const conversationId = getActiveConversationId();
    if (!conversationId) {
        alert("Bạn chưa chọn cuộc trò chuyện.");
        return;
    }

    const text = (msgInput.value || "").trim();
    if (!text) return;

    setSending(true);

    try {
        await chatService.sendTextMessage(conversationId, text, null);
        msgInput.value = "";
        await reloadMessages(conversationId);
        msgInput.focus();
    } catch (err) {
        console.error(err);
        alert("Gửi tin nhắn thất bại.");
    } finally {
        setSending(false);
    }
}

// ===============================
// GỬI ẢNH (chưa gọi API)
// ===============================

async function handleSendImage() {
    const conversationId = getActiveConversationId();
    if (!conversationId || !selectedImageFile) return;

    try {
        await chatService.sendImageMessage(
            conversationId,
            selectedImageFile,
            null
        );

        clearImagePreview();
        await reloadMessages(conversationId);
    } catch (err) {
        console.error(err);
        alert("Gửi ảnh thất bại.");
    }
}

// ===============================

function setSending(isSending) {
    sendBtn.disabled = isSending;
    msgInput.disabled = isSending;
}

async function reloadMessages(conversationId) {
    const scroller = document.getElementById("messageScroller");
    if (!scroller) return;

    try {
        const res = await chatService.getConversationView(conversationId);
        const html = res.data;

        const temp = document.createElement("div");
        temp.innerHTML = html;

        const newSection = temp.querySelector("#messageScroller");
        scroller.innerHTML = newSection ? newSection.innerHTML : html;

        scroller.scrollTop = scroller.scrollHeight;
    } catch (err) {
        console.error(err);
        scroller.innerHTML = `<div class="error">Không tải được tin nhắn.</div>`;
    }
}

let mediaRecorder = null;
let audioChunks = [];
let recordedBlob = null;

const recordBtn = document.getElementById("btn-record-voice");

// ==============================
// BẮT ĐẦU / DỪNG GHI ÂM
// ==============================
if (recordBtn) {
    recordBtn.addEventListener("click", async () => {
        if (!mediaRecorder || mediaRecorder.state === "inactive") {
            startRecording();
        } else {
            stopRecording();
        }
    });
}

async function startRecording() {
    try {
        const stream = await navigator.mediaDevices.getUserMedia({ audio: true });

        mediaRecorder = new MediaRecorder(stream);
        audioChunks = [];

        mediaRecorder.ondataavailable = e => {
            audioChunks.push(e.data);
        };

        mediaRecorder.onstop = () => {
            recordedBlob = new Blob(audioChunks, { type: "audio/webm" });
            showAudioPreview(recordedBlob);
        };

        mediaRecorder.start();
        recordBtn.classList.add("recording");
    } catch (err) {
        alert("Không thể truy cập micro.");
    }
}

function stopRecording() {
    if (mediaRecorder) {
        mediaRecorder.stop();
        recordBtn.classList.remove("recording");
    }
}

// ==============================
// PREVIEW AUDIO
// ==============================

function showAudioPreview(blob) {
    const inputWrap = document.querySelector(".input-wrap");

    msgInput.style.display = "none";
    msgInput.disabled = true;

    const old = inputWrap.querySelector(".audio-preview");
    if (old) old.remove();

    const audioUrl = URL.createObjectURL(blob);

    const container = document.createElement("div");
    container.className = "audio-preview";

    container.innerHTML = `
        <audio controls src="${audioUrl}"></audio>
        <button class="remove-audio">×</button>
    `;

    inputWrap.appendChild(container);

    container.querySelector(".remove-audio").addEventListener("click", clearAudioPreview);
}

function clearAudioPreview() {
    recordedBlob = null;

    const preview = document.querySelector(".audio-preview");
    if (preview) preview.remove();

    msgInput.style.display = "block";
    msgInput.disabled = false;
}

// ==============================
// GỬI AUDIO
// ==============================

async function handleSendAudio() {
    const conversationId = getActiveConversationId();
    if (!conversationId || !recordedBlob) return;

    const file = new File([recordedBlob], "voice.webm", {
        type: "audio/webm"
    });

    try {
        await chatService.sendAudioMessage(conversationId, file);
        clearAudioPreview();
        await reloadMessages(conversationId);
    } catch (err) {
        alert("Gửi ghi âm thất bại.");
    }
}
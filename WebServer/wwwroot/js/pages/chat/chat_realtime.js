import { subscribeConversation, connectWs } from "../../services/ws-client.js";

const scroller = document.getElementById("messageScroller");

console.log("chat-realtime loaded");
connectWs();

function getActiveConversationId() {
    const active = document.querySelector(".thread-item.active");
    if (!active) return null;
    return parseInt(active.dataset.id, 10);
}

export function subscribeActiveConversation() {
    const conversationId = getActiveConversationId();
    if (!conversationId) return;

    console.log("Subscribing to conversation:", conversationId);
    subscribeConversation(conversationId);
}

/* ==============================
   WS MESSAGE ROUTER
============================== */
window.addEventListener("ws:message", (event) => {
    const msg = event.detail;
    if (!msg) return;

    // bạn tách type: message-text | message-image | message-audio
    const supportedTypes = new Set(["message-text", "message-image", "message-audio"]);
    if (!supportedTypes.has(msg.type)) return;

    const activeConversationId = getActiveConversationId();
    if (!activeConversationId) return;

    if (msg.conversationId !== activeConversationId) return;

    appendMessageToScroller(msg.payload, msg.type);
});

/* ==============================
   APPEND MESSAGE UI
============================== */
function appendMessageToScroller(message, wsType) {
    if (!scroller) return;

    const meId = parseInt(scroller.dataset.meId || "0", 10);
    const senderId = message?.sender?.accountId ?? message?.sender?.AccountId ?? message?.senderId ?? message?.SenderId;
    const isMe = senderId === meId;
    const sideClass = isMe ? "right" : "left";

    const curTime = parseDate(message.createdAt || message.CreatedAt);
    const now = new Date();

    const lastMsgTime = getLastMessageTime();

    if (lastMsgTime && needDivider(lastMsgTime, curTime)) {
        const divider = document.createElement("div");
        divider.className = "message-timestamp-divider";
        divider.textContent = dividerLabel(curTime, now);
        scroller.appendChild(divider);
    }

    const spacing = spacingClass(lastMsgTime, curTime);

    const group = document.createElement("div");
    group.className = `bubble-group ${spacing}`.trim();

    // messageType lấy từ wsType (ưu tiên) hoặc từ payload.MessageType
    const payloadTypeRaw = (message.messageType ?? message.MessageType ?? "").toString().toLowerCase();
    const messageKind = wsType === "message-image" ? "image"
        : wsType === "message-audio" ? "audio"
            : wsType === "message-text" ? "text"
                : payloadTypeRaw || "text";

    if (messageKind === "image") {
        group.appendChild(renderImageBubble(message, sideClass, curTime));
    } else if (messageKind === "audio") {
        group.appendChild(renderAudioBubble(message, sideClass, curTime));
    } else {
        group.appendChild(renderTextBubble(message, sideClass, curTime));
    }

    scroller.appendChild(group);
    scroller.scrollTop = scroller.scrollHeight;
}

function renderTextBubble(message, sideClass, curTime) {
    const msgDiv = document.createElement("div");
    msgDiv.className = `msg ${sideClass}`;

    msgDiv.appendChild(document.createTextNode(message.content ?? message.Content ?? ""));

    const tip = document.createElement("div");
    tip.className = "message-time-tooltip";
    tip.textContent = tooltipTime(curTime);
    msgDiv.appendChild(tip);

    return msgDiv;
}

function renderImageBubble(message, sideClass, curTime) {
    const wrap = document.createElement("div");
    wrap.className = `image-wrapper ${sideClass}`;

    const img = document.createElement("img");
    img.className = "chat-image-modern";
    img.alt = "image";
    img.src = message.content ?? message.Content ?? "";
    wrap.appendChild(img);

    const time = document.createElement("div");
    time.className = "image-time";
    time.textContent = tooltipTime(curTime);
    wrap.appendChild(time);

    return wrap;
}

function renderAudioBubble(message, sideClass, curTime) {
    const msgDiv = document.createElement("div");
    msgDiv.className = `msg ${sideClass}`;

    const audio = document.createElement("audio");
    audio.className = "chat-audio";
    audio.controls = true;
    audio.preload = "metadata";

    const source = document.createElement("source");
    source.src = message.content ?? message.Content ?? "";
    source.type = "audio/webm";
    audio.appendChild(source);

    msgDiv.appendChild(audio);

    const time = document.createElement("div");
    time.className = "audio-time";
    time.textContent = tooltipTime(curTime);
    msgDiv.appendChild(time);

    return msgDiv;
}

/* ================= Helpers giống Razor ================= */

function parseDate(v) {
    if (!v) return new Date();
    if (v instanceof Date) return v;
    const d = new Date(v);
    if (!isNaN(d.getTime())) return d;
    return new Date();
}

function tooltipTime(dt) {
    const hh = String(dt.getHours()).padStart(2, "0");
    const mm = String(dt.getMinutes()).padStart(2, "0");
    return `${hh}:${mm}`;
}

function dividerLabel(dt, now) {
    const dayDiff = Math.floor((stripTime(now) - stripTime(dt)) / (24 * 60 * 60 * 1000));
    const time = tooltipTime(dt);

    if (dayDiff === 0) return time;
    if (dayDiff === 1) return `Hôm qua, ${time}`;
    if (dayDiff < 7) return `${dayDiff} ngày trước, ${time}`;

    const dd = String(dt.getDate()).padStart(2, "0");
    const mo = String(dt.getMonth() + 1).padStart(2, "0");
    const yy = dt.getFullYear();
    return `${dd}/${mo}/${yy}, ${time}`;
}

function stripTime(d) {
    return new Date(d.getFullYear(), d.getMonth(), d.getDate()).getTime();
}

function spacingClass(prevTime, curTime) {
    if (!prevTime) return "";
    const diffMs = curTime - prevTime;
    return diffMs < 60 * 1000 ? "spacing-tight" : "spacing-spaced";
}

function needDivider(prevTime, curTime) {
    if (!prevTime) return false;
    return (curTime - prevTime) > 15 * 60 * 1000;
}

function getLastMessageTime() {
    const lastTip =
        scroller.querySelector(".bubble-group:last-of-type .message-time-tooltip") ||
        scroller.querySelector(".bubble-group:last-of-type .image-time") ||
        scroller.querySelector(".bubble-group:last-of-type .audio-time");

    if (!lastTip) return null;

    const t = (lastTip.textContent || "").trim();
    const m = /^(\d{2}):(\d{2})$/.exec(t);
    if (!m) return null;

    const hh = parseInt(m[1], 10);
    const mm = parseInt(m[2], 10);

    const base = new Date();
    base.setHours(hh, mm, 0, 0);
    return base;
}
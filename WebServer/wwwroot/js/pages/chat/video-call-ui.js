import { chatService, callService } from "../../services/chatService.js";
import { sendCall } from "../../services/ws-client.js";
import {
    setVideoElements,
    startCallerOffer,
    handleIncomingOffer,
    handleIncomingAnswer,
    handleIncomingIce,
    hangup,
    toggleMic,
    toggleCamera
} from "./webrtc-service.js";

const MODAL_HOST_ID = "form_modal-main";
const INVITE_TIMEOUT_MS = 45000;

const state = {
    status: "idle",
    role: null,
    conversationId: null,
    callType: "video",
    meId: null,
    peerId: null,
    peerName: "",
    inviteTimer: null
};

const btnVideoCall = document.getElementById("btnVideoCall");
const btnVoiceCall = document.getElementById("btnVoiceCall");

function getHost() {
    return document.getElementById(MODAL_HOST_ID);
}

function getCallRoot() {
    return document.querySelector('[data-call-popup="1"]');
}

function getIncomingRoot() {
    return document.querySelector('[data-vdcall-popup="1"]');
}

function isBusy() {
    return state.status !== "idle" && state.status !== "ended";
}

function clearInviteTimer() {
    if (state.inviteTimer) {
        clearTimeout(state.inviteTimer);
        state.inviteTimer = null;
    }
}

function resetState() {
    clearInviteTimer();
    state.status = "idle";
    state.role = null;
    state.conversationId = null;
    state.callType = "video";
    state.meId = null;
    state.peerId = null;
    state.peerName = "";
}

function closeCallUi() {
    const host = getHost();
    if (host) host.innerHTML = "";
}

function setStatus(status, text) {
    state.status = status;
    const root = getCallRoot();
    if (root) root.dataset.callState = status;

    document.querySelectorAll("[data-call-status]").forEach((el) => {
        el.textContent = text;
    });
}

function setControlIcon(button, enabled) {
    const action = button?.dataset?.action;
    const icon = button?.querySelector("i");
    if (!icon) return;

    if (action === "toggle-mic") {
        icon.className = `fa-solid ${enabled ? "fa-microphone" : "fa-microphone-slash"}`;
    }

    if (action === "toggle-camera") {
        icon.className = `fa-solid ${enabled ? "fa-video" : "fa-video-slash"}`;
    }
}

function showEnded(message, closeDelay = 1100) {
    setStatus("ended", message);
    clearInviteTimer();
    hangup();

    window.setTimeout(() => {
        closeCallUi();
        resetState();
    }, closeDelay);
}

function getActiveThreadItem() {
    return document.querySelector(".thread-item.active");
}

function getActiveConversationId() {
    const id = getActiveThreadItem()?.dataset?.id;
    return id ? Number(id) : null;
}

async function getPeerInfo(conversationId) {
    return await chatService.getPeer(conversationId)
        .then((res) => res.data)
        .catch((err) => {
            console.error("Failed to get peer info:", err);
            return null;
        });
}

async function openCallPopup({ conversationId, callType = "video", statusText = "Đang gọi..." }) {
    const host = getHost();
    if (!host) return null;

    const res = await callService.getCallPopup(conversationId, callType);
    host.innerHTML = res.data;

    const root = getCallRoot();
    const localVideo = host.querySelector("#localVideo");
    const remoteVideo = host.querySelector("#remoteVideo");
    setVideoElements(localVideo, remoteVideo);
    setStatus(state.status === "idle" ? "calling" : state.status, statusText);

    return root;
}

async function showIncomingCallPopup(payload) {
    const host = getHost();
    if (!host) return;

    const res = await callService.getIncomingPopup(payload);
    host.innerHTML = res.data;
}

function sendSignal(toUserId, payload) {
    if (!toUserId) return;
    sendCall(String(toUserId), payload);
}

async function startCall(callType = "video", conversationId = null) {
    if (isBusy()) {
        alert("Bạn đang có một cuộc gọi khác.");
        return;
    }

    const activeConversationId = conversationId || getActiveConversationId();
    if (!activeConversationId) {
        alert("Bạn chưa chọn cuộc trò chuyện.");
        return;
    }

    const peerInfo = await getPeerInfo(activeConversationId);
    if (!peerInfo?.me?.accountId || !peerInfo?.peer?.accountId) {
        alert("Không lấy được thông tin người nhận cuộc gọi.");
        return;
    }

    state.status = "calling";
    state.role = "caller";
    state.conversationId = activeConversationId;
    state.callType = callType;
    state.meId = Number(peerInfo.me.accountId);
    state.peerId = Number(peerInfo.peer.accountId);
    state.peerName = peerInfo.peer.accountName || "Người dùng";

    await openCallPopup({
        conversationId: activeConversationId,
        callType,
        statusText: callType === "audio" ? "Đang gọi thoại..." : "Đang gọi video..."
    });

    sendSignal(state.peerId, {
        kind: "invite",
        callType,
        conversationId: activeConversationId,
        fromUserId: state.meId,
        fromUserName: peerInfo.me.accountName,
        fromUserPhoto: peerInfo.me.photoPath,
        toUserId: state.peerId,
        toUserName: peerInfo.peer.accountName,
        toUserPhoto: peerInfo.peer.photoPath
    });

    clearInviteTimer();
    state.inviteTimer = window.setTimeout(() => {
        if (state.status !== "calling") return;
        sendSignal(state.peerId, {
            kind: "cancel",
            conversationId: state.conversationId,
            callType: state.callType,
            reason: "timeout"
        });
        showEnded("Không có phản hồi.");
    }, INVITE_TIMEOUT_MS);
}

function buildIncomingPayload(msg) {
    const p = msg.payload || {};
    return {
        conversationId: Number(p.conversationId),
        callType: p.callType || "video",
        fromUserId: Number(msg.fromUserId || p.fromUserId),
        fromUserName: p.fromUserName || "Người dùng",
        fromUserPhoto: p.fromUserPhoto || "",
        toUserId: Number(msg.toUserId || p.toUserId || 0),
        toUserName: p.toUserName || "",
        toUserPhoto: p.toUserPhoto || ""
    };
}

async function handleInvite(msg) {
    const payload = buildIncomingPayload(msg);
    if (!payload.conversationId || !payload.fromUserId) return;

    if (isBusy()) {
        sendSignal(payload.fromUserId, {
            kind: "busy",
            conversationId: payload.conversationId,
            callType: payload.callType
        });
        return;
    }

    state.status = "incoming";
    state.role = "callee";
    state.conversationId = payload.conversationId;
    state.callType = payload.callType;
    state.meId = payload.toUserId || null;
    state.peerId = payload.fromUserId;
    state.peerName = payload.fromUserName;

    await showIncomingCallPopup(payload);
}

async function acceptIncomingCall(popup) {
    const payload = readIncomingPopup(popup);
    if (!payload.peerId || !payload.conversationId) return;

    state.status = "connecting";
    state.role = "callee";
    state.conversationId = payload.conversationId;
    state.callType = payload.callType || "video";
    state.meId = payload.meId;
    state.peerId = payload.peerId;
    state.peerName = payload.peerName;

    sendSignal(payload.peerId, {
        kind: "accept",
        conversationId: payload.conversationId,
        callType: payload.callType,
        fromUserId: payload.meId,
        fromUserName: payload.meName,
        fromUserPhoto: payload.mePhoto,
        toUserId: payload.peerId,
        toUserName: payload.peerName,
        toUserPhoto: payload.peerPhoto
    });

    await openCallPopup({
        conversationId: payload.conversationId,
        callType: payload.callType,
        statusText: "Đang kết nối..."
    });
}

function declineIncomingCall(popup, reason = "decline") {
    const payload = readIncomingPopup(popup);
    if (payload.peerId) {
        sendSignal(payload.peerId, {
            kind: reason,
            conversationId: payload.conversationId,
            callType: payload.callType
        });
    }

    closeCallUi();
    hangup();
    resetState();
}

function readIncomingPopup(popup) {
    return {
        conversationId: Number(popup.dataset.conversationId),
        callType: popup.dataset.callType || "video",
        meId: Number(popup.dataset.meId),
        meName: popup.dataset.meName || "",
        mePhoto: popup.dataset.mePhoto || "",
        peerId: Number(popup.dataset.peerId),
        peerName: popup.dataset.peerName || "",
        peerPhoto: popup.dataset.peerPhoto || ""
    };
}

async function handleAccept(msg) {
    if (state.role !== "caller") return;

    clearInviteTimer();
    const p = msg.payload || {};
    const peerId = Number(msg.fromUserId || p.fromUserId);
    const conversationId = Number(p.conversationId || state.conversationId);
    const callType = p.callType || state.callType || "video";

    state.status = "connecting";
    setStatus("connecting", "Đang kết nối...");

    await startCallerOffer({
        meId: state.meId,
        peerId,
        conversationId,
        callType
    });
}

async function handleOffer(msg) {
    const p = msg.payload || {};
    const fromUserId = Number(msg.fromUserId || p.fromUserId);
    const conversationId = Number(p.conversationId || state.conversationId);
    const callType = p.callType || state.callType || "video";

    if (!getCallRoot()) {
        state.status = "connecting";
        await openCallPopup({ conversationId, callType, statusText: "Đang kết nối..." });
    }

    await handleIncomingOffer({
        meId: state.meId || Number(msg.toUserId || p.toUserId),
        fromUserId,
        conversationId,
        callType,
        sdp: p.sdp
    });

    setStatus("connected", "Đã kết nối");
}

async function handleAnswer(msg) {
    await handleIncomingAnswer({ sdp: msg.payload?.sdp });
    setStatus("connected", "Đã kết nối");
}

function handleRemoteEnd(msg) {
    const kind = msg.payload?.kind;
    const text = kind === "busy"
        ? "Người này đang bận."
        : kind === "decline"
            ? "Cuộc gọi bị từ chối."
            : kind === "cancel"
                ? "Cuộc gọi đã bị hủy."
                : "Cuộc gọi đã kết thúc.";

    closeIncomingIfVisible();
    showEnded(text);
}

function closeIncomingIfVisible() {
    if (getIncomingRoot()) closeCallUi();
}

function endCurrentCall(reason = "end") {
    const target = state.peerId || Number(getCallRoot()?.dataset.peerId || 0);
    if (target) {
        sendSignal(target, {
            kind: reason,
            conversationId: state.conversationId || Number(getCallRoot()?.dataset.conversationId || 0),
            callType: state.callType || getCallRoot()?.dataset.callType || "video",
            reason: "user_hangup"
        });
    }

    showEnded("Cuộc gọi đã kết thúc.", 500);
}

btnVideoCall?.addEventListener("click", () => startCall("video"));
btnVoiceCall?.addEventListener("click", () => startCall("audio"));

document.addEventListener("click", async (e) => {
    const videoMenu = e.target.closest(".js-video");
    const voiceMenu = e.target.closest(".js-call");
    if (videoMenu || voiceMenu) {
        e.preventDefault();
        e.stopPropagation();

        const item = e.target.closest(".thread-item");
        if (item && !item.classList.contains("active")) {
            item.click();
            window.setTimeout(() => startCall(videoMenu ? "video" : "audio", Number(item.dataset.id)), 120);
        } else {
            await startCall(videoMenu ? "video" : "audio", item?.dataset.id ? Number(item.dataset.id) : null);
        }
        return;
    }

    const incomingClose = e.target.closest(".vdcall_consumer_close-btn");
    if (incomingClose) {
        const popup = incomingClose.closest('[data-vdcall-popup="1"]') || getIncomingRoot();
        if (popup) declineIncomingCall(popup, "decline");
        return;
    }

    const incomingBackdrop = e.target.closest(".vdcall_consumer_call-modal-backdrop");
    if (incomingBackdrop && e.target === incomingBackdrop) {
        declineIncomingCall(incomingBackdrop, "decline");
        return;
    }

    const incomingAction = e.target.closest(".vdcall_consumer_action-btn[data-action]");
    if (incomingAction) {
        const popup = incomingAction.closest('[data-vdcall-popup="1"]') || getIncomingRoot();
        if (!popup) return;

        if (incomingAction.dataset.action === "accept") {
            await acceptIncomingCall(popup);
        } else {
            declineIncomingCall(popup, "decline");
        }
        return;
    }

    const callButton = e.target.closest('[data-call-popup="1"] button[data-action]');
    if (!callButton) return;

    const action = callButton.dataset.action;
    if (action === "end") {
        endCurrentCall(state.status === "calling" || state.status === "incoming" ? "cancel" : "end");
        return;
    }

    if (action === "toggle-mic") {
        const next = callButton.dataset.on !== "1";
        callButton.dataset.on = next ? "1" : "0";
        setControlIcon(callButton, next);
        toggleMic(next);
        return;
    }

    if (action === "toggle-camera") {
        const next = callButton.dataset.on !== "1";
        callButton.dataset.on = next ? "1" : "0";
        setControlIcon(callButton, next);
        toggleCamera(next);
    }
});

document.addEventListener("keydown", (e) => {
    if (e.key !== "Escape") return;
    const popup = getIncomingRoot();
    if (popup) declineIncomingCall(popup, "decline");
});

window.addEventListener("ws:message", async (e) => {
    const msg = e.detail;
    if (msg?.type !== "call.event") return;

    const kind = msg.payload?.kind;

    try {
        if (kind === "invite") await handleInvite(msg);
        else if (kind === "accept") await handleAccept(msg);
        else if (kind === "decline" || kind === "busy" || kind === "cancel" || kind === "end") handleRemoteEnd(msg);
        else if (kind === "offer") await handleOffer(msg);
        else if (kind === "answer") await handleAnswer(msg);
        else if (kind === "ice") await handleIncomingIce({ candidate: msg.payload?.candidate });
    } catch (err) {
        console.error("[CALL] failed to handle event", err);
        showEnded("Không thể thiết lập cuộc gọi.");
    }
});

window.addEventListener("call:rtc-state", (e) => {
    if (state.status === "idle" || state.status === "ended") return;

    const connectionState = e.detail?.connectionState;
    const iceState = e.detail?.iceConnectionState;

    if (connectionState === "connected" || iceState === "connected" || iceState === "completed") {
        setStatus("connected", "Đã kết nối");
    }

    if (connectionState === "failed" || connectionState === "disconnected") {
        setStatus("connecting", "Kết nối không ổn định...");
    }

    if (connectionState === "closed") {
        showEnded("Cuộc gọi đã kết thúc.", 500);
    }
});

window.addEventListener("call:media-error", (e) => {
    console.error("[CALL] media error", e.detail?.error);
    alert("Không truy cập được camera/micro. Vui lòng kiểm tra quyền trình duyệt.");
    endCurrentCall("cancel");
});

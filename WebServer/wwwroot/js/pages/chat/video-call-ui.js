// video-call-ui.js
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
//import { openCallPopup } from "./call-popup-ui.js"; 
const btnVideoCall = document.getElementById("btnVideoCall");
const MODAL_HOST_ID = "form_modal-main";
function getHost() {
    return document.getElementById(MODAL_HOST_ID);
}
async function openCallPopup({ conversationId, callType = "video" }) {
    const host = getHost();
    if (!host) return;

    const res = await callService.getCallPopup(conversationId, callType);
    host.innerHTML = res.data;

    const localVideo = host.querySelector("#localVideo");
    const remoteVideo = host.querySelector("#remoteVideo");
    setVideoElements(localVideo, remoteVideo);
}

function getActiveThreadItem() {
    return document.querySelector(".thread-item.active");
}

function getActiveConversationId() {
    const active = getActiveThreadItem();
    const id = active?.dataset?.id;
    return id ? parseInt(id, 10) : null;
}
function getActivePeerName() {
    const active = getActiveThreadItem();
    return active?.dataset?.name || "Người dùng";
}

async function getActivePeerInfo() {
    const conversationId = getActiveConversationId();
    if (!conversationId) return null;
    return await chatService.getPeer(conversationId)
        .then(res => res.data)
        .catch(err => {
            console.error("Failed to get peer info:", err);
            return null;
        });
}

async function showIncomingCallPopup(p) { // người nhận cuộc gọi

    const res = await callService.getIncomingPopup({
        conversationId: p.conversationId,
        callType: p.callType,

        fromUserId: p.fromUserId,
        fromUserName: p.fromUserName,
        fromUserPhoto: p.fromUserPhoto,

        toUserId: p.toUserId ?? 0,
        toUserName: p.toUserName ?? "",
        toUserPhoto: p.toUserPhoto ?? ""
    });

    document.getElementById("form_modal-main").innerHTML = res.data;
}
if (btnVideoCall) {
    btnVideoCall.addEventListener("click", async () => {
        const conversationId = getActiveConversationId();
        const InfoMePeer = await getActivePeerInfo();
        if (!conversationId) {
            alert("Bạn chưa chọn cuộc trò chuyện.");
            return;
        }
        const peerName = getActivePeerName();
        sendCall(InfoMePeer.peer.accountId, {
            kind: "invite",
            callType: "video",
            conversationId,
            fromUserName: InfoMePeer.me.accountName,
            fromUserPhoto: InfoMePeer.me.photoPath,
            toUserName: peerName,
            toUserPhoto: InfoMePeer.peer.photoPath,
        });
        await openCallPopup({ conversationId, callType: "video" });
    });
} else {
    console.warn("btnVideoCall not found (#btnVideoCall)");
}

// nhận tin nhắn WS về cuộc gọi video
window.addEventListener("ws:message", async (e) => {
    const msg = e.detail;

    if (msg?.type !== "call.event") return;

    console.log("[CALL EVENT]", msg);

    // payload.kind === "invite"
    if (msg.payload?.kind === "invite") {
        const p = msg.payload;

        const payload = {
            conversationId: p.conversationId,
            callType: p.callType,

            fromUserId: msg.fromUserId,
            fromUserName: p.fromUserName,
            fromUserPhoto: p.fromUserPhoto,

            toUserId: msg.toUserId ?? 0,
            toUserName: p.toUserName ?? "",
            toUserPhoto: p.toUserPhoto ?? ""
        };

        await showIncomingCallPopup(payload);
    }
    else if (msg.payload?.kind === "accept") {
        // TODO: xử lý accept
        //alert("người nghe đồng ý gọi!");
        const p = msg.payload;

        // msg.fromUserId = người accept (peer)
        const peerId = Number(msg.fromUserId);
        const conversationId = Number(p.conversationId);
        const callType = p.callType || "video";

        // me info: bạn có thể lấy từ UI call popup (data-me-id)
        const callPopup = document.querySelector('[data-call-popup="1"]');
        const meId = callPopup ? Number(callPopup.dataset.meId) : null;

        // CALLER tạo offer sau khi nhận accept
        await startCallerOffer({
            meId,
            peerId,
            conversationId,
            callType
        });

    }

    else if (msg.payload?.kind === "decline") {
        alert("người nghe từ chối cuộc gọi!");
        const host = document.getElementById("form_modal-main");
        if (host) host.innerHTML = "";
    }
    else if (msg.payload?.kind === "offer") { // callee consummer offer
        const p = msg.payload;
        const callPopup = document.querySelector('[data-call-popup="1"]');
        const meId = callPopup ? Number(callPopup.dataset.meId) : null;

        await handleIncomingOffer({
            meId,
            fromUserId: Number(msg.fromUserId),
            conversationId: Number(p.conversationId),
            callType: p.callType || "video",
            sdp: p.sdp
        });
    }
    else if (msg.payload?.kind === "answer") {
        await handleIncomingAnswer({ sdp: msg.payload.sdp });
    }
    else if (msg.payload?.kind === "ice") {
        await handleIncomingIce({ candidate: msg.payload.candidate });
    }
});
//================================== UI ==========================
// Bind 1 lần: đóng popup khi bấm nút X (hoặc click backdrop)
(function bindIncomingCallPopupEvents() {
    document.addEventListener("click", (e) => {
        const closeBtn = e.target.closest(".vdcall_consumer_close-btn");
        if (closeBtn) {
            const host = document.getElementById("form_modal-main");
            if (host) host.innerHTML = "";
            return;
        }

        const backdrop = e.target.closest(".vdcall_consumer_call-modal-backdrop");
        if (backdrop && e.target === backdrop) {
            const host = document.getElementById("form_modal-main");
            if (host) host.innerHTML = "";
        }
    });

    document.addEventListener("keydown", (e) => {
        if (e.key === "Escape") {
            const host = document.getElementById("form_modal-main");
            if (host) host.innerHTML = "";
        }
    });
    document.addEventListener("click", async (e) => {
        const btn = e.target.closest(".vdcall_consumer_action-btn");
        if (!btn) return;

        const action = btn.dataset.action; // accept | decline
        const popup = btn.closest('[data-vdcall-popup="1"]')
            || document.querySelector('[data-vdcall-popup="1"]');
        if (!popup) return;

        const payload = {
            conversationId: Number(popup.dataset.conversationId),
            callType: popup.dataset.callType,

            meId: Number(popup.dataset.meId),
            meName: popup.dataset.meName,
            mePhoto: popup.dataset.mePhoto,

            peerId: Number(popup.dataset.peerId),
            peerName: popup.dataset.peerName,
            peerPhoto: popup.dataset.peerPhoto
        };

        if (action === "accept") {
            sendCall(payload.peerId, {
                kind: "accept",
                conversationId: payload.conversationId,
                callType: payload.callType,

                fromUserName: payload.meName,
                fromUserPhoto: payload.mePhoto,

                toUserName: payload.peerName,
                toUserPhoto: payload.peerPhoto
            });
            const cv_id = payload.conversationId;
            await openCallPopup({ conversationId: payload.conversationId, callType: "video" });
        } else if (action === "decline") {
            sendCall(payload.peerId, {
                kind: "decline",
                conversationId: payload.conversationId,
                callType: payload.callType
            });
            const host = document.getElementById("form_modal-main");
            if (host) host.innerHTML = "";
        }
    });
})();
(function bindCallPopupControls() {
    document.addEventListener("click", (e) => {
        const root = e.target.closest('[data-call-popup="1"]');
        if (!root) return;

        const btn = e.target.closest("button[data-action]");
        if (!btn) return;

        const action = btn.dataset.action;

        if (action === "end") {
            hangup();
            const host = getHost();
            if (host) host.innerHTML = "";
            return;
        }

        if (action === "toggle-mic") {
            const on = btn.dataset.on === "1";
            const next = !on;
            btn.dataset.on = next ? "1" : "0";
            toggleMic(next);
            return;
        }

        if (action === "toggle-camera") {
            const on = btn.dataset.on === "1";
            const next = !on;
            btn.dataset.on = next ? "1" : "0";
            toggleCamera(next);
            return;
        }
    });
})();
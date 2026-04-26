// webrtc-service.js
import { sendCall } from "../../services/ws-client.js";

const RTC_CONFIG = {
    iceServers: [
        { urls: "stun:stun.l.google.com:19302" },
        // PROD: nên có TURN để qua NAT tốt
        // { urls: "turn:YOUR_TURN:3478", username: "...", credential: "..." }
    ],
};

// session state
const rtc = {
    pc: null,
    localStream: null,
    remoteStream: null,

    meId: null,
    peerId: null,
    conversationId: null,
    callType: "video",

    localVideoEl: null,
    remoteVideoEl: null,

    // ICE queue nếu ICE tới trước khi setRemoteDescription
    pendingIce: [],
};

function log(...args) {
    console.log("[RTC]", ...args);
}

export function setVideoElements(localVideoEl, remoteVideoEl) {
    rtc.localVideoEl = localVideoEl;
    rtc.remoteVideoEl = remoteVideoEl;

    if (rtc.localStream && rtc.localVideoEl) {
        rtc.localVideoEl.srcObject = rtc.localStream;
        rtc.localVideoEl.muted = true;
        rtc.localVideoEl.autoplay = true;
        rtc.localVideoEl.playsInline = true;
    }
    if (rtc.remoteStream && rtc.remoteVideoEl) {
        rtc.remoteVideoEl.srcObject = rtc.remoteStream;
        rtc.remoteVideoEl.autoplay = true;
        rtc.remoteVideoEl.playsInline = true;
    }
}

async function ensureLocalStream(callType) {
    if (rtc.localStream) return rtc.localStream;

    const constraints =
        callType === "audio"
            ? { audio: true, video: false }
            : { audio: true, video: { width: 640, height: 360 } };

    rtc.localStream = await navigator.mediaDevices.getUserMedia(constraints);

    if (rtc.localVideoEl) {
        rtc.localVideoEl.srcObject = rtc.localStream;
        rtc.localVideoEl.muted = true;
        rtc.localVideoEl.autoplay = true;
        rtc.localVideoEl.playsInline = true;
    }

    return rtc.localStream;
}

function ensureRemoteStream() {
    if (!rtc.remoteStream) rtc.remoteStream = new MediaStream();
    if (rtc.remoteVideoEl) {
        rtc.remoteVideoEl.srcObject = rtc.remoteStream;
        rtc.remoteVideoEl.autoplay = true;
        rtc.remoteVideoEl.playsInline = true;
    }
    return rtc.remoteStream;
}

export async function ensurePeerConnection({ meId, peerId, conversationId, callType }) {
    // nếu pc đã tồn tại thì chỉ update state
    if (rtc.pc) {
        rtc.meId = meId ?? rtc.meId;
        rtc.peerId = peerId ?? rtc.peerId;
        rtc.conversationId = conversationId ?? rtc.conversationId;
        rtc.callType = callType ?? rtc.callType;
        return rtc.pc;
    }

    rtc.meId = meId;
    rtc.peerId = peerId;
    rtc.conversationId = conversationId;
    rtc.callType = callType || "video";

    rtc.pc = new RTCPeerConnection(RTC_CONFIG);

    // local tracks
    const local = await ensureLocalStream(rtc.callType);
    local.getTracks().forEach((t) => rtc.pc.addTrack(t, local));

    // remote tracks
    ensureRemoteStream();
    rtc.pc.ontrack = (ev) => {
        const stream = ensureRemoteStream();
        ev.streams[0].getTracks().forEach((t) => stream.addTrack(t));
    };

    // ICE -> send to peer
    rtc.pc.onicecandidate = (ev) => {
        if (!ev.candidate) return;
        sendCall(String(rtc.peerId), {
            kind: "ice",
            conversationId: rtc.conversationId,
            candidate: ev.candidate,
        });
    };

    rtc.pc.onconnectionstatechange = () => {
        log("connectionState =", rtc.pc.connectionState);
    };

    rtc.pc.oniceconnectionstatechange = () => {
        log("iceConnectionState =", rtc.pc.iceConnectionState);
    };

    return rtc.pc;
}

/**
 * CALLER: gọi sau khi nhận "accept"
 * Tạo offer -> setLocal -> send offer
 */
export async function startCallerOffer({ meId, peerId, conversationId, callType }) {
    await ensurePeerConnection({ meId, peerId, conversationId, callType });

    const offer = await rtc.pc.createOffer({
        offerToReceiveAudio: true,
        offerToReceiveVideo: callType !== "audio",
    });

    await rtc.pc.setLocalDescription(offer);

    sendCall(String(peerId), {
        kind: "offer",
        conversationId,
        callType,
        sdp: rtc.pc.localDescription,
    });

    log("offer sent");
}

/**
 * CALLEE: nhận offer -> setRemote(offer) -> createAnswer -> setLocal(answer) -> send answer
 */
export async function handleIncomingOffer({ meId, fromUserId, conversationId, callType, sdp }) {
    await ensurePeerConnection({
        meId,
        peerId: fromUserId,
        conversationId,
        callType,
    });

    await rtc.pc.setRemoteDescription(new RTCSessionDescription(sdp));

    // add ICE queued (nếu có)
    if (rtc.pendingIce.length > 0) {
        for (const c of rtc.pendingIce) {
            try { await rtc.pc.addIceCandidate(c); } catch { }
        }
        rtc.pendingIce = [];
    }

    const answer = await rtc.pc.createAnswer();
    await rtc.pc.setLocalDescription(answer);

    sendCall(String(fromUserId), {
        kind: "answer",
        conversationId,
        callType,
        sdp: rtc.pc.localDescription,
    });

    log("answer sent");
}

/**
 * CALLER: nhận answer -> setRemote(answer)
 */
export async function handleIncomingAnswer({ sdp }) {
    if (!rtc.pc) {
        log("ignore answer because pc not ready");
        return;
    }
    await rtc.pc.setRemoteDescription(new RTCSessionDescription(sdp));

    // add ICE queued (nếu có)
    if (rtc.pendingIce.length > 0) {
        for (const c of rtc.pendingIce) {
            try { await rtc.pc.addIceCandidate(c); } catch { }
        }
        rtc.pendingIce = [];
    }

    log("answer setRemote done");
}

/**
 * Both: nhận ICE -> addIceCandidate
 * Nếu chưa setRemoteDescription thì queue lại
 */
export async function handleIncomingIce({ candidate }) {
    if (!candidate) return;

    if (!rtc.pc) {
        log("queue ice because pc not ready");
        rtc.pendingIce.push(new RTCIceCandidate(candidate));
        return;
    }

    // Nếu chưa có remoteDescription, addIceCandidate sẽ hay fail -> queue
    if (!rtc.pc.remoteDescription) {
        rtc.pendingIce.push(new RTCIceCandidate(candidate));
        return;
    }

    try {
        await rtc.pc.addIceCandidate(new RTCIceCandidate(candidate));
    } catch (e) {
        log("addIceCandidate failed:", e);
    }
}

export function hangup() {
    try { rtc.pc?.close(); } catch { }
    rtc.pc = null;

    if (rtc.localStream) {
        rtc.localStream.getTracks().forEach((t) => t.stop());
        rtc.localStream = null;
    }
    rtc.remoteStream = null;
    rtc.pendingIce = [];

    rtc.meId = rtc.peerId = rtc.conversationId = null;
    rtc.callType = "video";

    if (rtc.localVideoEl) rtc.localVideoEl.srcObject = null;
    if (rtc.remoteVideoEl) rtc.remoteVideoEl.srcObject = null;

    log("hangup done");
}

export function toggleMic(enabled) {
    if (!rtc.localStream) return;
    rtc.localStream.getAudioTracks().forEach((t) => (t.enabled = enabled));
}

export function toggleCamera(enabled) {
    if (!rtc.localStream) return;
    rtc.localStream.getVideoTracks().forEach((t) => (t.enabled = enabled));
}
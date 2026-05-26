import { sendGroupCall } from "../../services/ws-client.js";

const RTC_CONFIG = {
    iceServers: [
        { urls: "stun:stun.l.google.com:19302" }
    ]
};

const rtc = {
    roomId: null,
    conversationId: null,
    callType: "video",
    meId: null,
    localStream: null,
    localVideoEl: null,
    peers: new Map()
};

function emit(name, detail = {}) {
    window.dispatchEvent(new CustomEvent(name, { detail }));
}

function log(...args) {
    console.log("[GROUP RTC]", ...args);
}

function attachLocalVideo() {
    if (!rtc.localVideoEl || !rtc.localStream) return;

    rtc.localVideoEl.srcObject = rtc.localStream;
    rtc.localVideoEl.muted = true;
    rtc.localVideoEl.autoplay = true;
    rtc.localVideoEl.playsInline = true;
}

export function setGroupLocalVideo(localVideoEl) {
    rtc.localVideoEl = localVideoEl;
    attachLocalVideo();
}

async function ensureLocalStream(callType) {
    if (rtc.localStream) return rtc.localStream;

    const constraints = callType === "audio"
        ? { audio: true, video: false }
        : { audio: true, video: { width: 960, height: 540 } };

    try {
        rtc.localStream = await navigator.mediaDevices.getUserMedia(constraints);
        attachLocalVideo();
        return rtc.localStream;
    } catch (error) {
        emit("group-call:media-error", { error: error?.message || String(error) });
        throw error;
    }
}

export async function initGroupRtc({ roomId, conversationId, callType, meId, localVideoEl = null }) {
    rtc.roomId = roomId || rtc.roomId;
    rtc.conversationId = conversationId || rtc.conversationId;
    rtc.callType = callType || rtc.callType || "video";
    rtc.meId = Number(meId || rtc.meId);

    if (localVideoEl) setGroupLocalVideo(localVideoEl);
    await ensureLocalStream(rtc.callType);
}

function createPeerMeta(remoteUserId) {
    const pc = new RTCPeerConnection(RTC_CONFIG);
    const remoteStream = new MediaStream();
    const meta = {
        pc,
        remoteUserId: Number(remoteUserId),
        remoteStream,
        pendingIce: [],
        offered: false
    };

    rtc.localStream?.getTracks().forEach((track) => pc.addTrack(track, rtc.localStream));

    pc.ontrack = (event) => {
        const incomingTracks = event.streams?.[0]?.getTracks?.() || [event.track];
        incomingTracks.forEach((track) => {
            if (!remoteStream.getTracks().some((existing) => existing.id === track.id)) {
                remoteStream.addTrack(track);
            }
        });

        emit("group-call:remote-stream", {
            roomId: rtc.roomId,
            userId: Number(remoteUserId),
            stream: remoteStream
        });
    };

    pc.onicecandidate = (event) => {
        if (!event.candidate) return;

        sendGroupCall("group.call.signal", {
            kind: "ice",
            roomId: rtc.roomId,
            conversationId: rtc.conversationId,
            toUserId: Number(remoteUserId),
            candidate: event.candidate
        });
    };

    pc.onconnectionstatechange = () => {
        emit("group-call:peer-state", {
            roomId: rtc.roomId,
            userId: Number(remoteUserId),
            connectionState: pc.connectionState,
            iceConnectionState: pc.iceConnectionState
        });
    };

    pc.oniceconnectionstatechange = () => {
        emit("group-call:peer-state", {
            roomId: rtc.roomId,
            userId: Number(remoteUserId),
            connectionState: pc.connectionState,
            iceConnectionState: pc.iceConnectionState
        });
    };

    rtc.peers.set(Number(remoteUserId), meta);
    return meta;
}

async function ensureGroupPeer(remoteUserId) {
    const key = Number(remoteUserId);
    if (rtc.peers.has(key)) return rtc.peers.get(key);

    await ensureLocalStream(rtc.callType);
    return createPeerMeta(key);
}

async function flushPendingIce(meta) {
    if (!meta.pc.remoteDescription || meta.pendingIce.length === 0) return;

    const queue = [...meta.pendingIce];
    meta.pendingIce = [];

    for (const candidate of queue) {
        try {
            await meta.pc.addIceCandidate(candidate);
        } catch (error) {
            log("addIceCandidate failed", error);
        }
    }
}

export async function createGroupOffer(remoteUserId) {
    const meta = await ensureGroupPeer(remoteUserId);
    if (meta.offered || meta.pc.signalingState !== "stable") return;

    meta.offered = true;

    const offer = await meta.pc.createOffer({
        offerToReceiveAudio: true,
        offerToReceiveVideo: rtc.callType !== "audio"
    });

    await meta.pc.setLocalDescription(offer);

    sendGroupCall("group.call.signal", {
        kind: "offer",
        roomId: rtc.roomId,
        conversationId: rtc.conversationId,
        callType: rtc.callType,
        toUserId: Number(remoteUserId),
        sdp: meta.pc.localDescription
    });
}

export async function handleGroupOffer({ fromUserId, roomId, conversationId, callType, sdp }) {
    rtc.roomId = roomId || rtc.roomId;
    rtc.conversationId = conversationId || rtc.conversationId;
    rtc.callType = callType || rtc.callType;

    const meta = await ensureGroupPeer(fromUserId);
    await meta.pc.setRemoteDescription(new RTCSessionDescription(sdp));
    await flushPendingIce(meta);

    const answer = await meta.pc.createAnswer();
    await meta.pc.setLocalDescription(answer);

    sendGroupCall("group.call.signal", {
        kind: "answer",
        roomId: rtc.roomId,
        conversationId: rtc.conversationId,
        callType: rtc.callType,
        toUserId: Number(fromUserId),
        sdp: meta.pc.localDescription
    });
}

export async function handleGroupAnswer({ fromUserId, sdp }) {
    const meta = rtc.peers.get(Number(fromUserId));
    if (!meta) return;

    await meta.pc.setRemoteDescription(new RTCSessionDescription(sdp));
    await flushPendingIce(meta);
}

export async function handleGroupIce({ fromUserId, candidate }) {
    if (!candidate) return;

    const meta = await ensureGroupPeer(fromUserId);
    const ice = new RTCIceCandidate(candidate);

    if (!meta.pc.remoteDescription) {
        meta.pendingIce.push(ice);
        return;
    }

    try {
        await meta.pc.addIceCandidate(ice);
    } catch (error) {
        log("addIceCandidate failed", error);
    }
}

export function removeGroupPeer(userId) {
    const key = Number(userId);
    const meta = rtc.peers.get(key);
    if (!meta) return;

    try {
        meta.pc.close();
    } catch { }

    rtc.peers.delete(key);
}

export function toggleGroupMic(enabled) {
    rtc.localStream?.getAudioTracks().forEach((track) => {
        track.enabled = enabled;
    });
}

export function toggleGroupCamera(enabled) {
    rtc.localStream?.getVideoTracks().forEach((track) => {
        track.enabled = enabled;
    });
}

export function hangupGroupCall() {
    rtc.peers.forEach((meta) => {
        try {
            meta.pc.close();
        } catch { }
    });
    rtc.peers.clear();

    if (rtc.localStream) {
        rtc.localStream.getTracks().forEach((track) => track.stop());
        rtc.localStream = null;
    }

    rtc.roomId = null;
    rtc.conversationId = null;
    rtc.callType = "video";
    rtc.meId = null;

    if (rtc.localVideoEl) rtc.localVideoEl.srcObject = null;
    rtc.localVideoEl = null;
}

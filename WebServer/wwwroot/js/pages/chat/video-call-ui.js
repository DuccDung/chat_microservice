import { chatService, callService } from "../../services/chatService.js";
import { sendCall, sendGroupCall } from "../../services/ws-client.js";
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
import {
    setGroupLocalVideo,
    initGroupRtc,
    createGroupOffer,
    handleGroupOffer,
    handleGroupAnswer,
    handleGroupIce,
    removeGroupPeer,
    toggleGroupMic,
    toggleGroupCamera,
    hangupGroupCall
} from "./group-webrtc-service.js";

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

const groupState = {
    status: "idle",
    role: null,
    roomId: null,
    conversationId: null,
    callType: "video",
    meId: null,
    micOn: true,
    cameraOn: true,
    participants: new Map(),
    pendingRoom: null
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

function getGroupCallRoot() {
    return document.querySelector('[data-group-call-popup="1"]');
}

function getGroupIncomingRoot() {
    return document.querySelector('[data-group-incoming-popup="1"]');
}

function isGroupBusy() {
    return groupState.status !== "idle" && groupState.status !== "ended";
}

function isBusy() {
    return (state.status !== "idle" && state.status !== "ended") || isGroupBusy();
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

function resetGroupState() {
    groupState.status = "idle";
    groupState.role = null;
    groupState.roomId = null;
    groupState.conversationId = null;
    groupState.callType = "video";
    groupState.meId = null;
    groupState.micOn = true;
    groupState.cameraOn = true;
    groupState.participants.clear();
    groupState.pendingRoom = null;
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

function setGroupStatus(status, text) {
    groupState.status = status;
    const root = getGroupCallRoot();
    if (root) root.dataset.callState = status;

    document.querySelectorAll("[data-group-call-status]").forEach((el) => {
        el.textContent = text;
    });
}

function setGroupControlIcon(button, enabled) {
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

function showGroupEnded(message, closeDelay = 900) {
    setGroupStatus("ended", message);
    hangupGroupCall();

    window.setTimeout(() => {
        closeCallUi();
        resetGroupState();
    }, closeDelay);
}

function getActiveThreadItem() {
    return document.querySelector(".thread-item.active");
}

function getActiveConversationId() {
    const id = getActiveThreadItem()?.dataset?.id;
    return id ? Number(id) : null;
}

function getThreadItemByConversationId(conversationId) {
    return document.querySelector(`.thread-item[data-id="${conversationId}"]`);
}

function isGroupConversation(conversationId = null) {
    const item = conversationId
        ? getThreadItemByConversationId(conversationId)
        : getActiveThreadItem();

    return item?.dataset?.isGroup === "true";
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

async function openGroupCallPopup({ conversationId, callType = "video", roomId = "", statusText = "Đang kết nối..." }) {
    const host = getHost();
    if (!host) return null;

    const res = await callService.getGroupCallPopup(conversationId, callType, roomId);
    host.innerHTML = res.data;

    const root = getGroupCallRoot();
    const localVideo = host.querySelector("#groupLocalVideo");
    setGroupLocalVideo(localVideo);
    setGroupStatus(groupState.status === "idle" ? "calling" : groupState.status, statusText);

    return root;
}

async function showGroupIncomingCallPopup(room) {
    const host = getHost();
    if (!host || !room) return;

    const res = await callService.getGroupIncomingPopup({
        conversationId: room.conversationId,
        callType: room.callType || "video",
        roomId: room.roomId,
        fromUserId: room.startedByUserId
    });
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

    if (isGroupConversation(activeConversationId)) {
        await startGroupCall(callType, activeConversationId);
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

async function startGroupCall(callType = "video", conversationId = null) {
    if ((state.status !== "idle" && state.status !== "ended") || isGroupBusy()) {
        alert("Bạn đang có một cuộc gọi khác.");
        return;
    }

    const activeConversationId = conversationId || getActiveConversationId();
    if (!activeConversationId) {
        alert("Bạn chưa chọn cuộc trò chuyện.");
        return;
    }

    groupState.status = "calling";
    groupState.role = "caller";
    groupState.conversationId = activeConversationId;
    groupState.callType = callType;
    groupState.micOn = true;
    groupState.cameraOn = callType !== "audio";

    const root = await openGroupCallPopup({
        conversationId: activeConversationId,
        callType,
        statusText: callType === "audio" ? "Đang gọi thoại nhóm..." : "Đang gọi video nhóm..."
    });

    if (!root) {
        resetGroupState();
        return;
    }

    groupState.meId = Number(root.dataset.meId);

    try {
        await initGroupRtc({
            conversationId: activeConversationId,
            callType,
            meId: groupState.meId,
            localVideoEl: root.querySelector("#groupLocalVideo")
        });

        sendGroupCall("group.call.start", {
            conversationId: activeConversationId,
            callType
        });
    } catch (error) {
        console.error("[GROUP CALL] media failed", error);
        closeCallUi();
        resetGroupState();
    }
}

function readGroupIncomingPopup(popup) {
    return {
        conversationId: Number(popup.dataset.conversationId),
        roomId: popup.dataset.roomId || "",
        callType: popup.dataset.callType || "video",
        meId: Number(popup.dataset.meId),
        meName: popup.dataset.meName || "",
        mePhoto: popup.dataset.mePhoto || "",
        peerId: Number(popup.dataset.peerId),
        peerName: popup.dataset.peerName || "",
        peerPhoto: popup.dataset.peerPhoto || ""
    };
}

async function acceptGroupIncomingCall(popup) {
    const payload = readGroupIncomingPopup(popup);
    if (!payload.roomId || !payload.conversationId) return;

    groupState.status = "connecting";
    groupState.role = "callee";
    groupState.roomId = payload.roomId;
    groupState.conversationId = payload.conversationId;
    groupState.callType = payload.callType || "video";
    groupState.meId = payload.meId;
    groupState.micOn = true;
    groupState.cameraOn = groupState.callType !== "audio";

    const root = await openGroupCallPopup({
        conversationId: payload.conversationId,
        callType: payload.callType,
        roomId: payload.roomId,
        statusText: "Đang tham gia cuộc gọi nhóm..."
    });

    try {
        await initGroupRtc({
            roomId: payload.roomId,
            conversationId: payload.conversationId,
            callType: payload.callType,
            meId: payload.meId,
            localVideoEl: root?.querySelector("#groupLocalVideo")
        });

        sendGroupCall("group.call.accept", {
            roomId: payload.roomId
        });

        if (groupState.pendingRoom) {
            applyGroupRoom(groupState.pendingRoom);
        }
    } catch (error) {
        console.error("[GROUP CALL] accept failed", error);
        sendGroupCall("group.call.decline", { roomId: payload.roomId });
        showGroupEnded("Không truy cập được camera/micro.", 900);
    }
}

function declineGroupIncomingCall(popup) {
    const payload = readGroupIncomingPopup(popup);
    if (payload.roomId) {
        sendGroupCall("group.call.decline", {
            roomId: payload.roomId
        });
    }

    closeCallUi();
    hangupGroupCall();
    resetGroupState();
}

function updateGroupTileMedia(userId, stream) {
    const tile = document.querySelector(`[data-group-call-tile="${userId}"]`);
    const video = tile?.querySelector("[data-remote-video]");
    if (!video) return;

    video.srcObject = stream;
    video.autoplay = true;
    video.playsInline = true;
}

function participantStatusText(status) {
    if (status === "joined") return "Đã tham gia";
    if (status === "declined") return "Đã từ chối";
    if (status === "left") return "Đã rời";
    return "Đang chờ";
}

function participantDisplayName(participant) {
    if (!participant) return "Người dùng";
    if (Number(participant.accountId) === Number(groupState.meId)) return "Bạn";
    return participant.accountName || "Người dùng";
}

function participantPhoto(participant) {
    return participant?.photoPath || "/assets/images/avatar-default.png";
}

function escapeHtml(value) {
    return String(value ?? "")
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll('"', "&quot;")
        .replaceAll("'", "&#039;");
}

function ensureGroupTile(participant) {
    const userId = Number(participant.accountId);
    let tile = document.querySelector(`[data-group-call-tile="${userId}"]`);
    if (tile) return tile;

    const grid = document.querySelector("[data-group-call-grid]");
    if (!grid) return null;

    tile = document.createElement("article");
    tile.className = "group_call_tile";
    tile.dataset.groupCallTile = String(userId);
    tile.dataset.micOn = participant.micEnabled ? "1" : "0";
    tile.dataset.cameraOn = participant.cameraEnabled ? "1" : "0";
    tile.innerHTML = `
        <video class="group_call_video" data-remote-video autoplay playsinline></video>
        <div class="group_call_avatar_fallback">
            <img src="${escapeHtml(participantPhoto(participant))}" alt="${escapeHtml(participantDisplayName(participant))}" onerror="this.onerror=null;this.src='/assets/images/avatar-default.png';" />
        </div>
        <div class="group_call_tile_shade"></div>
        <div class="group_call_tile_footer">
            <span class="group_call_tile_name">${escapeHtml(participantDisplayName(participant))}</span>
            <span class="group_call_tile_state">
                <i class="fa-solid ${participant.micEnabled ? "fa-microphone" : "fa-microphone-slash"}"></i>
                <i class="fa-solid ${participant.cameraEnabled ? "fa-video" : "fa-video-slash"}"></i>
            </span>
        </div>`;

    grid.appendChild(tile);
    return tile;
}

function updateGroupTile(participant) {
    const userId = Number(participant.accountId);
    const isMe = userId === Number(groupState.meId);
    const tile = isMe
        ? document.querySelector(`[data-group-call-tile="${userId}"]`)
        : ensureGroupTile(participant);

    if (!tile) return;

    tile.dataset.micOn = participant.micEnabled ? "1" : "0";
    tile.dataset.cameraOn = participant.cameraEnabled ? "1" : "0";

    const name = tile.querySelector(".group_call_tile_name");
    if (name) name.textContent = participantDisplayName(participant);

    const icons = tile.querySelector(".group_call_tile_state");
    if (icons) {
        icons.innerHTML = `
            <i class="fa-solid ${participant.micEnabled ? "fa-microphone" : "fa-microphone-slash"}"></i>
            <i class="fa-solid ${participant.cameraEnabled ? "fa-video" : "fa-video-slash"}"></i>`;
    }
}

function removeGroupTile(userId) {
    const tile = document.querySelector(`[data-group-call-tile="${userId}"]`);
    if (tile && !tile.dataset.localTile) tile.remove();
}

function renderGroupParticipants(room) {
    if (!room?.participants) return;

    const participants = room.participants;
    const joined = participants.filter((p) => p.status === "joined");
    const count = document.querySelector("[data-group-call-count]");
    if (count) count.textContent = `${joined.length}/${participants.length} đang tham gia`;

    const list = document.querySelector("[data-group-call-participants]");
    if (list) {
        list.innerHTML = participants.map((participant) => `
            <div class="group_call_participant"
                 data-group-call-participant="${participant.accountId}"
                 data-status="${escapeHtml(participant.status)}">
                <img src="${escapeHtml(participantPhoto(participant))}" alt="${escapeHtml(participantDisplayName(participant))}" onerror="this.onerror=null;this.src='/assets/images/avatar-default.png';" />
                <div>
                    <strong>${escapeHtml(participantDisplayName(participant))}</strong>
                    <span>${escapeHtml(participantStatusText(participant.status))}</span>
                </div>
            </div>
        `).join("");
    }

    participants.forEach((participant) => {
        const userId = Number(participant.accountId);
        groupState.participants.set(userId, participant);

        if (participant.status === "joined") {
            updateGroupTile(participant);
        } else {
            removeGroupTile(userId);
            removeGroupPeer(userId);
        }
    });
}

async function connectGroupParticipants(room) {
    if (!room?.participants || !groupState.meId) return;

    const me = room.participants.find((participant) => Number(participant.accountId) === Number(groupState.meId));
    if (me?.status !== "joined") return;

    const joined = room.participants.filter((participant) => participant.status === "joined");
    for (const participant of joined) {
        const remoteId = Number(participant.accountId);
        if (!remoteId || remoteId === Number(groupState.meId)) continue;

        if (Number(groupState.meId) < remoteId) {
            try {
                await createGroupOffer(remoteId);
            } catch (error) {
                console.error("[GROUP CALL] offer failed", error);
            }
        }
    }
}

function applyGroupRoom(room) {
    if (!room) return;

    groupState.pendingRoom = room;
    groupState.roomId = room.roomId || groupState.roomId;
    groupState.conversationId = Number(room.conversationId || groupState.conversationId);
    groupState.callType = room.callType || groupState.callType || "video";

    const root = getGroupCallRoot();
    if (root) {
        root.dataset.roomId = groupState.roomId || "";
        root.dataset.conversationId = String(groupState.conversationId || "");
        root.dataset.callType = groupState.callType;
    }

    const me = room.participants?.find((participant) => Number(participant.accountId) === Number(groupState.meId));
    if (me) {
        groupState.micOn = !!me.micEnabled;
        groupState.cameraOn = !!me.cameraEnabled;
    }

    renderGroupParticipants(room);

    const joinedCount = room.participants?.filter((participant) => participant.status === "joined").length || 1;
    const statusText = room.callType === "audio"
        ? `Đang gọi thoại nhóm - ${joinedCount} người tham gia`
        : `Đang gọi video nhóm - ${joinedCount} người tham gia`;
    setGroupStatus(joinedCount > 1 ? "connected" : "calling", statusText);

    connectGroupParticipants(room);
}

async function handleGroupInvite(msg) {
    const room = msg.payload?.room;
    if (!room?.roomId) return;

    if (isBusy()) {
        sendGroupCall("group.call.decline", { roomId: room.roomId });
        return;
    }

    groupState.status = "incoming";
    groupState.role = "callee";
    groupState.roomId = room.roomId;
    groupState.conversationId = Number(room.conversationId);
    groupState.callType = room.callType || "video";
    groupState.pendingRoom = room;

    await showGroupIncomingCallPopup(room);
}

async function handleGroupStarted(msg) {
    const room = msg.payload?.room;
    if (!room?.roomId) return;

    groupState.roomId = room.roomId;
    groupState.conversationId = Number(room.conversationId);
    groupState.callType = room.callType || groupState.callType;

    await initGroupRtc({
        roomId: groupState.roomId,
        conversationId: groupState.conversationId,
        callType: groupState.callType,
        meId: groupState.meId,
        localVideoEl: getGroupCallRoot()?.querySelector("#groupLocalVideo")
    });

    applyGroupRoom(room);
}

async function handleGroupCallEvent(msg) {
    const payload = msg.payload || {};
    const kind = payload.kind;

    if (kind === "invite") {
        await handleGroupInvite(msg);
        return;
    }

    if (kind === "started") {
        await handleGroupStarted(msg);
        return;
    }

    if (kind === "participants" || kind === "participant_state") {
        if (!getGroupCallRoot()) {
            groupState.pendingRoom = payload.room || groupState.pendingRoom;
            return;
        }
        applyGroupRoom(payload.room);
        return;
    }

    if (kind === "participant_left") {
        if (!getGroupCallRoot()) {
            groupState.pendingRoom = payload.room || groupState.pendingRoom;
            return;
        }
        if (payload.leftUserId) {
            removeGroupPeer(Number(payload.leftUserId));
            removeGroupTile(Number(payload.leftUserId));
        }
        applyGroupRoom(payload.room);
        return;
    }

    if (kind === "ended") {
        showGroupEnded("Cuộc gọi nhóm đã kết thúc.");
        return;
    }

    if (kind === "offer") {
        if (!getGroupCallRoot()) return;
        await handleGroupOffer({
            fromUserId: Number(msg.fromUserId || payload.fromUserId),
            roomId: payload.roomId,
            conversationId: Number(payload.conversationId || groupState.conversationId),
            callType: payload.callType || groupState.callType,
            sdp: payload.sdp
        });
        setGroupStatus("connected", "Đã kết nối cuộc gọi nhóm");
        return;
    }

    if (kind === "answer") {
        await handleGroupAnswer({
            fromUserId: Number(msg.fromUserId || payload.fromUserId),
            sdp: payload.sdp
        });
        setGroupStatus("connected", "Đã kết nối cuộc gọi nhóm");
        return;
    }

    if (kind === "ice") {
        await handleGroupIce({
            fromUserId: Number(msg.fromUserId || payload.fromUserId),
            candidate: payload.candidate
        });
    }
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

    const groupIncomingAction = e.target.closest(".group_incoming_action[data-action], .group_incoming_close[data-action]");
    if (groupIncomingAction) {
        const popup = groupIncomingAction.closest('[data-group-incoming-popup="1"]') || getGroupIncomingRoot();
        if (!popup) return;

        if (groupIncomingAction.dataset.action === "accept") {
            await acceptGroupIncomingCall(popup);
        } else {
            declineGroupIncomingCall(popup);
        }
        return;
    }

    const groupIncomingBackdrop = e.target.closest(".group_incoming_backdrop");
    if (groupIncomingBackdrop && e.target === groupIncomingBackdrop) {
        declineGroupIncomingCall(groupIncomingBackdrop);
        return;
    }

    const groupCallButton = e.target.closest('[data-group-call-popup="1"] button[data-action]');
    if (groupCallButton) {
        const action = groupCallButton.dataset.action;

        if (action === "toggle-participants") {
            getGroupCallRoot()?.classList.toggle("group_call_modal--participants-open");
            return;
        }

        if (action === "end") {
            if (groupState.roomId) {
                sendGroupCall("group.call.end", { roomId: groupState.roomId });
            }
            showGroupEnded("Cuộc gọi nhóm đã kết thúc.", 500);
            return;
        }

        if (action === "toggle-mic") {
            const next = groupCallButton.dataset.on !== "1";
            groupCallButton.dataset.on = next ? "1" : "0";
            groupState.micOn = next;
            setGroupControlIcon(groupCallButton, next);
            toggleGroupMic(next);
            const localTile = document.querySelector(`[data-group-call-tile="${groupState.meId}"]`);
            if (localTile) localTile.dataset.micOn = next ? "1" : "0";
            if (groupState.roomId) {
                sendGroupCall("group.call.state", {
                    roomId: groupState.roomId,
                    micEnabled: next,
                    cameraEnabled: groupState.cameraOn
                });
            }
            return;
        }

        if (action === "toggle-camera") {
            const next = groupCallButton.dataset.on !== "1";
            groupCallButton.dataset.on = next ? "1" : "0";
            groupState.cameraOn = next;
            setGroupControlIcon(groupCallButton, next);
            toggleGroupCamera(next);
            const localTile = document.querySelector(`[data-group-call-tile="${groupState.meId}"]`);
            if (localTile) localTile.dataset.cameraOn = next ? "1" : "0";
            if (groupState.roomId) {
                sendGroupCall("group.call.state", {
                    roomId: groupState.roomId,
                    micEnabled: groupState.micOn,
                    cameraEnabled: next
                });
            }
            return;
        }
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
    const groupPopup = getGroupIncomingRoot();
    if (groupPopup) {
        declineGroupIncomingCall(groupPopup);
        return;
    }

    const popup = getIncomingRoot();
    if (popup) declineIncomingCall(popup, "decline");
});

window.addEventListener("ws:message", async (e) => {
    const msg = e.detail;

    if (msg?.type === "group.call.event") {
        try {
            await handleGroupCallEvent(msg);
        } catch (err) {
            console.error("[GROUP CALL] failed to handle event", err);
            showGroupEnded("Không thể thiết lập cuộc gọi nhóm.");
        }
        return;
    }

    if (msg?.type === "group.call.error") {
        console.error("[GROUP CALL]", msg.message);
        alert(msg.message || "Không thể xử lý cuộc gọi nhóm.");
        showGroupEnded("Cuộc gọi nhóm đã kết thúc.", 500);
        return;
    }

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

window.addEventListener("group-call:remote-stream", (e) => {
    updateGroupTileMedia(Number(e.detail?.userId), e.detail?.stream);
});

window.addEventListener("group-call:peer-state", (e) => {
    if (groupState.status === "idle" || groupState.status === "ended") return;

    const userId = Number(e.detail?.userId);
    const connectionState = e.detail?.connectionState;
    const iceState = e.detail?.iceConnectionState;
    const tile = document.querySelector(`[data-group-call-tile="${userId}"]`);

    if (tile) {
        tile.dataset.connectionState = connectionState || iceState || "";
    }

    if (connectionState === "failed" || connectionState === "disconnected") {
        setGroupStatus("connecting", "Một số kết nối chưa ổn định...");
    }
});

window.addEventListener("group-call:media-error", (e) => {
    console.error("[GROUP CALL] media error", e.detail?.error);
    alert("Không truy cập được camera/micro. Vui lòng kiểm tra quyền trình duyệt.");

    if (groupState.roomId) {
        sendGroupCall("group.call.leave", { roomId: groupState.roomId });
    }

    showGroupEnded("Không thể tham gia cuộc gọi nhóm.", 700);
});

window.addEventListener("beforeunload", () => {
    if (groupState.roomId && groupState.status !== "idle" && groupState.status !== "ended") {
        sendGroupCall("group.call.leave", { roomId: groupState.roomId });
    }
});

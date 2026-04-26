import { callService } from "../../services/chatService"; // hoặc đường dẫn bạn đang dùng

const MODAL_HOST_ID = "form_modal-main";

function getHost() {
    return document.getElementById(MODAL_HOST_ID);
}

export async function openCallPopup({ conversationId, callType = "video" }) {
    const host = getHost();
    if (!host) return;

    const res = await callService.getCallPopup(conversationId, callType);
    host.innerHTML = res.data; // partial html
}

export function closeCallPopup() {
    const host = getHost();
    if (host) host.innerHTML = "";
}

// Event delegation cho các nút trong popup
document.addEventListener("click", (e) => {
    const btn = e.target.closest("[data-action]");
    if (!btn) return;

    const popup = btn.closest('[data-call-popup="1"]');
    if (!popup) return;

    const action = btn.dataset.action;

    // data từ server render sẵn
    const conversationId = Number(popup.dataset.conversationId);
    const callType = popup.dataset.callType;

    const meId = Number(popup.dataset.meId);
    const peerId = Number(popup.dataset.peerId);

    // UI-only toggle (không can thiệp WebRTC ở đây)
    if (action === "toggle-camera") {
        const on = btn.dataset.on === "1";
        btn.dataset.on = on ? "0" : "1";
        btn.innerHTML = on
            ? `<i class="lucide lucide-video-off"></i>`
            : `<i class="lucide lucide-video"></i>`;

        // Nếu bạn muốn phát event để code WebRTC khác bắt:
        window.dispatchEvent(new CustomEvent("call:toggle-camera", {
            detail: { conversationId, callType, meId, peerId, enabled: !on }
        }));
        return;
    }

    if (action === "toggle-mic") {
        const on = btn.dataset.on === "1";
        btn.dataset.on = on ? "0" : "1";
        btn.innerHTML = on
            ? `<i class="lucide lucide-mic-off"></i>`
            : `<i class="lucide lucide-mic"></i>`;

        window.dispatchEvent(new CustomEvent("call:toggle-mic", {
            detail: { conversationId, callType, meId, peerId, enabled: !on }
        }));
        return;
    }

    if (action === "end") {
        window.dispatchEvent(new CustomEvent("call:end", {
            detail: { conversationId, callType, meId, peerId, reason: "user_hangup" }
        }));
        closeCallPopup();
        return;
    }
});
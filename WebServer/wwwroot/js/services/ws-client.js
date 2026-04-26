let ws = null;

export function connectWs() {
    if (ws && (ws.readyState === WebSocket.OPEN || ws.readyState === WebSocket.CONNECTING))
        return ws;

    const scheme = location.protocol === "https:" ? "wss" : "ws";
    ws = new WebSocket(`${scheme}://${location.host}/ws`);

    ws.onopen = () => console.log("[WS] Connected");

    ws.onmessage = (evt) => {
        try {
            const data = JSON.parse(evt.data);

            // phát custom event toàn cục
            window.dispatchEvent(new CustomEvent("ws:message", {
                detail: data
            }));

        } catch (e) {
            console.warn("Invalid WS message:", evt.data);
        }
    };

    ws.onclose = () => console.log("[WS] Closed");
    ws.onerror = (err) => console.error("[WS] Error", err);

    return ws;
}

export function subscribeConversation(conversationId) {
    connectWs();

    if (!ws) return;

    const send = () => ws.send(JSON.stringify({
        type: "subscribe",
        conversationId
    }));

    if (ws.readyState === WebSocket.OPEN)
        send();
    else
        ws.addEventListener("open", send, { once: true });
}

export function sendCall(toUserId, payload) {
    connectWs();
    if (!ws) return;

    const msg = {
        type: "call.send",
        toUserId: String(toUserId),
        payload
    };

    const send = () => ws.send(JSON.stringify(msg));
    if (ws.readyState === WebSocket.OPEN) send();
    else ws.addEventListener("open", send, { once: true });
}
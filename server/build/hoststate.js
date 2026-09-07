/**
 * AiConnect host-state signaling adapter (integration layer — upstream
 * untouched). The stdio bridge's /health derives its three-state from marker
 * lines the stdio server emits on stdout:
 *
 *   {"aiconnect_host_state":"waiting_for_host"|"connected"}
 *
 * The Revit MCP server connects to the add-in TCP socket PER TOOL CALL
 * (ConnectionManager.withRevitConnection → SocketClient). This module runs an
 * independent, lightweight TCP probe against the add-in port so host
 * availability is reported WITHOUT waiting for a tool call, and only emits a
 * marker when the state CHANGES (no per-call spam).
 *
 * Env-gated: markers only when AICONNECT_HOST_STATE=1 (set by the gateway for
 * stdio host_plugin connectors). Standalone use of the upstream server keeps
 * its stdout pristine for plain MCP.
 */
import net from "net";
let lastState = null;
let timer = null;
let enabled = false;
function emit(state) {
    if (state !== lastState) {
        lastState = state;
        if (enabled) {
            process.stdout.write(JSON.stringify({ aiconnect_host_state: state }) + "\n");
        }
    }
}
function probe() {
    const port = Number(process.env.REVIT_SOCKET_PORT ?? 8088);
    const host = process.env.REVIT_SOCKET_HOST ?? "127.0.0.1";
    let finished = false;
    const sock = net.connect({ host, port });
    const finish = (state) => {
        if (finished)
            return;
        finished = true;
        sock.destroy();
        emit(state);
    };
    sock.setTimeout(1500, () => finish("waiting_for_host"));
    sock.once("connect", () => {
        // Send a JSON-RPC ping to verify this is genuinely Revit MCP Server, not an unrelated listener
        const pingMsg = JSON.stringify({ jsonrpc: "2.0", method: "ping", id: "probe" }) + "\n";
        sock.write(pingMsg);
    });
    sock.on("data", (data) => {
        try {
            const resp = JSON.parse(data.toString());
            if (resp && (resp.id === "probe" || resp.result !== undefined)) {
                finish("connected");
            }
            else {
                finish("waiting_for_host");
            }
        }
        catch {
            finish("waiting_for_host");
        }
    });
    sock.once("error", () => finish("waiting_for_host"));
}
/** Start the host-state probe loop. No-op (silent) unless
 * AICONNECT_HOST_STATE=1 — see module doc. */
export function startHostStateProbe(intervalMs = 2000) {
    enabled = process.env.AICONNECT_HOST_STATE === "1";
    probe();
    if (enabled) {
        timer = setInterval(probe, intervalMs);
        if (timer.unref)
            timer.unref();
    }
}

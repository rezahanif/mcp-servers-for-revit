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

let lastState: string | null = null;
let timer: NodeJS.Timeout | null = null;
let enabled = false;

function emit(state: string) {
  if (state !== lastState) {
    lastState = state;
    if (enabled) {
      process.stdout.write(JSON.stringify({ aiconnect_host_state: state }) + "\n");
    }
  }
}

function probe() {
  const port = Number(process.env.REVIT_SOCKET_PORT ?? 8080);
  const sock = net.connect({ host: "127.0.0.1", port });
  const finish = (state: string) => {
    sock.destroy();
    emit(state);
  };
  sock.setTimeout(1000, () => finish("waiting_for_host"));
  sock.once("connect", () => finish("connected"));
  sock.once("error", () => finish("waiting_for_host"));
}

/** Start the host-state probe loop. No-op (silent) unless
 * AICONNECT_HOST_STATE=1 — see module doc. */
export function startHostStateProbe(intervalMs = 2000): void {
  enabled = process.env.AICONNECT_HOST_STATE === "1";
  probe();
  if (enabled) {
    timer = setInterval(probe, intervalMs);
    if (timer.unref) timer.unref();
  }
}

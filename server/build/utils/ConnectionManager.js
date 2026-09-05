import { RevitClientConnection } from "./SocketClient.js";
// Mutex to serialize all Revit connections - prevents race conditions
// when multiple requests are made in parallel
let connectionMutex = Promise.resolve();
/**
 * 连接到Revit客户端并执行操作
 * @param operation 连接成功后要执行的操作函数
 * @returns 操作的结果
 */
export async function withRevitConnection(operation) {
    // Wait for any pending connection to complete before starting a new one
    const previousMutex = connectionMutex;
    let releaseMutex;
    connectionMutex = new Promise((resolve) => {
        releaseMutex = resolve;
    });
    await previousMutex;
    const port = Number(process.env.REVIT_SOCKET_PORT ?? 8080);
    const host = process.env.REVIT_SOCKET_HOST ?? "127.0.0.1";
    const revitClient = new RevitClientConnection(host, port);
    try {
        // 连接到Revit客户端
        if (!revitClient.isConnected) {
            await new Promise((resolve, reject) => {
                const onConnect = () => {
                    revitClient.socket.removeListener("connect", onConnect);
                    revitClient.socket.removeListener("error", onError);
                    resolve();
                };
                const onError = (error) => {
                    revitClient.socket.removeListener("connect", onConnect);
                    revitClient.socket.removeListener("error", onError);
                    reject(new Error("connect to revit client failed"));
                };
                revitClient.socket.on("connect", onConnect);
                revitClient.socket.on("error", onError);
                revitClient.connect();
                setTimeout(() => {
                    revitClient.socket.removeListener("connect", onConnect);
                    revitClient.socket.removeListener("error", onError);
                    reject(new Error("连接到Revit客户端失败"));
                }, 5000);
            });
        }
        // 执行操作
        return await operation(revitClient);
    }
    finally {
        // 断开连接
        revitClient.disconnect();
        // Release the mutex so the next request can proceed
        releaseMutex();
    }
}

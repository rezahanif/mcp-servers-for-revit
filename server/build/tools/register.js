import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";
import { ensureLicensed, envelope } from "../aioconnect.js";
export async function registerTools(server) {
    // AiConnect: env-gated license gate + envelope wrap (AICONNECT_ENABLE=1).
    // Disabled → plain upstream server (standalone/dev runs need no token,
    // no envelope). Managed spawns inject AICONNECT_ENABLE=1, so the gate
    // refuses to register any tool without a valid bound MCP_LICENSE_TOKEN,
    // and every tool's handler gets per-call recheck + response envelope.
    const aioconnectEnabled = process.env.AICONNECT_ENABLE === "1";
    const license = aioconnectEnabled ? await ensureLicensed() : null;
    if (aioconnectEnabled) {
        // Generic monkey-patch of server.tool, so the 24 tool files need zero
        // edits (Phase 5 contract: structured envelope everywhere).
        const origTool = server.tool.bind(server);
        server.tool = (name, desc, schema, handler) => {
            const cb = handler ?? schema;
            const wrapped = async (args, extra) => {
                license.ensureLicensed(); // per-call recheck (cheap HS256)
                const result = await cb(args, extra);
                if (result && Array.isArray(result.content)) {
                    result.content = result.content.map((c) => c.type === "text" ? { ...c, text: envelope(c.text) } : c);
                }
                return result;
            };
            if (handler)
                return origTool(name, desc, schema, wrapped);
            return origTool(name, schema, wrapped);
        };
    }
    // 获取当前文件的目录路径
    const __filename = fileURLToPath(import.meta.url);
    const __dirname = path.dirname(__filename);
    // 读取tools目录下的所有文件
    const files = fs.readdirSync(__dirname);
    // 过滤出.ts或.js文件，但排除index文件和register文件
    const toolFiles = files.filter((file) => (file.endsWith(".ts") || file.endsWith(".js")) &&
        file !== "index.ts" &&
        file !== "index.js" &&
        file !== "register.ts" &&
        file !== "register.js");
    // 动态导入并注册每个工具
    for (const file of toolFiles) {
        try {
            // 构建导入路径
            const importPath = `./${file.replace(/\.(ts|js)$/, ".js")}`;
            // 动态导入模块
            const module = await import(importPath);
            // 查找并执行注册函数
            const registerFunctionName = Object.keys(module).find((key) => key.startsWith("register") && typeof module[key] === "function");
            if (registerFunctionName) {
                module[registerFunctionName](server);
                console.error(`已注册工具: ${file}`);
            }
            else {
                console.warn(`警告: 在文件 ${file} 中未找到注册函数`);
            }
        }
        catch (error) {
            console.error(`注册工具 ${file} 时出错:`, error);
        }
    }
}

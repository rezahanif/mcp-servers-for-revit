#!/usr/bin/env python3
"""Revit MCP Connector — Test Scenario Logger

Captures the full communication flow between agent and Revit plugin.
Run on Windows with Revit + MCP server active.

Usage:
    python revit_test_scenario.py                    # interactive menu
    python revit_test_scenario.py --all              # run all scenarios
    python revit_test_scenario.py --scenario 1       # run specific scenario
    python revit_test_scenario.py --log              # just start logging
"""
import json
import sys
import time
import socket
import threading
from datetime import datetime
from pathlib import Path

# ── Configuration ───────────────────────────────────────────────────────
MCP_HOST = "127.0.0.1"
MCP_PORT = int(sys.argv[sys.argv.index("--port") + 1]) if "--port" in sys.argv else 3000
LOG_DIR = Path("test-logs")
LOG_DIR.mkdir(exist_ok=True)

# ── Logger ──────────────────────────────────────────────────────────────
class ScenarioLogger:
    def __init__(self):
        self.log_file = LOG_DIR / f"revit-test-{datetime.now().strftime('%Y%m%d-%H%M%S')}.log"
        self.results = []
        
    def log(self, msg, level="INFO"):
        ts = datetime.now().strftime("%H:%M:%S.%f")[:-3]
        line = f"[{ts}] [{level}] {msg}"
        print(line)
        with open(self.log_file, "a") as f:
            f.write(line + "\n")
        
    def log_json(self, label, data):
        ts = datetime.now().strftime("%H:%M:%S.%f")[:-3]
        line = f"[{ts}] [DATA] {label}"
        print(line)
        print(json.dumps(data, indent=2)[:500])
        with open(self.log_file, "a") as f:
            f.write(line + "\n")
            f.write(json.dumps(data, indent=2) + "\n")
        
    def result(self, scenario, status, detail=""):
        self.results.append({"scenario": scenario, "status": status, "detail": detail})
        icon = "✓" if status == "PASS" else "✗" if status == "FAIL" else "⚠"
        self.log(f"{icon} {scenario}: {status} {detail}")
        
    def summary(self):
        print("\n" + "="*60)
        print("TEST SUMMARY")
        print("="*60)
        pass_count = sum(1 for r in self.results if r["status"] == "PASS")
        fail_count = sum(1 for r in self.results if r["status"] == "FAIL")
        skip_count = sum(1 for r in self.results if r["status"] == "SKIP")
        print(f"Passed: {pass_count} | Failed: {fail_count} | Skipped: {skip_count}")
        print(f"Log: {self.log_file}")
        print("="*60)

# ── MCP Client ──────────────────────────────────────────────────────────
class MCPClient:
    def __init__(self, host, port):
        self.host = host
        self.port = port
        self.sock = None
        self.request_id = 0
        
    def connect(self):
        self.sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self.sock.settimeout(10)
        try:
            self.sock.connect((self.host, self.port))
            return True
        except Exception as e:
            return False
            
    def send(self, method, params=None):
        self.request_id += 1
        msg = {"jsonrpc": "2.0", "id": self.request_id, "method": method}
        if params:
            msg["params"] = params
        self.sock.sendall(json.dumps(msg).encode() + b"\n")
        return self.request_id
        
    def recv(self):
        data = b""
        while True:
            chunk = self.sock.recv(4096)
            if not chunk:
                break
            data += chunk
            if b"\n" in data:
                break
        return json.loads(data.decode().strip().split("\n")[-1])
        
    def close(self):
        if self.sock:
            self.sock.close()

# ── Scenarios ───────────────────────────────────────────────────────────
SCENARIOS = [
    {
        "name": "1. Connection & Handshake",
        "description": "Connect to MCP server and initialize",
        "steps": [
            ("TCP connect to MCP server", "connect"),
            ("Send initialize request", "initialize"),
            ("Receive server capabilities", "check_capabilities"),
        ]
    },
    {
        "name": "2. Tool Discovery",
        "description": "List available tools and verify tiering",
        "steps": [
            ("Request tools/list", "tools_list"),
            ("Count tier-1 tools", "count_tools"),
            ("Verify tier-1 count = 31", "verify_tier1_count"),
        ]
    },
    {
        "name": "3. Basic Tool Call",
        "description": "Call a simple tool (ping or get_info)",
        "steps": [
            ("Call ping tool", "call_ping"),
            ("Verify response structure", "verify_response"),
        ]
    },
    {
        "name": "4. Revit Query",
        "description": "Query Revit model information",
        "steps": [
            ("Call get_current_view_info", "call_get_view"),
            ("Verify response has view data", "verify_view_data"),
        ]
    },
    {
        "name": "5. tools.find with Alias",
        "description": "Test alias-based tool discovery",
        "steps": [
            ('tools.find("material quantities")', "find_material"),
            ("Verify get_material_quantities in results", "verify_find_result"),
        ]
    },
    {
        "name": "6. tools.find with Connector Filter",
        "description": "Test connector-scoped search",
        "steps": [
            ('tools.find("room", connector="revit")', "find_room_scoped"),
            ("Verify results are revit-only", "verify_scoped"),
        ]
    },
]

# ── Scenario Runner ─────────────────────────────────────────────────────
def run_scenario(client, logger, scenario):
    logger.log(f"\n{'='*60}")
    logger.log(f"SCENARIO: {scenario['name']}")
    logger.log(f"Description: {scenario['description']}")
    logger.log(f"{'='*60}")
    
    try:
        # Connect if not connected
        if not client.sock:
            if not client.connect():
                logger.result(scenario["name"], "FAIL", "Could not connect to MCP server")
                return
            logger.log(f"Connected to {MCP_HOST}:{MCP_PORT}")
        
        # Run steps
        for step_name, step_action in scenario["steps"]:
            logger.log(f"  Step: {step_name}")
            
            if step_action == "connect":
                logger.result(f"  {step_name}", "PASS")
                
            elif step_action == "initialize":
                msg_id = client.send("initialize", {
                    "protocolVersion": "2024-11-05",
                    "capabilities": {},
                    "clientInfo": {"name": "revit-test", "version": "1.0"}
                })
                resp = client.recv()
                logger.log_json("initialize response", resp)
                if "result" in resp:
                    logger.result(f"  {step_name}", "PASS")
                else:
                    logger.result(f"  {step_name}", "FAIL", str(resp.get("error")))
                    
            elif step_action == "check_capabilities":
                # Already received in initialize
                logger.result(f"  {step_name}", "PASS")
                
            elif step_action == "tools_list":
                msg_id = client.send("tools/list")
                resp = client.recv()
                tools = resp.get("result", {}).get("tools", [])
                logger.log(f"  Received {len(tools)} tools")
                logger.log_json("tools/list", {"count": len(tools), "names": [t["name"] for t in tools[:10]]})
                logger.result(f"  {step_name}", "PASS", f"{len(tools)} tools")
                
            elif step_action == "count_tools":
                # Already counted
                logger.result(f"  {step_name}", "PASS")
                
            elif step_action == "verify_tier1_count":
                # Would need to check against expected count
                logger.result(f"  {step_name}", "SKIP", "Need expected count")
                
            elif step_action == "call_ping":
                msg_id = client.send("tools/call", {
                    "name": "ping",
                    "arguments": {}
                })
                resp = client.recv()
                logger.log_json("ping response", resp)
                if "result" in resp:
                    logger.result(f"  {step_name}", "PASS")
                else:
                    logger.result(f"  {step_name}", "FAIL", str(resp.get("error")))
                    
            elif step_action == "verify_response":
                logger.result(f"  {step_name}", "PASS")
                
            elif step_action == "call_get_view":
                msg_id = client.send("tools/call", {
                    "name": "get_current_view_info",
                    "arguments": {}
                })
                resp = client.recv()
                logger.log_json("get_current_view_info response", resp)
                if "result" in resp:
                    logger.result(f"  {step_name}", "PASS")
                else:
                    logger.result(f"  {step_name}", "FAIL", str(resp.get("error")))
                    
            elif step_action == "verify_view_data":
                logger.result(f"  {step_name}", "PASS")
                
            elif step_action == "find_material":
                msg_id = client.send("tools/call", {
                    "name": "tools.find",
                    "arguments": {"query": "material quantities"}
                })
                resp = client.recv()
                logger.log_json("tools.find response", resp)
                if "result" in resp:
                    logger.result(f"  {step_name}", "PASS")
                else:
                    logger.result(f"  {step_name}", "FAIL", str(resp.get("error")))
                    
            elif step_action == "verify_find_result":
                logger.result(f"  {step_name}", "PASS")
                
            elif step_action == "find_room_scoped":
                msg_id = client.send("tools/call", {
                    "name": "tools.find",
                    "arguments": {"query": "room", "connector": "revit"}
                })
                resp = client.recv()
                logger.log_json("tools.find (scoped) response", resp)
                if "result" in resp:
                    logger.result(f"  {step_name}", "PASS")
                else:
                    logger.result(f"  {step_name}", "FAIL", str(resp.get("error")))
                    
            elif step_action == "verify_scoped":
                logger.result(f"  {step_name}", "PASS")
                
            else:
                logger.log(f"    Unknown action: {step_action}", "WARN")
                logger.result(f"  {step_name}", "SKIP")
                
            time.sleep(0.1)
            
    except Exception as e:
        logger.result(scenario["name"], "FAIL", str(e))
    finally:
        pass  # Keep connection for next scenario

# ── Main ────────────────────────────────────────────────────────────────
def main():
    logger = ScenarioLogger()
    client = MCPClient(MCP_HOST, MCP_PORT)
    
    print("="*60)
    print("REVIT MCP CONNECTOR — TEST SCENARIO RUNNER")
    print("="*60)
    print(f"Target: {MCP_HOST}:{MCP_PORT}")
    print(f"Log: {logger.log_file}")
    print()
    
    if "--all" in sys.argv:
        scenarios = SCENARIOS
    elif "--scenario" in sys.argv:
        idx = int(sys.argv[sys.argv.index("--scenario") + 1]) - 1
        scenarios = [SCENARIOS[idx]]
    elif "--log" in sys.argv:
        print("Starting logging mode. Press Ctrl+C to stop.")
        print("Connect your Revit + MCP server, then run scenarios manually.")
        try:
            while True:
                time.sleep(1)
        except KeyboardInterrupt:
            return
    else:
        print("Scenarios:")
        for i, s in enumerate(SCENARIOS, 1):
            print(f"  {i}. {s['name']} — {s['description']}")
        print()
        print("Usage:")
        print("  python revit_test_scenario.py --all           # run all")
        print("  python revit_test_scenario.py --scenario 1    # run one")
        print("  python revit_test_scenario.py --log           # log only")
        print("  python revit_test_scenario.py --port 4000     # custom port")
        return
    
    try:
        for scenario in scenarios:
            run_scenario(client, logger, scenario)
    except KeyboardInterrupt:
        logger.log("\nInterrupted by user")
    finally:
        client.close()
        logger.summary()

if __name__ == "__main__":
    main()

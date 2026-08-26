/**
 * Typed error hierarchy for Revit MCP — architecture copied from ltspice-mcp.
 * Each error type carries error_code + hint + recovery list for agent self-healing.
 */
export class RevitError extends Error {
    error_code;
    hint;
    recovery;
    details;
    constructor(message, opts = {}) {
        super(message);
        this.name = "RevitError";
        this.error_code = opts.error_code ?? "revit_error";
        this.hint = opts.hint ?? "";
        this.recovery = opts.recovery ?? [];
        this.details = opts.details ?? {};
    }
    toPayload() {
        return {
            error: this.error_code,
            message: this.message,
            hint: this.hint,
            recovery: this.recovery,
            ...this.details,
        };
    }
}
export class ConnectionError extends RevitError {
    constructor(message, details) {
        super(message, {
            error_code: "connection",
            hint: "Revit is not running or the MCP plugin is not loaded.",
            recovery: [
                "Ensure Revit is running with the MCP plugin installed",
                "Check REVIT_SOCKET_PORT environment variable",
                "Restart Revit and reload the MCP plugin",
            ],
            details,
        });
        this.name = "ConnectionError";
    }
}
export class TransactionError extends RevitError {
    constructor(message, details) {
        super(message, {
            error_code: "transaction",
            hint: "Revit transaction failed — check element parameters and state.",
            recovery: [
                "Verify the element exists and is not locked",
                "Check parameter names and types",
                "Try transactionMode='none' if managing transactions manually",
            ],
            details,
        });
        this.name = "TransactionError";
    }
}
export class ElementNotFoundError extends RevitError {
    constructor(message, details) {
        super(message, {
            error_code: "element_not_found",
            hint: "The specified element ID does not exist in the current document.",
            recovery: [
                "Call get_current_view_elements() to see available elements",
                "Call get_selected_elements() to check current selection",
                "Verify the ElementId is correct",
            ],
            details,
        });
        this.name = "ElementNotFoundError";
    }
}
export class FamilyNotFoundError extends RevitError {
    constructor(message, details) {
        super(message, {
            error_code: "family_not_found",
            hint: "The family type name does not match any loaded family.",
            recovery: [
                "Call get_available_family_types() to list loaded families",
                "Check the family name spelling and category",
                "Load the family into the project first",
            ],
            details,
        });
    }
}
export class CodeExecutionError extends RevitError {
    constructor(message, details) {
        super(message, {
            error_code: "code_execution",
            hint: "C# code execution failed in Revit.",
            recovery: [
                "Check the C# code for compilation errors",
                "Ensure the code uses the provided Document and parameters",
                "Try transactionMode='auto' for simple operations",
            ],
            details,
        });
        this.name = "CodeExecutionError";
    }
}
export class ViewError extends RevitError {
    constructor(message, details) {
        super(message, {
            error_code: "view_error",
            hint: "The current view does not support this operation.",
            recovery: [
                "Switch to a plan or 3D view",
                "Call get_current_view_info() to check the view type",
                "Some operations require a specific view type",
            ],
            details,
        });
        this.name = "ViewError";
    }
}
export class PermissionError extends RevitError {
    constructor(message, details) {
        super(message, {
            error_code: "permission_denied",
            hint: "Operation not permitted — element may be locked or in a workset.",
            recovery: [
                "Check if the element is locked",
                "Verify workset permissions",
                "Try operating on a different element",
            ],
            details,
        });
        this.name = "PermissionError";
    }
}
/**
 * Map error_code → recovery guidance for agent self-healing.
 * Agent can query this without parsing exception messages.
 */
export const ERROR_HINTS = {
    connection: {
        description: "Revit connection unavailable",
        primary_action: "Check Revit is running with MCP plugin",
        diagnostic_tool: "send_code_to_revit",
    },
    transaction: {
        description: "Revit transaction failed",
        primary_action: "Verify element state and parameters",
        diagnostic_tool: "get_selected_elements",
    },
    element_not_found: {
        description: "Element ID not found in document",
        primary_action: "List available elements",
        diagnostic_tool: "get_current_view_elements",
    },
    family_not_found: {
        description: "Family type not loaded",
        primary_action: "List loaded family types",
        diagnostic_tool: "get_available_family_types",
    },
    code_execution: {
        description: "C# code execution failed",
        primary_action: "Check code for compilation errors",
        diagnostic_tool: "send_code_to_revit",
    },
    view_error: {
        description: "Operation not supported in current view",
        primary_action: "Switch to compatible view type",
        diagnostic_tool: "get_current_view_info",
    },
    permission_denied: {
        description: "Element locked or workset restricted",
        primary_action: "Check element lock and workset state",
        diagnostic_tool: "get_selected_elements",
    },
};

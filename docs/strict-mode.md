# Strict Mode

Strict mode is the default target for Unity. A gameplay DSL that silently ignores typos is not useful in real projects.

## Implemented in v1.0.1 Public Preview

- Empty action name
- Duplicate action binding name
- Unknown action during direct runtime execution
- Unknown action while loading specs
- Wrong action argument count while loading specs
- Wrong literal argument type where the runtime can infer it safely
- Unknown entity in handler, condition, action, or value reference
- Unknown property in condition, set action, or value reference
- Empty event name
- Duplicate entity id in the scene
- Unsupported condition syntax, which fails closed with a warning
- Duplicate state
- Duplicate event handler
- Unreachable state
- Unknown transition target
- Invalid state machine interval
- Missing entity declarations for behavior and scenario surfaces

## Planned strict diagnostics

- Richer argument type checking for entity references and project-defined action arguments
- Condition always false where detectable
- Typo-based null fallback
- Null action target

## Error Shape

Diagnostics should carry:

- Severity
- Code
- Message
- Source
- Line
- Column

This lets the CLI, Unity console, debugger window, and future language server show the same error model.

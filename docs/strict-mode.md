# Strict Mode

Strict mode is the default target for Unity. A gameplay DSL that silently ignores typos is not useful in real projects.

## Implemented in the current prototype

- Empty action name
- Duplicate action binding name
- Unknown action during direct runtime execution
- Unknown action while loading specs
- Wrong action argument count while loading specs
- Unknown entity in handler, condition, action, or value reference
- Unknown property in condition, set action, or value reference
- Empty event name
- Duplicate entity id in the scene
- Unsupported condition syntax, which fails closed with a warning

## Planned strict diagnostics

- Unknown entity in broader language constructs
- Unknown action in all future parser surfaces
- Unknown property in all future parser surfaces
- Wrong argument type
- Duplicate state
- Duplicate event handler
- Unreachable state
- Unknown transition target
- Missing action binding in future runtime slices
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

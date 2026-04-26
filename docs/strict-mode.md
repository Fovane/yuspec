# Strict Mode

Strict mode is the default target for Unity. A gameplay DSL that silently ignores
typos is not useful in real projects.

Planned diagnostics:

- Unknown entity
- Unknown action
- Unknown property
- Wrong argument count
- Wrong argument type
- Duplicate state
- Duplicate event handler
- Unreachable state
- Unknown transition target
- Missing action binding
- Condition always false where detectable
- Typo-based null fallback
- Duplicate entity id in the scene
- Empty event name
- Null action target

## Initial Scaffold Coverage

The Unity scaffold can already report basic runtime-level problems:

- Duplicate action binding name
- Empty action name
- Unknown action during direct runtime execution
- Empty event name
- Duplicate entity id in the scene

The parser and semantic validator will expand this into source-level diagnostics
with file, line, and column.

## Error Shape

Diagnostics should carry:

- Severity
- Code
- Message
- Source
- Line
- Column

This lets the CLI, Unity console, debugger window, and future language server
show the same error model.

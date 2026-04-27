# YUSPEC for VS Code

Language support for `.yuspec` gameplay rule files.

## Features

- Syntax highlighting for YUSPEC keywords, event handlers, state transitions, property access, and time literals.
- Bracket matching through the language configuration.
- Keyword completion.
- Keyword hover descriptions.
- Go-to-definition for capitalized entity references.
- Save-time diagnostics for unmatched braces.

## Packaging

This extension uses the VS Code extension host API and can be packaged or published with:

```bash
vsce package
vsce publish
```

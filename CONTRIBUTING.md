# Contributing to YUSPEC

Thanks for helping improve YUSPEC.

This repository currently focuses on the Unity package under
`unity/Packages/com.yuspec.unity`, plus the top-level docs and sample files that
describe the current prototype honestly.

## What to Contribute

- Bug fixes in the Unity package runtime or editor code
- Documentation corrections in `README.md`, `docs/`, or package docs
- Sample `.yuspec` files that better match the current parser
- Small parser/runtime improvements that preserve current behavior
- Debugger or diagnostics polish
- Tests, validation steps, or reproducible repro cases

## Before You Open a PR

Please keep changes focused.

- Do not add large new features unless they are explicitly requested
- Do not rewrite architecture just to make a sample work
- Prefer the smallest change that fixes the issue
- Keep docs honest about what is implemented versus planned

## Good Places to Edit

- `README.md`
- `docs/`
- `unity/Packages/com.yuspec.unity/Runtime/`
- `unity/Packages/com.yuspec.unity/Editor/`
- `unity/Packages/com.yuspec.unity/Samples~/`

## Local Checks

If you change runtime or editor code, verify the relevant files still compile in
Unity and that the sample behavior still matches the docs.

If you change docs or sample files, make sure the raw files are readable in the
repository and not compressed into single-line blobs.

Useful checks:

1. Open the changed `.cs` files and confirm they are normal multi-line source
	files.
2. Check the sample `.yuspec` files against the current parser rules.
3. Run the narrowest Unity validation or editor check that covers the change.

## Style Guidance

- Use one `using` per line in C# files
- Keep namespace, class, and method indentation conventional
- Keep Markdown readable with real headings, lists, and fenced blocks
- Keep `.json` files pretty-printed with stable indentation
- Keep `.yuspec` samples aligned with the currently supported syntax

## Pull Requests

When you open a PR, include:

- A short summary of what changed
- The reason for the change
- Any validation you ran
- Any known limitations that remain

PRs are easiest to review when they do one thing well.

## Reporting Bugs

If you find a bug, include:

- The file or feature involved
- What you expected to happen
- What actually happened
- A minimal reproduction if possible
- Any relevant console output or parser diagnostics

## Security Issues

Please do not file security vulnerabilities as public issues. Contact the
repository owner privately instead.

## Questions

If something is unclear, open an issue or discussion with the smallest possible
example. A short repro is usually more useful than a long description.

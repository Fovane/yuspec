# Contributing to YUSPEC

Thank you for your interest in contributing! YUSPEC is an open-source
Entity-Behavior Programming language — every kind of contribution is welcome.

---

## Ways to Contribute

| Type | How |
|------|-----|
| Bug reports | Open a GitHub issue using the bug report template |
| Feature requests | Open a GitHub issue using the feature request template |
| Fix a bug | Fork → branch → fix → PR |
| New example | Add a `.yus` file to `examples/<domain>/` |
| Documentation | Edit `README.md`, `docs/`, or code comments |
| Language design | Open a Discussion with proposal + rationale |
| Bindings / tooling | VS Code extension, playground, LSP |

---

## Getting Started

### Prerequisites

- CMake 3.16+
- C++17 compiler: MSVC 2019+, GCC 9+, or Clang 10+
- Git

### Build from Source

```bash
git clone https://github.com/<your-username>/yuspec.git
cd yuspec

cmake -S . -B build
cmake --build build --target yuspec1

# Run the test suite
./build/Debug/yuspec1 test examples/testing/01_scenario.yus
./build/Debug/yuspec1 test examples/game/01_mmo.yus
```

All tests must pass (34/34 + 11/11) before submitting a PR.

---

## Branch Naming

```
feature/<short-description>   # new features
fix/<short-description>       # bug fixes
docs/<short-description>      # documentation only
example/<domain-name>         # new .yus example
```

---

## Coding Style

- C++17, no external dependencies for compiler/runtime
- `snake_case` for variables/functions, `PascalCase` for classes
- Every class has a `reset()` method (scenario isolation requirement)
- Public API changes must be reflected in `CHANGELOG.md`
- New language features need at least one `examples/` file demonstrating them

---

## Pull Request Checklist

- [ ] All existing tests still pass
- [ ] New feature has at least one `.yus` example or test scenario
- [ ] `CHANGELOG.md` updated
- [ ] Code follows existing style (no trailing whitespace, Unix line endings)
- [ ] No new compiler warnings introduced

---

## Adding a New Domain Example

1. Create `examples/<domain>/<NN>_<description>.yus`
2. Make sure it passes `yuspec1 test examples/<domain>/<file>.yus`
3. Add domain to the examples table in `README.md`

---

## Reporting Security Issues

Please **do not** file a public issue for security vulnerabilities.  
Email: [maintainer email — add yours here]

---

## Code of Conduct

Be kind, be constructive, be patient. We are all here to build something cool.

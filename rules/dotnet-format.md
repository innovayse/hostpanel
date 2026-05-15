# dotnet format Rules — Innovayse Backend

## Setup

```bash
# Run formatter
dotnet format

# Check without applying (CI)
dotnet format --verify-no-changes

# Format specific project
dotnet format src/Innovayse.API/Innovayse.API.csproj
```

## .editorconfig

The `.editorconfig` at solution root enforces all formatting. `dotnet format` reads it automatically.

Key settings enforced:
- Indentation: 4 spaces (no tabs)
- `var` usage: prefer explicit types for builtins, `var` for others
- `using` directives: inside namespace, sorted, system-first
- Trailing whitespace: removed
- Final newline: required
- Line endings: LF (Unix)
- Max line length: 120

## CI Integration

Add to GitHub Actions / pipeline:
```yaml
- name: Check formatting
  run: dotnet format --verify-no-changes
```

## IDE Setup

- Rider: respects `.editorconfig` automatically
- VS Code: install `C# Dev Kit` + enable `editor.formatOnSave`
- Visual Studio: Tools → Options → Text Editor → C# → Code Style → use .editorconfig

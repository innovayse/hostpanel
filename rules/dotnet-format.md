# dotnet format rules for the HostPanel backend

## Setup

```bash
# Run formatter
dotnet format

# Check without applying (CI)
dotnet format --verify-no-changes

# Format specific project
dotnet format src/Innovayse.Hostpanel.API/Innovayse.Hostpanel.API.csproj
```

## .editorconfig

The `.editorconfig` at solution root enforces all formatting. `dotnet format` reads it automatically.

Key settings enforced:
- Indentation: 4 spaces (no tabs)
- `var` usage: prefer explicit types for builtins, `var` for others
- `using` directives: outside the namespace, sorted, system-first
- Trailing whitespace: removed
- Final newline: required
- Line endings: LF (Unix)
- Max line length: 120

## Style rules in the build

The `.editorconfig` rules are enforced by the build itself, not only by `dotnet format`:

- `StyleCop.Analyzers` is referenced for every project from `backend/Directory.Build.props` (version in
  `Directory.Packages.props`), and its settings live in `backend/stylecop.json`. `EnforceCodeStyleInBuild` is on, so the
  IDE rules of `.editorconfig` run in `dotnet build` too.
- **Which rule fails the build.** A rule set to `none` does not run, and a `suggestion` or `silent` rule only shows in the
  editor. Every other enabled rule fails the build in `src` and `tests`, whether `.editorconfig` sets it to `error` or leaves it
  at the analyzer's default `warning`: `TreatWarningsAsErrors` is on in `src/Directory.Build.props` and
  `tests/Directory.Build.props`, and it turns a warning into an error. So there is no group of SA rules that merely warn; to
  let a rule report without failing, set it to `suggestion`, and say why in the file.
- A new member without its XML documentation (`<summary>`, every `<param>`, `<returns>`, `<typeparam>`) fails the
  build, in `src` and in `tests` alike. A `Task`-returning member still needs a `<returns>`.
- Rules switched off on purpose, each with its reason in `.editorconfig`: SA1310 only in
  `Common/Errors/HostpanelErrorCodes.cs` (the error codes are UPPER_SNAKE_CASE by contract, they are the wire value clients
  branch on), and the ones the file already lists (file headers, `this.` prefix, ordering of members, and so on).
- Fix a report in the code; do not add `#pragma warning disable`, `NoWarn` or `SuppressMessage`.
- The `Hostpanel.Sdk` submodule keeps its own conventions: `backend/Directory.Build.props` leaves it out of the analyzers.

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

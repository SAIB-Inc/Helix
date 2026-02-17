# Contributing to Helix

## Branch Workflow

1. **Always work in a PR branch** — never commit directly to `main`.
2. Create a feature branch from `main`:
   ```bash
   git checkout main
   git pull origin main
   git checkout -b <type>/<short-description>
   ```
3. Push your branch and open a Pull Request.
4. All changes must be reviewed and merged via PR.

## Commit Convention

We use [Conventional Commits](https://www.conventionalcommits.org/).

Format:
```
<type>(<scope>): <description>

[optional body]

[optional footer(s)]
```

### Types

| Type | Description |
|------|-------------|
| `feat` | A new feature |
| `fix` | A bug fix |
| `docs` | Documentation only changes |
| `style` | Code style changes (formatting, no logic change) |
| `refactor` | Code change that neither fixes a bug nor adds a feature |
| `perf` | Performance improvement |
| `test` | Adding or updating tests |
| `build` | Changes to build system or dependencies |
| `ci` | CI/CD configuration changes |
| `chore` | Other changes that don't modify src or test files |

### Examples

```
feat(mail): add list-mail-messages tool
fix(auth): handle token refresh on 401 response
docs: update README with setup instructions
refactor(core): extract pagination into helper
test(calendar): add unit tests for calendar tools
build: add Microsoft.Graph NuGet package
```

## Design Principles

These principles apply to all MCP tool implementations in Helix.

### 1. Files go through the filesystem, never through the context

Large binary content (attachments, documents, images) must **never** be passed inline as tool parameters or return values. This consumes LLM context and can exceed token limits.

- **Downloading:** Save files to disk (e.g. `/tmp/helix-attachments/`), return the file path.
- **Uploading:** Accept a file path on disk, read the bytes internally.
- **Never** accept or return base64-encoded file content as a tool parameter.

### 2. Lean responses by default

Tool responses must stay small enough for LLM context windows.

- **List operations:** Use a default `$select` with only essential fields (id, subject, from, date, preview). Never return full HTML bodies by default.
- **Mutations:** Return `{ "success": true }` — not the full mutated object.
- **Errors:** Return structured `{ "error": true, "code": "...", "message": "..." }` — not stack traces.
- **Enums:** Serialize as camelCase strings (`"normal"`), not integers (`1`).

### 3. Descriptive tool metadata

Tool descriptions guide the LLM agent. They must be accurate and actionable.

- Document required formats (e.g. KQL search must be double-quoted).
- Warn about dangerous assumptions (e.g. "Never guess email addresses").
- Reference related tools (e.g. "Use 'list-mail-folders' to get folder IDs").
- Mark read-only tools with `ReadOnly = true`.

### 4. Graceful error handling

All Graph API tools must catch `ODataError` and return meaningful error messages instead of letting exceptions propagate as generic "An error occurred" messages.

### 5. Thread safety for shared state

The MCP SDK creates new tool instances per invocation. Any state shared across invocations (e.g. `LoginSession`) must use static fields with proper synchronization (e.g. `Lock`) to support both stdio and HTTP transports.

---

## Merging

- **Always squash merge** PRs into `main`.
- The squash commit message must follow conventional commit format.
- **Always delete the branch** after merging.

## Summary

```
main (protected)
  └── feat/my-feature (PR branch)
        ├── feat(scope): commit 1
        ├── fix(scope): commit 2
        └── squash merge → main → delete branch
```

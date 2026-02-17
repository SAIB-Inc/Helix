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

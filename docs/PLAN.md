# Helix: .NET 10 MCP Server for Microsoft 365

## Context

.NET 10 MCP server for Microsoft 365 using the official `ModelContextProtocol` C# SDK + `Microsoft.Graph` SDK v5. Porting from the TypeScript reference (`/tmp/ms-365-mcp-server`, ~92 tools).

---

## Solution Structure

```
Helix.slnx
global.json
Directory.Build.props
Directory.Packages.props
src/
  Helix.Core/         <- Auth, Graph wrapper, config, helpers
  Helix.Tools/        <- All MCP tool classes
  Helix.Stdio/        <- Console host (stdio transport)
  Helix.Http/         <- ASP.NET Core host (HTTP transport)
tests/
  Helix.Core.Tests/
  Helix.Tools.Tests/
```

---

## Phase 1: Scaffolding + Auth + First Tool ✅

- Solution structure, central package management, Roslyn analyzers, CI
- MSAL device code flow with persistent token cache (keyring/keychain/DPAPI)
- `get-current-user` tool over stdio and HTTP transports
- `login` / `login-status` / `logout` MCP tools (non-blocking two-step device code flow)
- `helix login` / `helix logout` CLI subcommands

---

## Phase 2: Mail (21 tools)

The most-used M365 feature. Requires `Mail.Read`, `Mail.ReadWrite`, `Mail.Send` permissions.

### 2a. Core helpers (build as needed)

- `Helix.Core/Helpers/PaginationHelper.cs` — follow `@odata.nextLink` up to configurable max pages

### 2b. Mail tools

**`Helix.Tools/Mail/MailTools.cs`** — core mailbox operations:
- `list-mail-messages` — list messages with $filter, $search (KQL), $select, $top, $orderby
- `get-mail-message` — get single message by ID
- `send-mail` — send email immediately
- `delete-mail-message` — delete by ID
- `move-mail-message` — move to folder
- `update-mail-message` — update properties (read status, categories, etc.)

**`Helix.Tools/Mail/MailFolderTools.cs`** — folder navigation:
- `list-mail-folders` — list all mail folders
- `list-mail-child-folders` — list child folders
- `list-mail-folder-messages` — list messages in a specific folder

**`Helix.Tools/Mail/MailDraftTools.cs`** — compose without sending:
- `create-draft-email` — create new draft
- `send-draft-message` — send an existing draft
- `create-reply-draft` — draft a reply
- `create-reply-all-draft` — draft a reply-all
- `create-forward-draft` — draft a forward

**`Helix.Tools/Mail/MailActionTools.cs`** — reply/forward (send immediately):
- `reply-mail-message` — reply to message
- `reply-all-mail-message` — reply-all
- `forward-mail-message` — forward to recipients

**`Helix.Tools/Mail/MailAttachmentTools.cs`** — attachment CRUD:
- `list-mail-attachments` — list attachments on a message
- `get-mail-attachment` — get specific attachment
- `add-mail-attachment` — add attachment to draft
- `delete-mail-attachment` — remove attachment

### Azure permissions needed
Add to app registration: `Mail.Read`, `Mail.ReadWrite`, `Mail.Send`

---

## Phase 3: Calendar (15 tools)

- `Helix.Tools/Calendar/CalendarTools.cs` — event CRUD, calendar views, instances
- `Helix.Tools/Calendar/MeetingTools.cs` — find-meeting-times

Timezone via `Prefer: outlook.timezone="..."`. Requires `Calendars.Read`, `Calendars.ReadWrite`.

---

## Phase 4: Files + OneDrive (8 tools)

- `Helix.Tools/Files/FileTools.cs` — drives, folders, download, upload, delete, search

Requires `Files.Read`, `Files.ReadWrite`.

---

## Phase 5: To Do + Contacts (10 tools)

- `Helix.Tools/Todo/TodoTools.cs` — task lists + tasks CRUD
- `Helix.Tools/Contacts/ContactTools.cs` — contacts CRUD

Requires `Tasks.Read`, `Tasks.ReadWrite`, `Contacts.Read`, `Contacts.ReadWrite`.

---

## Phase 6: Org-Mode Tools (33 tools)

Only registered when `OrgMode == true`. Requires admin/delegated permissions.

- `Helix.Tools/Users/UserTools.cs` — `list-users`, `get-user`
- `Helix.Tools/Mail/SharedMailboxTools.cs` — 4 shared mailbox tools
- `Helix.Tools/Teams/TeamTools.cs`, `ChatTools.cs`, `ChannelMessageTools.cs`
- `Helix.Tools/SharePoint/SharePointTools.cs`
- `Helix.Tools/Planner/PlannerTools.cs`
- `Helix.Tools/Groups/GroupTools.cs`
- `Helix.Tools/Search/SearchTools.cs` — `search-query`

---

## Phase 7: HTTP Transport with OAuth 2.1

Full spec-compliant HTTP transport:
- JWT Bearer validation with Microsoft Entra ID
- Per-request `GraphServiceClient` from Bearer token
- `ResourceMetadata` pointing to Entra ID as Authorization Server

---

## Phase 8: Infrastructure + Polish

- Read-only mode (`--read-only` suppresses write tools)
- Tool filtering (`--enabled-tools` regex pattern)
- Discovery mode (`search-tools` + `execute-tool`)
- Excel + OneNote tools
- Unit tests
- CLI args: `--read-only`, `--org-mode`, `--preset`, `--enabled-tools`, `--cloud`, `-v`

---

## Verification

1. `dotnet build` — compiles with 0 warnings
2. `helix login` — device code flow works, token cached
3. MCP server starts over stdio and HTTP
4. `get-current-user` → returns profile
5. `list-mail-messages` with `$top=5` → returns messages
6. `send-mail` → sends email
7. `dotnet test` — passes
8. CI green on PR

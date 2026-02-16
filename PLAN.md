# Helix: Port MS-365-MCP-Server to .NET 10

## Context

Porting the TypeScript MCP server (`/tmp/ms-365-mcp-server`, ~92 Microsoft Graph tools) to .NET 10 as **Helix**. Using the official `ModelContextProtocol` C# SDK + `Microsoft.Graph` SDK v5. Multi-project solution, spec-compliant auth.

**Key departure from source:** The source project's device-code-flow-as-MCP-tool is an anti-pattern per the MCP spec (2025-11-25). Helix follows the spec:
- **Stdio:** credentials from environment (env vars / `appsettings.json`). A separate `helix login` CLI command (outside MCP) handles device code flow and stores tokens.
- **HTTP:** Helix is an OAuth 2.1 Resource Server. Microsoft Entra ID is the Authorization Server. MCP client drives the OAuth flow, sends `Bearer` tokens.
- **No `login`/`logout` MCP tools.** Auth is not a tool concern.

---

## Solution Structure

```
Helix.slnx                          <- .NET 10 XML solution format
global.json
Directory.Build.props
Directory.Packages.props
src/
  Helix.Core/                       <- Auth, Graph wrapper, config, helpers
  Helix.Tools/                      <- All MCP tool classes
  Helix.Stdio/                      <- Console host (stdio transport)
  Helix.Http/                       <- ASP.NET Core host (HTTP transport)
tests/
  Helix.Core.Tests/
  Helix.Tools.Tests/
```

---

## Phase 1: Project + Auth + First Working Tool

Collapse scaffolding, auth, and first tool into one deliverable. At the end: a working MCP server with `get-current-user` responding over stdio.

### 1a. Solution scaffolding

**Create:** `Helix.slnx`, `global.json` (pin .NET 10), `Directory.Build.props` (net10.0, C# 14, nullable), `Directory.Packages.props` (CPM), all 6 `.csproj` files with references.

**NuGet packages:**
- `ModelContextProtocol` 0.8.0-preview.1, `ModelContextProtocol.AspNetCore`
- `Microsoft.Graph` v5, `Azure.Identity`, `Microsoft.Identity.Client`, `Microsoft.Identity.Client.Extensions.Msal`
- `Microsoft.Extensions.Hosting`
- `xunit` v3, `Moq`

### 1b. Auth -- spec-compliant

**Stdio auth** -- credentials from environment:
- `Helix.Core/Configuration/HelixOptions.cs` -- binds `Helix:*` config section (ClientId, TenantId, ClientSecret, CloudType, ReadOnly, OrgMode, EnabledToolsPattern)
- `Helix.Core/Configuration/CloudConfiguration.cs` -- global vs china endpoints
- `Helix.Core/Auth/TokenCredentialFactory.cs` -- creates the right `Azure.Identity` `TokenCredential` based on config:
  - If access token provided via env var -> wrap in a static `AccessTokenCredential`
  - If client secret -> `ClientSecretCredential`
  - If cached token from `helix login` -> `DeviceCodeCredential` with cached token via MSAL Extensions
- `Helix.Core/Auth/GraphClientFactory.cs` -- creates `GraphServiceClient` from credential + cloud endpoint
- `Helix.Core/Extensions/ServiceCollectionExtensions.cs` -- `AddHelixCore()` DI wiring

**CLI login command** (separate from MCP):
- `Helix.Stdio/Program.cs` handles `helix login` / `helix logout` as CLI subcommands (not MCP tools)
- Triggers device code flow via MSAL, stores token in persistent cache (`Microsoft.Identity.Client.Extensions.Msal`)
- `helix login` -> authenticate -> store token -> exit
- `helix serve` (or no subcommand) -> start MCP server using cached/env credentials

**HTTP auth** (Phase 1 just stubs it, full implementation in Phase 3):
- `Helix.Http/Program.cs` -- placeholder with `RequireAuthorization()` on `MapMcp()`

### 1c. First tool + MCP server bootstrap

- `Helix.Core/Helpers/GraphResponseHelper.cs` -- format Graph responses, strip `@odata.*`
- `Helix.Tools/Users/UserTools.cs` -- `get-current-user` tool
- `Helix.Stdio/Program.cs` -- full stdio host: `AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly()`

### Result
```bash
helix login          # device code flow, caches token
helix serve          # starts MCP stdio server
# or with env var:
HELIX_ACCESS_TOKEN=eyJ... helix serve
```
MCP Inspector connects, calls `get-current-user`, gets back user profile.

---

## Phase 2: Core Infrastructure + Users + Search

Build the shared tool infrastructure, then port the simplest services.

- `Helix.Core/Helpers/PaginationHelper.cs` -- follow `@odata.nextLink` up to 100 pages
- `Helix.Core/Helpers/ToolFilterService.cs` -- read-only mode + regex tool filtering
- `Helix.Tools/Users/UserTools.cs` -- add `list-users` (org-mode only)
- `Helix.Tools/Search/SearchTools.cs` -- `search-query`

**Result:** 3 tools work with OData params, pagination, response formatting, read-only filtering.

---

## Phase 3: HTTP Transport with OAuth 2.1

Full spec-compliant HTTP transport:

- `Helix.Http/Program.cs`:
  - `AddAuthentication()` with dual scheme: `McpAuthenticationDefaults` (challenge) + `JwtBearer` (validation)
  - `ResourceMetadata` pointing to Microsoft Entra ID as Authorization Server
  - `UseAuthentication()` + `UseAuthorization()` + `MapMcp().RequireAuthorization()`
  - Extract user identity from JWT claims, create per-request `GraphServiceClient` with on-behalf-of flow or token exchange

**Result:** HTTP transport works with proper OAuth 2.1 flow. MCP client authenticates via Entra ID, Helix validates Bearer token.

---

## Phase 4: Mail (20 tools)

- `Helix.Tools/Mail/MailTools.cs` -- list, get, send, delete, move, update messages + folders
- `Helix.Tools/Mail/MailAttachmentTools.cs` -- CRUD attachments
- `Helix.Tools/Mail/MailDraftTools.cs` -- drafts, reply, forward
- `Helix.Tools/Mail/SharedMailboxTools.cs` -- shared mailbox ops (org-mode)

---

## Phase 5: Calendar (15 tools)

- `Helix.Tools/Calendar/CalendarTools.cs` -- event CRUD, views, instances, calendars
- `Helix.Tools/Calendar/MeetingTools.cs` -- find-meeting-times (org-mode)

Timezone via `Prefer: outlook.timezone="..."`, extended properties via `$expand`.

---

## Phase 6: Files + Excel + OneNote (18 tools)

- `Helix.Tools/Files/FileTools.cs` -- drives, folders, download, upload, delete
- `Helix.Tools/Excel/ExcelTools.cs` -- worksheets, ranges, charts, format, sort
- `Helix.Tools/OneNote/OneNoteTools.cs` -- notebooks, sections, pages

---

## Phase 7: To Do + Planner + Contacts (18 tools)

- `Helix.Tools/Todo/TodoTools.cs` -- task lists + tasks CRUD
- `Helix.Tools/Planner/PlannerTools.cs` -- plans, tasks, task details (ETag headers)
- `Helix.Tools/Contacts/ContactTools.cs` -- contacts CRUD

---

## Phase 8: Teams + SharePoint + Groups (33 tools, org-mode)

- `Helix.Tools/Teams/TeamTools.cs`, `ChatTools.cs`, `ChannelMessageTools.cs`
- `Helix.Tools/SharePoint/SharePointTools.cs`
- `Helix.Tools/Groups/GroupTools.cs`

Conditional registration: only when `OrgMode == true`.

---

## Phase 9: Discovery Mode + CLI Polish + Tests

- `Helix.Tools/Discovery/DiscoveryTools.cs` -- `search-tools` + `execute-tool`
- `Helix.Core/Configuration/ToolCategories.cs` -- category presets
- CLI args: `--read-only`, `--org-mode`, `--preset`, `--enabled-tools`, `--discovery`, `--cloud`, `-v`
- Unit tests for all tool classes, auth, helpers

---

## Verification

1. `dotnet build` -- compiles
2. `helix login` -- device code flow works, token cached
3. `helix serve` -- starts stdio server
4. MCP Inspector -> `get-current-user` -> returns profile
5. `list-mail-messages` with `top=5` -> returns messages
6. `dotnet test` -- passes
7. HTTP transport with Bearer token -> tools work
8. `--read-only` -> write tools blocked
9. `--org-mode` -> Teams/SharePoint tools appear
10. `--discovery` -- only search-tools + execute-tool registered

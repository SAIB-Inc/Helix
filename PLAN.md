# Helix: Port MS-365-MCP-Server to .NET 10

## Context

The TypeScript project at `/tmp/ms-365-mcp-server` is an MCP server exposing ~92 Microsoft Graph API endpoints as tools. We're porting it to .NET 10 as **Helix** (`/home/rawriclark/Projects/Helix`) using the official `ModelContextProtocol` C# SDK + `Microsoft.Graph` SDK v5. The goal is a multi-project solution that preserves all tool names and functionality from the source.

---

## Solution Structure

```
Helix.sln
├── global.json / Directory.Build.props / Directory.Packages.props
├── src/
│   ├── Helix.Core/          — Auth, GraphClient wrapper, config, helpers
│   ├── Helix.Tools/         — All MCP tool classes (~20 files, one per service area)
│   ├── Helix.Stdio/         — Console host (stdio transport)
│   └── Helix.Http/          — ASP.NET Core host (HTTP/SSE transport)
└── tests/
    ├── Helix.Core.Tests/
    └── Helix.Tools.Tests/
```

---

## Phase 1: Solution Scaffolding

**Create:**
- `Helix.sln`, `global.json`, `Directory.Build.props` (net10.0, nullable, implicit usings), `Directory.Packages.props` (central package management)
- All 6 `.csproj` files with project references
- Placeholder `Program.cs` for both hosts, `appsettings.json`

**NuGet packages:** `Microsoft.Graph`, `Azure.Identity`, `Microsoft.Identity.Client`, `Microsoft.Identity.Client.Extensions.Msal`, `ModelContextProtocol` (0.8.0-preview.1), `ModelContextProtocol.AspNetCore`, `Microsoft.Extensions.Hosting`, `xunit`, `Moq`

**Result:** `dotnet build` succeeds.

---

## Phase 2: Authentication + GraphServiceClient

**Create in `Helix.Core/`:**
- `Configuration/HelixOptions.cs` — options class mapping `MS365_MCP_*` env vars
- `Configuration/CloudConfiguration.cs` — global vs china endpoints (port of `src/cloud-config.ts`)
- `Auth/IAuthManager.cs` — interface: `GetTokenAsync`, `AcquireTokenByDeviceCodeAsync`, `TestLoginAsync`, `LogoutAsync`, `ListAccountsAsync`, `SelectAccountAsync`, `RemoveAccountAsync`
- `Auth/AuthManager.cs` — MSAL.NET `PublicClientApplication` with device code flow (port of `src/auth.ts`)
- `Auth/TokenCacheHelper.cs` — persistent file cache via `Microsoft.Identity.Client.Extensions.Msal`
- `Auth/HelixTokenCredential.cs` — bridges `IAuthManager` to `Azure.Core.TokenCredential`
- `Auth/GraphClientFactory.cs` — creates `GraphServiceClient` using `HelixTokenCredential`
- `Extensions/ServiceCollectionExtensions.cs` — `AddHelixCore()` DI registration

**Key source files to port from:**
- `/tmp/ms-365-mcp-server/src/auth.ts` — device code flow, silent token, multi-account
- `/tmp/ms-365-mcp-server/src/cloud-config.ts` — cloud endpoints

**Result:** Can authenticate via device code and call `graphClient.Me.GetAsync()`.

---

## Phase 3: Auth Tools + MCP Server Bootstrap

**Create:**
- `Helix.Tools/Auth/AuthTools.cs` — 6 tools: `login`, `logout`, `verify-login`, `list-accounts`, `select-account`, `remove-account` (port of `src/auth-tools.ts`)
- `Helix.Stdio/Program.cs` — full stdio host: `AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly()`
- `Helix.Http/Program.cs` — full HTTP host: `AddMcpServer().WithHttpTransport()` + `app.MapMcp()`

**Result:** Both transports start, MCP Inspector can connect, auth tools work.

---

## Phase 4: Core Infrastructure + Users + Search (3 tools)

**Create in `Helix.Core/`:**
- `Helpers/GraphResponseHelper.cs` — format responses, strip `@odata.*` props (port of `graph-client.ts:formatJsonResponse`)
- `Helpers/PaginationHelper.cs` — follow `@odata.nextLink` up to 100 pages (port of `graph-tools.ts` pagination logic)
- `Helpers/ToolFilterService.cs` — read-only mode + regex tool filtering

**Create in `Helix.Tools/`:**
- `Users/UserTools.cs` — `get-current-user` (`Me.GetAsync()`), `list-users` (`Users.GetAsync()`)
- `Search/SearchTools.cs` — `search-query` (`Search.Query.PostAsync()`)

**All tools follow this pattern:**
```csharp
[McpServerToolType]
public class UserTools(GraphServiceClient graphClient)
{
    [McpServerTool(Name = "get-current-user", ReadOnly = true),
     Description("Get the currently authenticated user's profile")]
    public async Task<string> GetCurrentUser() { ... }
}
```

**Result:** 3 tools work end-to-end with OData params, pagination, response formatting.

---

## Phase 5: Mail (20 tools)

**Create in `Helix.Tools/Mail/`:**
- `MailTools.cs` — `list-mail-messages`, `list-mail-folders`, `list-mail-child-folders`, `list-mail-folder-messages`, `get-mail-message`, `send-mail`, `delete-mail-message`, `move-mail-message`, `update-mail-message`
- `MailAttachmentTools.cs` — `list-mail-attachments`, `get-mail-attachment`, `add-mail-attachment`, `delete-mail-attachment`
- `MailDraftTools.cs` — `create-draft-email`, `forward-mail-message`, `reply-mail-message`, `reply-all-mail-message`, `create-forward-draft`, `create-reply-draft`, `create-reply-all-draft`, `send-draft-message`
- `SharedMailboxTools.cs` (org-mode) — `list-shared-mailbox-messages`, `list-shared-mailbox-folder-messages`, `get-shared-mailbox-message`, `send-shared-mailbox-mail`

**Graph SDK calls:** `Me.Messages.GetAsync()`, `Me.SendMail.PostAsync()`, `Me.Messages[id].Reply.PostAsync()`, etc.

---

## Phase 6: Calendar (15 tools)

**Create in `Helix.Tools/Calendar/`:**
- `CalendarTools.cs` — all event CRUD, calendar views, instances, calendars list (14 tools)
- `MeetingTools.cs` (org-mode) — `find-meeting-times`

**Special handling:** timezone via `Prefer: outlook.timezone="..."` header, `expandExtendedProperties` via `$expand=singleValueExtendedProperties`, `calendarView` requires `startDateTime`+`endDateTime`.

---

## Phase 7: Files + Excel + OneNote (18 tools)

**Create in `Helix.Tools/`:**
- `Files/FileTools.cs` — 7 tools: drives, folders, download (via `@microsoft.graph.downloadUrl`), upload, delete
- `Excel/ExcelTools.cs` — 5 tools: worksheets, ranges, charts, format, sort
- `OneNote/OneNoteTools.cs` — 6 tools: notebooks, sections, pages (create uses `text/html`)

---

## Phase 8: To Do + Planner + Contacts (18 tools)

**Create in `Helix.Tools/`:**
- `Todo/TodoTools.cs` — 6 tools: task lists and tasks CRUD
- `Planner/PlannerTools.cs` — 7 tools: plans, tasks, task details (updates need `If-Match` ETag header)
- `Contacts/ContactTools.cs` — 5 tools: contacts CRUD

---

## Phase 9: Teams + SharePoint + Groups (33 tools, org-mode only)

**Create in `Helix.Tools/`:**
- `Teams/TeamTools.cs` — teams, channels, members (6 tools)
- `Teams/ChatTools.cs` — chats and messages (7 tools)
- `Teams/ChannelMessageTools.cs` — channel messages and replies (5 tools)
- `SharePoint/SharePointTools.cs` — sites, drives, lists, items (12 tools)
- `Groups/GroupTools.cs` — conversations, threads, replies (3 tools)

**Conditional registration:** only when `HelixOptions.OrgMode == true`.

---

## Phase 10: Discovery Mode + CLI + Tests

**Create:**
- `Helix.Tools/Discovery/DiscoveryTools.cs` — `search-tools` + `execute-tool` meta-tools
- `Helix.Core/Configuration/ToolCategories.cs` — category presets (port of `src/tool-categories.ts`)
- Full CLI arg parsing: `--read-only`, `--org-mode`, `--preset`, `--enabled-tools`, `--discovery`, `--cloud`, `-v`
- Unit tests for auth, response formatting, tool filtering, and each tool class

---

## Verification

1. `dotnet build` — all projects compile
2. `dotnet run --project src/Helix.Stdio -- --login` — device code flow works
3. Connect MCP Inspector to stdio transport — all tools listed
4. Invoke `get-current-user` — returns user profile
5. Invoke `list-mail-messages` with `top=5` — returns messages
6. Run `dotnet test` — all tests pass
7. Test HTTP transport: `dotnet run --project src/Helix.Http` + connect via MCP Inspector
8. Test `--read-only` — write tools return error
9. Test `--org-mode` — Teams/SharePoint tools appear
10. Test `--discovery` — only `search-tools` and `execute-tool` registered

---

## Key Source Files Reference

| Source file | What to port |
|---|---|
| `/tmp/ms-365-mcp-server/src/endpoints.json` | Tool names, descriptions, scopes, llmTips |
| `/tmp/ms-365-mcp-server/src/auth.ts` | Device code flow, token cache, multi-account |
| `/tmp/ms-365-mcp-server/src/graph-tools.ts` | Tool registration, OData params, pagination, response formatting |
| `/tmp/ms-365-mcp-server/src/graph-client.ts` | Response formatting, OData stripping, error handling |
| `/tmp/ms-365-mcp-server/src/tool-categories.ts` | Category presets and patterns |
| `/tmp/ms-365-mcp-server/src/cloud-config.ts` | Cloud endpoints (global/china) |
| `/tmp/ms-365-mcp-server/src/auth-tools.ts` | Auth tool implementations |

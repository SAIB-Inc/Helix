# Helix

**An open-source Model Context Protocol (MCP) server for Microsoft 365, built with .NET 10.**

[![CI](https://github.com/SAIB-Inc/Helix/actions/workflows/ci.yml/badge.svg)](https://github.com/SAIB-Inc/Helix/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10-purple.svg)](https://dotnet.microsoft.com/)
[![MCP](https://img.shields.io/badge/MCP-Compatible-blue.svg)](https://modelcontextprotocol.io/)

---

## Overview

**Helix** is an MCP server that bridges AI agents with Microsoft 365. It exposes M365 services as MCP tools so AI models like Claude can read your email, send messages, manage attachments, and more — all through the standardized [Model Context Protocol](https://modelcontextprotocol.io/).

Built by [SAIB Inc](https://github.com/saib-inc).

## Install

### One-line installer (macOS & Linux)

```bash
curl -fsSL https://raw.githubusercontent.com/SAIB-Inc/Helix/main/install.sh | bash
```

This downloads a self-contained binary to `~/.helix/bin/helix` — no .NET SDK required.

To install a specific version:

```bash
HELIX_VERSION=0.1.0 curl -fsSL https://raw.githubusercontent.com/SAIB-Inc/Helix/main/install.sh | bash
```

### Verify

```bash
helix --version
```

## Prerequisites

You need an Azure AD app registration to authenticate with Microsoft 365.

1. Go to [Azure Portal > App registrations](https://portal.azure.com/#view/Microsoft_AAD_RegisteredApps/ApplicationsListBlade) and create a new registration
2. Set **Supported account types** to your preference (single tenant or multi-tenant)
3. Under **Authentication**, enable **Allow public client flows** (required for device code auth)
4. Under **API permissions**, add the following **delegated** permissions:
   - `User.Read`
   - `Mail.Read`
   - `Mail.ReadWrite`
   - `Mail.Send`
5. Note your **Application (client) ID** and **Directory (tenant) ID**

## Configure

Set your Azure app credentials via environment variables:

```bash
export HELIX__ClientId="your-client-id"
export HELIX__TenantId="your-tenant-id"   # optional, defaults to "common"
```

Or create a `.env` file in your working directory:

```env
HELIX__ClientId=your-client-id
HELIX__TenantId=your-tenant-id
```

## Usage with Claude Desktop

Add to your `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "helix": {
      "command": "helix",
      "env": {
        "HELIX__ClientId": "your-client-id",
        "HELIX__TenantId": "your-tenant-id"
      }
    }
  }
}
```

> If `helix` is not on your PATH, use the full path: `~/.helix/bin/helix`

## Available Tools

### Authentication

| Tool | Description |
|------|-------------|
| `login` | Start device code authentication flow |
| `login-status` | Check if authentication is complete |
| `logout` | Sign out and clear cached tokens |
| `get-current-user` | Get the authenticated user's profile |

### Mail

| Tool | Description |
|------|-------------|
| `list-mail-messages` | List messages with filtering, search (KQL), and paging |
| `get-mail-message` | Get a full message by ID |
| `send-mail` | Send an email |
| `delete-mail-message` | Delete a message (moves to Deleted Items) |
| `move-mail-message` | Move a message to a different folder |
| `update-mail-message` | Update read status, importance, categories, or subject |

### Mail Attachments

| Tool | Description |
|------|-------------|
| `list-mail-attachments` | List attachments on a message |
| `get-mail-attachment` | Download an attachment to disk |
| `add-mail-attachment` | Attach a file from disk to a draft |
| `delete-mail-attachment` | Remove an attachment |

### Mail Folders

| Tool | Description |
|------|-------------|
| `list-mail-folders` | List all mail folders with unread counts |
| `list-mail-folder-messages` | List messages in a specific folder |

## Roadmap

| Service | Status |
|---------|--------|
| Mail | **Available** |
| Calendar | Planned |
| Contacts | Planned |
| OneDrive | Planned |
| Teams | Planned |
| Planner / To Do | Planned |

## Build from Source

If you prefer to build from source instead of using the installer:

```bash
git clone https://github.com/SAIB-Inc/Helix.git
cd Helix
dotnet build
dotnet run --project src/Helix.Stdio
```

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

## Contributing

See [docs/CONTRIBUTING.md](docs/CONTRIBUTING.md) for branch workflow, commit conventions, and design principles.

## License

MIT — see [LICENSE](LICENSE).

---

Built and maintained by **[SAIB Inc](https://github.com/saib-inc)**.

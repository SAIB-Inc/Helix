# Project Helix

**An open-source Model Context Protocol (MCP) server for Microsoft 365, built with .NET 10.**

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10-purple.svg)](https://dotnet.microsoft.com/)
[![MCP](https://img.shields.io/badge/MCP-Compatible-blue.svg)](https://modelcontextprotocol.io/)

---

## Overview

**Helix** is an MCP server that bridges AI agents and assistants with the Microsoft 365 ecosystem. It exposes Microsoft 365 services as MCP tools, enabling AI models to interact with Outlook, Teams, OneDrive, SharePoint, Calendar, Planner, and more through a standardized protocol.

Built by [SAIB Inc (Softwarez at its Best Incorporated)](https://github.com/saib-inc).

## Features

- **Full Microsoft 365 Coverage** - Access the complete M365 suite through MCP tools
- **Dual Transport Support** - Works over both **Stdio** and **SSE** (Server-Sent Events)
- **Built on .NET 10** - Modern, high-performance, cross-platform runtime
- **Microsoft Graph API** - Powered by the Microsoft Graph API for unified M365 access
- **Open Source** - MIT licensed and community-driven

## Supported Services

| Service | Status |
|---------|--------|
| Outlook (Mail) | Planned |
| Calendar | Planned |
| Teams | Planned |
| OneDrive | Planned |
| SharePoint | Planned |
| Planner | Planned |
| Contacts | Planned |
| To Do | Planned |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- A Microsoft 365 account
- An Azure AD app registration with appropriate Microsoft Graph permissions

## Getting Started

### Clone the repository

```bash
git clone https://github.com/saib-inc/Helix.git
cd Helix
```

### Build

```bash
dotnet build
```

### Configure

Create an `appsettings.json` or set environment variables for your Azure AD app registration:

```json
{
  "AzureAd": {
    "TenantId": "your-tenant-id",
    "ClientId": "your-client-id",
    "ClientSecret": "your-client-secret"
  }
}
```

### Run

**Stdio transport:**

```bash
dotnet run -- --transport stdio
```

**SSE transport:**

```bash
dotnet run -- --transport sse
```

## MCP Client Configuration

### Claude Desktop

Add the following to your Claude Desktop configuration:

```json
{
  "mcpServers": {
    "helix": {
      "command": "dotnet",
      "args": ["run", "--project", "/path/to/Helix"],
      "env": {
        "AZURE_TENANT_ID": "your-tenant-id",
        "AZURE_CLIENT_ID": "your-client-id",
        "AZURE_CLIENT_SECRET": "your-client-secret"
      }
    }
  }
}
```

## Contributing

We welcome contributions from the community! Please see [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/my-feature`)
3. Commit your changes (`git commit -m 'Add my feature'`)
4. Push to the branch (`git push origin feature/my-feature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

Built and maintained by **SAIB Inc (Softwarez at its Best Incorporated)**.

---

> *Helix - Intertwining AI and Microsoft 365.*

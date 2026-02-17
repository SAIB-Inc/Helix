#!/usr/bin/env bash
set -euo pipefail

# Helix Installer
# Usage: curl -fsSL https://raw.githubusercontent.com/SAIB-Inc/Helix/main/install.sh | bash
#
# Environment variables:
#   HELIX_VERSION     - Specific version to install (e.g. "0.1.0" or "v0.1.0"). Defaults to latest.
#   HELIX_INSTALL_DIR - Custom install directory. Defaults to ~/.helix/bin.

REPO="SAIB-Inc/Helix"
INSTALL_DIR="${HELIX_INSTALL_DIR:-$HOME/.helix/bin}"
BINARY_NAME="helix"

# --- Helpers ---

info() { printf '%s\n' "$@"; }
error() { printf 'Error: %s\n' "$@" >&2; exit 1; }

detect_fetcher() {
    if command -v curl >/dev/null 2>&1; then
        FETCH="curl"
    elif command -v wget >/dev/null 2>&1; then
        FETCH="wget"
    else
        error "Neither curl nor wget found. Please install one of them."
    fi
}

fetch() {
    if [ "$FETCH" = "curl" ]; then
        curl -fsSL "$1"
    else
        wget -qO- "$1"
    fi
}

fetch_to_file() {
    if [ "$FETCH" = "curl" ]; then
        curl -fsSL -o "$2" "$1"
    else
        wget -q -O "$2" "$1"
    fi
}

# --- Platform detection ---

detect_platform() {
    local os arch
    os="$(uname -s)"
    arch="$(uname -m)"

    case "$os" in
        Linux)  OS="linux" ;;
        Darwin) OS="osx" ;;
        *)      error "Unsupported operating system: $os" ;;
    esac

    case "$arch" in
        x86_64|amd64)   ARCH="x64" ;;
        aarch64|arm64)  ARCH="arm64" ;;
        *)              error "Unsupported architecture: $arch" ;;
    esac

    RID="${OS}-${ARCH}"
}

# --- Version resolution ---

resolve_version() {
    if [ -n "${HELIX_VERSION:-}" ]; then
        VERSION="$HELIX_VERSION"
        case "$VERSION" in
            v*) ;;
            *)  VERSION="v${VERSION}" ;;
        esac
    else
        info "Fetching latest release..."
        local api_url="https://api.github.com/repos/${REPO}/releases/latest"
        VERSION="$(fetch "$api_url" | grep '"tag_name"' | sed -E 's/.*"tag_name":[[:space:]]*"([^"]+)".*/\1/')"
        if [ -z "$VERSION" ]; then
            error "Could not determine latest version. Set HELIX_VERSION manually."
        fi
    fi
}

# --- Install ---

install() {
    local asset_name="helix-${RID}.tar.gz"
    local download_url="https://github.com/${REPO}/releases/download/${VERSION}/${asset_name}"
    local tmp_dir

    tmp_dir="$(mktemp -d)"
    trap 'rm -rf "$tmp_dir"' EXIT

    info "Downloading Helix ${VERSION} for ${RID}..."
    fetch_to_file "$download_url" "${tmp_dir}/${asset_name}"

    info "Extracting..."
    tar -xzf "${tmp_dir}/${asset_name}" -C "$tmp_dir"

    mkdir -p "$INSTALL_DIR"
    mv "${tmp_dir}/${BINARY_NAME}" "${INSTALL_DIR}/${BINARY_NAME}"
    chmod +x "${INSTALL_DIR}/${BINARY_NAME}"

    # macOS: remove quarantine attribute to avoid Gatekeeper prompt
    if [ "$OS" = "osx" ] && command -v xattr >/dev/null 2>&1; then
        xattr -d com.apple.quarantine "${INSTALL_DIR}/${BINARY_NAME}" 2>/dev/null || true
    fi

    info "Installed helix to ${INSTALL_DIR}/${BINARY_NAME}"
}

# --- PATH ---

configure_path() {
    case ":${PATH}:" in
        *:"${INSTALL_DIR}":*) return ;;
    esac

    local shell_name profile_file
    shell_name="$(basename "${SHELL:-/bin/bash}")"

    case "$shell_name" in
        zsh)  profile_file="$HOME/.zshrc" ;;
        bash)
            if [ "$(uname -s)" = "Darwin" ] && [ -f "$HOME/.bash_profile" ]; then
                profile_file="$HOME/.bash_profile"
            else
                profile_file="$HOME/.bashrc"
            fi
            ;;
        *)    profile_file="$HOME/.profile" ;;
    esac

    if [ -f "$profile_file" ] && grep -qF "$INSTALL_DIR" "$profile_file" 2>/dev/null; then
        return
    fi

    printf '\n# Helix MCP Server\nexport PATH="%s:$PATH"\n' "$INSTALL_DIR" >> "$profile_file"
    info "Added ${INSTALL_DIR} to PATH in ${profile_file}"
    info ""
    info "Restart your shell or run:"
    info "  export PATH=\"${INSTALL_DIR}:\$PATH\""
}

# --- Main ---

main() {
    info ""
    info "Helix Installer"
    info "==============="
    info ""

    detect_fetcher
    detect_platform
    resolve_version
    install
    configure_path

    info ""
    info "Done! Helix ${VERSION} has been installed."
    info ""
    info "Verify:"
    info "  helix --version"
    info ""
    info "Configure for Claude Desktop (claude_desktop_config.json):"
    info "  {"
    info "    \"mcpServers\": {"
    info "      \"helix\": {"
    info "        \"command\": \"${INSTALL_DIR}/helix\","
    info "        \"env\": {"
    info "          \"HELIX__ClientId\": \"your-client-id\""
    info "        }"
    info "      }"
    info "    }"
    info "  }"
    info ""
}

main

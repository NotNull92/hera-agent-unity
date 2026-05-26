#!/bin/sh
set -e

REPO="NotNull92/hera-agent"

OS="$(uname -s | tr '[:upper:]' '[:lower:]')"
case "$OS" in
  linux)  ;;
  darwin) ;;
  *)      echo "Unsupported OS: $OS (use Windows instructions in README)"; exit 1 ;;
esac

ARCH="$(uname -m)"
case "$ARCH" in
  x86_64|amd64)  ARCH="amd64" ;;
  aarch64|arm64)  ARCH="arm64" ;;
  *)              echo "Unsupported architecture: $ARCH"; exit 1 ;;
esac

INSTALL_DIR="$HOME/.local/bin"
mkdir -p "$INSTALL_DIR"

# Old Money banner (Antique Gold). Hidden when stdout isn't a TTY
# or when NO_COLOR is set, so log captures and CI runs stay clean.
if [ -t 1 ] && [ -z "${NO_COLOR:-}" ]; then
  GOLD=$(printf '\033[1;38;2;201;162;39m')
  RESET=$(printf '\033[0m')
else
  GOLD=""
  RESET=""
fi

echo ""
printf '%s\n' "${GOLD}  ╦ ╦ ╔═╗ ╔═╗ ╔═╗   ╔═╗ ╔═╗ ╔═╗ ╔╗╔ ╔╦╗   ╦   ╔╦╗ ╔╦╗ ╔═╗${RESET}"
printf '%s\n' "${GOLD}  ╠═╣ ║╣  ╠╦╝ ╠═╣   ╠═╣ ║ ╦ ║╣  ║║║  ║    ║    ║   ║  ║╣ ${RESET}"
printf '%s\n' "${GOLD}  ╩ ╩ ╚═╝ ╩╚═ ╩ ╩   ╩ ╩ ╚═╝ ╚═╝ ╝╚╝  ╩    ╚══ ╚╩╝  ╩  ╚═╝${RESET}"
echo ""

URL="https://github.com/${REPO}/releases/latest/download/hera-agent-${OS}-${ARCH}"

echo "Downloading hera-agent for ${OS}/${ARCH}..."
curl -fsSL "$URL" -o "$INSTALL_DIR/hera-agent"
chmod +x "$INSTALL_DIR/hera-agent"

case ":$PATH:" in
  *":$INSTALL_DIR:"*) ;;
  *)
    export PATH="$INSTALL_DIR:$PATH"
    LINE="export PATH=\"$INSTALL_DIR:\$PATH\""
    SHELL_NAME="$(basename "$SHELL")"
    case "$SHELL_NAME" in
      zsh)  RC_FILE="$HOME/.zshrc" ;;
      bash) RC_FILE="$HOME/.bashrc" ;;
      *)    RC_FILE="$HOME/.profile" ;;
    esac
    touch "$RC_FILE"
    echo "$LINE" >> "$RC_FILE"
    echo "Added $INSTALL_DIR to PATH (restart shell to apply)" ;;
esac

echo "Installed hera-agent to $INSTALL_DIR/hera-agent"
echo
echo "Next, get your AI agent to use it:"
echo "  - Discover: ask Claude Code CLI or Codex in any terminal:"
echo '      "Check whether the hera-agent CLI tool is installed and explore its capabilities."'
echo "  - Lock in (recommended): add to your project's CLAUDE.md / AGENTS.md:"
echo '      "For any Unity work, always use hera-agent."'
echo
"$INSTALL_DIR/hera-agent" version

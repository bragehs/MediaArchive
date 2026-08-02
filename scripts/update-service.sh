#!/bin/zsh
# Rebuild the app and restart the always-on background service, so the
# MediaArchive web app reflects your latest code.
#
# The service (a launchd agent, ~/Library/LaunchAgents/no.norapps.mediaarchive.server.plist)
# runs the built Release DLL, so a source change needs a rebuild + restart.
set -e
cd "$(dirname "$0")/.."
echo "→ building Release…"
/usr/local/share/dotnet/dotnet build -c Release --nologo -v q
echo "→ restarting service…"
launchctl kickstart -k "gui/$(id -u)/no.norapps.mediaarchive.server"
echo "✓ MediaArchive service updated — refresh the web app window."

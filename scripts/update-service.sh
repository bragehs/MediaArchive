#!/bin/zsh
# Restart the always-on MediaArchive service so it picks up your latest code.
#
# The service (launchd agent no.norapps.mediaarchive.server) runs `dotnet run`,
# which rebuilds on start — so a graceful stop+start is all that's needed.
# NOTE: avoid `launchctl kickstart -k`; it SIGKILLs the wrapper and orphans the
# child server, which then squats on the port. bootout sends SIGTERM, which
# `dotnet run` forwards to the server for a clean shutdown.
set -e
LABEL="no.norapps.mediaarchive.server"
PLIST="$HOME/Library/LaunchAgents/$LABEL.plist"

echo "→ restarting service (rebuilds on start)…"
launchctl bootout "gui/$(id -u)/$LABEL" 2>/dev/null || true
# bootout is async — wait until it's fully unloaded, or bootstrap races and
# fails with "Input/output error".
for i in {1..15}; do
    launchctl print "gui/$(id -u)/$LABEL" >/dev/null 2>&1 || break
    sleep 1
done
launchctl bootstrap "gui/$(id -u)" "$PLIST"

for i in {1..90}; do
    [ "$(curl -s -o /dev/null -w '%{http_code}' http://localhost:5064/ 2>/dev/null)" = "200" ] && {
        echo "✓ up at http://localhost:5064 — refresh the web-app window (⌘R)"
        exit 0
    }
    sleep 2
done
echo "! didn't come up in time — check /tmp/mediaarchive.log"
exit 1

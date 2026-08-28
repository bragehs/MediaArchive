#!/usr/bin/env zsh
#
# install-weekly-job.sh — schedule `./ma weekly` for Tuesdays at 10:00.
#
# Backs the phone's database up and reinstalls the app, so the 7-day free
# signature never lapses into an app that won't open. Both halves need the
# phone reachable, so `ma weekly` retries for two hours before giving up.
#
# launchd, not cron: if the Mac is asleep at 10:00, StartCalendarInterval fires
# on the next wake instead of silently skipping the week.
#
#   scripts/install-weekly-job.sh            install / replace
#   scripts/install-weekly-job.sh --remove   uninstall
#   scripts/install-weekly-job.sh --run-now  trigger a run immediately

set -euo pipefail

REPO="${0:A:h:h}"
LABEL="no.norapps.mediaarchive.weekly"
PLIST="$HOME/Library/LaunchAgents/$LABEL.plist"
LOG="$HOME/Library/Logs/mediaarchive-weekly.log"
DOMAIN="gui/$(id -u)"

case "${1:-}" in
    --remove)
        launchctl bootout "$DOMAIN/$LABEL" 2>/dev/null || true
        rm -f "$PLIST"
        print "removed $LABEL"
        exit 0
        ;;
    --run-now)
        launchctl kickstart -p "$DOMAIN/$LABEL"
        print "triggered — follow along with: tail -f $LOG"
        exit 0
        ;;
esac

mkdir -p "$HOME/Library/LaunchAgents" "$HOME/Library/Logs"

# launchd gives an agent almost no PATH, and `dotnet` lives outside the default.
cat > "$PLIST" <<PLISTEOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>Label</key>
    <string>$LABEL</string>

    <!-- Invoke zsh explicitly rather than relying on the shebang: ~/Documents is
         TCC-protected, and the Full Disk Access grant has to name a real binary.
         With this, that binary is unambiguously /bin/zsh. -->
    <key>ProgramArguments</key>
    <array>
        <string>/bin/zsh</string>
        <string>$REPO/ma</string>
        <string>weekly</string>
    </array>

    <key>WorkingDirectory</key>
    <string>$REPO</string>

    <key>EnvironmentVariables</key>
    <dict>
        <key>PATH</key>
        <string>/usr/local/share/dotnet:/usr/bin:/bin:/usr/sbin:/sbin:/usr/local/bin</string>
        <key>DOTNET_CLI_TELEMETRY_OPTOUT</key>
        <string>1</string>
    </dict>

    <!-- Tuesday = 2. Missed while asleep -> runs on wake. -->
    <key>StartCalendarInterval</key>
    <dict>
        <key>Weekday</key><integer>2</integer>
        <key>Hour</key><integer>10</integer>
        <key>Minute</key><integer>0</integer>
    </dict>

    <key>StandardOutPath</key>
    <string>$LOG</string>
    <key>StandardErrorPath</key>
    <string>$LOG</string>

    <!-- The run polls for the phone for up to 2h; don't let launchd kill it. -->
    <key>ExitTimeOut</key>
    <integer>0</integer>
    <key>ProcessType</key>
    <string>Background</string>
</dict>
</plist>
PLISTEOF

# bootout is async; wait for it to clear or bootstrap races with "Input/output error".
launchctl bootout "$DOMAIN/$LABEL" 2>/dev/null || true
for i in {1..15}; do
    launchctl print "$DOMAIN/$LABEL" >/dev/null 2>&1 || break
    sleep 1
done

launchctl bootstrap "$DOMAIN" "$PLIST"
print "installed $LABEL — Tuesdays at 10:00"
print "  log:       $LOG"
print "  run now:   scripts/install-weekly-job.sh --run-now"
print "  uninstall: scripts/install-weekly-job.sh --remove"

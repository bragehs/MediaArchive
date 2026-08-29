#!/usr/bin/env zsh
#
# ma — build and run the MediaArchive iOS app without opening Rider.
#
#   ./ma                    run on the simulator (default)
#   ./ma sim "iPhone Air"   run on a named simulator
#   ./ma phone              renew signing, then build + install + launch on the iPhone
#   ./ma renew              only refresh the provisioning profile
#   ./ma renew --force      refresh even if the current profile is still good
#   ./ma pull               copy the phone's database + covers back into the repo
#   ./ma weekly             pull + redeploy, retrying until the phone appears
#
# Why `renew` exists
# ------------------
# A free Apple ID ("Personal Team") only gets 7-day provisioning profiles, so
# device builds break every week with "Could not find any available provisioning
# profiles". Xcode reissues one non-interactively via -allowProvisioningUpdates,
# but it insists on an .xcodeproj — which a MAUI project never produces. So we
# generate a throwaway Xcode target whose only job is to carry the bundle id and
# trigger the reissue. The .NET build then finds the fresh profile on its own:
# it reads the same ~/Library/Developer/Xcode/UserData/Provisioning Profiles
# that Xcode writes to, so nothing needs copying.
#
# Exit codes: 0 ok, 1 usage, 2 build/deploy failure.

set -euo pipefail

REPO="${0:A:h}"
PROJ="$REPO/MediaArchive.Mobile/MediaArchive.Mobile.csproj"
BUNDLE_ID="no.norapps.mediaarchive"
TEAM_ID="ZVNYY55W9L"
TFM="net10.0-ios"
SIM_DEFAULT="iPhone 17 Pro"
STUB="$REPO/.provisioning"
PROFILE_DIR="$HOME/Library/Developer/Xcode/UserData/Provisioning Profiles"

# The home-screen widget is a native Swift extension with its own bundle id
# (and thus its own 7-day profile). xcodebuild compiles it; the .NET build
# embeds the .appex via AdditionalAppExtensions in the csproj.
WIDGET_DIR="$REPO/widget"
WIDGET_BUNDLE_ID="no.norapps.mediaarchive.widget"
APP_GROUP="group.no.norapps.mediaarchive"

# Free profiles last 7 days; refresh once we're inside the last two.
RENEW_THRESHOLD_DAYS=2

say()  { print -P "%F{green}→%f $*" >&2; }
warn() { print -P "%F{yellow}!%f $*" >&2; }
die()  { print -P "%F{red}✗%f $*" >&2; exit "${2:-2}"; }

# macOS ships no coreutils `timeout`, and every devicectl call can block forever
# when the phone drops off mid-transfer — which is exactly what an unattended
# weekly job must not do. Exits 124 if the command outlives its budget.
run_timeout() {
    local secs=$1; shift
    perl -e '
        my $t = shift;
        my $pid = fork();
        die "fork failed: $!" unless defined $pid;
        if ($pid == 0) { exec @ARGV; exit 127 }
        $SIG{ALRM} = sub { kill "TERM", $pid; sleep 2; kill "KILL", $pid; exit 124 };
        alarm $t;
        waitpid($pid, 0);
        alarm 0;
        exit($? >> 8);
    ' "$secs" "$@"
}

# ---------------------------------------------------------------- provisioning

# Days until the given bundle's profile expires; -1 if there isn't a usable
# one. "Usable" now also means it carries the App Group entitlement — an older
# profile without it would make codesigning fail, so it counts as absent.
profile_days_left() {
    local bundle="${1:-$BUNDLE_ID}" tmp appid exp epoch now best=-1
    tmp=$(mktemp)
    for p in "$PROFILE_DIR"/*.mobileprovision(N); do
        security cms -D -i "$p" >"$tmp" 2>/dev/null || continue
        appid=$(/usr/libexec/PlistBuddy -c "Print :Entitlements:application-identifier" "$tmp" 2>/dev/null) || continue
        [[ "$appid" == "$TEAM_ID.$bundle" ]] || continue
        /usr/libexec/PlistBuddy -c "Print :Entitlements:com.apple.security.application-groups" "$tmp" >/dev/null 2>&1 || continue
        exp=$(/usr/libexec/PlistBuddy -c "Print :ExpirationDate" "$tmp" 2>/dev/null) || continue
        epoch=$(date -j -f "%a %b %d %H:%M:%S %Z %Y" "$exp" +%s 2>/dev/null) || continue
        now=$(date +%s)
        (( best = (epoch - now) / 86400 > best ? (epoch - now) / 86400 : best ))
    done
    rm -f "$tmp"
    print -- "$best"
}

# A minimal iOS app target carrying our bundle id. Regenerated if missing;
# gitignored, since it's build scaffolding rather than source.
write_stub() {
    mkdir -p "$STUB/MAProvision.xcodeproj"
    print 'print("provisioning stub")' > "$STUB/main.swift"
    # The stub must request the same capabilities the real app signs with,
    # or the reissued profile won't cover the App Group and codesign fails.
    cat > "$STUB/stub.entitlements" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>com.apple.security.application-groups</key>
    <array>
        <string>$APP_GROUP</string>
    </array>
</dict>
</plist>
EOF
    cat > "$STUB/MAProvision.xcodeproj/project.pbxproj" <<'PBXEOF'
// !$*UTF8*$!
{
	archiveVersion = 1;
	classes = {
	};
	objectVersion = 56;
	objects = {

/* Begin PBXBuildFile section */
		AA0000000000000000000001 /* main.swift in Sources */ = {isa = PBXBuildFile; fileRef = AA0000000000000000000002 /* main.swift */; };
/* End PBXBuildFile section */

/* Begin PBXFileReference section */
		AA0000000000000000000002 /* main.swift */ = {isa = PBXFileReference; lastKnownFileType = sourcecode.swift; path = main.swift; sourceTree = "<group>"; };
		AA0000000000000000000003 /* MAProvision.app */ = {isa = PBXFileReference; explicitFileType = wrapper.application; includeInIndex = 0; path = MAProvision.app; sourceTree = BUILT_PRODUCTS_DIR; };
/* End PBXFileReference section */

/* Begin PBXFrameworksBuildPhase section */
		AA0000000000000000000004 /* Frameworks */ = {
			isa = PBXFrameworksBuildPhase;
			buildActionMask = 2147483647;
			files = (
			);
			runOnlyForDeploymentPostprocessing = 0;
		};
/* End PBXFrameworksBuildPhase section */

/* Begin PBXGroup section */
		AA0000000000000000000005 = {
			isa = PBXGroup;
			children = (
				AA0000000000000000000002 /* main.swift */,
				AA0000000000000000000006 /* Products */,
			);
			sourceTree = "<group>";
		};
		AA0000000000000000000006 /* Products */ = {
			isa = PBXGroup;
			children = (
				AA0000000000000000000003 /* MAProvision.app */,
			);
			name = Products;
			sourceTree = "<group>";
		};
/* End PBXGroup section */

/* Begin PBXNativeTarget section */
		AA0000000000000000000007 /* MAProvision */ = {
			isa = PBXNativeTarget;
			buildConfigurationList = AA0000000000000000000008 /* Build configuration list for PBXNativeTarget "MAProvision" */;
			buildPhases = (
				AA0000000000000000000009 /* Sources */,
				AA0000000000000000000004 /* Frameworks */,
			);
			buildRules = (
			);
			dependencies = (
			);
			name = MAProvision;
			productName = MAProvision;
			productReference = AA0000000000000000000003 /* MAProvision.app */;
			productType = "com.apple.product-type.application";
		};
/* End PBXNativeTarget section */

/* Begin PBXProject section */
		AA000000000000000000000A /* Project object */ = {
			isa = PBXProject;
			attributes = {
				BuildIndependentTargetsInParallel = 1;
				LastSwiftUpdateCheck = 1600;
				LastUpgradeCheck = 1600;
				TargetAttributes = {
					AA0000000000000000000007 = {
						CreatedOnToolsVersion = 16.0;
					};
				};
			};
			buildConfigurationList = AA000000000000000000000B /* Build configuration list for PBXProject "MAProvision" */;
			compatibilityVersion = "Xcode 14.0";
			developmentRegion = en;
			hasScannedForEncodings = 0;
			knownRegions = (
				en,
				Base,
			);
			mainGroup = AA0000000000000000000005;
			productRefGroup = AA0000000000000000000006 /* Products */;
			projectDirPath = "";
			projectRoot = "";
			targets = (
				AA0000000000000000000007 /* MAProvision */,
			);
		};
/* End PBXProject section */

/* Begin PBXSourcesBuildPhase section */
		AA0000000000000000000009 /* Sources */ = {
			isa = PBXSourcesBuildPhase;
			buildActionMask = 2147483647;
			files = (
				AA0000000000000000000001 /* main.swift in Sources */,
			);
			runOnlyForDeploymentPostprocessing = 0;
		};
/* End PBXSourcesBuildPhase section */

/* Begin XCBuildConfiguration section */
		AA000000000000000000000C /* Debug */ = {
			isa = XCBuildConfiguration;
			buildSettings = {
				ALWAYS_SEARCH_USER_PATHS = NO;
				CLANG_ENABLE_MODULES = YES;
				COPY_PHASE_STRIP = NO;
				ENABLE_STRICT_OBJC_MSGSEND = YES;
				GCC_NO_COMMON_BLOCKS = YES;
				IPHONEOS_DEPLOYMENT_TARGET = 18.0;
				ONLY_ACTIVE_ARCH = YES;
				SDKROOT = iphoneos;
				SWIFT_OPTIMIZATION_LEVEL = "-Onone";
				SWIFT_VERSION = 5.0;
			};
			name = Debug;
		};
		AA000000000000000000000D /* Debug */ = {
			isa = XCBuildConfiguration;
			buildSettings = {
				CODE_SIGN_ENTITLEMENTS = stub.entitlements;
				CODE_SIGN_STYLE = Automatic;
				CURRENT_PROJECT_VERSION = 1;
				DEVELOPMENT_TEAM = ZVNYY55W9L;
				GENERATE_INFOPLIST_FILE = YES;
				MARKETING_VERSION = 1.0;
				PRODUCT_BUNDLE_IDENTIFIER = no.norapps.mediaarchive;
				PRODUCT_NAME = "$(TARGET_NAME)";
				SWIFT_VERSION = 5.0;
				TARGETED_DEVICE_FAMILY = "1,2";
			};
			name = Debug;
		};
/* End XCBuildConfiguration section */

/* Begin XCConfigurationList section */
		AA000000000000000000000B /* Build configuration list for PBXProject "MAProvision" */ = {
			isa = XCConfigurationList;
			buildConfigurations = (
				AA000000000000000000000C /* Debug */,
			);
			defaultConfigurationIsVisible = 0;
			defaultConfigurationName = Debug;
		};
		AA0000000000000000000008 /* Build configuration list for PBXNativeTarget "MAProvision" */ = {
			isa = XCConfigurationList;
			buildConfigurations = (
				AA000000000000000000000D /* Debug */,
			);
			defaultConfigurationIsVisible = 0;
			defaultConfigurationName = Debug;
		};
/* End XCConfigurationList section */
	};
	rootObject = AA000000000000000000000A /* Project object */;
}
PBXEOF
}

renew() {
    local force="${1:-}" days
    days=$(profile_days_left)

    if [[ "$force" != "--force" ]] && (( days > RENEW_THRESHOLD_DAYS )); then
        say "profile valid for $days more days — skipping renewal"
        return 0
    fi

    if (( days < 0 )); then
        say "no usable profile — requesting one from Apple…"
    else
        say "profile expires in $days day(s) — renewing…"
    fi

    write_stub
    # Xcode reissues the profile as a side effect of signing this stub target.
    # If the Apple ID session has lapsed this is where it fails, and the fix is
    # a GUI one: Xcode → Settings → Accounts, re-enter the 2FA code.
    if ! run_timeout 900 xcodebuild -project "$STUB/MAProvision.xcodeproj" -scheme MAProvision \
            -destination 'generic/platform=iOS' -allowProvisioningUpdates build \
            >"$STUB/xcodebuild.log" 2>&1; then
        warn "provisioning failed — last lines of $STUB/xcodebuild.log:"
        tail -15 "$STUB/xcodebuild.log" >&2
        die "could not renew the provisioning profile (is your Apple ID still signed in to Xcode?)"
    fi

    days=$(profile_days_left)
    (( days >= 0 )) || die "xcodebuild succeeded but no profile for $BUNDLE_ID appeared"
    say "profile renewed — good for $days more days"
}

# ----------------------------------------------------------------------- build

# Compile the widget .appex plus the WidgetLink.framework shim (the app's
# C#-callable bridge to WidgetKit); the .NET build embeds both. Device builds
# pass -allowProvisioningUpdates, which also creates/renews the widget's own
# 7-day profile as a side effect — no separate stub needed.
build_widget() {
    local sdk="$1"; shift
    say "building widget ($sdk)…"
    if ! run_timeout 900 xcodebuild -project "$WIDGET_DIR/MediaArchiveWidget.xcodeproj" \
            -alltargets -configuration Debug -sdk "$sdk" \
            SYMROOT="$WIDGET_DIR/build" "$@" build \
            >"$WIDGET_DIR/xcodebuild.log" 2>&1; then
        warn "widget build failed — last lines of widget/xcodebuild.log:"
        tail -15 "$WIDGET_DIR/xcodebuild.log" >&2
        die "widget build failed"
    fi
}

build() {
    local rid="$1"
    say "building ($rid)…"
    dotnet build "$PROJ" -f "$TFM" -r "$rid" --nologo -v quiet >&2 \
        || die "build failed"
    print -- "$REPO/MediaArchive.Mobile/bin/Debug/$TFM/$rid/MediaArchive.Mobile.app"
}

# ------------------------------------------------------------------- simulator

sim_run() {
    local want="${1:-}" udid rid app
    [[ $(uname -m) == arm64 ]] && rid=iossimulator-arm64 || rid=iossimulator-x64

    if [[ -n "$want" ]]; then
        udid=$(xcrun simctl list devices available \
               | grep -F "$want (" | head -1 | grep -oE '[0-9A-F-]{36}') \
            || die "no available simulator matching \"$want\""
    else
        # Reuse whatever is already booted; otherwise fall back to the default.
        udid=$(xcrun simctl list devices booted | grep -oE '[0-9A-F-]{36}' | head -1 || true)
        if [[ -z "$udid" ]]; then
            udid=$(xcrun simctl list devices available \
                   | grep -F "$SIM_DEFAULT (" | head -1 | grep -oE '[0-9A-F-]{36}') \
                || die "default simulator \"$SIM_DEFAULT\" not found — pass one: ./ma sim \"iPhone 17\""
        fi
    fi

    build_widget iphonesimulator
    app=$(build "$rid")

    say "booting simulator…"
    xcrun simctl boot "$udid" 2>/dev/null || true
    open -a Simulator --args -CurrentDeviceUDID "$udid"
    xcrun simctl bootstatus "$udid" -b >/dev/null 2>&1 || true

    say "installing…"
    xcrun simctl install "$udid" "$app" || die "install failed"
    say "launching…"
    xcrun simctl launch "$udid" "$BUNDLE_ID" >/dev/null || die "launch failed"
    say "running on simulator"
}

# ----------------------------------------------------------------------- phone

# The first reachable paired device, or nothing. Callers decide whether that's fatal.
#
# `devicectl list devices` reports "available" straight from the cached pairing
# record, so it says yes for a phone that is asleep or off the network. Only an
# actual query proves there's a live tunnel, so each candidate gets probed.
find_device() {
    local id
    for id in ${(f)"$(xcrun devicectl list devices 2>/dev/null | grep -i 'available' | grep -oE '[0-9A-F]{8}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{12}')"}; do
        if run_timeout 60 xcrun devicectl device info details --device "$id" >/dev/null 2>&1; then
            print -- "$id"
            return 0
        fi
    done
    return 0
}

notify() {
    osascript -e "display notification \"$2\" with title \"$1\"" >/dev/null 2>&1 || true
}

phone_run() {
    local id="${1:-}" app
    renew
    [[ -n "$id" ]] || id=$(find_device)
    [[ -n "$id" ]] || die "no paired iPhone reachable — unlock it and check it's on the same Wi-Fi"
    build_widget iphoneos -allowProvisioningUpdates
    app=$(build ios-arm64)

    say "installing to device…"
    run_timeout 600 xcrun devicectl device install app --device "$id" "$app" >/dev/null \
        || die "install failed or timed out — is the phone unlocked and reachable?"

    say "launching…"
    if ! run_timeout 120 xcrun devicectl device process launch --device "$id" "$BUNDLE_ID" >/dev/null 2>&1; then
        warn "installed, but iOS refused to launch it."
        warn "Trust the certificate once on the phone, then tap the app:"
        warn "  Settings → General → VPN & Device Management → Apple Development → Trust"
        exit 0
    fi
    say "running on device"
}

# ------------------------------------------------------------------------ pull

# The phone is currently the only head that runs, so its container holds the
# only live database. This copies it back into the repo: a backup and a refresh
# of the design-time db, NOT a two-way sync — nothing merges, the phone wins.
#
# LocalImagePath needs rewriting because each head stores a different form of
# the same fact: the MAUI app uses a WebView scheme (covers://c/x.jpg) that
# means nothing outside it. That coupling is worth removing at some point.
pull_from_phone() {
    local id="${1:-}" snap
    [[ -n "$id" ]] || id=$(find_device)
    [[ -n "$id" ]] || die "no paired iPhone reachable — unlock it and check it's on the same Wi-Fi"
    snap="$REPO/backups/$(date +%Y%m%d-%H%M%S)"
    mkdir -p "$snap"

    say "pulling database…"
    run_timeout 300 xcrun devicectl device copy from --device "$id" \
        --domain-type appDataContainer --domain-identifier "$BUNDLE_ID" \
        --source Library/mediaarchive.db --destination "$snap/mediaarchive.db" >/dev/null \
        || die "could not read the app container — is the phone unlocked and reachable?"

    say "pulling cover cache…"
    run_timeout 300 xcrun devicectl device copy from --device "$id" \
        --domain-type appDataContainer --domain-identifier "$BUNDLE_ID" \
        --source Library/covers --destination "$snap/covers" >/dev/null 2>&1 || true

    # Keep the outgoing repo db inside the snapshot, so a bad pull is undoable.
    [[ -f "$REPO/mediaarchive.db" ]] && cp "$REPO/mediaarchive.db" "$snap/repo-db-before-pull.db"
    rm -f "$REPO/mediaarchive.db-shm" "$REPO/mediaarchive.db-wal"
    cp "$snap/mediaarchive.db" "$REPO/mediaarchive.db"
    sqlite3 "$REPO/mediaarchive.db" "UPDATE MediaItems SET LocalImagePath = '/covers/' || substr(LocalImagePath, length('covers://c/') + 1) WHERE LocalImagePath LIKE 'covers://c/%';"

    say "snapshot saved to ${snap#$REPO/}"
    say "$(sqlite3 "$REPO/mediaarchive.db" 'SELECT COUNT(*) FROM MediaItems;') items, \
$(sqlite3 "$REPO/mediaarchive.db" 'SELECT COUNT(*) FROM ConsumptionEntries;') entries, \
$(ls "$snap/covers" 2>/dev/null | wc -l | tr -d ' ') covers"
}

# ---------------------------------------------------------------------- weekly

# What the Tuesday job runs. Two jobs in one: back the database up, and
# reinstall so the 7-day signature never lapses into a dead app on the phone.
#
# A fixed clock time will regularly find the phone asleep, locked, or off the
# network, so this waits rather than failing — up to WEEKLY_RETRIES attempts,
# WEEKLY_RETRY_WAIT apart. Backup runs BEFORE deploy: if the install goes wrong,
# the snapshot is already on disk.
# Overridable so the loop can be exercised without waiting hours.
WEEKLY_RETRIES=${WEEKLY_RETRIES:-12}
WEEKLY_RETRY_WAIT=${WEEKLY_RETRY_WAIT:-600}   # 10 min × 12 = a 2-hour window

weekly() {
    local tries=0 id
    while (( tries < WEEKLY_RETRIES )); do
        tries=$(( tries + 1 ))

        id=$(find_device)
        if [[ -z "$id" ]]; then
            say "phone not reachable (attempt $tries/$WEEKLY_RETRIES) — waiting $(( WEEKLY_RETRY_WAIT / 60 ))m"
            sleep "$WEEKLY_RETRY_WAIT"
            continue
        fi

        say "=== weekly run $(date '+%Y-%m-%d %H:%M') (attempt $tries) ==="
        # A phone can drop off mid-copy when it locks, so retry the whole run,
        # not just the discovery. Subshell keeps `die` from killing the loop.
        if ( pull_from_phone "$id" && phone_run "$id" ); then
            return 0
        fi

        warn "run failed (attempt $tries/$WEEKLY_RETRIES) — phone may have slept; waiting $(( WEEKLY_RETRY_WAIT / 60 ))m"
        sleep "$WEEKLY_RETRY_WAIT"
    done
    die "phone never stayed reachable long enough — skipping this week"
}

# ------------------------------------------------------------------------ main

case "${1:-sim}" in
    sim)   sim_run "${2:-}" ;;
    phone) phone_run ;;
    pull)  pull_from_phone ;;
    weekly)
        # Subshell so `die`'s exit is a status we can report, not a silent death.
        if ( weekly ); then
            notify "MediaArchive" "Weekly sync + deploy complete"
        else
            notify "MediaArchive" "Weekly sync failed — see ~/Library/Logs/mediaarchive-weekly.log"
            exit 2
        fi
        ;;
    renew) renew "${2:-}" ;;
    -h|--help|help)
        sed -n '3,12p' "$0" | sed 's/^# \{0,1\}//'
        ;;
    *) die "unknown command: $1 (try: sim | phone | pull | renew | weekly)" 1 ;;
esac

// The Live Activity for a timed session: a countdown against a known runtime
// (a film, one episode) or a stopwatch otherwise. Display plus one button: the
// widgetURL opens the app to log, and Pause runs an intent in the app. Nothing
// here writes a row.

import ActivityKit
import SwiftUI
import WidgetKit

struct SessionLiveActivity: Widget {
    var body: some WidgetConfiguration {
        ActivityConfiguration(for: SessionAttributes.self) { context in
            LockScreenSession(context: context)
                .activityBackgroundTint(Palette.bg)
                .activitySystemActionForegroundColor(Palette.ink)
        } dynamicIsland: { context in
            DynamicIsland {
                DynamicIslandExpandedRegion(.leading) {
                    SessionCover(attributes: context.attributes, width: 30).padding(.leading, 4)
                }
                DynamicIslandExpandedRegion(.center) {
                    VStack(alignment: .leading, spacing: 3) {
                        Text(context.attributes.title)
                            .font(.system(size: 14, weight: .semibold))
                            .foregroundStyle(Palette.ink)
                            .lineLimit(1)
                        Text(sessionSubtitle(context).text)
                            .font(.system(size: 9.5, weight: .medium))
                            .tracking(1)
                            .foregroundStyle(sessionSubtitle(context).color)
                    }
                    .frame(maxWidth: .infinity, alignment: .leading)
                }
                DynamicIslandExpandedRegion(.trailing) {
                    CompactClock(context: context, size: 18, width: 62).padding(.trailing, 4)
                }
                DynamicIslandExpandedRegion(.bottom) {
                    HStack(alignment: .center, spacing: 12) {
                        SessionBar(context: context).frame(maxWidth: .infinity)
                        PauseButton(context: context)
                    }
                    .padding(.top, 4)
                }
            } compactLeading: {
                SessionCover(attributes: context.attributes, width: 11)
            } compactTrailing: {
                // Leading-aligned so the digits sit right of the sensor, like a system timer.
                CompactClock(context: context, size: 14, width: 34)
            } minimal: {
                Circle().fill(accent(context.attributes.kind)).frame(width: 12, height: 12)
            }
            .widgetURL(logURL(context))
        }
    }
}

private struct LockScreenSession: View {
    let context: ActivityViewContext<SessionAttributes>

    var body: some View {
        HStack(alignment: .center, spacing: 12) {
            SessionCover(attributes: context.attributes, width: 40)
            VStack(alignment: .leading, spacing: 4) {
                Text(context.attributes.title)
                    .font(.system(size: 15, weight: .semibold))
                    .foregroundStyle(Palette.ink)
                    .lineLimit(1)
                Text(subtitle.text)
                    .font(.system(size: 10, weight: .medium))
                    .tracking(1.2)
                    .foregroundStyle(subtitle.color)
                SessionBar(context: context).padding(.top, 4)
            }
            VStack(spacing: 6) {
                SessionClock(context: context, size: 22)
                PauseButton(context: context)
            }
        }
        .padding(.horizontal, 16)
        .padding(.vertical, 14)
        .widgetURL(logURL(context))
    }

    private var subtitle: (text: String, color: Color) { sessionSubtitle(context) }
}

// The stale date is an hour before the system takes the timer away.
private func sessionSubtitle(_ context: ActivityViewContext<SessionAttributes>) -> (text: String, color: Color) {
    let kind = context.attributes.kind.uppercased()
    if context.isStale { return ("\(kind) · 1H LEFT · LOG IT", Palette.ac2) }
    if context.state.pausedAt != nil { return ("\(kind) · PAUSED", Palette.dim) }
    return (context.attributes.targetMinutes == nil ? "\(kind) · LENGTH UNKNOWN" : kind, Palette.dim)
}

private struct PauseButton: View {
    let context: ActivityViewContext<SessionAttributes>

    var body: some View {
        Button(intent: PauseIntent(sessionId: context.attributes.sessionId)) {
            Text(context.state.pausedAt == nil ? "Pause" : "Resume")
                .font(.system(size: 11, weight: .semibold))
        }
        .buttonStyle(.bordered)
        .tint(accent(context.attributes.kind))
    }
}

// Ticks with zero process time: the system renders the interval from the anchor,
// and holds it at pauseTime while the session is paused.
private struct SessionClock: View {
    let context: ActivityViewContext<SessionAttributes>
    let size: CGFloat

    var body: some View {
        Group {
            if let end = target(context) {
                // A pause at or past the target reads as "not started" to the system, so it is drawn by hand.
                if let pausedAt = context.state.pausedAt, pausedAt >= end {
                    Text("0:00").foregroundStyle(Palette.dim)
                } else {
                    Text(timerInterval: context.state.anchor...end, pauseTime: context.state.pausedAt, countsDown: true)
                        .foregroundStyle(color(running: accent(context.attributes.kind)))
                }
            } else {
                Text(timerInterval: context.state.anchor...Date.distantFuture, pauseTime: context.state.pausedAt, countsDown: false)
                    .foregroundStyle(color(running: Palette.ink))
            }
        }
        .font(.system(size: size, weight: .semibold, design: .rounded))
        .monospacedDigit()
        .multilineTextAlignment(.trailing)
        .frame(minWidth: size * 2.6, alignment: .trailing)
    }

    private func color(running: Color) -> Color {
        if context.state.pausedAt != nil { return Palette.dim }
        return context.isStale ? Palette.ac2 : running
    }
}

// The island's clock: a fixed width, or the timer text takes all it is offered and
// pushes the two sides apart. Past an hour the stopwatch scales down instead.
private struct CompactClock: View {
    let context: ActivityViewContext<SessionAttributes>
    let size: CGFloat
    let width: CGFloat

    var body: some View {
        Group {
            if let end = target(context) {
                if let pausedAt = context.state.pausedAt, pausedAt >= end {
                    Text("0:00")
                } else {
                    Text(timerInterval: context.state.anchor...end, pauseTime: context.state.pausedAt, countsDown: true)
                }
            } else {
                Text(timerInterval: context.state.anchor...Date.distantFuture, pauseTime: context.state.pausedAt, countsDown: false)
            }
        }
        .font(.system(size: size, weight: .semibold, design: .rounded))
        .monospacedDigit()
        .lineLimit(1)
        .minimumScaleFactor(0.55)
        .frame(width: width, alignment: .leading)
        .foregroundStyle(context.state.pausedAt != nil ? Palette.dim : accent(context.attributes.kind))
    }
}

// Only a known target earns a bar; a stopwatch has no denominator.
private struct SessionBar: View {
    let context: ActivityViewContext<SessionAttributes>

    var body: some View {
        if let end = target(context) {
            Group {
                if let pausedAt = context.state.pausedAt {
                    ProgressView(value: min(1, pausedAt.timeIntervalSince(context.state.anchor) / end.timeIntervalSince(context.state.anchor)))
                } else {
                    ProgressView(timerInterval: context.state.anchor...end, countsDown: false, label: {}, currentValueLabel: {})
                }
            }
            .progressViewStyle(.linear)
            .tint(context.state.pausedAt == nil ? accent(context.attributes.kind) : Palette.dim)
            .frame(height: 4)
        }
    }
}

private struct SessionCover: View {
    let attributes: SessionAttributes
    let width: CGFloat

    var body: some View {
        if let image = coverImage(attributes.cover) {
            Image(uiImage: image)
                .resizable()
                .aspectRatio(contentMode: .fill)
                .frame(width: width, height: width * 1.5)
                .clipShape(RoundedRectangle(cornerRadius: 6))
        } else {
            RoundedRectangle(cornerRadius: 6)
                .fill(accent(attributes.kind).opacity(0.22))
                .frame(width: width, height: width * 1.5)
                .overlay(
                    Text(String(attributes.title.prefix(1)))
                        .font(.system(size: width * 0.45, weight: .bold))
                        .foregroundStyle(accent(attributes.kind))
                )
        }
    }
}

private func target(_ context: ActivityViewContext<SessionAttributes>) -> Date? {
    context.attributes.targetMinutes.map { context.state.anchor.addingTimeInterval(TimeInterval($0 * 60)) }
}

// The same grammar the home-screen widget uses, so the tap lands in the log sheet.
private func logURL(_ context: ActivityViewContext<SessionAttributes>) -> URL {
    URL(string: "mediaarchive://log/\(context.attributes.userMediaItemId)")!
}

// MediaType raw values; the accents are the app's, generated into this target's Palette.
private func accent(_ kind: String) -> Color {
    switch kind {
    case "Book": Palette.book
    case "Game": Palette.game
    case "Movie": Palette.movie
    default: Palette.show
    }
}

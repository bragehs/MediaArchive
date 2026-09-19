// The Live Activity for a timed session: a countdown against a known runtime
// (a film, one episode) or a stopwatch otherwise. Display only, plus the
// widgetURL — tapping opens the app to log; nothing here writes.

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
                    SessionCover(attributes: context.attributes, width: 34)
                }
                DynamicIslandExpandedRegion(.trailing) {
                    SessionClock(context: context, size: 20)
                }
                DynamicIslandExpandedRegion(.bottom) {
                    VStack(alignment: .leading, spacing: 6) {
                        Text(context.attributes.title)
                            .font(.system(size: 14, weight: .semibold))
                            .foregroundStyle(Palette.ink)
                            .lineLimit(1)
                        SessionBar(context: context)
                    }
                    .widgetURL(logURL(context))
                }
            } compactLeading: {
                RoundedRectangle(cornerRadius: 2)
                    .fill(accent(context.attributes.kind))
                    .frame(width: 10, height: 16)
            } compactTrailing: {
                SessionClock(context: context, size: 14)
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
                Text(subtitle)
                    .font(.system(size: 10, weight: .medium))
                    .tracking(1.2)
                    .foregroundStyle(Palette.dim)
                SessionBar(context: context).padding(.top, 4)
            }
            SessionClock(context: context, size: 22)
        }
        .padding(.horizontal, 16)
        .padding(.vertical, 14)
        .widgetURL(logURL(context))
    }

    private var subtitle: String {
        let kind = context.attributes.kind.uppercased()
        if context.state.frozenMinutes != nil { return "\(kind) · PAUSED" }
        return context.attributes.targetMinutes == nil ? "\(kind) · LENGTH UNKNOWN" : kind
    }
}

// Ticks with zero process time: the system renders the interval from the anchor.
private struct SessionClock: View {
    let context: ActivityViewContext<SessionAttributes>
    let size: CGFloat

    var body: some View {
        Group {
            if let frozen = context.state.frozenMinutes {
                Text(String(format: "%d:%02d", frozen / 60, frozen % 60))
                    .foregroundStyle(Palette.dim)
            } else if let end = target(context) {
                Text(timerInterval: context.state.anchor...end, countsDown: true)
                    .foregroundStyle(accent(context.attributes.kind))
            } else {
                Text(timerInterval: context.state.anchor...Date.distantFuture, countsDown: false)
                    .foregroundStyle(Palette.ink)
            }
        }
        .font(.system(size: size, weight: .semibold, design: .rounded))
        .monospacedDigit()
        .multilineTextAlignment(.trailing)
        .frame(minWidth: size * 2.6, alignment: .trailing)
    }
}

// Only a known target earns a bar; a stopwatch has no denominator.
private struct SessionBar: View {
    let context: ActivityViewContext<SessionAttributes>

    var body: some View {
        if let end = target(context), context.state.frozenMinutes == nil {
            ProgressView(timerInterval: context.state.anchor...end, countsDown: false, label: {}, currentValueLabel: {})
                .progressViewStyle(.linear)
                .tint(accent(context.attributes.kind))
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

import SwiftUI

// Fixed app bar and one NavigationStack per tab over the system's glass tab bar.
// The navigation bars stay hidden so the theme carries the top chrome.
struct Shell: View {
    @Bindable private var router = Router.shared

    var body: some View {
        VStack(spacing: 0) {
            AppBar()
            TabView(selection: $router.selected) {
                ForEach(AppTab.allCases, id: \.self) { tab in
                    Tab(tab.label, systemImage: tab.symbol, value: tab, role: tab == .search ? .search : nil) {
                        NavigationStack(path: pathBinding(tab)) {
                            root(tab)
                                .screen()
                                .navigationDestination(for: Route.self) { route in
                                    destination(route).screen()
                                }
                        }
                    }
                }
            }
            .tint(Palette.ac)
            .minimizingTabBar()
        }
        .background(Palette.bg.ignoresSafeArea())
        .onAppear { router.ready = true }
    }

    private func pathBinding(_ tab: AppTab) -> Binding<[Route]> {
        Binding(get: { router.path(for: tab) }, set: { router.setPath($0, for: tab) })
    }

    @ViewBuilder
    private func root(_ tab: AppTab) -> some View {
        switch tab {
        case .now: HomeView()
        case .explore: ExploreView()
        case .library: LibraryView()
        case .profile: ProfileView()
        case .search: SearchView()
        }
    }

    @ViewBuilder
    private func destination(_ route: Route) -> some View {
        switch route {
        case .item(let id, let log): ItemView(userMediaItemId: id, openLog: log)
        case .diaryMonth(let year, let month): DiaryMonthView(year: year, month: month)
        case .onDeck: OnDeckView()
        case .constellation: ConstellationView()
        case .universes: UniversesView()
        case .creators: CreatorsView()
        }
    }
}

extension View {
    // Every page: hidden system bar, the ground colour, no system back button.
    func screen() -> some View {
        self
            .toolbar(.hidden, for: .navigationBar)
            .navigationBarBackButtonHidden(true)
            .background(Palette.bg.ignoresSafeArea())
    }

    // The viewport gutter, applied to the scroll content rather than the scroll
    // view, so the indicator stays at the screen edge and over nothing.
    func page() -> some View {
        self
            .contentMargins(.horizontal, 16, for: .scrollContent)
            .contentMargins(.top, 2, for: .scrollContent)
            .contentMargins(.bottom, 24, for: .scrollContent)
            .modifier(ThinScroller())
    }
}

struct Brand: View {
    var size: CGFloat = 17

    var body: some View {
        HStack(spacing: size * 0.5) {
            if let mark = UIImage(named: "brandmark") {
                Image(uiImage: mark)
                    .resizable()
                    .frame(width: size * 1.65, height: size * 1.65)
            }
            (Text("MEDIA") + Text("ARCHIVE").foregroundStyle(Palette.ac))
                .font(Fonts.title(size))
                .tracking(size * 0.04)
                .foregroundStyle(Palette.ink)
                .lineLimit(1)
        }
    }
}

private struct AppBar: View {
    var body: some View {
        HStack {
            Brand(size: 17)
            Spacer()
        }
        .padding(.horizontal, 16)
        .padding(.top, 10)
        .padding(.bottom, 10)
        .background(Palette.bg)
    }
}

extension View {
    // The glass bar shrinks to a pill as content scrolls; older systems keep the plain bar.
    @ViewBuilder
    func minimizingTabBar() -> some View {
        if #available(iOS 26, *) {
            self.tabBarMinimizeBehavior(.onScrollDown)
        } else {
            self
        }
    }
}

// The system's indicator is as long as the page is short; this one is a small
// pill in the gutter that follows the scroll and fades a moment after it stops.
private struct ThinScroller: ViewModifier {
    private let height: CGFloat = 56
    private let inset: CGFloat = 6

    @State private var fraction: CGFloat = 0
    @State private var scrollable = false
    @State private var visible = false
    @State private var fade: Task<Void, Never>?

    func body(content: Content) -> some View {
        content
            .scrollIndicators(.hidden)
            .onScrollGeometryChange(for: ScrollGeometry.self) { $0 } action: { _, geometry in
                let travel = geometry.contentSize.height + geometry.contentInsets.top + geometry.contentInsets.bottom
                    - geometry.containerSize.height
                scrollable = travel > 1
                guard scrollable else { return }
                fraction = min(max((geometry.contentOffset.y + geometry.contentInsets.top) / travel, 0), 1)
                visible = true
                fade?.cancel()
                fade = Task {
                    try? await Task.sleep(for: .seconds(1.2))
                    guard !Task.isCancelled else { return }
                    withAnimation(.easeOut(duration: 0.35)) { visible = false }
                }
            }
            .overlay(alignment: .topTrailing) {
                GeometryReader { geo in
                    Capsule()
                        .fill(Palette.dim.opacity(0.75))
                        .frame(width: 3, height: height)
                        .offset(x: geo.size.width - inset, y: inset + fraction * (geo.size.height - height - inset * 2))
                        .opacity(visible && scrollable ? 1 : 0)
                }
                .allowsHitTesting(false)
            }
    }
}

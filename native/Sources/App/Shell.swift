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

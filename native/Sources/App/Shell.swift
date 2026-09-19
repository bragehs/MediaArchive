import SwiftUI

// Fixed app bar, one NavigationStack per tab, custom tab bar — the Blazor
// shell's grammar. The system bars are hidden so the theme carries the chrome.
struct Shell: View {
    @Bindable private var router = Router.shared

    var body: some View {
        VStack(spacing: 0) {
            AppBar()
            TabView(selection: $router.selected) {
                ForEach(Tab.allCases, id: \.self) { tab in
                    NavigationStack(path: pathBinding(tab)) {
                        root(tab)
                            .screen()
                            .navigationDestination(for: Route.self) { route in
                                destination(route).screen()
                            }
                    }
                    .toolbar(.hidden, for: .tabBar)
                    .tag(tab)
                }
            }
            .toolbar(.hidden, for: .tabBar)
            TabBar(selected: $router.selected)
        }
        .background(Palette.bg.ignoresSafeArea())
        .onAppear { router.ready = true }
    }

    private func pathBinding(_ tab: Tab) -> Binding<[Route]> {
        Binding(get: { router.path(for: tab) }, set: { router.setPath($0, for: tab) })
    }

    @ViewBuilder
    private func root(_ tab: Tab) -> some View {
        switch tab {
        case .home: HomeView()
        case .explore: ExploreView()
        case .library: LibraryView(isActive: router.selected == .library && router.path(for: .library).isEmpty)
        case .diary: DiaryView()
        case .profile: ProfileView()
        }
    }

    @ViewBuilder
    private func destination(_ route: Route) -> some View {
        switch route {
        case .item(let id, let log): ItemView(userMediaItemId: id, openLog: log)
        case .diaryMonth(let year, let month): DiaryMonthView(year: year, month: month)
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

    // The `.viewport` padding — pages scroll inside it.
    func page() -> some View {
        self.padding(.horizontal, 16).padding(.top, 2).padding(.bottom, 24)
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

private struct TabBar: View {
    @Binding var selected: Tab

    var body: some View {
        HStack(alignment: .bottom, spacing: 0) {
            ForEach(Tab.allCases, id: \.self) { tab in
                Button {
                    if selected == tab {
                        Router.shared.setPath([], for: tab)
                    } else {
                        selected = tab
                    }
                } label: {
                    VStack(spacing: 4) {
                        Text(tab.glyph)
                            .font(.system(size: 17))
                            .frame(height: 18)
                        Eyebrow(tab.label, size: 8, color: selected == tab ? Palette.ac : Palette.dim,
                                tracking: 0.08)
                    }
                    .foregroundStyle(selected == tab ? Palette.ac : Palette.dim)
                    .frame(maxWidth: .infinity)
                    .padding(.bottom, 2)
                }
                .buttonStyle(.plain)
            }
        }
        .padding(.horizontal, 14)
        .padding(.top, 8)
        .padding(.bottom, 8)
        .background(Color(red: 10 / 255, green: 17 / 255, blue: 10 / 255).opacity(0.92))
        .overlay(alignment: .top) { Rectangle().fill(Palette.line).frame(height: 1) }
    }
}

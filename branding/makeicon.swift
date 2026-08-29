import AppKit
import CoreText

// One source for both marks: the iOS app icon and the in-app masthead emblem.
// Rendering them from the same code is the only way they stay identical — CSS
// border-radius draws a circular corner and cannot match the iOS squircle.
//
//   Resources/AppIcon/appicon.png  full-bleed square, iOS applies its own mask
//   wwwroot/brandmark.png          pre-clipped to the squircle, for the appbar
//
// Colours track the app tokens: #0a0a0a ground, --ac rim and "A", --ink "M".

let ground = NSColor(srgbRed: 0x0a/255, green: 0x0a/255, blue: 0x0a/255, alpha: 1)
let green  = NSColor(srgbRed: 0x8b/255, green: 0xbe/255, blue: 0x5a/255, alpha: 1)
let ink    = NSColor(srgbRed: 0xfa/255, green: 0xfc/255, blue: 0xf8/255, alpha: 1)

let RIM_SHARE: CGFloat = 14/456   // the .brandmark's 1px-on-31px border
let INK_SHARE: CGFloat = 0.74     // monogram width as a share of the square

let fontData = try! Data(contentsOf: URL(fileURLWithPath: "branding/CinzelDecorative-Bold.ttf"))
let cgFont = CGFont(CGDataProvider(data: fontData as CFData)!)!

// n = 5 approximates the iOS icon superellipse. A circular rounded rect would
// leave the rim visibly thicker at the corners than along the edges.
func squircle(_ S: CGFloat, inset d: CGFloat, n: CGFloat = 5, steps: Int = 1440) -> CGPath {
    let path = CGMutablePath()
    let a = (S - d*2)/2, c = S/2
    for i in 0...steps {
        let t = CGFloat(i) / CGFloat(steps) * 2 * .pi
        let ct = cos(t), st = sin(t)
        let x = c + a * copysign(pow(abs(ct), 2/n), ct)
        let y = c + a * copysign(pow(abs(st), 2/n), st)
        i == 0 ? path.move(to: CGPoint(x: x, y: y)) : path.addLine(to: CGPoint(x: x, y: y))
    }
    path.closeSubpath()
    return path
}

func monogram(_ size: CGFloat) -> CTLine {
    let font = CTFontCreateWithGraphicsFont(cgFont, size, nil, nil)
    let s = NSMutableAttributedString()
    s.append(NSAttributedString(string: "M", attributes: [
        .font: font, .foregroundColor: ink, .kern: -0.02 * size]))
    s.append(NSAttributedString(string: "A", attributes: [.font: font, .foregroundColor: green]))
    return CTLineCreateWithAttributedString(s)
}

func render(_ S: CGFloat, clipped: Bool) -> CGImage {
    guard let ctx = CGContext(data: nil, width: Int(S), height: Int(S), bitsPerComponent: 8,
                              bytesPerRow: 0, space: CGColorSpace(name: CGColorSpace.sRGB)!,
                              bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue) else {
        fatalError("no context")
    }

    // The app icon stays square and lets iOS mask it; the in-app copy has to
    // carry the same shape itself, so it is clipped here.
    if clipped { ctx.addPath(squircle(S, inset: 0)); ctx.clip() }

    // The rim IS the edge: fill green full-bleed, then punch the ground out of
    // it. Stroking an inset path instead leaves ground colour outside the rim.
    ctx.setFillColor(green.cgColor)
    ctx.fill(CGRect(x: 0, y: 0, width: S, height: S))
    ctx.addPath(squircle(S, inset: S * RIM_SHARE))
    ctx.setFillColor(ground.cgColor)
    ctx.fillPath()

    var size: CGFloat = 100
    size *= (S * INK_SHARE) / CTLineGetImageBounds(monogram(size), ctx).width
    let bounds = CTLineGetImageBounds(monogram(size), ctx)
    ctx.textPosition = CGPoint(x: (S - bounds.width)/2 - bounds.minX,
                               y: (S - bounds.height)/2 - bounds.minY)
    CTLineDraw(monogram(size), ctx)

    return ctx.makeImage()!
}

func write(_ image: CGImage, to path: String) {
    try! NSBitmapImageRep(cgImage: image).representation(using: .png, properties: [:])!
        .write(to: URL(fileURLWithPath: path))
    print("wrote \(path)  \(image.width)x\(image.height)")
}

write(render(1024, clipped: false), to: "MediaArchive.Mobile/Resources/AppIcon/appicon.png")
write(render(256, clipped: true), to: "wwwroot/brandmark.png")

from PIL import Image, ImageDraw, ImageFont

FONT = "CinzelDecorative-Bold.ttf"
S = 2048                      # supersample, downscaled at the end
R = int(S * 0.225)            # squircle-ish corner radius

# brand colours
TOP   = (37, 59, 35)          # --panel   #253b23
BOT   = (18, 28, 12)          # deeper forest
GREEN = (139, 190, 90, 255)   # --ac      #8bbe5a  → "Archive"
INK   = (250, 252, 248, 255)  # --ink     #fafcf8  → "Media"
MAR   = (214, 167, 74, 255)   # --ac2     #d6a74a

img = Image.new("RGBA", (S, S), (0, 0, 0, 0))

# vertical forest gradient
grad = Image.new("RGB", (1, S))
for y in range(S):
    t = y / (S - 1)
    grad.putpixel((0, y), tuple(int(TOP[i] + (BOT[i] - TOP[i]) * t) for i in range(3)))
grad = grad.resize((S, S))

# rounded-rect mask → transparent corners
mask = Image.new("L", (S, S), 0)
ImageDraw.Draw(mask).rounded_rectangle([0, 0, S - 1, S - 1], radius=R, fill=255)
img.paste(grad, (0, 0), mask)

draw = ImageDraw.Draw(img)

# marigold corner brackets (illuminated-manuscript trim)
inset = int(S * 0.135)
arm   = int(S * 0.072)
w     = int(S * 0.010)
def bracket(cx, cy, dx, dy):
    draw.line([(cx + dx * arm, cy), (cx, cy), (cx, cy + dy * arm)],
              fill=MAR, width=w, joint="curve")
bracket(inset,     inset,      1,  1)
bracket(S - inset, inset,     -1,  1)
bracket(inset,     S - inset,  1, -1)
bracket(S - inset, S - inset, -1, -1)

# the illuminated MA monogram — real Cinzel Decorative, two-tone like the
# wordmark (M = bone "Media", A = green "Archive"), fitted to the frame width
text = "MA"
target_w = int(S * 0.56)
size = 100
f = ImageFont.truetype(FONT, size)
bb = f.getbbox(text)
size = int(size * target_w / (bb[2] - bb[0]))
f = ImageFont.truetype(FONT, size)
bb = f.getbbox(text)                      # ink extents of the whole monogram
gw, gh = bb[2] - bb[0], bb[3] - bb[1]
x = (S - gw) / 2 - bb[0]                  # pen origin so the ink is centred
y = (S - gh) / 2 - bb[1]
draw.text((x, y), "M", font=f, fill=INK)
draw.text((x + f.getlength("M"), y), "A", font=f, fill=GREEN)

# downscale for antialiasing, export masters
final = img.resize((1024, 1024), Image.LANCZOS)
final.save("icon_1024.png")
for sz in (512, 256, 128, 64, 32, 16):
    img.resize((sz, sz), Image.LANCZOS).save(f"icon_{sz}.png")
print("wrote icon_1024.png and downscaled sizes")

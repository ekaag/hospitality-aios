"""Draws the Aurora Grand: a grand lakeside hotel at dusk under the northern lights, reflected in the water (original art).

Run `python tools/draw-hotel.py` in packages/theme to redraw src/aurora-grand.svg; the layout is seeded, so it is the same each run."""
import math
import pathlib
import random

rng = random.Random(412)
W, H, HORIZON = 1600, 900, 640
out = []
add = out.append


def f(x):
    return f"{x:.1f}".rstrip("0").rstrip(".")


add(f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {W} {H}" preserveAspectRatio="xMidYMid slice" role="img" aria-labelledby="t">')
add('<title id="t">The Aurora Grand at dusk: a grand lakeside hotel with lit windows under the northern lights</title>')
add("""<defs>
<linearGradient id="sky" x1="0" y1="0" x2="0" y2="1">
<stop offset="0" stop-color="#060b26"/><stop offset=".42" stop-color="#111c52"/><stop offset=".72" stop-color="#2a2a6c"/>
<stop offset=".9" stop-color="#6a3d78"/><stop offset="1" stop-color="#c8707c"/></linearGradient>
<linearGradient id="water" x1="0" y1="0" x2="0" y2="1">
<stop offset="0" stop-color="#2a2763"/><stop offset=".35" stop-color="#141a48"/><stop offset="1" stop-color="#070a22"/></linearGradient>
<linearGradient id="ribbonGreen" x1="0" y1="0" x2="0" y2="1">
<stop offset="0" stop-color="#43ffb5" stop-opacity="0"/><stop offset=".45" stop-color="#43ffb5" stop-opacity=".55"/>
<stop offset=".8" stop-color="#2be0d2" stop-opacity=".25"/><stop offset="1" stop-color="#2be0d2" stop-opacity="0"/></linearGradient>
<linearGradient id="ribbonTeal" x1="0" y1="0" x2="0" y2="1">
<stop offset="0" stop-color="#2bd8ff" stop-opacity="0"/><stop offset=".5" stop-color="#2bd8ff" stop-opacity=".38"/><stop offset="1" stop-color="#2bd8ff" stop-opacity="0"/></linearGradient>
<linearGradient id="ribbonViolet" x1="0" y1="0" x2="0" y2="1">
<stop offset="0" stop-color="#b07bff" stop-opacity="0"/><stop offset=".5" stop-color="#b07bff" stop-opacity=".4"/><stop offset="1" stop-color="#ff7bd0" stop-opacity="0"/></linearGradient>
<linearGradient id="ray" x1="0" y1="0" x2="0" y2="1">
<stop offset="0" stop-color="#9dffd9" stop-opacity="0"/><stop offset=".55" stop-color="#5cffc0" stop-opacity=".7"/><stop offset="1" stop-color="#5cffc0" stop-opacity="0"/></linearGradient>
<linearGradient id="stone" x1="0" y1="0" x2="0" y2="1">
<stop offset="0" stop-color="#2c2b63"/><stop offset="1" stop-color="#16163b"/></linearGradient>
<linearGradient id="stoneLit" x1="0" y1="0" x2="1" y2="0">
<stop offset="0" stop-color="#3b3a7c"/><stop offset=".5" stop-color="#2a2962"/><stop offset="1" stop-color="#1c1c48"/></linearGradient>
<linearGradient id="roof" x1="0" y1="0" x2="0" y2="1">
<stop offset="0" stop-color="#3f4a8f"/><stop offset="1" stop-color="#232659"/></linearGradient>
<radialGradient id="moonGlow"><stop offset="0" stop-color="#fff2cf" stop-opacity=".55"/><stop offset="1" stop-color="#fff2cf" stop-opacity="0"/></radialGradient>
<radialGradient id="horizonGlow"><stop offset="0" stop-color="#ffab7d" stop-opacity=".5"/><stop offset="1" stop-color="#ffab7d" stop-opacity="0"/></radialGradient>
<radialGradient id="entranceGlow"><stop offset="0" stop-color="#ffd38a" stop-opacity=".9"/><stop offset="1" stop-color="#ffb35c" stop-opacity="0"/></radialGradient>
<filter id="soft" x="-20%" y="-50%" width="140%" height="200%"><feGaussianBlur stdDeviation="16"/></filter>
<filter id="haze" x="-20%" y="-50%" width="140%" height="200%"><feGaussianBlur stdDeviation="5"/></filter>
<filter id="glow" x="-50%" y="-50%" width="200%" height="200%">
<feGaussianBlur in="SourceGraphic" stdDeviation="4" result="b"/><feMerge><feMergeNode in="b"/><feMergeNode in="SourceGraphic"/></feMerge></filter>
<filter id="ripple" x="0" y="0" width="100%" height="100%">
<feTurbulence type="fractalNoise" baseFrequency="0.002 0.09" numOctaves="2" seed="7" result="n"/>
<feDisplacementMap in="SourceGraphic" in2="n" scale="18" xChannelSelector="R" yChannelSelector="G" result="d"/>
<feGaussianBlur in="d" stdDeviation="1.6"/></filter>
<mask id="crescent"><circle cx="1330" cy="128" r="30" fill="#fff"/><circle cx="1343" cy="119" r="26" fill="#000"/></mask>
</defs>""")

# Sky, stars, moon.
add(f'<rect width="{W}" height="{HORIZON}" fill="url(#sky)"/>')
add("<g fill=\"#fff\">")
for _ in range(170):
    x, y = rng.uniform(0, W), rng.uniform(0, 520) ** 1.0
    r = rng.choice([0.6, 0.8, 0.9, 1.1, 1.3, 1.7])
    add(f'<circle cx="{f(x)}" cy="{f(y)}" r="{r}" opacity="{f(rng.uniform(.35, .95))}"/>')
add("</g>")
add('<circle cx="1330" cy="128" r="110" fill="url(#moonGlow)"/>')
add('<circle cx="1330" cy="128" r="30" fill="#fff4d8" mask="url(#crescent)"/>')

# The aurora: three blurred ribbons and curtains of rays along them.
add('<g filter="url(#soft)">')
add('<path d="M-100 250 C 200 120 470 330 820 210 S 1380 110 1700 220 L 1700 400 C 1380 280 1120 420 820 370 S 230 300 -100 430 Z" fill="url(#ribbonGreen)"/>')
add('<path d="M-100 150 C 300 60 600 230 900 140 S 1400 40 1700 120 L 1700 260 C 1400 190 1150 300 900 260 S 300 180 -100 300 Z" fill="url(#ribbonViolet)"/>')
add('<path d="M-100 330 C 260 250 560 420 900 320 S 1420 260 1700 330 L 1700 470 C 1400 400 1120 500 880 450 S 260 390 -100 500 Z" fill="url(#ribbonTeal)"/>')
add("</g>")
add('<g filter="url(#haze)" opacity=".55">')
for i in range(90):
    x = -40 + i * 19 + rng.uniform(-6, 6)
    centre = 290 + 70 * math.sin(x / 210) + 30 * math.sin(x / 77)
    h = rng.uniform(120, 260)
    add(f'<rect x="{f(x)}" y="{f(centre - h * .6)}" width="{f(rng.uniform(4, 12))}" height="{f(h)}" fill="url(#ray)" opacity="{f(rng.uniform(.25, .7))}"/>')
add("</g>")
add(f'<ellipse cx="800" cy="{HORIZON}" rx="760" ry="90" fill="url(#horizonGlow)"/>')

# Everything that is reflected in the lake.
add('<g id="scene">')


def ridge(base, peaks, fill, opacity=1.0):
    points = [(-20, HORIZON)]
    for x, y in peaks:
        points.append((x, y))
    points.append((W + 20, HORIZON))
    d = "M" + " L".join(f"{f(x)} {f(y)}" for x, y in points) + " Z"
    add(f'<path d="{d}" fill="{fill}" opacity="{opacity}"/>')


ridge(HORIZON, [(-20, 520), (120, 470), (230, 505), (360, 430), (470, 488), (560, 470), (700, 520), (1000, 515),
                (1110, 455), (1230, 495), (1330, 420), (1450, 480), (1540, 450), (1620, 500)], "#2b2867", .9)
ridge(HORIZON, [(-20, 575), (90, 545), (200, 570), (300, 530), (420, 578), (1180, 580), (1290, 540), (1400, 565),
                (1500, 528), (1620, 560)], "#1b1a48")

# Shoreline terrace.
add(f'<rect x="0" y="{HORIZON - 18}" width="{W}" height="18" fill="#0f0f30"/>')
add(f'<rect x="300" y="{HORIZON - 26}" width="1000" height="10" rx="2" fill="#1a1a44"/>')


def windows(x0, x1, top, floors, cols, w=13, h=17, gap_y=30, lit=.6):
    span = (x1 - x0)
    step = span / cols
    lit_rects, dark_rects = [], []
    for floor in range(floors):
        for col in range(cols):
            x = x0 + col * step + (step - w) / 2
            y = top + 16 + floor * gap_y
            rect = f'<rect x="{f(x)}" y="{f(y)}" width="{w}" height="{h}" rx="1.5"'
            if rng.random() < lit:
                colour = rng.choice(["#ffd27a", "#ffc766", "#ffe0a3", "#ffb85c"])
                lit_rects.append(f'{rect} fill="{colour}" opacity="{f(rng.uniform(.75, 1))}"/>')
            else:
                dark_rects.append(f'{rect} fill="#33367a"/>')
    add("".join(dark_rects))
    add('<g filter="url(#glow)">' + "".join(lit_rects) + "</g>")


# Pavilions, wings, and the central tower with its crown.
for x0, x1 in [(350, 470), (1130, 1250)]:
    add(f'<rect x="{x0}" y="560" width="{x1 - x0}" height="{HORIZON - 26 - 560}" fill="url(#stone)"/>')
    add(f'<rect x="{x0 - 6}" y="552" width="{x1 - x0 + 12}" height="10" rx="2" fill="url(#roof)"/>')
    windows(x0, x1, 556, 2, 4)
for x0, x1 in [(470, 685), (915, 1130)]:
    add(f'<rect x="{x0}" y="468" width="{x1 - x0}" height="{HORIZON - 26 - 468}" fill="url(#stoneLit)"/>')
    add(f'<path d="M{x0 - 8} 470 L{x0 + 12} 446 L{x1 - 12} 446 L{x1 + 8} 470 Z" fill="url(#roof)"/>')
    for k in range(5):
        cx = x0 + (x1 - x0) * (k + .5) / 5
        add(f'<path d="M{f(cx - 9)} 448 L{f(cx)} 432 L{f(cx + 9)} 448 Z" fill="#2f3876"/>')
    windows(x0, x1, 462, 5, 8)
add(f'<rect x="685" y="330" width="230" height="{HORIZON - 26 - 330}" fill="url(#stoneLit)"/>')
add('<path d="M672 334 L700 296 L900 296 L928 334 Z" fill="url(#roof)"/>')
add('<path d="M738 298 C 738 236 862 236 862 298 Z" fill="#35418a"/>')
add('<path d="M752 298 C 752 250 848 250 848 298 Z" fill="#2a3170" opacity=".8"/>')
add('<rect x="796" y="196" width="8" height="52" fill="#3b4790"/>')
add('<circle cx="800" cy="192" r="5" fill="#ffd27a" filter="url(#glow)"/>')
add('<rect x="700" y="338" width="200" height="26" rx="3" fill="#20205a"/>')
add('<text x="800" y="357" text-anchor="middle" font-family="Georgia, \'Times New Roman\', serif" font-size="15" letter-spacing="5" fill="#ffe1a8">AURORA GRAND</text>')
windows(695, 905, 356, 8, 8, lit=.7)

# The entrance: a lit archway, canopy, and lamps.
add(f'<ellipse cx="800" cy="{HORIZON - 30}" rx="150" ry="46" fill="url(#entranceGlow)"/>')
add(f'<path d="M772 {HORIZON - 26} L772 590 C 772 568 828 568 828 590 L828 {HORIZON - 26} Z" fill="#ffd38a"/>')
add('<rect x="748" y="582" width="104" height="8" rx="2" fill="#3c4690"/>')
for lx in (735, 865):
    add(f'<rect x="{lx - 1.5}" y="586" width="3" height="{HORIZON - 26 - 586}" fill="#11122f"/>')
    add(f'<circle cx="{lx}" cy="584" r="5" fill="#ffe0a3" filter="url(#glow)"/>')
add('<g filter="url(#glow)">')
for i in range(26):
    x = 312 + i * 38.5
    add(f'<circle cx="{f(x)}" cy="{HORIZON - 21}" r="2" fill="#ffd894" opacity=".9"/>')
add("</g>")


def pine(x, h):
    w = h * .42
    layers = []
    for k in range(4):
        top = HORIZON - 18 - h + k * h * .2
        half = w * (.45 + k * .18)
        layers.append(f'M{f(x - half)} {f(top + h * .32)} L{f(x)} {f(top)} L{f(x + half)} {f(top + h * .32)} Z')
    add(f'<path d="{" ".join(layers)}" fill="#0b0c27"/>')
    add(f'<rect x="{f(x - 2)}" y="{f(HORIZON - 18 - h * .2)}" width="4" height="{f(h * .2)}" fill="#0b0c27"/>')


for x in list(range(10, 330, 34)) + list(range(1272, 1600, 36)):
    pine(x + rng.uniform(-8, 8), rng.uniform(70, 150))
add("</g>")

# The lake, the reflection, and ripples of light.
add(f'<rect y="{HORIZON}" width="{W}" height="{H - HORIZON}" fill="url(#water)"/>')
add(f'<g opacity=".42" filter="url(#ripple)"><use href="#scene" transform="translate(0 {2 * HORIZON}) scale(1 -1)"/></g>')
add('<g stroke="#ffd894" stroke-linecap="round">')
for _ in range(46):
    y = rng.uniform(HORIZON + 8, H - 20)
    spread = (y - HORIZON) / (H - HORIZON)
    x = 800 + rng.gauss(0, 60 + 180 * spread)
    length = rng.uniform(14, 60) * (1 + spread)
    add(f'<line x1="{f(x - length / 2)}" y1="{f(y)}" x2="{f(x + length / 2)}" y2="{f(y)}" stroke-width="{f(rng.uniform(1, 2.4))}" opacity="{f(rng.uniform(.12, .45))}"/>')
add("</g>")
add('<g stroke="#7fe8d0" stroke-linecap="round" opacity=".25">')
for _ in range(30):
    y = rng.uniform(HORIZON + 6, H - 10)
    x = rng.uniform(0, W)
    length = rng.uniform(30, 120)
    add(f'<line x1="{f(x)}" y1="{f(y)}" x2="{f(x + length)}" y2="{f(y)}" stroke-width="1"/>')
add("</g>")
add("</svg>")

target = pathlib.Path(__file__).resolve().parent.parent / "src" / "aurora-grand.svg"
target.write_text("\n".join(out) + "\n", encoding="utf-8", newline="\n")
print(target, target.stat().st_size)

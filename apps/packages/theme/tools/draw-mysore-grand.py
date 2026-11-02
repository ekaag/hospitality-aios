"""Draws the Mysore Grand after Mysore Palace illuminated: an Indo-Saracenic palace hotel at blue hour, its five-tier
central tower crowned by a gilded dome, rose domes on its towers, a seven-arch portico, and every edge traced in
bulbs; Chamundi Hill and its temple behind, palms, and the lit facade reflected in the palace pool (original art).

Run `python tools/draw-mysore-grand.py` in packages/theme to redraw src/mysore-grand.svg; the layout is seeded, so it is
the same each run."""
import math
import pathlib
import random

rng = random.Random(1912)
W, H, HORIZON = 1600, 900, 640
GROUND = HORIZON - 14
out = []
add = out.append
bulbs = []


def f(x):
    return f"{x:.1f}".rstrip("0").rstrip(".")


def poly(points):
    return "M" + " L".join(f"{f(x)} {f(y)}" for x, y in points)


add(f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {W} {H}" preserveAspectRatio="xMidYMid slice" role="img" aria-labelledby="t">')
add('<title id="t">The Mysore Grand at blue hour: a palace hotel outlined in lights, its gilded dome and rose domes glowing, with Chamundi Hill behind</title>')
add("""<defs>
<linearGradient id="sky" x1="0" y1="0" x2="0" y2="1">
<stop offset="0" stop-color="#050c33"/><stop offset=".4" stop-color="#0f2366"/><stop offset=".7" stop-color="#26468c"/>
<stop offset=".88" stop-color="#55609c"/><stop offset=".96" stop-color="#b57a86"/><stop offset="1" stop-color="#e59a72"/></linearGradient>
<linearGradient id="water" x1="0" y1="0" x2="0" y2="1">
<stop offset="0" stop-color="#2b2f63"/><stop offset=".35" stop-color="#141a46"/><stop offset="1" stop-color="#060924"/></linearGradient>
<linearGradient id="stone" x1="0" y1="0" x2="0" y2="1">
<stop offset="0" stop-color="#5b4a6e"/><stop offset="1" stop-color="#2f2647"/></linearGradient>
<linearGradient id="stoneLit" x1="0" y1="0" x2="1" y2="0">
<stop offset="0" stop-color="#6b5675"/><stop offset=".5" stop-color="#4c3d62"/><stop offset="1" stop-color="#352b4f"/></linearGradient>
<linearGradient id="gilt" x1="0" y1="0" x2="1" y2="0">
<stop offset="0" stop-color="#ffd877"/><stop offset=".4" stop-color="#d79b34"/><stop offset="1" stop-color="#7a4b1c"/></linearGradient>
<linearGradient id="rose" x1="0" y1="0" x2="1" y2="0">
<stop offset="0" stop-color="#f0849a"/><stop offset=".45" stop-color="#b8435e"/><stop offset="1" stop-color="#64213a"/></linearGradient>
<linearGradient id="arch" x1="0" y1="0" x2="0" y2="1">
<stop offset="0" stop-color="#ffe4a6"/><stop offset="1" stop-color="#ffb55a"/></linearGradient>
<linearGradient id="hill" x1="0" y1="0" x2="0" y2="1">
<stop offset="0" stop-color="#1d2a5e"/><stop offset="1" stop-color="#121a44"/></linearGradient>
<radialGradient id="moonGlow"><stop offset="0" stop-color="#fff2cf" stop-opacity=".45"/><stop offset="1" stop-color="#fff2cf" stop-opacity="0"/></radialGradient>
<radialGradient id="palaceGlow"><stop offset="0" stop-color="#ffcf7a" stop-opacity=".6"/><stop offset=".6" stop-color="#ff9f4a" stop-opacity=".18"/><stop offset="1" stop-color="#ff9f4a" stop-opacity="0"/></radialGradient>
<filter id="glow" x="-20%" y="-20%" width="140%" height="140%">
<feGaussianBlur in="SourceGraphic" stdDeviation="2.4" result="b"/><feMerge><feMergeNode in="b"/><feMergeNode in="b"/><feMergeNode in="SourceGraphic"/></feMerge></filter>
<filter id="halo" x="-30%" y="-30%" width="160%" height="160%"><feGaussianBlur stdDeviation="22"/></filter>
<filter id="ripple" x="0" y="0" width="100%" height="100%">
<feTurbulence type="fractalNoise" baseFrequency="0.002 0.09" numOctaves="2" seed="7" result="n"/>
<feDisplacementMap in="SourceGraphic" in2="n" scale="16" xChannelSelector="R" yChannelSelector="G" result="d"/>
<feGaussianBlur in="d" stdDeviation="1.4"/></filter>
<mask id="crescent"><circle cx="1220" cy="110" r="24" fill="#fff"/><circle cx="1231" cy="102" r="21" fill="#000"/></mask>
</defs>""")

# Blue-hour sky, a few stars, a crescent moon.
add(f'<rect width="{W}" height="{HORIZON}" fill="url(#sky)"/>')
add('<g fill="#fff">')
for _ in range(110):
    x, y = rng.uniform(0, W), rng.uniform(0, 330)
    add(f'<circle cx="{f(x)}" cy="{f(y)}" r="{rng.choice([0.6, 0.8, 1, 1.3])}" opacity="{f(rng.uniform(.3, .9) * (1 - y / 400))}"/>')
add("</g>")
add('<circle cx="1220" cy="110" r="85" fill="url(#moonGlow)"/>')
add('<circle cx="1220" cy="110" r="24" fill="#fff4d8" mask="url(#crescent)"/>')

add('<g id="scene">')

# Chamundi Hill behind, its temple lit at the summit and the road up it traced in lamps.
add(f'<path d="M760 {HORIZON} C 930 560 1080 470 1230 452 C 1320 442 1400 466 1470 515 C 1530 556 1580 586 1620 596 L1620 {HORIZON} Z" fill="url(#hill)"/>')
add(f'<path d="M-20 {HORIZON} L-20 580 C 110 556 250 586 360 600 L 400 {HORIZON} Z" fill="#141c48"/>')
add('<path d="M1216 452 L1220 426 L1240 426 L1244 452 Z M1223 426 L1226 412 L1234 412 L1237 426 Z" fill="#2a3470"/>')
bulbs.append("M1216 452 L1220 426 L1240 426 L1244 452 M1223 426 L1226 412 L1234 412 L1237 426 M1230 412 V402")
add('<g fill="#ffd894" filter="url(#glow)">')
for k in range(9):
    x = 1250 + k * 24
    y = 452 + (x - 1236) ** 1.3 / 8
    add(f'<circle cx="{f(x)}" cy="{f(y)}" r="1.4" opacity=".8"/>')
add("</g>")

# The halo the illuminated palace casts on the sky and grounds.
add(f'<ellipse cx="800" cy="{GROUND - 120}" rx="620" ry="260" fill="url(#palaceGlow)" filter="url(#halo)"/>')
add(f'<rect x="0" y="{GROUND}" width="{W}" height="{HORIZON - GROUND}" fill="#120e2c"/>')


def block(x0, x1, top, fill="url(#stoneLit)"):
    add(f'<rect x="{f(x0)}" y="{f(top)}" width="{f(x1 - x0)}" height="{f(GROUND - top)}" fill="{fill}"/>')
    bulbs.append(poly([(x0, GROUND), (x0, top), (x1, top), (x1, GROUND)]))


def tier(x0, x1, top, bottom):
    add(f'<rect x="{f(x0)}" y="{f(top)}" width="{f(x1 - x0)}" height="{f(bottom - top)}" fill="url(#stoneLit)"/>')
    add(f'<rect x="{f(x0 - 5)}" y="{f(top - 6)}" width="{f(x1 - x0 + 10)}" height="7" fill="#7a6488"/>')
    bulbs.append(poly([(x0, bottom), (x0, top), (x1, top), (x1, bottom)]))
    bulbs.append(poly([(x0 - 5, top - 6), (x1 + 5, top - 6)]))


def dome(cx, base, w, h, fill):
    d = (f"M{f(cx - w)} {f(base)} C {f(cx - w * 1.28)} {f(base - h * .46)} {f(cx - w * .3)} {f(base - h * .74)} {f(cx)} {f(base - h)} "
         f"C {f(cx + w * .3)} {f(base - h * .74)} {f(cx + w * 1.28)} {f(base - h * .46)} {f(cx + w)} {f(base)} Z")
    add(f'<path d="{d}" fill="{fill}"/>')
    # Ribs, then the finial and its lamp.
    for k in (-.5, 0, .5):
        add(f'<path d="M{f(cx + k * w * 1.6)} {f(base)} Q {f(cx + k * w * .9)} {f(base - h * .6)} {f(cx)} {f(base - h)}" fill="none" stroke="#000" stroke-opacity=".18" stroke-width="1.5"/>')
    add(f'<rect x="{f(cx - 1.5)}" y="{f(base - h - 20)}" width="3" height="20" fill="#e6b24f"/>')
    add(f'<circle cx="{f(cx)}" cy="{f(base - h - 22)}" r="3.5" fill="#ffe08a" filter="url(#glow)"/>')
    bulbs.append(d.replace(" Z", ""))


def chhatri(cx, base, w, fill="url(#rose)"):
    add(f'<rect x="{f(cx - w)}" y="{f(base - w * 1.2)}" width="{f(2 * w)}" height="{f(w * 1.2)}" fill="#3a2e55"/>')
    for k in (-1, 0, 1):
        add(f'<rect x="{f(cx + k * w * .66 - 1.5)}" y="{f(base - w * 1.2)}" width="3" height="{f(w * 1.2)}" fill="#7a6488"/>')
    dome(cx, base - w * 1.2, w * 1.05, w * 1.45, fill)


def windows(x0, x1, top, floors, bays, gap, lit=.8, outline=False):
    step = (x1 - x0) / bays
    shapes = []
    for floor in range(floors):
        for bay in range(bays):
            w = step * .5
            x = x0 + bay * step + (step - w) / 2
            y = top + 12 + floor * gap
            h = gap * .62
            r = w / 2
            path = f"M{f(x)} {f(y + h)} V{f(y + r)} A{f(r)} {f(r)} 0 0 1 {f(x + w)} {f(y + r)} V{f(y + h)}"
            fill = "url(#arch)" if rng.random() < lit else "#2a2244"
            shapes.append(f'<path d="{path} Z" fill="{fill}" opacity="{f(rng.uniform(.7, .95))}"/>')
            if outline:
                bulbs.append(path)
    add("<g>" + "".join(shapes) + "</g>")


# Wings, with pilasters traced in lights, chhatris along the parapet, and corner towers.
for x0, x1 in [(360, 600), (1000, 1240)]:
    block(x0, x1, 500)
    windows(x0, x1, 500, 3, 8, 38)
    add(f'<rect x="{x0 - 5}" y="493" width="{x1 - x0 + 10}" height="8" fill="#7a6488"/>')
    bulbs.append(poly([(x0 - 5, 493), (x1 + 5, 493)]))
    for k in range(1, 8):
        x = x0 + (x1 - x0) * k / 8
        bulbs.append(poly([(x, GROUND), (x, 500)]))
    for k in range(1, 4):
        chhatri(x0 + (x1 - x0) * k / 4, 493, 9)
for x0, x1 in [(340, 400), (1200, 1260)]:
    block(x0, x1, 440)
    windows(x0, x1, 440, 5, 2, 36)
    add(f'<rect x="{x0 - 5}" y="434" width="{x1 - x0 + 10}" height="7" fill="#7a6488"/>')
    dome((x0 + x1) / 2, 434, 26, 50, "url(#rose)")

# Flanking towers with rose domes.
for x0, x1 in [(555, 640), (960, 1045)]:
    block(x0, x1, 405)
    windows(x0, x1, 405, 6, 2, 36, lit=.85)
    add(f'<rect x="{x0 - 6}" y="399" width="{x1 - x0 + 12}" height="8" fill="#7a6488"/>')
    dome((x0 + x1) / 2, 399, 36, 66, "url(#rose)")
    chhatri(x0 + 4, 399, 7)
    chhatri(x1 - 4, 399, 7)

# The central block: the seven-arch portico, then the tower rising in tiers to the gilded dome.
block(640, 960, 455)
add('<rect x="640" y="535" width="320" height="8" fill="#7a6488"/>')
bulbs.append(poly([(640, 535), (960, 535)]))
portico = []
for k in range(7):
    w = 34 if k != 3 else 50
    cx = 800 + (k - 3) * 44
    if k > 3:
        cx += 8
    elif k < 3:
        cx -= 8
    x = cx - w / 2
    top = 560 if k != 3 else 548
    r = w / 2
    path = f"M{f(x)} {GROUND} V{f(top + r)} A{f(r)} {f(r)} 0 0 1 {f(x + w)} {f(top + r)} V{GROUND}"
    portico.append(f'<path d="{path} Z" fill="url(#arch)" opacity=".92"/>')
    bulbs.append(path)
add("<g>" + "".join(portico) + "</g>")
windows(650, 950, 455, 2, 8, 38, lit=.9)
tier(675, 925, 395, 455)
windows(675, 925, 395, 1, 7, 40, lit=.95)
tier(705, 895, 345, 395)
windows(705, 895, 345, 1, 6, 38, lit=.95)
tier(732, 868, 302, 345)
windows(732, 868, 302, 1, 5, 34, lit=.95)
tier(756, 844, 270, 302)
for k in range(5):
    add(f'<rect x="{762 + k * 17}" y="276" width="8" height="20" rx="4" fill="url(#arch)"/>')
dome(800, 264, 54, 92, "url(#gilt)")
chhatri(690, 389, 9)
chhatri(910, 389, 9)
chhatri(718, 339, 7)
chhatri(882, 339, 7)
add('<rect x="728" y="462" width="144" height="20" rx="3" fill="#2a1f52" stroke="#e6b24f" stroke-width="1.5"/>')
add('<text x="800" y="476.5" text-anchor="middle" font-family="Georgia, \'Times New Roman\', serif" font-size="12" letter-spacing="3.5" fill="#ffe1a8">MYSORE GRAND</text>')

# Every edge traced in bulbs, then the lamps along the forecourt.
add(f'<path d="{" ".join(bulbs)}" fill="none" stroke="#fff3c9" stroke-width="2.6" stroke-linecap="round" stroke-dasharray="0 6" filter="url(#glow)"/>')
add('<g fill="#ffd894" filter="url(#glow)">')
for i in range(31):
    add(f'<circle cx="{f(290 + i * 34)}" cy="{GROUND - 3}" r="2" opacity=".9"/>')
add("</g>")


def palm(x, h, lean):
    top_x, top_y = x + lean, GROUND - h
    add(f'<path d="M{f(x - 4)} {GROUND} Q {f(x + lean * .2)} {f(GROUND - h * .5)} {f(top_x - 2)} {f(top_y)} L{f(top_x + 2)} {f(top_y)} '
        f'Q {f(x + lean * .2 + 6)} {f(GROUND - h * .5)} {f(x + 4)} {GROUND} Z" fill="#0b0a22"/>')
    fronds = []
    for angle, length in [(-160, 70), (-130, 78), (-100, 60), (-70, 64), (-40, 80), (-15, 70), (190, 60), (10, 52)]:
        a = math.radians(angle)
        ex, ey = top_x + math.cos(a) * length, top_y + math.sin(a) * length * .55 + length * .35
        cx, cy = top_x + math.cos(a) * length * .5, top_y + math.sin(a) * length * .5 - 14
        fronds.append(f"M{f(top_x)} {f(top_y)} Q {f(cx)} {f(cy)} {f(ex)} {f(ey)}")
    add(f'<path d="{" ".join(fronds)}" fill="none" stroke="#0b0a22" stroke-width="7" stroke-linecap="round"/>')


for x, h, lean in [(60, 220, 18), (140, 170, -12), (225, 240, 10), (290, 150, -8), (1320, 160, 10), (1400, 250, -16), (1480, 190, 10), (1555, 235, -12)]:
    palm(x + rng.uniform(-6, 6), h, lean)
add("</g>")

# The palace pool, the reflection, and long ripples of gold.
add(f'<rect y="{HORIZON}" width="{W}" height="{H - HORIZON}" fill="url(#water)"/>')
add(f'<g opacity=".5" filter="url(#ripple)"><use href="#scene" transform="translate(0 {2 * HORIZON}) scale(1 -1)"/></g>')
add('<g stroke="#ffd894" stroke-linecap="round">')
for _ in range(60):
    y = rng.uniform(HORIZON + 8, H - 20)
    spread = (y - HORIZON) / (H - HORIZON)
    x = 800 + rng.gauss(0, 110 + 200 * spread)
    length = rng.uniform(14, 60) * (1 + spread)
    add(f'<line x1="{f(x - length / 2)}" y1="{f(y)}" x2="{f(x + length / 2)}" y2="{f(y)}" stroke-width="{f(rng.uniform(1, 2.4))}" opacity="{f(rng.uniform(.12, .45))}"/>')
add("</g>")
add("</svg>")

target = pathlib.Path(__file__).resolve().parent.parent / "src" / "mysore-grand.svg"
target.write_text("\n".join(out) + "\n", encoding="utf-8", newline="\n")
print(target, target.stat().st_size)

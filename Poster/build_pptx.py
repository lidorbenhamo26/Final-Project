"""Build the editable PPTX twin of the Mission Focus A0 poster (for Canva import).
Same theme/content as MissionFocus_Poster_A0.html, rebuilt as native shapes + text.
Run:  python build_pptx.py
"""
import os
from pptx import Presentation
from pptx.util import Mm, Pt
from pptx.dml.color import RGBColor
from pptx.enum.text import PP_ALIGN, MSO_ANCHOR
from pptx.enum.shapes import MSO_SHAPE
from pptx.enum.lang import MSO_LANGUAGE_ID

HERE = os.path.dirname(os.path.abspath(__file__))
A = lambda name: os.path.join(HERE, "Assets", name)

# ---------- palette ----------
BG      = RGBColor(0x06, 0x0D, 0x1F)
CARD    = RGBColor(0x0A, 0x14, 0x2C)
CARD2   = RGBColor(0x12, 0x24, 0x48)
EDGE    = RGBColor(0x3E, 0x6F, 0xA0)
INK     = RGBColor(0xE8, 0xF0, 0xFF)
SOFT    = RGBColor(0xDF, 0xE9, 0xFB)
CYAN    = RGBColor(0x4F, 0xD8, 0xFF)
CYANSOFT= RGBColor(0x9F, 0xDC, 0xFF)
ORANGE  = RGBColor(0xFF, 0x9B, 0x42)
GREEN   = RGBColor(0x5E, 0xE6, 0xA8)
WHITE   = RGBColor(0xFF, 0xFF, 0xFF)
NAVY    = RGBColor(0x06, 0x12, 0x1F)
# report-panel light palette
RP_BG   = RGBColor(0xF2, 0xF6, 0xFB)
RP_TILE = RGBColor(0xFF, 0xFF, 0xFF)
RP_LINE = RGBColor(0xD2, 0xDC, 0xEA)
RP_NAVY = RGBColor(0x0F, 0x17, 0x2A)
RP_GRAY = RGBColor(0x47, 0x55, 0x69)
RP_MUTE = RGBColor(0x64, 0x74, 0x8B)
RP_BLUE = RGBColor(0x25, 0x63, 0xEB)
RP_GREEN= RGBColor(0x16, 0xA3, 0x4A)

ORB = "Orbitron"
OS_ = "Open Sans"

prs = Presentation()
prs.slide_width = Mm(841)
prs.slide_height = Mm(1189)
slide = prs.slides.add_slide(prs.slide_layouts[6])  # blank


def no_line(shape):
    shape.line.fill.background()


def rect(x, y, w, h, fill, line=None, line_w=0.7, radius=None):
    shp_type = MSO_SHAPE.ROUNDED_RECTANGLE if radius is not None else MSO_SHAPE.RECTANGLE
    s = slide.shapes.add_shape(shp_type, Mm(x), Mm(y), Mm(w), Mm(h))
    if radius is not None:
        try:
            s.adjustments[0] = radius
        except Exception:
            pass
    if fill is None:
        s.fill.background()
    else:
        s.fill.solid(); s.fill.fore_color.rgb = fill
    if line is None:
        no_line(s)
    else:
        s.line.color.rgb = line; s.line.width = Pt(line_w)
    s.shadow.inherit = False
    return s


def text(x, y, w, h, runs_lines, align=PP_ALIGN.LEFT, anchor=MSO_ANCHOR.TOP,
         line_spacing=1.18, space_after=4):
    """runs_lines: list of paragraphs; each paragraph = list of run dicts
    {t, font, size, color, bold, italic, spacing}"""
    tb = slide.shapes.add_textbox(Mm(x), Mm(y), Mm(w), Mm(h))
    tf = tb.text_frame
    tf.word_wrap = True
    tf.vertical_anchor = anchor
    tf.margin_left = tf.margin_right = tf.margin_top = tf.margin_bottom = 0
    for i, para in enumerate(runs_lines):
        p = tf.paragraphs[0] if i == 0 else tf.add_paragraph()
        p.alignment = align
        p.line_spacing = line_spacing
        p.space_after = Pt(space_after)
        for r in para:
            run = p.add_run()
            run.text = r["t"]
            f = run.font
            f.name = r.get("font", OS_)
            f.size = Pt(r.get("size", 18))
            f.color.rgb = r.get("color", SOFT)
            f.bold = r.get("bold", False)
            f.italic = r.get("italic", False)
            f.language_id = MSO_LANGUAGE_ID.ENGLISH_US
    return tb


def R(t, **kw):
    d = {"t": t}; d.update(kw); return d


def sec_title(x, y, w, num, title, title_size=31):
    runs = []
    if num:
        runs.append(R(num + "  ", font=ORB, size=20, color=CYAN, bold=True))
    runs.append(R(title.upper(), font=ORB, size=title_size, color=WHITE, bold=True))
    text(x, y, w, 18, [runs])


# ============ BACKGROUND ============
rect(0, 0, 841, 1189, BG)
# stars
import random
random.seed(26117)
for _ in range(220):
    sx, sy = random.uniform(2, 839), random.uniform(2, 1185)
    sr = random.uniform(0.35, 0.9)
    c = random.choice([WHITE, CYANSOFT, RGBColor(0xBE, 0xE6, 0xFF)])
    st = slide.shapes.add_shape(MSO_SHAPE.OVAL, Mm(sx), Mm(sy), Mm(sr), Mm(sr))
    st.fill.solid(); st.fill.fore_color.rgb = c
    no_line(st); st.shadow.inherit = False

# ============ HEADER ============
# logo chip
rect(22, 16, 96, 40, WHITE, radius=0.18)
slide.shapes.add_picture(A("braude_logo.png"), Mm(29), Mm(21), height=Mm(30))
# badges
rect(540, 18, 168, 16.5, CARD, line=EDGE, radius=0.5)
text(540, 21.6, 168, 11, [[R("CAPSTONE PROJECT — PHASE B", font=ORB, size=16, color=CYANSOFT, bold=True)]], align=PP_ALIGN.CENTER)
rect(714, 18, 105, 16.5, CYAN, radius=0.5)
text(714, 21.6, 105, 11, [[R("TEAM 26-1-D-17", font=ORB, size=16, color=NAVY, bold=True)]], align=PP_ALIGN.CENTER)

# title block
text(22, 64, 600, 60, [[
    R("MISSION ", font=ORB, size=128, color=WHITE, bold=True),
    R("FOCUS", font=ORB, size=128, color=CYAN, bold=True),
]], line_spacing=1.0)
text(22, 121, 600, 20, [[
    R("A SPACE-STATION COGNITIVE ASSESSMENT GAME ", font=OS_, size=37, color=CYANSOFT, bold=True),
    R("FOR ADHD", font=OS_, size=37, color=ORANGE, bold=True),
]])
text(22, 147, 600, 18, [[
    R("Lidor Ben Hamo ", font=OS_, size=34, color=INK, bold=True),
    R("& ", font=OS_, size=34, color=ORANGE, bold=True),
    R("Yahli Rapaport", font=OS_, size=34, color=INK, bold=True),
]])
text(22, 167, 700, 14, [[
    R("Advisor: ", font=OS_, size=25, color=SOFT),
    R("Dr. Moshe Sulamy", font=OS_, size=25, color=WHITE, bold=True),
    R("   ·   Department of Software Engineering & Information Systems", font=OS_, size=25, color=SOFT),
]])

# ============ COLUMN GEOMETRY ============
LX, CX, RX = 22, 282, 569
LW, CW, RW = 250, 277, 250
TOP, BOTTOM = 200, 1075


def stack(x, w, cards):
    """cards: list of (height, draw_fn(x, y, w, h)). Distributes leftover as equal gaps.
    Never exceeds BOTTOM: gap floors at 3mm and a warning is printed if cards over-fill."""
    total = sum(h for h, _ in cards)
    gaps = len(cards) - 1
    leftover = BOTTOM - TOP - total
    if gaps and leftover < 3 * gaps:
        print(f"WARNING: column at x={x} over budget by {3 * gaps - leftover:.0f}mm — shrink card heights")
    gap = max(3, leftover / gaps) if gaps else 0
    y = TOP
    for h, fn in cards:
        rect(x, y, w, h, CARD, line=EDGE, radius=0.045)
        fn(x, y, w, h)
        y += h + gap


PAD = 11  # card padding


def bullets(x, y, w, items, size=20, gap=6, marker="◆", mcolor=CYAN, msize=None):
    paras = []
    for runs in items:
        paras.append([R(marker + "  ", font=OS_, size=msize or size, color=mcolor, bold=True)] + runs)
    text(x, y, w, 200, paras, space_after=gap, line_spacing=1.2)


# ---------- LEFT ----------
def bg_need(x, y, w, h):
    sec_title(x + PAD, y + PAD, w, "01", "Background & Need")
    text(x + PAD, y + PAD + 19, w - 2 * PAD, 24, [[
        R("Executive-function assessment still relies on ", size=25),
        R("questionnaires (BRIEF-A)", size=25, color=WHITE, bold=True),
        R(" and traditional lab-style tests:", size=25),
    ]])
    bullets(x + PAD, y + PAD + 51, w - 2 * PAD, [
        [R("Stressful & repetitive", size=24, color=WHITE, bold=True), R(" — scores may reflect motivation, not true ability", size=24)],
        [R("Low engagement", size=24, color=WHITE, bold=True), R(", especially for young users", size=24)],
        [R("Subjective ratings", size=24, color=WHITE, bold=True), R(" — no objective behavioral data", size=24)],
    ], size=24, gap=6)
    rect(x + PAD, y + h - 44, w - 2 * PAD, 32, RGBColor(0x33, 0x24, 0x1A), line=ORANGE, line_w=1.5, radius=0.12)
    text(x + PAD + 6, y + h - 40, w - 2 * PAD - 12, 24, [[
        R("The need: ", size=23, color=WHITE, bold=True),
        R("measurement that is objective, repeatable — and actually fun to take.",
          size=23, color=RGBColor(0xFF, 0xD9, 0xB3), bold=True)]], line_spacing=1.15)


def solution(x, y, w, h):
    sec_title(x + PAD, y + PAD, w, "02", "The Solution")
    text(x + PAD, y + PAD + 18, w - 2 * PAD, 26, [[
        R("Mission Focus turns assessment into a 10-minute spaceship mission.", size=25, color=WHITE, bold=True),
    ]], line_spacing=1.2)
    text(x + PAD, y + PAD + 46, w - 2 * PAD, 24, [[
        R("Instead of asking the participant to describe behavior after the fact, the game ", size=22, color=INK),
        R("observes behavior while it happens", size=22, color=WHITE, bold=True),
        R(".", size=22, color=INK),
    ]], line_spacing=1.18)
    bullets(x + PAD, y + PAD + 76, w - 2 * PAD, [
        [R("More engaging", size=22, color=WHITE, bold=True), R(" — the player completes a mission, not a dry test", size=22, color=INK)],
        [R("Objective measurement", size=22, color=WHITE, bold=True), R(" — reactions, errors, choices and timing are logged automatically", size=22, color=INK)],
        [R("More realistic behavior", size=22, color=WHITE, bold=True), R(" — the player handles tasks, distractions and priorities inside one dynamic environment", size=22, color=INK)],
        [R("Assessor-ready output", size=22, color=WHITE, bold=True), R(" — the session becomes an executive-function profile + CSV", size=22, color=INK)],
    ], size=22, gap=5)
    rect(x + PAD, y + h - 46, w - 2 * PAD, 34, RGBColor(0x10, 0x2A, 0x44), line=CYAN, line_w=1.5, radius=0.1)
    text(x + PAD + 6, y + h - 42, w - 2 * PAD - 12, 26, [[
        R("Why it works: ", size=21.5, color=CYAN, bold=True),
        R("the player feels inside a mission, while the system quietly turns behavior into measurable data.",
          size=21.5, color=INK, bold=True)]], line_spacing=1.15)


def capabilities(x, y, w, h):
    sec_title(x + PAD, y + PAD, w, "03", "Core Capabilities")
    bullets(x + PAD, y + PAD + 20, w - 2 * PAD, [
        [R("Playable 10-minute mission", size=24, color=WHITE, bold=True), R(" — game-based, not a quiz", size=24)],
        [R("Measures key executive functions", size=24, color=WHITE, bold=True), R(" — attention, memory, inhibition & planning", size=24)],
        [R("Logs behavior automatically", size=24, color=WHITE, bold=True), R(" — reactions, errors, choices and timing", size=24)],
        [R("Generates assessor outputs", size=24, color=WHITE, bold=True), R(" — HTML report + CSV", size=24)],
        [R("Keeps data local", size=24, color=WHITE, bold=True), R(" — privacy by design", size=24)],
    ], size=24, gap=6, marker="✓", mcolor=GREEN)


def how_it_works(x, y, w, h):
    sec_title(x + PAD, y + PAD, w, "04", "How It Works")
    steps = [
        ("1", "Participant Onboarding", [R("Basic details are entered before the mission", size=21)]),
        ("2", "Interactive Tutorial", [R("The player learns movement and station controls", size=21)]),
        ("3", "10-Minute Mission", [R("Tasks appear across ", size=21), R("4 stations", size=21, color=WHITE, bold=True),
                                    R(" while the player chooses priorities", size=21)]),
        ("4", "Background Logging", [R("Reactions, errors, choices and timing are recorded silently", size=21)]),
        ("5", "Assessor Report", [R("Executive-function profile + CSV", size=21, color=WHITE, bold=True),
                                  R(", generated instantly", size=21)]),
    ]
    sy = y + PAD + 21
    row_h = (h - PAD - 24 - PAD) / 5
    for i, (n, t, drs) in enumerate(steps):
        yy = sy + i * row_h
        if i < 4:
            rect(x + PAD + 8, yy + 18, 1, row_h - 16, CYAN)
        c = slide.shapes.add_shape(MSO_SHAPE.OVAL, Mm(x + PAD), Mm(yy), Mm(17), Mm(17))
        c.fill.solid(); c.fill.fore_color.rgb = RGBColor(0x14, 0x30, 0x5C)
        c.line.color.rgb = CYAN; c.line.width = Pt(2); c.shadow.inherit = False
        text(x + PAD, yy + 3, 17, 10, [[R(n, font=ORB, size=19, color=CYAN, bold=True)]], align=PP_ALIGN.CENTER)
        text(x + PAD + 24, yy - 1, w - 2 * PAD - 24, 12, [[R(t, size=23.5, color=WHITE, bold=True)]])
        text(x + PAD + 24, yy + 10.5, w - 2 * PAD - 24, row_h - 10, [drs], line_spacing=1.12)


# ---------- CENTER ----------
def tasks_filmstrip(x, y, w, h):
    sec_title(x + PAD, y + PAD, w, "05", "Playable Cognitive Tasks", title_size=28)
    from PIL import Image as PILImage
    cells = [
        ("RADAR SCAN", "SUSTAINED ATTENTION · INHIBITION",
         "Hits · False alarms · d′ sensitivity", A("shot_radar.png")),
        ("CODE RECALL", "WORKING MEMORY",
         "Recall accuracy · Typos · Timeout", A("shot_station.png")),
        ("STROOP CONSOLE", "INHIBITION",
         "RT mean/SD · Commissions · Post-error slowing", A("shot_stroop_clean.png")),
        ("POWER CELL RUN", "PLANNING · ORGANIZATION",
         "Pickup latency · Wiring errors · Completion time", A("shot_wires.png")),
    ]
    cw = w - 2 * PAD
    meta_h = 34
    gap = 9
    ch = (h - PAD - 20 - PAD - 3 * gap - 4 * meta_h) / 4
    yy = y + PAD + 20
    for name, dom, mes, img in cells:
        pic = slide.shapes.add_picture(img, Mm(x + PAD), Mm(yy), Mm(cw), Mm(ch))
        iw, ih = PILImage.open(img).size  # crop source to cell aspect (center)
        src_ar, cell_ar = iw / ih, cw / ch
        if src_ar > cell_ar:
            cut = (1 - cell_ar / src_ar) / 2
            pic.crop_left = cut; pic.crop_right = cut
        else:
            cut = (1 - src_ar / cell_ar) / 2
            pic.crop_top = cut; pic.crop_bottom = cut
        pic.line.color.rgb = EDGE; pic.line.width = Pt(1.4)
        text(x + PAD, yy + ch + 3, cw, 13, [[
            R(name, font=ORB, size=24, color=WHITE, bold=True),
            R("   " + dom, font=OS_, size=18, color=CYANSOFT, bold=True),
        ]])
        text(x + PAD, yy + ch + 17, cw, 14, [[
            R("Measures: ", size=20.5, color=ORANGE, bold=True),
            R(mes, size=20.5, color=WHITE, bold=True),
        ]])
        yy += ch + meta_h + gap


# ---------- RIGHT ----------
def tech(x, y, w, h):
    sec_title(x + PAD, y + PAD, w, "06", "Tech Stack")
    rows = [
        ("Unity 6 + URP", "3D spaceship mission", CYAN),
        ("C#", "gameplay, tasks & data logging", ORANGE),
        ("HTML + CSV", "assessor report outputs", GREEN),
        ("GitHub", "version control & collaboration", CYAN),
        ("AI Tools", "voice-over and 3D asset support", ORANGE),
    ]
    rh = (h - PAD - 22 - PAD - 4 * 6) / 5
    yy = y + PAD + 20
    for name, desc, dc in rows:
        rect(x + PAD, yy, w - 2 * PAD, rh, RGBColor(0x0A, 0x15, 0x2E), line=EDGE, radius=0.22)
        rect(x + PAD + 5, yy + rh / 2 - 3.6, 7.2, 7.2, dc, radius=0.3)
        text(x + PAD + 17, yy + rh / 2 - 6, w - 2 * PAD - 23, 11, [[
            R(name, size=23.5, color=WHITE, bold=True),
            R("  — " + desc, size=21, color=SOFT),
        ]])
        yy += rh + 6


def challenges(x, y, w, h):
    sec_title(x + PAD, y + PAD, w, "07", "Design Challenges")
    items = [
        ("01", [R("Fun vs. valid data", size=23, color=WHITE, bold=True), R(" — the mission feels playful, while console tasks stay precisely timed", size=23)]),
        ("02", [R("Invisible measurement", size=23, color=WHITE, bold=True), R(" — the player focuses on the game while behavior is logged silently", size=23)]),
        ("03", [R("Modular task design", size=23, color=WHITE, bold=True), R(" — new cognitive tasks can be added without rebuilding the system", size=23)]),
        ("04", [R("3D asset integration", size=23, color=WHITE, bold=True), R(" — AI-generated props needed Unity cleanup and adaptation", size=23)]),
    ]
    paras = [[R(n + "  ", font=ORB, size=18, color=ORANGE, bold=True)] + rs for n, rs in items]
    text(x + PAD, y + PAD + 20, w - 2 * PAD, h - PAD - 22 - PAD, paras, space_after=9, line_spacing=1.24)


def report_card(x, y, w, h):
    sec_title(x + PAD, y + PAD, w, "08", "The Assessor Report")
    text(x + PAD, y + PAD + 19, w - 2 * PAD, 36, [[
        R("Every mission ends with a ", size=22.5, color=INK),
        R("clear, auto-generated profile", size=22.5, color=WHITE, bold=True),
        R(" — pass/fail per cognitive domain, plus a CSV with ", size=22.5, color=INK),
        R("~26 raw metrics", size=22.5, color=WHITE, bold=True),
        R(" for research.", size=22.5, color=INK),
    ]], line_spacing=1.2)
    # light report panel
    px, pw = x + PAD, w - 2 * PAD
    py = y + PAD + 60
    ph_ = h - PAD - 60 - 16 - PAD
    rect(px, py, pw, ph_, RP_BG, radius=0.04)
    ip = 6  # inner padding
    cx0 = px + ip
    cw0 = pw - 2 * ip
    # header row
    text(cx0, py + ip, cw0 - 58, 11, [[R("EXECUTIVE FUNCTION REPORT", size=19, color=RP_NAVY, bold=True)]])
    rect(cx0 + cw0 - 56, py + ip - 1, 56, 10.5, RP_BLUE, radius=0.25)
    text(cx0 + cw0 - 56, py + ip + 1.2, 56, 8, [[R("AUTO-GENERATED", size=13, color=WHITE, bold=True)]], align=PP_ALIGN.CENTER)
    rect(cx0, py + ip + 12.5, cw0, 0.5, RP_LINE)
    # summary tiles
    ty = py + ip + 16.5
    tile_h = 24
    tile_w = (cw0 - 2 * 5) / 3
    sums = [("6", "TASKS", RP_NAVY), ("5", "PASSED", RP_GREEN), ("83%", "ACCURACY", RP_NAVY)]
    for i, (v, l, c) in enumerate(sums):
        tx = cx0 + i * (tile_w + 5)
        rect(tx, ty, tile_w, tile_h, RP_TILE, line=RP_LINE, line_w=1.0, radius=0.12)
        text(tx, ty + 2.5, tile_w, 13, [[R(v, size=30, color=c, bold=True)]], align=PP_ALIGN.CENTER)
        text(tx, ty + 16, tile_w, 8, [[R(l, size=14.5, color=RP_MUTE, bold=True)]], align=PP_ALIGN.CENTER)
    # domain rows
    rows = [
        ("INHIBIT", "Stroop console · 100% accuracy · RT 1563 ms"),
        ("WORKING MEMORY", "Code recall · 0 wrong keys"),
        ("TASK MONITOR", "Radar scan · d′ 3.17 · 0 false alarms"),
        ("PLAN / ORGANIZE", "Power cell run · delivered on time"),
    ]
    ry = ty + tile_h + 5
    row_h = (py + ph_ - ip - ry - 3 * 4.5) / 4
    for dom, met in rows:
        rect(cx0, ry, cw0, row_h, RP_TILE, line=RP_LINE, line_w=1.0, radius=0.1)
        rect(cx0, ry, 1.8, row_h, RP_BLUE)
        text(cx0 + 5.5, ry + 2.5, cw0 - 40, 11, [[R(dom, size=19.5, color=RP_NAVY, bold=True)]])
        text(cx0 + 5.5, ry + 13.5, cw0 - 40, 9, [[R(met, size=16, color=RP_GRAY)]])
        rect(cx0 + cw0 - 28, ry + row_h / 2 - 4.5, 23, 9, RP_GREEN, radius=0.5)
        text(cx0 + cw0 - 28, ry + row_h / 2 - 3, 23, 7, [[R("PASS", size=14.5, color=WHITE, bold=True)]], align=PP_ALIGN.CENTER)
        ry += row_h + 4.5
    # caption
    text(px, y + h - PAD - 12, pw, 12, [[
        R("Sample from a real session", size=18.5, color=WHITE, bold=True),
        R(" — the full HTML report opens in any browser.", size=18.5, color=SOFT),
    ]])


def qr_row(x, y, w, h):
    rect(x + PAD - 4, y + (h - 82) / 2, 82, 82, WHITE, radius=0.08)
    slide.shapes.add_picture(A("qr_github.png"), Mm(x + PAD), Mm(y + (h - 82) / 2 + 4), Mm(74), Mm(74))
    tx = x + PAD + 86
    tw = w - PAD - 96
    text(tx, y + 12, tw, 13, [[R("FULL CODE & DOCS", font=ORB, size=21, color=WHITE, bold=True)]])
    text(tx, y + 27, tw, 30, [[R("github.com/ lidorbenhamo26/ Final-Project", size=19.5, color=WHITE, bold=True)]], line_spacing=1.15)
    text(tx, y + 66, tw, 22, [[R("Unity project · project book · demo video", size=18, color=SOFT)]], line_spacing=1.15)


# ---------- build columns ----------
stack(LX, LW, [
    (185, bg_need),
    (290, solution),
    (165, capabilities),
    (225, how_it_works),
])
stack(CX, CW, [
    (BOTTOM - TOP, tasks_filmstrip),
])
stack(RX, RW, [
    (230, tech),
    (210, challenges),
    (295, report_card),
    (104, qr_row),
])

# ============ TAKEAWAY BANNER ============
BNY = 1085
rect(21, BNY - 1, 799, 66, CYAN, radius=0.09)
rect(22.2, BNY + 0.2, 796.6, 63.6, RGBColor(0x0B, 0x18, 0x34), radius=0.09)
text(22, BNY + 8, 700, 24, [[
    R("Not a test — ", font=ORB, size=53, color=WHITE, bold=True),
    R("a mission that measures.", font=ORB, size=53, color=CYAN, bold=True),
]], align=PP_ALIGN.CENTER, line_spacing=1.0)
text(22, BNY + 42, 700, 14, [[
    R("A ", size=24, color=SOFT),
    R("10-minute space mission", size=24, color=WHITE, bold=True),
    R(" produces an ", size=24, color=SOFT),
    R("executive-function profile", size=24, color=WHITE, bold=True),
    R(" while keeping the player engaged.", size=24, color=SOFT),
]], align=PP_ALIGN.CENTER)
# wave astronaut + bubble (inside banner, right side; bubble snug to astronaut)
rect(668, BNY + 14, 84, 14, RGBColor(0x10, 0x2A, 0x44), line=CYANSOFT, radius=0.5)
text(668, BNY + 17.2, 84, 9, [[R("Ready for launch!", size=15.5, color=CYANSOFT, bold=True)]], align=PP_ALIGN.CENTER)
slide.shapes.add_picture(A("char_wave.png"), Mm(754), Mm(BNY + 4), height=Mm(58))

# ============ FOOTER ============
rect(0, 1168, 841, 21, RGBColor(0x05, 0x0B, 0x1A), line=None)
rect(0, 1168, 841, 0.8, EDGE)
text(0, 1173.5, 841, 12, [[
    R("Braude College of Engineering, Karmiel", size=17.5, color=WHITE, bold=True),
    R("   ◆   Department of Software Engineering & Information Systems   ◆   Capstone Project 61999 — Phase B   ◆   ", size=17.5, color=SOFT),
    R("2026", size=17.5, color=WHITE, bold=True),
]], align=PP_ALIGN.CENTER)

# ============ GAME LOGO + HERO ASTRONAUT (on top, header right) ============
# astronaut sits left of the logo (raised) so the emblem badge stays fully visible
slide.shapes.add_picture(A("logo_game.png"), Mm(674), Mm(56), height=Mm(138))
slide.shapes.add_picture(A("char_hero.png"), Mm(553), Mm(70), height=Mm(112))

out = os.path.join(HERE, "MissionFocus_Poster_A0.pptx")
prs.save(out)
print("saved", out)

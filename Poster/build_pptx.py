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


def sec_title(x, y, w, num, title, title_size=27):
    runs = []
    if num:
        runs.append(R(num + "  ", font=ORB, size=17, color=CYAN, bold=True))
    runs.append(R(title.upper(), font=ORB, size=title_size, color=WHITE, bold=True))
    text(x, y, w, 16, [runs])


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
rect(548, 18, 162, 15.5, CARD, line=EDGE, radius=0.5)
text(548, 21.4, 162, 10, [[R("CAPSTONE PROJECT — PHASE B", font=ORB, size=15, color=CYANSOFT, bold=True)]], align=PP_ALIGN.CENTER)
rect(716, 18, 103, 15.5, CYAN, radius=0.5)
text(716, 21.4, 103, 10, [[R("TEAM 26-1-D-17", font=ORB, size=15, color=NAVY, bold=True)]], align=PP_ALIGN.CENTER)

# title block
text(22, 64, 600, 60, [[
    R("MISSION ", font=ORB, size=128, color=WHITE, bold=True),
    R("FOCUS", font=ORB, size=128, color=CYAN, bold=True),
]], line_spacing=1.0)
text(22, 120, 600, 20, [[R("A SPACE-STATION COGNITIVE ASSESSMENT GAME", font=OS_, size=37, color=CYANSOFT, bold=True)]])
text(22, 146, 600, 18, [[
    R("Lidor Ben Hamo ", font=OS_, size=33, color=INK, bold=True),
    R("& ", font=OS_, size=33, color=ORANGE, bold=True),
    R("Yahli Rapaport", font=OS_, size=33, color=INK, bold=True),
]])
text(22, 165, 700, 14, [[
    R("Advisor: ", font=OS_, size=24, color=SOFT),
    R("Dr. Moshe Sulamy", font=OS_, size=24, color=WHITE, bold=True),
    R("   ·   Department of Software Engineering & Information Systems", font=OS_, size=24, color=SOFT),
]])

# hero ring + astronaut
ring = slide.shapes.add_shape(MSO_SHAPE.OVAL, Mm(648), Mm(28), Mm(138), Mm(138))
ring.fill.solid(); ring.fill.fore_color.rgb = RGBColor(0x0B, 0x1E, 0x3E)
ring.line.color.rgb = EDGE; ring.line.width = Pt(2); ring.shadow.inherit = False
slide.shapes.add_picture(A("char_hero.png"), Mm(642), Mm(40), height=Mm(148))

# ============ COLUMN GEOMETRY ============
LX, CX, RX = 22, 282, 569
LW, CW, RW = 250, 277, 250
TOP, BOTTOM = 200, 1012


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
    text(x + PAD, y + PAD + 17, w - 2 * PAD, 24, [[
        R("Executive-function assessment still relies on ", size=24),
        R("questionnaires (BRIEF-A)", size=24, color=WHITE, bold=True),
        R(" and traditional lab-style tests:", size=24),
    ]])
    bullets(x + PAD, y + PAD + 47, w - 2 * PAD, [
        [R("Stressful & repetitive", size=23.5, color=WHITE, bold=True), R(" — scores may reflect motivation, not true ability", size=23.5)],
        [R("Low engagement", size=23.5, color=WHITE, bold=True), R(", especially for young users", size=23.5)],
        [R("Subjective ratings", size=23.5, color=WHITE, bold=True), R(" — no objective behavioral data", size=23.5)],
    ], size=23.5, gap=6)
    rect(x + PAD, y + h - 42, w - 2 * PAD, 30, RGBColor(0x33, 0x24, 0x1A), line=ORANGE, line_w=1.5, radius=0.12)
    text(x + PAD + 6, y + h - 38, w - 2 * PAD - 12, 22, [[
        R("The need: ", size=22, color=WHITE, bold=True),
        R("measurement that is objective, repeatable — and actually fun to take.",
          size=22, color=RGBColor(0xFF, 0xD9, 0xB3), bold=True)]], line_spacing=1.15)


def solution(x, y, w, h):
    sec_title(x + PAD, y + PAD, w, "02", "The Solution")
    text(x + PAD, y + PAD + 16, w - 2 * PAD, 26, [[
        R("Mission Focus turns assessment into a 10-minute spaceship mission.", size=24, color=WHITE, bold=True),
    ]], line_spacing=1.2)
    text(x + PAD, y + PAD + 42, w - 2 * PAD, 24, [[
        R("Instead of asking the participant to describe behavior after the fact, the game ", size=21, color=INK),
        R("observes behavior while it happens", size=21, color=WHITE, bold=True),
        R(".", size=21, color=INK),
    ]], line_spacing=1.18)
    bullets(x + PAD, y + PAD + 70, w - 2 * PAD, [
        [R("More engaging", size=21, color=WHITE, bold=True), R(" — the player completes a mission, not a dry test", size=21, color=INK)],
        [R("Objective measurement", size=21, color=WHITE, bold=True), R(" — reactions, errors, choices and timing are logged automatically", size=21, color=INK)],
        [R("More realistic behavior", size=21, color=WHITE, bold=True), R(" — the player handles tasks, distractions and priorities inside one dynamic environment", size=21, color=INK)],
        [R("Assessor-ready output", size=21, color=WHITE, bold=True), R(" — the session becomes an executive-function profile + CSV", size=21, color=INK)],
    ], size=21, gap=5)
    rect(x + PAD, y + h - 44, w - 2 * PAD, 32, RGBColor(0x10, 0x2A, 0x44), line=CYAN, line_w=1.5, radius=0.1)
    text(x + PAD + 6, y + h - 40, w - 2 * PAD - 12, 24, [[
        R("Why it works: ", size=20.5, color=CYAN, bold=True),
        R("the player feels inside a mission, while the system quietly turns behavior into measurable data.",
          size=20.5, color=INK, bold=True)]], line_spacing=1.15)


def capabilities(x, y, w, h):
    sec_title(x + PAD, y + PAD, w, "03", "Core Capabilities")
    bullets(x + PAD, y + PAD + 18, w - 2 * PAD, [
        [R("Playable 10-minute mission", size=23.5, color=WHITE, bold=True), R(" — game-based, not a quiz", size=23.5)],
        [R("Measures key executive functions", size=23.5, color=WHITE, bold=True), R(" — attention, memory, inhibition & planning", size=23.5)],
        [R("Logs behavior automatically", size=23.5, color=WHITE, bold=True), R(" — reactions, errors, choices and timing", size=23.5)],
        [R("Generates assessor outputs", size=23.5, color=WHITE, bold=True), R(" — HTML report + CSV", size=23.5)],
        [R("Keeps data local", size=23.5, color=WHITE, bold=True), R(" — privacy by design", size=23.5)],
    ], size=23.5, gap=6, marker="✓", mcolor=GREEN)


def how_it_works(x, y, w, h):
    sec_title(x + PAD, y + PAD, w, "04", "How It Works")
    steps = [
        ("1", "Participant Onboarding", [R("Basic details are entered before the mission", size=20)]),
        ("2", "Interactive Tutorial", [R("The player learns movement and station controls", size=20)]),
        ("3", "10-Minute Mission", [R("Tasks appear across ", size=20), R("4 stations", size=20, color=WHITE, bold=True),
                                    R(" while the player chooses priorities", size=20)]),
        ("4", "Background Logging", [R("Reactions, errors, choices and timing are recorded silently", size=20)]),
        ("5", "Assessor Report", [R("Executive-function profile + CSV", size=20, color=WHITE, bold=True),
                                  R(", generated instantly", size=20)]),
    ]
    sy = y + PAD + 19
    row_h = (h - PAD - 22 - PAD) / 5
    for i, (n, t, drs) in enumerate(steps):
        yy = sy + i * row_h
        if i < 4:
            rect(x + PAD + 7.5, yy + 17, 1, row_h - 15, CYAN)
        c = slide.shapes.add_shape(MSO_SHAPE.OVAL, Mm(x + PAD), Mm(yy), Mm(16), Mm(16))
        c.fill.solid(); c.fill.fore_color.rgb = RGBColor(0x14, 0x30, 0x5C)
        c.line.color.rgb = CYAN; c.line.width = Pt(2); c.shadow.inherit = False
        text(x + PAD, yy + 2.6, 16, 10, [[R(n, font=ORB, size=18, color=CYAN, bold=True)]], align=PP_ALIGN.CENTER)
        text(x + PAD + 23, yy - 1, w - 2 * PAD - 23, 11, [[R(t, size=22, color=WHITE, bold=True)]])
        text(x + PAD + 23, yy + 9.5, w - 2 * PAD - 23, row_h - 9, [drs], line_spacing=1.12)


# ---------- CENTER ----------
def tasks_filmstrip(x, y, w, h):
    sec_title(x + PAD, y + PAD, w, "05", "Playable Cognitive Tasks")
    from PIL import Image as PILImage
    cells = [
        ("RADAR SCAN", "SUSTAINED ATTENTION · INHIBITION",
         "Hits · False alarms · d′ sensitivity", A("shot_radar.png")),
        ("CODE RECALL", "WORKING MEMORY",
         "Recall accuracy · Typos · Timeout", A("shot_station.png")),
        ("STROOP CONSOLE", "INHIBITION",
         "RT mean/SD · Commissions · Post-error slowing", A("shot_stroop.png")),
        ("POWER CELL RUN", "PLANNING · ORGANIZATION",
         "Pickup latency · Wiring errors · Completion time", A("shot_wires.png")),
    ]
    cw = w - 2 * PAD
    meta_h = 32
    gap = 9
    ch = (h - PAD - 19 - PAD - 3 * gap - 4 * meta_h) / 4
    yy = y + PAD + 19
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
        text(x + PAD, yy + ch + 3, cw, 12, [[
            R(name, font=ORB, size=22, color=WHITE, bold=True),
            R("   " + dom, font=OS_, size=16.5, color=CYANSOFT, bold=True),
        ]])
        text(x + PAD, yy + ch + 15.5, cw, 13, [[
            R("Measures: ", size=19, color=ORANGE, bold=True),
            R(mes, size=19, color=WHITE, bold=True),
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
    rh = (h - PAD - 20 - PAD - 4 * 4.5) / 5
    yy = y + PAD + 18
    for name, desc, dc in rows:
        rect(x + PAD, yy, w - 2 * PAD, rh, RGBColor(0x0A, 0x15, 0x2E), line=EDGE, radius=0.22)
        rect(x + PAD + 5, yy + rh / 2 - 3.4, 6.8, 6.8, dc, radius=0.3)
        text(x + PAD + 16, yy + rh / 2 - 5.5, w - 2 * PAD - 22, 10, [[
            R(name, size=21.5, color=WHITE, bold=True),
            R("  — " + desc, size=19.5, color=SOFT),
        ]])
        yy += rh + 4.5


def challenges(x, y, w, h):
    sec_title(x + PAD, y + PAD, w, "07", "Design Challenges")
    items = [
        ("01", [R("Fun vs. valid data", size=22, color=WHITE, bold=True), R(" — the mission feels playful, while console tasks stay precisely timed", size=22)]),
        ("02", [R("Invisible measurement", size=22, color=WHITE, bold=True), R(" — the player focuses on the game while behavior is logged silently", size=22)]),
        ("03", [R("Modular task design", size=22, color=WHITE, bold=True), R(" — new cognitive tasks can be added without rebuilding the system", size=22)]),
        ("04", [R("3D asset integration", size=22, color=WHITE, bold=True), R(" — AI-generated props needed Unity cleanup and adaptation", size=22)]),
    ]
    paras = [[R(n + "  ", font=ORB, size=17, color=ORANGE, bold=True)] + rs for n, rs in items]
    text(x + PAD, y + PAD + 18, w - 2 * PAD, h - PAD - 20 - PAD, paras, space_after=7, line_spacing=1.22)


def outcomes(x, y, w, h):
    sec_title(x + PAD, y + PAD, w, "08", "Outcomes & Future Work")
    items = [
        ("✔", GREEN, [R("Four cognitive tasks", size=22, color=WHITE, bold=True), R(" playable end-to-end", size=22)]),
        ("✔", GREEN, [R("~26 metrics per session", size=22, color=WHITE, bold=True), R(" — HTML report + CSV + raw log", size=22)]),
        ("✔", GREEN, [R("Verified pipeline", size=22, color=WHITE, bold=True), R(" — gameplay → logs → metrics → report", size=22)]),
        ("✔", GREEN, [R("Demo-ready", size=22, color=WHITE, bold=True), R(" — stable 10-minute missions", size=22)]),
        ("↻", ORANGE, [R("Lesson learned", size=22, color=WHITE, bold=True), R(" — playtest earlier", size=22)]),
        ("→", CYAN, [R("Future work", size=22, color=WHITE, bold=True), R(" — compare gameplay metrics with BRIEF-A in a controlled study", size=22)]),
    ]
    paras = [[R(m + "  ", size=22, color=c, bold=True)] + rs for m, c, rs in items]
    text(x + PAD, y + PAD + 18, w - 2 * PAD, 158, paras, space_after=6, line_spacing=1.2)
    # output preview: real assessor report excerpt
    from PIL import Image as PILImage
    img = A("shot_report.png")
    pw = w - 2 * PAD
    ph_ = h - PAD - 18 - 160 - 14 - PAD
    pic = slide.shapes.add_picture(img, Mm(x + PAD), Mm(y + PAD + 18 + 160), Mm(pw), Mm(ph_))
    iw, ih = PILImage.open(img).size
    src_ar, cell_ar = iw / ih, pw / ph_
    if src_ar > cell_ar:
        cut = (1 - cell_ar / src_ar) / 2
        pic.crop_left = cut; pic.crop_right = cut
    else:
        pic.crop_bottom = 1 - src_ar / cell_ar  # keep report header at top
    pic.line.color.rgb = EDGE; pic.line.width = Pt(1.2)
    text(x + PAD, y + h - PAD - 11, pw, 11, [[
        R("Assessor HTML report", size=18, color=WHITE, bold=True),
        R(" — generated after every mission", size=18, color=SOFT),
    ]])


def qr_row(x, y, w, h):
    rect(x + PAD - 4, y + 6, 48, 48, WHITE, radius=0.1)
    slide.shapes.add_picture(A("qr_github.png"), Mm(x + PAD - 1), Mm(y + 9), Mm(42), Mm(42))
    text(x + PAD + 50, y + 10, w - PAD - 60, 12, [[R("FULL CODE & DOCS", font=ORB, size=18, color=WHITE, bold=True)]])
    text(x + PAD + 50, y + 22, w - PAD - 60, 12, [[R("github.com/lidorbenhamo26/Final-Project", size=17, color=WHITE, bold=True)]])
    text(x + PAD + 50, y + 32, w - PAD - 60, 12, [[R("Unity project · project book · demo video", size=16, color=SOFT)]])


# ---------- build columns ----------
stack(LX, LW, [
    (165, bg_need),
    (256, solution),
    (140, capabilities),
    (215, how_it_works),
])
stack(CX, CW, [
    (BOTTOM - TOP, tasks_filmstrip),
])
stack(RX, RW, [
    (185, tech),
    (170, challenges),
    (350, outcomes),
    (66, qr_row),
])

# ============ TAKEAWAY BANNER ============
BNY = 1022
rect(21, BNY - 1, 799, 65, CYAN, radius=0.09)
rect(22.2, BNY + 0.2, 796.6, 62.6, RGBColor(0x0B, 0x18, 0x34), radius=0.09)
text(22, BNY + 7.5, 797, 24, [[
    R("Not a test — ", font=ORB, size=53, color=WHITE, bold=True),
    R("a mission that measures.", font=ORB, size=53, color=CYAN, bold=True),
]], align=PP_ALIGN.CENTER, line_spacing=1.0)
text(22, BNY + 41, 797, 14, [[
    R("A ", size=24, color=SOFT),
    R("10-minute space mission", size=24, color=WHITE, bold=True),
    R(" produces an ", size=24, color=SOFT),
    R("executive-function profile", size=24, color=WHITE, bold=True),
    R(" while keeping the player engaged.", size=24, color=SOFT),
]], align=PP_ALIGN.CENTER)

# ============ STATS BAND ============
BY = 1098
rect(22, BY, 797, 62, CARD, line=EDGE, radius=0.08)
stats = [("4", "COGNITIVE STATIONS", CYAN), ("10 MIN", "ONE FULL MISSION", ORANGE),
         ("~26", "METRICS PER SESSION", CYAN), ("3", "EXPORT ARTIFACTS", GREEN)]
cw_ = 150
for i, (v, l, c) in enumerate(stats):
    sx = 40 + i * cw_
    text(sx, BY + 8, cw_, 22, [[R(v, font=ORB, size=42, color=c, bold=True)]], align=PP_ALIGN.CENTER)
    text(sx, BY + 36, cw_, 12, [[R(l, size=17.5, color=SOFT, bold=True)]], align=PP_ALIGN.CENTER)
    if i:
        rect(sx - 2, BY + 10, 0.5, 42, EDGE)
# wave astronaut + bubble
slide.shapes.add_picture(A("char_wave.png"), Mm(742), Mm(BY - 14), height=Mm(74))
rect(650, BY + 5, 86, 14, RGBColor(0x10, 0x2A, 0x44), line=CYANSOFT, radius=0.5)
text(650, BY + 8.2, 86, 9, [[R("Ready for launch!", size=15.5, color=CYANSOFT, bold=True)]], align=PP_ALIGN.CENTER)

# ============ FOOTER ============
rect(0, 1168, 841, 21, RGBColor(0x05, 0x0B, 0x1A), line=None)
rect(0, 1168, 841, 0.8, EDGE)
text(0, 1173.5, 841, 12, [[
    R("Braude College of Engineering, Karmiel", size=17.5, color=WHITE, bold=True),
    R("   ◆   Department of Software Engineering & Information Systems   ◆   Capstone Project 61999 — Phase B   ◆   ", size=17.5, color=SOFT),
    R("2026", size=17.5, color=WHITE, bold=True),
]], align=PP_ALIGN.CENTER)

out = os.path.join(HERE, "MissionFocus_Poster_A0.pptx")
prs.save(out)
print("saved", out)

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
from pptx.enum.dml import MSO_LINE_DASH_STYLE
from pptx.oxml.ns import qn

HERE = os.path.dirname(os.path.abspath(__file__))
A = lambda name: os.path.join(HERE, "Assets", name)

# ---------- palette ----------
BG      = RGBColor(0x06, 0x0D, 0x1F)
CARD    = RGBColor(0x0E, 0x1B, 0x38)
CARD2   = RGBColor(0x12, 0x24, 0x48)
EDGE    = RGBColor(0x3E, 0x6F, 0xA0)
INK     = RGBColor(0xE8, 0xF0, 0xFF)
SOFT    = RGBColor(0xC2, 0xD2, 0xEF)
DIM     = RGBColor(0x8D, 0xA3, 0xC9)
CYAN    = RGBColor(0x4F, 0xD8, 0xFF)
CYANSOFT= RGBColor(0x9F, 0xDC, 0xFF)
ORANGE  = RGBColor(0xFF, 0x9B, 0x42)
VIOLET  = RGBColor(0xB1, 0x8C, 0xFF)
GREEN   = RGBColor(0x5E, 0xE6, 0xA8)
WHITE   = RGBColor(0xFF, 0xFF, 0xFF)
NAVY    = RGBColor(0x06, 0x12, 0x1F)

ORB = "Orbitron"
OS_ = "Open Sans"
MONO = "Consolas"

prs = Presentation()
prs.slide_width = Mm(841)
prs.slide_height = Mm(1189)
slide = prs.slides.add_slide(prs.slide_layouts[6])  # blank


def no_line(shape):
    shape.line.fill.background()


def rect(x, y, w, h, fill, line=None, line_w=0.7, radius=None, dash=None, shadow=False):
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
        if dash:
            s.line.dash_style = dash
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
TOP, BOTTOM = 200, 1042


def stack(x, w, cards):
    """cards: list of (height, draw_fn(x, y, w, h)). Distributes leftover as equal gaps."""
    total = sum(h for h, _ in cards)
    gaps = len(cards) - 1
    gap = max(8, (BOTTOM - TOP - total) / gaps) if gaps else 0
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
        R("Executive-function assessment still leans on ", size=21),
        R("questionnaires (BRIEF-A)", size=21, color=WHITE, bold=True),
        R(" and dry lab drills:", size=21),
    ]])
    bullets(x + PAD, y + PAD + 42, w - 2 * PAD, [
        [R("Stressful & repetitive", size=20, color=WHITE, bold=True), R(" — scores drop with motivation, not ability", size=20)],
        [R("Low engagement", size=20, color=WHITE, bold=True), R(", especially for children and young adults", size=20)],
        [R("Subjective ratings", size=20, color=WHITE, bold=True), R(" — little objective, trial-level data", size=20)],
    ])
    rect(x + PAD, y + h - 38, w - 2 * PAD, 27, RGBColor(0x33, 0x24, 0x1A), line=ORANGE, line_w=1.5, radius=0.12)
    text(x + PAD + 6, y + h - 34.5, w - 2 * PAD - 12, 20, [[
        R("The need: measurement that is objective, repeatable — and actually fun to take.",
          size=20, color=RGBColor(0xFF, 0xD9, 0xB3), bold=True)]], line_spacing=1.15)


def solution(x, y, w, h):
    sec_title(x + PAD, y + PAD, w, "02", "The Solution")
    text(x + PAD, y + PAD + 17, w - 2 * PAD, 22, [[
        R("Mission Focus", size=21, color=WHITE, bold=True),
        R(" turns assessment into a 10-minute space mission:", size=21),
    ]])
    bullets(x + PAD, y + PAD + 32, w - 2 * PAD, [
        [R("A first-person astronaut keeps a failing station alive", size=20)],
        [R("Every repair console is a ", size=20), R("cognitive paradigm in disguise", size=20, color=WHITE, bold=True)],
        [R("Every reaction is ", size=20), R("silently logged", size=20, color=WHITE, bold=True), R(" — no test anxiety, no clipboards", size=20)],
        [R("One click: ", size=20), R("assessor-ready HTML report", size=20, color=WHITE, bold=True), R(" + analysis-ready CSV", size=20)],
    ])


def requirements(x, y, w, h):
    sec_title(x + PAD, y + PAD, w, "03", "Main Requirements")
    bullets(x + PAD, y + PAD + 18, w - 2 * PAD, [
        [R("Playable 10-min mission", size=20, color=WHITE, bold=True), R(" — tutorial + voice guidance, not a quiz", size=20)],
        [R("Measure ", size=20), R("attention, working memory, inhibition, planning", size=20, color=WHITE, bold=True), R(" and response accuracy", size=20)],
        [R("Per-trial logging", size=20, color=WHITE, bold=True), R(": reaction time, accuracy, omissions, false alarms", size=20)],
        [R("Assessor tools", size=20, color=WHITE, bold=True), R(": freeze (F11) and live report overlay (F12)", size=20)],
        [R("Export ", size=20), R("HTML report + per-task CSV", size=20, color=WHITE, bold=True), R(" per participant", size=20)],
        [R("Local-only data", size=20, color=WHITE, bold=True), R(" — privacy by design, no network", size=20)],
    ], marker="✓", mcolor=GREEN)


def snapshots(x, y, w, h):
    sec_title(x + PAD, y + PAD, w, "", "Mission Snapshots", title_size=24)
    cw = (w - 2 * PAD - 8) / 2
    ch = (h - PAD - 22 - 26 - 14) / 2
    cells = [
        (x + PAD, y + PAD + 16, "Radar Scan console — in-mission task UI", None),
        (x + PAD + cw + 8, y + PAD + 16, "Aboard the station — free roam", None),
        (x + PAD, y + PAD + 16 + ch + 14, "Assessor HTML report — EF profile", None),
        (x + PAD + cw + 8, y + PAD + 16 + ch + 14, "Your astronaut — playable character", A("char_helmet_off.png")),
    ]
    for cx_, cy_, cap, img in cells:
        if img:
            pic = slide.shapes.add_picture(img, Mm(cx_), Mm(cy_), Mm(cw), Mm(ch))
            # crop 16:9 source to cell aspect
            src_ar, cell_ar = 1280 / 720, cw / ch
            if src_ar > cell_ar:
                cut = (1 - cell_ar / src_ar) / 2
                pic.crop_left = cut; pic.crop_right = cut
            pic.line.color.rgb = EDGE; pic.line.width = Pt(1)
        else:
            rect(cx_, cy_, cw, ch, RGBColor(0x09, 0x14, 0x2C), line=CYANSOFT, line_w=1.4,
                 radius=0.06, dash=MSO_LINE_DASH_STYLE.DASH)
            text(cx_, cy_ + ch / 2 - 5, cw, 10, [[R("SCREENSHOT SLOT", font=ORB, size=13, color=CYANSOFT)]],
                 align=PP_ALIGN.CENTER)
        text(cx_, cy_ + ch + 2, cw, 12, [[R(cap, size=15, color=DIM)]], line_spacing=1.1)


# ---------- CENTER ----------
def sys_flow(x, y, w, h):
    sec_title(x + PAD, y + PAD, w, "04", "System Flow")
    steps = [
        ("1", "Participant Onboarding", [R("ID, age and session details collected on a station terminal", size=20)]),
        ("2", "Interactive Tutorial", [R("Movement & console training — skippable for returning participants", size=20)]),
        ("3", "The Mission — 10 minutes", [R("Tasks spawn at ", size=20), R("4 stations", size=20, color=CYANSOFT, bold=True),
                                           R(" every ", size=20), R("15–25 s", size=20, color=CYANSOFT, bold=True),
                                           R(", up to ", size=20), R("3 concurrent", size=20, color=CYANSOFT, bold=True)]),
        ("4", "Silent Telemetry", [R("Every trial logged: ", size=20), R("reaction time, result, omissions, false alarms", size=20, color=CYANSOFT, bold=True)]),
        ("5", "Assessor Report", [R("HTML profile", size=20, color=CYANSOFT, bold=True), R(" by domain + ", size=20),
                                  R("Task-Summary CSV", size=20, color=CYANSOFT, bold=True), R(" + raw event log", size=20)]),
    ]
    sy = y + PAD + 20
    row_h = (h - PAD - 24 - PAD) / 5
    for i, (n, t, drs) in enumerate(steps):
        yy = sy + i * row_h
        if i < 4:
            ln = rect(x + PAD + 9, yy + 20, 1, row_h - 18, CYAN)
        c = slide.shapes.add_shape(MSO_SHAPE.OVAL, Mm(x + PAD), Mm(yy), Mm(19), Mm(19))
        c.fill.solid(); c.fill.fore_color.rgb = RGBColor(0x14, 0x30, 0x5C)
        c.line.color.rgb = CYAN; c.line.width = Pt(2); c.shadow.inherit = False
        text(x + PAD, yy + 3.2, 19, 12, [[R(n, font=ORB, size=21, color=CYAN, bold=True)]], align=PP_ALIGN.CENTER)
        text(x + PAD + 26, yy - 1, w - 2 * PAD - 26, 12, [[R(t, size=23, color=WHITE, bold=True)]])
        text(x + PAD + 26, yy + 10.5, w - 2 * PAD - 26, row_h - 10, [drs], line_spacing=1.15)


def tasks(x, y, w, h):
    sec_title(x + PAD, y + PAD, w, "05", "Cognitive Tasks")
    data = [
        ("RADAR SCAN", "NAVIGATION STATION", "ATTENTION · TASK-MONITOR", CYAN,
         [R("Spot rare asteroid contacts among streams of debris — ", size=19),
          R("40 rapid trials", size=19, color=WHITE, bold=True), R(", 1-second window.", size=19)],
         "Hits · False alarms · d′ sensitivity"),
        ("CODE RECALL", "ENGINE STATION", "WORKING MEMORY", VIOLET,
         [R("Memorize a ", size=19), R("4-digit reactor code", size=19, color=WHITE, bold=True),
          R(", hold it while crossing the station, recall it on a keypad.", size=19)],
         "Recall accuracy · Typos · Timeout"),
        ("STROOP CONSOLE", "COMMS STATION", "INHIBITION", ORANGE,
         [R("Word and ink color disagree — answer by ", size=19), R("color or meaning", size=19, color=WHITE, bold=True),
          R(" as the rule flips. Plus a ", size=19), R("Go/No-Go", size=19, color=WHITE, bold=True), R(" variant.", size=19)],
         "RT mean/SD · Commissions · Post-error slowing"),
        ("POWER CELL RUN", "LIFE-SUPPORT STATION", "PLAN · ORGANIZE", GREEN,
         [R("Fetch a power cell, route it across the station and ", size=19),
          R("re-wire the socket", size=19, color=WHITE, bold=True), R(" before power drains — ", size=19),
          R("100 s budget", size=19, color=WHITE, bold=True), R(".", size=19)],
         "Pickup latency · Wiring errors · Completion time"),
    ]
    cw = (w - 2 * PAD - 9) / 2
    ch = (h - PAD - 22 - 9 - PAD) / 2
    for i, (t, st, dom, dc, desc, chips) in enumerate(data):
        cx_ = x + PAD + (i % 2) * (cw + 9)
        cy_ = y + PAD + 18 + (i // 2) * (ch + 9)
        rect(cx_, cy_, cw, ch, CARD2, line=EDGE, radius=0.055)
        text(cx_ + 8, cy_ + 7, cw - 16, 12, [[R(t, font=ORB, size=20, color=WHITE, bold=True)]])
        text(cx_ + 8, cy_ + 17.5, cw - 16, 9, [[R(st, size=14, color=DIM, bold=True)]])
        pill_w = 6 + len(dom) * 3.1
        rect(cx_ + 8, cy_ + 26.5, pill_w, 10.5, None, line=dc, line_w=1.2, radius=0.5)
        text(cx_ + 8, cy_ + 28.6, pill_w, 7, [[R(dom, size=13, color=dc, bold=True)]], align=PP_ALIGN.CENTER)
        text(cx_ + 8, cy_ + 41, cw - 16, ch - 60, [desc], line_spacing=1.22)
        text(cx_ + 8, cy_ + ch - 12.5, cw - 16, 10, [[R(chips, size=14, color=CYANSOFT, bold=True)]])


def pipeline(x, y, w, h):
    sec_title(x + PAD, y + PAD, w, "", "Under the Hood — Measurement Pipeline", title_size=22)
    rows = [
        ("TaskStation", "spawns the task"),
        ("CognitiveTaskBase", "runs timed trials"),
        ("AssessmentResults", "collects metrics"),
        ("SessionManager", "logs every event"),
        ("Exporters", "HTML · CSV · raw log"),
    ]
    rh = (h - PAD - 20 - PAD - 4 * 4) / 5
    yy = y + PAD + 18
    for name, desc in rows:
        rect(x + PAD + 6, yy, w - 2 * PAD - 6, rh, RGBColor(0x0A, 0x15, 0x2E), line=EDGE, radius=0.18)
        text(x + PAD + 13, yy + rh / 2 - 5.5, w - 2 * PAD - 20, 10, [[
            R(name, font=MONO, size=19, color=CYANSOFT, bold=True),
            R("   " + desc, size=16.5, color=DIM),
        ]])
        yy += rh + 4
    rect(x + PAD, y + PAD + 20, 1.2, h - 2 * PAD - 22, ORANGE)


# ---------- RIGHT ----------
def tech(x, y, w, h):
    sec_title(x + PAD, y + PAD, w, "06", "Technologies & Tools")
    rows = [
        ("Unity 6 · URP", "3D engine & rendering pipeline", CYAN),
        ("C#", "gameplay & assessment logic", VIOLET),
        ("Unity Input System", "first-person controls", GREEN),
        ("TextMeshPro", "HUD & console UI", ORANGE),
        ("ElevenLabs", "AI mission voice-overs", CYAN),
        ("Meshy AI", "3D station props & consoles", VIOLET),
        ("Git · GitHub", "version control & delivery", GREEN),
    ]
    rh = (h - PAD - 20 - PAD - 6 * 3.5) / 7
    yy = y + PAD + 18
    for name, desc, dc in rows:
        rect(x + PAD, yy, w - 2 * PAD, rh, RGBColor(0x0A, 0x15, 0x2E), line=EDGE, radius=0.22)
        sq = rect(x + PAD + 5, yy + rh / 2 - 3.4, 6.8, 6.8, dc, radius=0.3)
        text(x + PAD + 16, yy + rh / 2 - 5.5, w - 2 * PAD - 22, 10, [[
            R(name, size=19, color=WHITE, bold=True),
            R("  — " + desc, size=16, color=DIM),
        ]])
        yy += rh + 3.5


def testing(x, y, w, h):
    sec_title(x + PAD, y + PAD, w, "07", "Testing & Evaluation")
    bullets(x + PAD, y + PAD + 18, w - 2 * PAD, [
        [R("Quick-test mode", size=19, color=WHITE, bold=True), R(" — the full pipeline exercised in 30-second missions", size=19)],
        [R("Deterministic seeds", size=19, color=WHITE, bold=True), R(" — reproducible trial sequences for debugging", size=19)],
        [R("Assessor console verified mid-mission", size=19, color=WHITE, bold=True), R(" — F11 freeze, F12 live report", size=19)],
        [R("Locale-safe CSV", size=19, color=WHITE, bold=True), R(" — opens clean in Excel, R and Python", size=19)],
        [R("Pilot playtests", size=19, color=WHITE, bold=True), R(" + peer-examiner demo ahead of the exhibition", size=19)],
    ], size=19, gap=5)


def challenges(x, y, w, h):
    sec_title(x + PAD, y + PAD, w, "08", "Challenges & Insights")
    items = [
        ("01", [R("Fun vs. validity", size=19, color=WHITE, bold=True), R(" — free-roam play, yet strict stimulus timing inside every console", size=19)]),
        ("02", [R("Invisible measurement", size=19, color=WHITE, bold=True), R(" — world-space consoles and voice guidance keep the “test” out of sight", size=19)]),
        ("03", [R("Many paradigms, one core", size=19, color=WHITE, bold=True), R(" — a shared task framework: a new task is one script, metrics flow automatically", size=19)]),
        ("04", [R("AI asset pipeline", size=19, color=WHITE, bold=True), R(" — AI-generated 3D models needed URP material and scale rework", size=19)]),
    ]
    paras = [[R(n + "  ", font=ORB, size=16, color=ORANGE, bold=True)] + rs for n, rs in items]
    text(x + PAD, y + PAD + 18, w - 2 * PAD, h - 70, paras, space_after=5, line_spacing=1.2)
    rect(x + PAD, y + h - 40, w - 2 * PAD, 29, RGBColor(0x10, 0x2A, 0x44), line=CYAN, line_w=1.5, radius=0.1)
    text(x + PAD + 6, y + h - 36, w - 2 * PAD - 12, 22, [[
        R("Players forget they are being measured — and strict timing is exactly what keeps the data meaningful.",
          size=18, color=CYANSOFT, italic=True)]], line_spacing=1.15)


def review(x, y, w, h):
    sec_title(x + PAD, y + PAD, w, "09", "Project Review")
    items = [
        ("✔", GREEN, [R("Goals met", size=19, color=WHITE, bold=True), R(" — all four cognitive domains playable end-to-end", size=19)]),
        ("✔", GREEN, [R("~26 metrics per session", size=19, color=WHITE, bold=True), R(" exported: HTML + CSV + raw event log", size=19)]),
        ("✔", GREEN, [R("Demo-ready", size=19, color=WHITE, bold=True), R(" — stable 10-minute missions with assessor tools", size=19)]),
        ("↻", ORANGE, [R("In hindsight", size=19, color=WHITE, bold=True), R(" — build the measurement core first, playtest earlier", size=19)]),
        ("→", CYAN, [R("Next", size=19, color=WHITE, bold=True), R(" — validation study against standard BRIEF-A scores", size=19)]),
    ]
    paras = [[R(m + "  ", size=19, color=c, bold=True)] + rs for m, c, rs in items]
    text(x + PAD, y + PAD + 18, w - 2 * PAD, h - 40, paras, space_after=5, line_spacing=1.2)


def takeaway(x, y, w, h):
    # gradient-ish border via outer cyan rect
    rect(x - 1, y - 1, w + 2, h + 2, CYAN, radius=0.06)
    rect(x + 0.6, y + 0.6, w - 1.2, h - 1.2, RGBColor(0x0B, 0x18, 0x34), radius=0.06)
    sec_title(x + PAD, y + PAD, w, "10", "Key Takeaway")
    text(x + PAD, y + PAD + 18, w - 2 * PAD, h - 40, [[
        R("Assessment doesn't have to feel like a test. A ", size=22, color=INK),
        R("10-minute space mission", size=22, color=ORANGE, bold=True),
        R(" can quietly produce a ", size=22, color=INK),
        R("full executive-function profile", size=22, color=CYAN, bold=True),
        R(" — engagement and measurement in the same loop.", size=22, color=INK),
    ]], line_spacing=1.3)


def qr_row(x, y, w, h):
    rect(x + PAD - 4, y + 6, 48, 48, WHITE, radius=0.1)
    slide.shapes.add_picture(A("qr_github.png"), Mm(x + PAD - 1), Mm(y + 9), Mm(42), Mm(42))
    text(x + PAD + 50, y + 10, w - PAD - 60, 12, [[R("FULL CODE & DOCS", font=ORB, size=18, color=WHITE, bold=True)]])
    text(x + PAD + 50, y + 22, w - PAD - 60, 12, [[R("github.com/lidorbenhamo26/Final-Project", size=16, color=CYANSOFT, bold=True)]])
    text(x + PAD + 50, y + 32, w - PAD - 60, 12, [[R("Unity project · project book · demo video", size=15, color=DIM)]])


# ---------- build columns ----------
stack(LX, LW, [
    (158, bg_need),
    (152, solution),
    (185, requirements),
    (300, snapshots),
])
stack(CX, CW, [
    (305, sys_flow),
    (330, tasks),
    (168, pipeline),
])
stack(RX, RW, [
    (172, tech),
    (158, testing),
    (205, challenges),
    (148, review),
    (95, takeaway),
    (62, qr_row),
])

# ============ STATS BAND ============
BY = 1056
rect(22, BY, 797, 68, CARD, line=EDGE, radius=0.08)
stats = [("4", "COGNITIVE STATIONS", CYAN), ("10 MIN", "ONE FULL MISSION", ORANGE),
         ("~26", "METRICS PER SESSION", VIOLET), ("3", "EXPORT ARTIFACTS", GREEN)]
cw_ = 150
for i, (v, l, c) in enumerate(stats):
    sx = 40 + i * cw_
    text(sx, BY + 10, cw_, 24, [[R(v, font=ORB, size=44, color=c, bold=True)]], align=PP_ALIGN.CENTER)
    text(sx, BY + 40, cw_, 12, [[R(l, size=16, color=DIM, bold=True)]], align=PP_ALIGN.CENTER)
    if i:
        rect(sx - 2, BY + 12, 0.5, 44, EDGE)
# wave astronaut + bubble
slide.shapes.add_picture(A("char_wave.png"), Mm(742), Mm(BY - 14), height=Mm(80))
rect(648, BY + 6, 88, 15, RGBColor(0x10, 0x2A, 0x44), line=CYANSOFT, radius=0.5)
text(648, BY + 9.4, 88, 9, [[R("Ready for launch!", size=16, color=CYANSOFT, bold=True)]], align=PP_ALIGN.CENTER)

# ============ FOOTER ============
rect(0, 1168, 841, 21, RGBColor(0x05, 0x0B, 0x1A), line=None)
rect(0, 1168, 841, 0.8, EDGE)
text(0, 1173.5, 841, 12, [[
    R("Braude College of Engineering, Karmiel", size=16, color=SOFT, bold=True),
    R("   ◆   Department of Software Engineering & Information Systems   ◆   Capstone Project 61999 — Phase B   ◆   ", size=16, color=DIM),
    R("2026", size=16, color=SOFT, bold=True),
]], align=PP_ALIGN.CENTER)

out = os.path.join(HERE, "MissionFocus_Poster_A0.pptx")
prs.save(out)
print("saved", out)

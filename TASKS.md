# Mission: Focus — Polish & Gameplay Task List

Working tracker for the capstone polish pass. Status as of 2026-06-13.

**Priority order:** Task 2 → Task 3 → Task 1 → Task 4, plus Task 5 & Task 6.

**How the world is built:** the ship interior is generated procedurally by editor
scripts under `Assets/Editor/Setup/` (menu `Setup/...`), not hand-placed. The main
ones: `EnvironmentBuilder.cs` (`Setup/1 - Build Environment`), `DoorFixup.cs`
(`Setup/3 - Fix Doors`). Stations/props/player are separate scene roots, so
rebuilding `Environment_Root` doesn't wipe them.

**Verification rule (important):** verify visual/gameplay changes through the
**actual third-person game camera in Play mode**, not a placed editor camera. The
third-person rig sits low and roughly level; placed-camera screenshots gave false
"looks fine" results during the door-label work (see Task 3 history).

---

## ✅ Task 2 — Fix premature E-key interaction  (DONE)
Pressing E before the system spawned a task used to spawn one on demand (a
half-initialized task that polluted the assessment).
- `StationDockController.cs`: removed on-demand spawn; `EnterDock` only when
  `station.HasActiveTask()`, else shows "No task here right now".
- `StationProximityPrompt.cs`: the `[E] INTERACT` hint only appears when the
  station has a live, system-spawned task.
- Tutorial unaffected — it calls `EnsurePracticeTask()` before its dock step.

## ✅ Task 3 — Doors: widen, label, fix stuck collider  (DONE)
Implemented in `Assets/Editor/Setup/DoorFixup.cs` (menu `Setup/3 - Fix Doors`),
also called at the end of `EnvironmentBuilder.BuildEnvironment`. Per door it:
- **Unstick:** disables the gate's mesh collider (rounded frame + bottom sill that
  snagged the 0.6 m-radius capsule); adds clean jamb + lintel box colliders →
  flush, threshold-free ~2 m opening.
- **Widen:** hides the closed door slab that covered ~half the frame.
- **Label:** one color-coded station-name sign per door (ENGINE=red,
  NAVIGATION=blue, COMMS=yellow, LIFE SUPPORT=green), accent glow.
- Idempotent; applied to all 8 doors in MainScene (saved). TutorialScene has no
  doors yet (0 found) — it inherits the fix when rebuilt in Task 1.

### Door-label sub-history (do NOT repeat these dead ends)
- **Attempt A — world-space Canvas text above door:** did not render from an
  edit-time script. → switched to mesh-based 3D `TextMeshPro`.
- **Attempt 1 — single sign in the wall band above the door:** clipped by the low
  ceiling AND occluded by the doorframe header in the real low camera. Lowering /
  shrinking / brightening did not fix it. Raising the ceiling was considered but
  rejected: modular 3 m walls would leave gaps, needs lighting/probe rebakes, and
  a higher sign reads *worse* in the low level camera.
- **Attempt 2 — two placards flanking the door:** split the word into fragments
  ("EN" | "NE"). Unreadable. Rejected.
- **Attempt 3 — ONE eye-level placard, fully on the panel right of the door
  (CURRENT/FINAL):** single full word on one line, on the solid wall to the RIGHT
  of the opening at ~1.6 m (local centre x≈2.9, 1.7 m wide => x:2.05..3.75, clear
  of the opening x:-1..1 and the rounded frame x:~1..2), floated 0.40 m proud so
  the first letter clears the frame edge. Same (right) side for all doors. Verified
  in the real game camera for Engine (red) + Comms (yellow); Navigation (blue) +
  Life Support (green) verified with an eye-level camera matched to the real rig.
  Caveat: this is tuned for the 4 HUB doors (wide adjacent panel). The 4 ROOM
  doors have only a ~1 m filler panel beside the opening, so their sign spills past
  the corner — revisit if room-door labels need to be perfect (narrower sign or
  per-door width detection).

### Task 3 — remaining optional extras (not done)
- Per-station icons (gear/radar/etc.) next to the name — needs icon assets / TMP
  sprite sheet.
- Match door label color ↔ HUD task-list color — fold into Task 4 (HUD work).

## ✅ Task 1 — Rebuild the tutorial to mirror the real ship  (DONE — pending playtest)
Approach chosen: **duplicate MainScene → TutorialScene** so the tutorial is an
exact replica of the ship (real corridors, 4 named + labeled stations).
- `TutorialScene.unity` is now a content-copy of `MainScene.unity` (git has the
  old bare-box tutorial for revert).
- `GameManager` got a `tutorialMode` bool: when set it wires up Instance + station
  refs but does NOT start the mission timer / task spawn loop. TutorialScene's
  GameManager has `tutorialMode = true`.
- `HUDManager` GameObject is **disabled** in TutorialScene (no mission HUD clutter;
  the tutorial has its own overlay). The practice task (CodeMemoryTask) does not
  depend on the HUD.
- `TutorialDirector.cs` rewritten to drive, in the real ship:
  move → sprint → jump x3 → station tour (highlights + names all 4) → walk to the
  Engine station → press E to dock → complete a practice task → finish → briefing.
  Adds a persistent **checklist HUD** (Walk / Sprint / Jump / Dock), keeps the
  **Skip** button and the `TutorialHighlight` waypoint. No instant/timer pass.
- Flow unchanged: StartScene form → Tutorial → (TutorialCompleted) → briefing →
  Begin → MainScene.

Round 2 refinements (done):
- Walk/sprint now gated by **accumulated distance**, not a key tap. Serialized on
  TutorialDirector: `walkDistance` (3.5 m), `sprintDistance` (5.5 m),
  `sprintSpeedThreshold` (3.5), `requiredJumps` (3), `stationArriveRadius` (2.6 m),
  `hubReturnRadius` (3.5 m). Checklist shows live progress ("WALK 2.1/3m").
- **Two** practice stations: Station 1 = Engine, Station 2 = Comms. Each spawns a
  new `TutorialPracticeTask` (Assets/Scripts/Onboarding/TutorialPracticeTask.cs) —
  a trivial dock-and-interact task NOT in CognitiveTaskCatalog (mode 0 = press the
  button; mode 1 = press the number 7). Never auto-expires. Built on
  CognitiveTaskBase so it renders on the docked console like the real tasks.
- New final step: **return to the central hub** (waypoint on a hub marker at world
  origin; arrival within `hubReturnRadius` ends the tutorial).
- Checklist expanded to WALK / SPRINT / JUMP / STATION 1 / STATION 2 / RETURN.
- Flow: walk Xm -> sprint Ym -> jump x3 -> tour -> walk S1 -> dock -> practice ->
  walk S2 -> dock -> practice -> return to hub -> finish.

Verified via MCP: scene renders as the real ship (direct Main-Camera render),
compiles clean, no runtime errors, overlay/director initialize. NOT yet visually
playtested end-to-end — input (WASD/jump/dock/buttons) can't be driven via MCP,
and the no-camera ScreenCapture returns an all-white frame in this URP/HDR project
(capture quirk; use `camera="Main Camera"` direct render). The step-gating and
two-station flow need a human playtest. Door labels for room doors still spill
(see Task 3 caveat).

## ✅ Task 4 — Difficulty ramp, distractions & clear task priority  (DONE — pending playtest)
Scope chosen: ramp + priority + asset-light distractions (passing drones deferred,
needs a model).

**Difficulty ramp (`GameManager`):** new `CurrentDifficulty` (0..1), 0 during a
`calmIntroSeconds` (75 s) opening, then driven by a serialized `difficultyCurve`
over the rest of the mission. Drives, via Lerp(calm→intense):
spawn interval (`spawnIntervalCalm` 20–28 s → `spawnIntervalIntense` 6–10 s),
max concurrent tasks (`maxConcurrentCalm` 1 → `maxConcurrentIntense` 3), and each
task's response window (`responseFactorCalm` 1.15 → `responseFactorIntense` 0.65,
floored at `minResponseWindow` 7 s so it never becomes impossible). All serialized
for inspector tuning. Logs `Task_Spawn` and `Difficulty` (with diff value) to
SessionManager per event.

**Distractions (`DistractionDirector.cs`, on a GameObject in MainScene):** ramps
with `CurrentDifficulty`, only after the calm intro, more frequent as it climbs:
red-alert light flashes (pulses room lights red), siren (`sfx_siren`) + crew
chatter (`voice_chatter`) audio hooks (silent until clips are added), and decoy
alert banners via the HUD (fake "PROXIMITY ALERT" etc. that need no action).
Passing drones deferred — needs a model.

**Task priority / urgency (`TaskListHUD`):** now tracks ALL concurrent active
tasks, shows the MOST URGENT (least time left) in the active row, with a
"+N MORE" badge for the rest. Rows color-coded by station accent (Engine red /
Nav blue / Comms yellow / Life Support green, matching the door labels), the
countdown bar flips to red + pulses under 5 s, and Critical-priority tasks show a
"PRIORITY" tag.

Verified via MCP: compiles clean, mission runs with the new spawn loop + per-frame
difficulty + reworked HUD + DistractionDirector with no runtime errors. NOT
experientially playtested — the ramp unfolds over ~10 min, distractions start
after 75 s, and the HUD overlay can't be screenshotted (HDR ScreenCapture quirk),
so the feel/visuals need a human playtest. To see it fast, lower
`calmIntroSeconds` or enable `quickTestMode` on the GameManager. Audio: add
`Resources/Audio/SFX/sfx_siren` and `Resources/Audio/Voice/voice_chatter` to hear
the siren/chatter.

## ✅ Task 5 — Assessor pause/stop controls  (DONE — pending playtest)
- `GameManager`: `MissionPaused` + `SetPaused(bool)` (reuses the freeze plumbing so
  timer + all task timers halt together; reaction times stay clean via
  MissionTask's SpawnTime slide) and `EndMissionEarly()` (sets MissionActive=false
  so the loops finalize and HUDManager shows the report with data so far).
- `AssessorControls.cs` (GameObject in MainScene): top-centre PAUSE/RESUME + STOP
  buttons, **P** keyboard shortcut, dimmed **PAUSED** overlay, and a **confirm
  dialog on STOP** (participant can't end by accident). Frees the cursor while
  paused; restores on resume.
- `ThirdPersonCamera`: mouse-look now gated on `Cursor.lockState == Locked`, so
  freeing the cursor (pause/dock/report) stops camera drift.
- Logs `Mission_Paused` / `Mission_Resumed` / `Mission_Stopped_Early` to the CSV.

Verified via MCP: compiles clean, mission runs with the control bar showing and
both overlays hidden, no runtime errors. NOT interactively playtested — MCP can't
press P or click the buttons, and the overlays can't be screenshotted (HDR capture
quirk). Needs a human to confirm: P pauses/resumes (PAUSED overlay + frozen
timer), STOP -> confirm -> report generates. Optional password on STOP not added
(confirm dialog only).

## ✅ Task 6 — Assessor-selectable assessment length  (DONE — pending playtest)
- Intake form (`ParticipantFormController`) gained a segmented **5 / 10 / 15 min**
  selector (beside the Age/Session column), default 10, **remembers last choice**
  via PlayerPrefs.
- Stored on `SessionContext.MissionMinutes` (not cleared on report-dismiss reset).
- `GameManager.Start` overrides `missionDuration` from `SessionContext.MissionMinutes`,
  so the timer AND the Task-4 difficulty ramp (which reads `missionDuration`)
  auto-scale to the chosen length.
- Chosen duration recorded everywhere in the report: `ReportData.DurationMinutes`,
  the CSV (`DurationMin` column), the runtime report view, and the HTML export.

Round 2 (layout + exact value):
- The length control is now its **own full-width row between Session # and Notes**
  (form reflowed to even 116px spacing, panel 700x940).
- It's an **integer-minutes input field** (ContentType IntegerNumber, default 10,
  validated 1-60 — `MinMinutes`/`MaxMinutes` constants, adjust if the team wants a
  different range; Continue is blocked on invalid/empty). The typed number is the
  source of truth.
- The **5 / 10 / 15 buttons remain as quick-picks** that just fill the field; a
  custom value (e.g. 7) un-highlights them. Last choice remembered via PlayerPrefs.

Verified via MCP: compiles clean, no errors. Script-only (form built at runtime),
no scene change. NOT playtested — needs a human to confirm the row position, typing
a custom value, validation, and that the mission timer + ramp + report use it.
A difficulty-preset picker was left out.

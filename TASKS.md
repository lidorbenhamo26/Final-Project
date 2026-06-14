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

---

# BATCH 2 — playtest feedback (Tasks 7–16)

All ⬜ not started. Suggested order (do related items together):
1. **Fairness (do first):** 9 (alien knocks battery), 11 (task while docked),
   10 (movement/camera), 7 (first-time instructions), 8 (names/floating text).
2. **Clarity:** 15 (priority colors — merge with Task 4), 12 (Stroop rule).
3. **Pacing/variety:** 13 (shorten radar), 14 (early variety), 16 (task variants).

Cross-links: 15 + parts of 14 ARE the Task-4 work; 7/8/12/15 feed the Task-1
tutorial — whoever touches Task 4 / Task 1 should pick these up together.

Codebase pointers (verified this session):
- Spawn loop / selection: `GameManager.TaskSpawnLoop`, `PickStation`,
  `BuildEligibleStationList`, `SpawnTaskAt`. Station→task map:
  `CognitiveTaskCatalog.CreateTaskForStation` (Engine=`WorkingMemoryTask`,
  Navigation=`RadarScanTask`, Comms=`StroopTask`, LifeSupport=`BatteryDeliveryTask`).
- Docked state: `StationDockController.Instance.IsDocked` / `.CurrentStation`.
- HUD task list + the current "+N MORE" / station accents / urgency: `UI/TaskListHUD.cs`.
- Station display name comes from `TaskStation.stationName` (raw, e.g.
  "EngineStation"); `TaskListHUD.PrettyStation()` already maps to "Engine" etc.
- Unused task variants to wire in (Task 16): `Tasks/CodeMemoryTask.cs` (used only
  by the tutorial now), `Tasks/InhibitTask.cs` (Go/No-Go — not in the catalog).
- Alien NPCs (Task 9): `WanderingAI.cs`, `AlienCuriosity.cs`; carry logic in
  `Interaction/CarryableBattery.cs` + `AstronautHandGrip`.
- Camera (Task 10): `ThirdPersonCamera.cs` (mouse-look, occlusion SphereCast).

## ⬜ Task 7 — First-time written instructions before each task type
First time each of the 4 task types appears, pause it and show a short instruction
card (1–2 lines + controls) with a "Got it" button; never count time/score until
dismissed; show once per session.
- Track first occurrence in a `seenTaskTypes` set on `SessionContext`.
- Reuse the freeze plumbing (`GameManager.SetDebugFrozen`/pause) so the trial
  clock doesn't run while the card is up (same pattern as the report/pause).
- Reuse the per-task copy for the Task-1 tutorial.
- Code: task spawn flow (`GameManager` / `CognitiveTaskBase.OnPlayerEnter`) + a
  small reusable instruction UI; copy in `Scripts/Onboarding`.

## ⬜ Task 8 — Fix station name formatting + remove stray floating text
- Show spaced/proper names everywhere ("Engine Station" / "Comms Station" …) — be
  consistent across tutorial, door labels (DoorFixup), and HUD. `PrettyStation()`
  already does the mapping; the tutorial/other spots use raw `stationName`.
- Find & remove the unexplained floating text at a station (likely a leftover
  `StationUI` placeholder / debug label) or replace with a real prompt.
- Audit all on-screen strings for run-together/placeholder text.
- Code: station prefabs / `StationUI`, `Scripts/Onboarding`, HUD.

## ✅ Task 9 — Alien must not knock the battery out of the player's hand  (DONE — pending playtest)
- Root cause: `AlienCuriosity` had a battery-SNATCH behaviour (chase + swat ->
  `CarryableBattery.Drop()`) gated by `snatchCarriedCell` (was true).
- Fix: default `snatchCarriedCell = false` AND set it false on the scene alien
  ("AlienBuddy") so it never chases/knocks the cell — it only wanders/pesters now.
- Added `IgnorePlayerCollision()` (Physics.IgnoreCollision between the alien's and
  player's colliders, once) so the body can never push the player either. (The
  alien root has no collider anyway, so the snatch was the whole problem.)
- Battery is only droppable via `Drop()` which only the snatch called -> now never;
  carry stays kinematic + collider-off. Alien remains a visual distractor.
- Verified: compiles, no runtime errors, alien still present in the hub. Needs a
  human playtest carrying the cell to confirm it's never knocked loose.

## ⬜ Task 10 — Smooth movement & camera through doors / between rooms
- Camera: gently auto-align/recenter behind the player toward the movement
  direction in corridors/doorways (smooth lerp, no snap) to cut manual mouse
  correction; ensure no clip/jerk at doorframes.
- Doors: widen triggers/colliders so the player never catches (ties to Task 3's
  jamb colliders in DoorFixup).
- Reduce dead travel time: slightly faster base move, and/or a waypoint arrow to
  the active station; ensure travel time doesn't cause task failures (lengthen
  response windows or shorten distances so failures reflect cognition).
- Code: `ThirdPersonCamera`, door triggers (`Scripts/Interaction`), `GameManager`
  response-window timing.

## ⬜ Task 11 — Don't start an attention task while the player is docked elsewhere
- While `StationDockController.IsDocked`, defer/suppress spawning of tasks that
  need the player to see the main screen (at minimum the code-memory display);
  re-evaluate the spawn queue on undock; never deadlock the spawner.
- Code: `GameManager.TaskSpawnLoop` + docking state. Connects to Task 4 + Task 2.

## ⬜ Task 12 — Comms (Stroop): make the rule clear, stop confusing mid-task switching
- Option A: one rule per task instance, fixed banner ("Respond to the INK color.").
- Option B: switch only between clearly separated rounds with a big banner + sound
  ("NEW RULE: respond to the WORD."). Always show the current rule each trial.
- Add the rule to the Task-7 first-time card.
- Code: `Tasks/StroopTask.cs`, Comms UI.

## ⬜ Task 13 — Shorten the Radar (CPT) task
- Expose trial count + inter-stimulus interval as serialized fields and lower them
  (~half, tune with team); keep enough trials for hit/false-alarm/d-prime and ≥2
  blocks for vigilance-decrement. Code: `Tasks/RadarScanTask.cs`.

## ⬜ Task 14 — Vary tasks from the start (kill early repetition)
- Early spawns rotated only code-memory + radar; rotate across all 4 from the
  start. Avoid immediate repeats (anti-repeat already partly in `PickStation` via
  `lastSpawnedStationName` — strengthen it / ensure all 4 are eligible early).
  Keep Task-4's calm pace but make variety present from the start.
- Code: `GameManager` spawn selection. Connects to Task 4 + Task 16.

## ⬜ Task 15 — Clear color-coded task priority + teach it in the tutorial
- Replace the unclear "+N MORE" with explicit priority levels (Red=critical,
  Yellow=medium, Green=low) on each HUD task row and ideally the station/door.
- With two active tasks the colors make the choice obvious; optional per-task
  countdown bars (the active row already has one).
- Teach it in the Task-1 tutorial ("Red tasks are urgent — do them first.").
- Code: `UI/TaskListHUD.cs` (currently shows most-urgent + "+N MORE" + station
  accent — extend to a real priority enum→color), task data model (`MissionTask`
  has `priority` Critical/NonCritical — may need 3 levels), tutorial copy.
- **Merge with Task 4** (this is its "clear priority" half).

## ⬜ Task 16 — Add a second alternating task variant to some stations
- For 1–2 stations, alternate two variants (e.g. Comms: Stroop ↔ Go/No-Go;
  Engine: WorkingMemory code ↔ CodeMemory). Wire in the existing unused variants
  (`CodeMemoryTask`, `InhibitTask`) instead of leaving them dead.
- Each variant must still report to the same BRIEF-A scale; make alternation
  configurable (on/off or weighting).
- Code: `Tasks/*`, `CognitiveTaskCatalog`, `GameManager`. Connects to Task 14 and
  resolves the book's "use-or-remove unused variants" point.

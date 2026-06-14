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

# BATCH 2 — bug fixes from playtest after Tasks 9–11

## ✅ Bug B — spawner stalled (one active task, nothing else spawned)  (DONE — pending playtest)
- Root cause: `maxConcurrentCalm` was 1, so with the slow difficulty ramp
  `maxConcurrent` rounded to 1 for the first several minutes — while one task sat
  unattended (~90s window) nothing else could spawn. (Task 11's docked-suppression
  was NOT the cause; the player was roaming.)
- Fix: `maxConcurrentCalm = 2` (code default + set on the MainScene GameManager).
  Now a 2nd task spawns alongside an ignored one; ramps to 3 with difficulty.
- Confirmed the base time-limit expiry DOES fire for an undocked task
  (RadarScanTask.Update calls base.Update; timeLimit 90 -> Omission). So unattended
  tasks expire; the stall was purely the concurrency cap.

## ◑ Bug A — Engine code not seen before the task  (MITIGATED — pending playtest)
- `WorkingMemoryTask` already shows the code reliably (a "AUTH CODE — MEMORIZE"
  panel + big digits via HUDManager.ShowCodeBanner) and only spawning is gated by
  Task 11 — the display is never cancelled. MainScene's HUDManager is enabled
  (only the tutorial copy was disabled), so the banner does render.
- Most likely the 4s flash was missed while travelling. Mitigation: alert 1.5->2.5s,
  code display 4->6s for more time to notice/memorise.
- Task 7 (first-time instruction card) will properly teach "watch for the code,
  then enter it at Engine" — the real fix for not knowing to look. If a re-test
  still shows NO code panel at all, it points to a scene/HUD issue needing a repro.

# BATCH 2 — balance pass (from log analysis of a real playtest)

Analyzed a real session log (radar ate ~96s incl. 32s travel; one task at a time;
difficulty ~0 for the first ~100s; WM code shown but missed while travelling).

## ✅ Balance #2 — per-task response-window floor  (DONE — pending playtest)
- `MissionTask.MinResponseWindowSeconds` (virtual, 0). `RadarScanTask` overrides it
  to `nTrials*ISI + 14s`. `GameManager.SpawnTaskAt` clamps the scaled window to
  `Max(minResponseWindow, task.MinResponseWindowSeconds)` so a task's window can
  never drop below its own completion time (fixes "late radar window < radar
  length = impossible").

## ✅ Balance #3 — cut travel  (DONE — pending playtest)
- `AstronautController.moveSpeed` 2.2 -> 3.2 (code default + both scene players).
- New `StationWaypointArrow` (UI/, on a MainScene object): HUD arrow + "ENGINE 14m"
  label pointing to the nearest active station (camera-relative), station-accent
  coloured; hidden when nothing active or while docked.

## ✅ Balance #5 — steepen / front-load the ramp  (DONE — pending playtest)
- `calmIntroSeconds` 75 -> 45 (code + MainScene). Replaced the back-loaded
  AnimationCurve with `difficultyRampExponent` (2.0, ease-out: d=1-(1-p)^exp), so
  difficulty hits ~0.25 by 2 min / ~0.7 by 5 min instead of ~0 for the first
  several minutes. Concurrency (2->3) and faster spawns now arrive by ~3-4 min.

Balance review follow-ups: Task 13 (shorten radar) DONE, Task 7 (first-time cards
incl. "watch for the code") DONE, radar pass bar loosened (hitRate 0.70->0.60,
faRate 0.10->0.20) DONE.

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

## ✅ Task 7 — First-time written instructions before each task type  (DONE — pending playtest)
- `CognitiveTaskBase` now shows a one-time instruction card on the *first dock* of
  each task type: dark full-canvas overlay with title + 1-2 line copy + controls +
  a "GOT IT" button. The trial clock is held via the existing debug-freeze flag
  (`GameManager.SetDebugFrozen`) while the card is up, so reading is never scored.
- First-occurrence tracked in `SessionContext.seenTaskTypes` (keyed by class name),
  cleared on `SessionContext.Reset()` so each new participant sees the cards again.
- Copy added via `InstructionTitle`/`InstructionBody` overrides on `RadarScanTask`,
  `WorkingMemoryTask`, `StroopTask`. Undocking before "GOT IT" re-shows the card.
- **Bug A fix folded in:** `WorkingMemoryTask` no longer flashes the code at spawn.
  The alert->code->recall reveal now starts on first dock (after the card), so a
  player still travelling can't miss the flash. Aligns WM with Radar/Stroop, which
  already start on dock; never docking now ends as a clean Omission.
- Battery delivery (the 4th type) is a physical carry task with no docked canvas;
  it already self-instructs via its step-by-step objective HUD, so no card there.
- Code: `Tasks/CognitiveTaskBase.cs`, `Onboarding/SessionContext.cs`, the 3 task
  files. TODO (later): reuse this copy in the Task-1 tutorial.

## ✅ Task 8 — Fix station name formatting + remove stray floating text  (DONE — pending playtest)
- FLOATING TEXT (resolved): the user identified it as the tutorial's `TutorialHighlight`
  marker, which showed a generic pulsing "OBJECTIVE" word above a station. Replaced
  with a real contextual prompt: the big line is now the target NAME and the small
  line the action — tour "ENGINE / STATION", walk "ENGINE / WALK HERE", dock
  "ENGINE / DOCK HERE", return "CENTRAL HUB / RETURN HERE". `SetTarget(t, title,
  action)`; the "OBJECTIVE" placeholder is gone. Code: `Onboarding/TutorialHighlight.cs`,
  `Onboarding/TutorialDirector.cs`.
- NAMES: standardized on the spaced/proper `PrettyStation()` form everywhere a
  name is shown. Fixed the two raw-`stationName` leaks:
  - `TutorialDirector` station labels (were "ENGINESTATION" -> now "ENGINE").
  - Report caption in `ReportData` (was "EngineStation" -> now "Engine").
  Already-correct spots left as-is: HUD (`TaskListHUD`), `NotificationFeed`, the
  waypoint arrow, door placards (`DoorFixup`: ENGINE/NAVIGATION/COMMS/LIFE SUPPORT)
  and the ship-map briefing (hardcoded clean). The session CSV keeps the raw id
  on purpose (stable machine-readable key).
- FLOATING TEXT: audited all 4 stations through the real game camera (player
  approach angle). No floating text found at any console — the only world text is
  the intended door placards, and the `StationUI` info panel is already hidden at
  runtime (`TaskStation.Start -> stationUI.Hide()`, the earlier "floating white
  squares" fix). The white star on the Comms wall is a light gobo (decor), not
  text. **Need the user to point at the specific floating text** (which station /
  screenshot) if it still appears — likely already resolved by the StationUI hide.
- Note (out of scope): each station still carries an inert legacy `EngineTask`/
  `CommsTask`/`NavigationTask`/`LifeSupportTask` (`:MissionTask`, TaskName="")
  component; harmless cruft, not removed.

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

## ◑ Task 10 — Smooth movement & camera through doors / between rooms  (CORE DONE — pending playtest)
- DONE — camera auto-align: `ThirdPersonCamera` now smoothly swings the yaw behind
  the player's travel direction when moving and not using the mouse (serialized
  `autoAlign`, `autoAlignDelay` 0.4s, `autoAlignSpeed` 3, `autoAlignMinSpeed` 1.2,
  `mouseActiveThreshold`). Mouse always takes priority; holding forward is stable;
  smooth LerpAngle, never snaps. This addresses the main "constantly fix the camera"
  complaint. Applies to both scenes (shared camera).
- Doors already widened/unstuck in Task 3 (DoorFixup jamb colliders) — no catch.
- DEFERRED / inspector-tunable (left for the team to dial in): faster base move
  speed (`AstronautController.moveSpeed`, currently 2.2 — bump in the inspector if
  travel feels slow) and a waypoint arrow to the active station (pairs with Task 15).
  Travel-time-causing-failures is partly mitigated by Task 4's response-window
  scaling + Task 11 (no spawn while docked); revisit windows/distances if a
  playtest still shows travel-driven misses.
- Verified: compiles, no errors. Camera feel needs a human playtest (motion — not
  screenshot-able). Tune `autoAlignSpeed`/`autoAlignDelay` if it feels off.

## ✅ Task 11 — Don't start a task while the player is docked elsewhere  (DONE — pending playtest)
- `GameManager.TaskSpawnLoop` now skips spawning (rechecks every
  `spawnRecheckInterval`) while `StationDockController.Instance.IsDocked`. Covers
  the code-memory main-screen flash and every task type — you can't attend to a
  new task while heads-down at a console. Resumes automatically on undock (next
  recheck); no deadlock (it's a poll, the player will undock).
- Tasks spawned earlier while roaming keep ticking during a dock (legitimate
  triage); only NEW spawns during a dock are suppressed.
- Verified: compiles, no runtime errors, spawn loop runs normally when undocked.
  Needs a human playtest: dock at one station and confirm nothing spawns until
  undock.

## ✅ Task 12 — Comms (Stroop): make the rule clear  (DONE — pending playtest)
- Implemented Option B. The rule no longer flips every round; the 6 rounds are now
  TWO blocks of 3 with exactly ONE rule switch. Each block's rule is announced:
  a plain "RULE" intro for block 1 and a loud "NEW RULE" callout (console splash +
  HUD alert banner + sound + 1.8s pause) for the mid-task switch.
- The current rule is shown every trial as a persistent, color-coded banner
  ("RULE: MATCH THE INK COLOR" cyan / "RULE: MATCH THE WORD" amber).
- First block's rule is randomized per instance (order-effect control); the set-
  shift trial is preserved for the Shift/flexibility measure. New `STROOP_RuleBlock`
  log event records each block's rule.
- Task-7 first-time card copy updated: "the rule changes ONCE, halfway - watch for
  the big NEW RULE banner." Code: `Tasks/StroopTask.cs`.

## ✅ Task 13 — Shorten the Radar (CPT) task  (DONE — pending playtest)
- `RadarScanTask` now exposes `fullBlockSize` (12), `fullBlocks` (2) and `trialIsi`
  (1.5s) as serialized/inspector-tunable fields (were consts). Full mode dropped
  from 40 trials/~60s to 24 trials/~36s. Target rate raised 20%->25% so each block
  still has ~3 asteroids (6 total) for a valid hit/FA/d-prime, and 2 blocks remain
  for the vigilance-decrement comparison. Code: `Tasks/RadarScanTask.cs`.

## ✅ Task 14 — Vary tasks from the start (kill early repetition)  (DONE — verified)
- Replaced the weak anti-immediate-repeat (`lastSpawnedStationName`) with
  least-recently-spawned rotation in `GameManager.PickStation`: among eligible
  stations, pick the one spawned longest ago (never-spawned = oldest), random
  tie-break. This guarantees all 4 task types cycle in before any repeat and an
  immediate repeat can't happen. New `lastSpawnedAt` dict tracks per-station times.
- Calm pace (Task 4 ramp) unchanged — only the *selection* changed.
- VERIFIED via the spawn CSV (ran with quickTestMode for run-in-background, then
  reverted): first spawns were Engine -> LifeSupport -> Comms (3 distinct types,
  no clustering) vs the old Engine/Navigation-only pattern.
- Code: `GameManager.cs`. Connects to Task 4 + Task 16.

## ✅ Task 15 — Clear color-coded task priority + teach it in the tutorial  (DONE — verified)
- Replaced the single most-urgent row + "+N MORE" badge with a stack of up to
  MAX_ACTIVE (3) rows — EVERY live task gets its own color-coded row, so the player
  can compare and choose. Each row has a colored frame + countdown bar + priority
  tag.
- 3-color tier (no data-model change; deriving from priority + time keeps the
  serialized enum/CSV stable): RED "CRITICAL" (Critical priority), YELLOW "LOW TIME"
  (non-critical, <34% window left), GREEN "ROUTINE" (non-critical, plenty of time);
  any row <5s left pulses RED "EXPIRING". Rows keep spawn order (no flicker).
- Tutorial teaches it (tour step helper): "RED = urgent (do first), YELLOW =
  running low, GREEN = routine."
- VERIFIED live (quickTestMode run-in-background, then reverted): with two tasks
  active the HUD showed ActiveRow0 "NAVIGATION / LOW TIME" and ActiveRow1
  "ENGINE / ROUTINE", correctly stacked and color-tagged.
- Code: `UI/TaskListHUD.cs`, `Onboarding/TutorialDirector.cs`. **Merges with Task 4**
  (its "clear priority" half).

## ⬜ Task 16 — Add a second alternating task variant to some stations
- For 1–2 stations, alternate two variants (e.g. Comms: Stroop ↔ Go/No-Go;
  Engine: WorkingMemory code ↔ CodeMemory). Wire in the existing unused variants
  (`CodeMemoryTask`, `InhibitTask`) instead of leaving them dead.
- Each variant must still report to the same BRIEF-A scale; make alternation
  configurable (on/off or weighting).
- Code: `Tasks/*`, `CognitiveTaskCatalog`, `GameManager`. Connects to Task 14 and
  resolves the book's "use-or-remove unused variants" point.

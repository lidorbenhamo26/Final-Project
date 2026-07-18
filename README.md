# Mission: Focus

**A 3D Game-Based Assessment for ADHD**

A Unity serious-game that observes attention and executive-function performance during an immersive spaceship mission.

Mission Focus turns a battery of cognitive and executive-function (EF) probes into one coherent, immersive experience: the player operates a space station, and everyday "ship duties" double as cognitive tasks. Performance is logged continuously and compiled into an assessor-facing report.

> Software Engineering Department, Braude College of Engineering · Capstone Project 26-1-D-17

---

## Overview

Traditional cognitive test batteries are controlled and measurable, but often abstract and easy to disengage from. Mission Focus embeds the same measurement ideas inside gameplay, so attention, working memory, inhibition, and prioritization are exercised *in flow* — while the player simply "runs the ship." One session produces both an engaging experience and structured, exportable data.

## Key features

- **Cognitive task suite** — sustained attention / vigilance (radar scan, CPT-style), working memory (code memory), response inhibition (Go/No-Go), plus operational tasks (comms, engine, navigation, life-support, battery delivery).
- **In-flow executive-function events** — occasional high-workload moments where several tasks arrive at once, measuring planning, prioritization, and task-switching *inside* normal gameplay (no separate "test mode").
- **Immersive station** — a third-person astronaut that switches to a first-person view when operating a console, interactive props (carry a battery, dock it in a socket), ambient crew/automation chatter, and procedurally-synthesized sound effects.
- **Guided onboarding** — an interactive tutorial scene that teaches each mechanic hands-on before the mission begins.
- **Assessor report** — a per-session report that maps task performance onto executive-function domains (BRIEF-A-inspired, e.g. Task-Monitor and Plan/Organize) and exports to **HTML** and **CSV** for further analysis.

## Assessment & data

Every trial records accuracy, reaction time, and commission/omission errors. At the end of a session the report summarizes results by cognitive domain and writes both a human-readable **HTML** report and a raw per-trial **CSV** for statistical analysis.

> **Note:** Mission Focus is a research and educational prototype. It is not intended to provide a clinical diagnosis on its own. Its results are designed to support interpretation alongside questionnaires, interviews, and professional judgment.

## Tech stack

- **Engine:** Unity **6000.4.3f1** (Unity 6.4), Universal Render Pipeline (URP)
- **Language:** C# (100+ project scripts)
- **UI / Text:** TextMeshPro
- **Large assets:** Git LFS (textures, audio, 3D models, animations)

## Getting started

> **Important — this repository uses Git LFS.** Large assets (textures, audio, models, animations) are stored with Git LFS. Install Git LFS **before** cloning, otherwise those assets download as small pointer files and Unity will show them as missing.

```bash
# 1) Install Git LFS once per machine
git lfs install

# 2) Clone (LFS assets are fetched automatically)
git clone <repository-url>
cd Final-Project

# If you cloned before installing LFS, fetch the assets now:
git lfs pull
```

Then:

1. Open the project in **Unity 6000.4.3f1** via Unity Hub (matching the version avoids import differences).
2. Let Unity import and compile on first open (this regenerates the local `Library/`).
3. Open **`Assets/Scenes/StartScene.unity`** and press **Play**.
   - `TutorialScene` — hands-on onboarding
   - `MainScene` — the full mission

## Project structure

```
Assets/
  Scripts/
    Tasks/         cognitive & operational tasks (radar, code-memory, Go/No-Go, engine, …)
    Onboarding/    interactive tutorial flow
    Interaction/   carryable battery, sockets, station interaction
    Report/        assessment results + HTML/CSV exporters
    UI/            HUD, notifications, panels
    Audio/         audio manager + procedural SFX
    Editor/        scene-setup and asset tooling
  Scenes/          StartScene, TutorialScene, MainScene
  Resources/Audio/ ambient / music / SFX
  Characters/, Models/, Prefabs/, Materials/   game art & assets
Packages/          Unity package manifest (restored on open)
ProjectSettings/   Unity project settings
docs/
  PhaseA/          Phase A book + presentation
  PhaseB/          Phase B book (with user & maintenance guides) + A0 poster
```

## Documentation

All project deliverables live under [`docs/`](docs/), organized by phase:

**Phase A — [`docs/PhaseA/`](docs/PhaseA/)**

- [`MissionFocus_PhaseA_Book.pdf`](docs/PhaseA/MissionFocus_PhaseA_Book.pdf) — Phase A project book
- [`MissionFocus_PhaseA_Presentation.pdf`](docs/PhaseA/MissionFocus_PhaseA_Presentation.pdf) — Phase A presentation (PDF; editable [`.pptx`](docs/PhaseA/MissionFocus_PhaseA_Presentation.pptx) source alongside)

**Phase B — [`docs/PhaseB/`](docs/PhaseB/)**

- [`MissionFocus_PhaseB_Book.pdf`](docs/PhaseB/MissionFocus_PhaseB_Book.pdf) — Phase B project book (includes **Appendix A – User Guide** and **Appendix B – Maintenance Guide**)
- [`MissionFocus_Poster_A0.pdf`](docs/PhaseB/MissionFocus_Poster_A0.pdf) — A0 research poster

## Authors

- **Lidor Ben Hamo**
- **Yahli Rapaport**

**Advisor:** Dr. Moshe Sulamy — Software Engineering Department, Braude College of Engineering

## Acknowledgements

Built with Unity and TextMeshPro. Includes third-party art packs (Minimal Sci-Fi, Vintage Controls) used under their respective licenses.

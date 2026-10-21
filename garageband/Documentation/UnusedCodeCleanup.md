# Unused custom-code cleanup

21 October 2026. This removes the retired manual drum-seat path while keeping
the current automatic experience. No APK was built or installed. The Git index
was left unchanged; these cleanup edits are not staged.

## Recovery and restoration

The before-state snapshot, Git status, staged/unstaged patches, file hashes,
removed files and validation reports are saved outside the repository at:

`/Users/serafinanala/Library/Application Support/GarageBandVR/CleanupBackups/20261021T113741Z`

`ProjectBeforeCleanup.tar.gz` contains the original source, scenes, assets,
packages, project settings and documentation, including ignored recovery files.
`RemovedFiles/garageband/Assets/Scripts` holds the three removed scripts and
matching .meta files. `ArchivedRecovery/garageband/Assets` holds the moved
`_Recovery` folder and `_Recovery.meta`, with their original GUIDs.

Use the external `Restore.txt` and `TaskCleanup.patch` for targeted restoration.
The reverse patch affects working files only; it does not reset the Git index.
Check it before applying if further changes have been made. Do not overwrite
newer recovery files or unrelated work when restoring the archive.

## What was removed and why

- `SeatViewOnSelect.cs` and its .meta: old mouse/XR-select seat teleport code.
  Its only component was a disabled one in the recovery scene. No current
  script installs/enables it, and no current scene, prefab or event needs it.
- `DrumSeatUI.cs` and its .meta: its ShowUI method was only called by the
  inactive MusicRoom Teleport Anchor. Proximity and automatic seating already
  show recording UI. Its component was removed from DrumSeatView.
- `DrumSeatLock.cs` and its .meta: its LockMovement callback was only assigned
  to the same inactive anchor. Current controls and automatic sequences keep
  manual movement disabled. Its component was removed from DrumSeatView.
- The inactive MusicRoom Teleport Anchor prefab instance, its stripped
  Transform, parent-list entry and callbacks. The shared template prefab and
  the SampleScene instance were not changed.
- The kit interactable's SelectEntered -> EnableDrumMode callback. This was
  the remaining manual-selection shortcut. EnableDrumMode itself is retained:
  automatic seating, recording, retake and performance return still call it.
- `QuestUI.Button()`: an unused static button factory. No C# callers, Inspector
  events, animation events or dynamic lookups used it; existing buttons use
  their current authored/runtime setup paths.
- `DrumRecorder.HasRecording`: an unused read-only property. The private
  hasRecording field and all recording-state checks remain unchanged.

`Assets/_Recovery` contained one old full scene, `0.unity`, and its .meta.
It was not identical to the current scene, was untracked/ignored, and still
contained the retired seat scripts. Neither its scene nor folder GUID was
referenced by another asset. It was not a build scene and was not loaded by
custom code, Resources or Addressables. It was moved, not destroyed.

## What remains

DrumSeatView, its child calibration, both automatic-sequence references, all
active colliders and tolerance meshes are unchanged. DrumProximity keeps its
existing distance calculation, recording-UI visibility and interactable
reference. The kit interactable remains because proximity requires it.

All 16 remaining custom scripts have current roles:

| Script | Current use |
| --- | --- |
| IntroSequence | Onboarding, musician choice, routes and automatic seating |
| PerformanceSequence | Performance/seat views, Pause/Delete and drummer setup |
| DrumRecorder | Hit timestamps, recording, preview, saved data and performance clock |
| DrumSound | Drum audio, kick input and recording notification |
| DrumstickHit | Player-tip trigger contacts and cooldown |
| DrumProximity | Distance-based recording UI and kit interactability |
| DrumModeVisuals | Player sticks, controllers and menu pointers |
| QuestExperienceControls | Runtime installation, A/X input, UI setup and movement lock |
| QuestControllerMenuRay | Runtime controller aiming, trigger and Help scroll input |
| RecordingButtonHover | Tracked-pointer labels for recording controls |
| HelpMenu | Help visibility, modal input blocking and menu restoration |
| HelpPanelLayout | Help scrolling, close button and layout |
| QuestUI | Shared canvas/text/background/layout helpers |
| CharacterMotion | Walking/idle body movement; suspension for seated drumming |
| ProceduralDrummer | Seated pose, timed arm movement and musician stick visuals |
| Editor/ExcludeSimulatorFromBuild | Unity build-scene callback; retained Editor tool |

Unity lifecycle methods, pointer interfaces, runtime-initialization callbacks,
build interfaces and Inspector button callbacks are retained even where they
have no direct C# caller. The saved-data hits/duration fields are consumed by
JSON serialization; recordedHitCount is retained for Inspector diagnostics.

CharacterMotion's WalkingWeight/IsSuspended/UsesClips readouts have validation
references. Some references are in historical tests; their wider test usage
is uncertain, so they were kept. Shared/template teleport areas and movement
providers were also left alone: they are outside custom-code cleanup and the
current controls already lock manual movement. Historical tests outside Assets
remain reference documents, not compiled gameplay code.

The locally generated, ignored Assembly-CSharp.csproj had only its three
removed Compile entries updated, so VS Code does not keep missing-file items.
Its original copy is under LocalToolingBefore in the external backup.

## Checks and limits

- Audited 19 initial custom scripts / 491 method, field and property
  declarations with a semantic C# model, plus serialized events, all scenes
  and prefabs, animation/import event names and runtime/dynamic lookups.
- Editor, Android-player and Editor/build-tool C# compilation passed.
- Script GUID checks across 7 remaining scenes and 117 prefabs found no
  missing scripts. No new dangling local file references were found.
- Unity loaded MusicRoom and 117 prefabs: 189 persistent event calls checked,
  zero missing scripts and zero broken event targets/method names.
- Isolated Unity Play Mode passed 93 assertions for Mika and 93 for Ren,
  including actual onboarding and choice callbacks, three recording/save/
  performance/delete cycles each, preview Pause/Resume, performance Pause/
  Resume, Delete while paused, Help blocking, hover text changes, no duplicate
  sticks or pointers, restored standing animation and unchanged seat marker.
- The tests invoke authored button events and inject A/pointer events. They
  do not prove physical Quest ray aim, controller tracking, headset visuals,
  subjective strike responsiveness or audible headset output.
- Only the test process used a temporary in-memory desktop XR settings copy
  and a separate test recording folder. Production XR settings and existing
  recordings were not edited. An unrelated Search-index exception from the
  copied Editor cache was excluded from test failure handling and logged.
- Original scripts outside the listed removals, shared assets, SampleScene,
  packages, XR/build/signing settings and previous APKs/logs match the snapshot.

No new Inspector assignment is required. Review/reload MusicRoom from disk
before saving an older open scene over it; preserve any unsaved Editor edits.
A later Quest pass should check onboarding with each musician, trigger/A
buttons, Help scrolling, drum hits and saved replay, hover labels, and at least
three complete recording/performance/re-record cycles with Pause/Resume.

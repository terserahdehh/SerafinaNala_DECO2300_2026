# Test the complete experience in Unity Editor

Open `Assets/MusicRoom.unity`, enter Play Mode, and click inside the **Game** view. No Quest, APK build, or deployment is needed. The experience still starts at the normal introduction; automatic walking, seated camera placement, Save blackout, performance playback, and Delete & Record Again all use the existing scripts and references.

## Controls

| Action | Editor control |
| --- | --- |
| Start, Next, musician selection, review buttons, Save, performance Pause, Delete & Record Again | Left mouse click on the visible UI button |
| Advance dialogue / start or stop recording when seated | `A` |
| Open/close Help | `X` or click the Help button |
| Pedal | `B` while the existing recording canvas is active |
| Show drumsticks (existing testing shortcut) | `P`; normal arrival at the stool already enables drum mode |
| Manipulate the left controller/stick | Hold **Left Shift** |
| Manipulate the right controller/stick | Hold **Space** |
| Position the selected controller | Move mouse; **arrow keys** move sideways/forward/back; **Q/E** move down/up; mouse wheel adjusts depth |
| Rotate the selected controller | Hold **Left Ctrl** while moving the mouse, or use the simulator's existing `R` transformation-mode toggle |
| Look around | Hold **right mouse button** and move the mouse |
| Toggle left/right controller selection | Existing simulator `T` / `Y` controls |
| Stop manipulating devices | `Esc` |
| X/Y/Z movement constraint | `F1` / `C` / `Z` (`X` belongs to Help) |

Hit the existing drum hit zones with the simulated controller sticks. They use the existing `DrumstickHit → DrumSound → DrumRecorder.RecordHit` collision path, so the sound and saved timestamps are the same as when using controllers. There are no new keyboard shortcuts that generate recorded hits directly. Start with `A` to record, select a stick with Shift or Space, move it above a drum with the arrows/mouse, then lower it with Q or mouse motion. Swing through the drum zone, lift, and strike again. `B` uses the existing pedal path.

Release device-manipulation keys (or press Esc) when using menus. Mouse movement alone does not drive the simulator's default FPS camera mode. The cursor stays available for UI clicks. Arrow positioning replaces the simulator's conflicting A/D movement; the experience's A/X/B/P keys retain their existing meanings. The default simulator overlay is hidden because it advertises conflicting default bindings. Controller mode remains selected; its hand-mode and body/FPS toggle shortcuts are suppressed for this experience.

## Inspector setup

**No new assignments are required in the current scene.**

- Keep exactly one **XR Device Simulator** instance, with its existing input action references. It can remain **inactive in Edit Mode**, as currently authored. `EditorExperienceTesting` finds and enables that instance during Play Mode and points it at the existing XR Origin camera. It does not instantiate another simulator.
- Keep the existing **EventSystem → XR UI Input Module**. Desktop testing enables mouse input on this module and adds/enables a normal Graphic Raycaster on UI canvases. It disables competing input modules and additional EventSystems at runtime. Do not add a separate Standalone Input Module or Input System UI Input Module.
- Keep **Input System Package (New)** as Active Input Handling, as currently configured. The simulator requires action-based tracking on the existing camera/controllers.
- Keep **XR Device Simulator Settings → Automatically Instantiate Simulator Prefab** off, as currently configured. The scene already supplies the simulator.
- Keep all IntroSequence, DrumRecorder, PerformanceSequence, drumstick/controller, hit-zone, camera and teleport-point references unchanged. The new Editor component installs itself on the XR Origin through the existing experience-control bootstrap.
- Seated musician references and visual calibration are described in `ProceduralDrumming.md`. Editor mode makes that calibration testable without a headset; it does not replace the seat/hand/target calibration.

If the Console reports a missing/duplicate simulator, restore the existing scene simulator or remove the extra instance. A missing simulator is reported instead of silently creating one.

## Editor/build separation

`EditorExperienceTesting` is entirely guarded by `UNITY_EDITOR`. Before XR Management starts, it uses a temporary in-memory XR settings clone with automatic hardware XR startup disabled. This lets Input System simulated tracking run without an OpenXR headset/runtime. Saved XR settings and Android loader settings are not edited; the original settings instance is restored when leaving Play Mode.

Editor UI uses mouse events on the existing XR UI Input Module. XR pointer UI processing is disabled in this mode to prevent a simulator-trigger click and a mouse click from activating a button twice. Simulated pose tracking and physical stick collisions continue to work. Runtime-created canvases use `QuestUI`; authored canvases, including the blackout overlay, receive mouse raycasters at startup. A click consumed by UI cannot also invoke the old mouse-to-drum-seat shortcut through a collider behind the panel.

Quest/player compilation excludes the Editor bootstrap and keyboard/mouse testing shortcuts. The existing XR UI module continues to use headset/controller rays and trigger input. `Assets/Editor/ExcludeSimulatorFromBuild.cs` removes simulator objects from **build scene copies** before a future player build. It does not change the saved scene or Play Mode scene. Manual locomotion remains locked by the existing controls; IntroSequence and PerformanceSequence retain ownership of automatic movement and camera transitions.

No APK was built for this change.

## Suggested visual pass

1. Press Play, click Start, advance dialogue with A or Next, and choose Mika. Check automatic player movement and the transition to the stool.
2. Record stick hits and B pedal hits. Check review playback, pause, retake and re-recording.
3. Click Save. Check blackout, player relocation, Mika's seated pose, alternating strokes and looping audio. Check performance pause/resume.
4. Click Delete & Record Again while playing and while paused. Verify the return blackout, restored recording UI and functioning sticks.
5. Repeat the whole flow with Ren. Check seat fit, palms/wrist roll, stick-tip contact and elbow bends as described in the drumming guide.
6. Exit and re-enter Play Mode to confirm simulator/input setup starts cleanly. The final Quest hardware visual/input pass can be done later; it is not required for this desktop iteration loop.

## Validation performed

Both Editor compilation (including the build-scene processor) and player compilation with `UNITY_EDITOR` excluded passed against this project's Unity/compiler references. An isolated Unity Play Mode session using the installed XR packages and existing simulator prefab passed simulator activation, a single active EventSystem/input module, collision-free experience key bindings, scripted camera teleport, runtime canvas mouse raycasting, and exactly one button activation from an injected mouse click. The click test used explicit input/module updates because a batch session has no focused Game view. The complete authored scene, physical stick reach and both musician meshes still need the visual pass above.

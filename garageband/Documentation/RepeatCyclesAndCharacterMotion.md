# Repeat performance interaction and character motion

## Investigation and interaction repair

`DrumRecorder` owns the recorded hit list and audio/playback clock. `PerformanceSequence` owns fades, seating, player relocation and the performance buttons. `DrumModeVisuals` selects drum versus menu controls, and `QuestControllerMenuRay` owns per-ray input actions and XR UI registration. The authored Pause and Delete buttons retain their persistent `TogglePerformancePause` and `DeleteAndRecordAgain` listeners; this change does not add or replace listeners.

The original code reset playback flags and stopped the playback coroutine, but performance entry only activated/repositioned its UI. It did not restore disabled Button/CanvasGroup/raycaster state or explicitly renew an already-active menu ray's actions/registration. The recording canvas could also remain/reactivate through `DrumProximity`, leaving a separate UI raycast surface in the performance stage. These are interaction-boundary weaknesses, rather than a reason to reset the recording clock or musician selection.

**Limit of diagnosis:** the reported Quest-only failure was not reproduced in the isolated Editor session. Before changes, three complete cycles also passed with simulated controller UI clicks, including Save and active proximity behaviour. The connected Quest app was not running and no relevant retained Unity logs were available. This work repairs the missing per-entry interaction restoration and verifies it under deliberately stale input/UI conditions; it does not establish which external state caused the user's headset failure. Confirm the original reproduction on Quest before calling the hardware issue closed.

Every performance entry now restores its own Canvas, CanvasGroup, tracked/Editor raycasters, button interactability and graphic raycast targets; clears blackout blocking; and restores menu rays/actions/UI registration. A genuine open Help modal continues to suppress performance interaction. Recording Canvas rendering and raycasting are disabled during performance/transition, even if proximity reactivates its GameObject, and restored for recording/review. No new EventSystem or XR UI module is created by the repair.

A single owned transition coroutine and state guards reject overlapping Save/Delete transitions. Disabling the sequence cancels its transition, releases the drummer and clears transition state. Pause is ignored while leaving performance. Repeated ray initialization does not allocate another action set; each ray retains its own enable/disable/dispose lifecycle. Existing recording timestamp calculations, loop scheduling, audio dispatch, musician selection and controller bindings are unchanged.

## Walking and relaxed standing

`IntroSequence.Awake` installs one `CharacterMotion` on each movement root, including inactive musicians. No new Inspector references are required in the saved MusicRoom scene: the existing `NewWalk` and `NewIdle02` clips are assigned to the new **Standing and Walking Animation** fields on IntroSequence.

- Ren's imported FBX has a valid Humanoid avatar. CharacterMotion samples the existing walk and idle clips through a manual PlayableGraph/mixer. Some idle is retained in the walk blend to soften the supplied clip's exaggerated knee/arm lift. Source animation assets/controllers are not edited.
- Mika and Ms Maya are GLB named-bone rigs without compatible Humanoid avatars or imported clips. They use procedural bone animation: distance-driven alternating steps with knee flexion, lowered arms and opposite arm swing; standing uses lowered arms, gentle pelvis movement and spine breathing.
- Real movement-root displacement drives walk phase and a 0.22-second walk/idle blend. Stationary dialogue, waiting and turns use idle. Existing entrance, route/following, final-approach and musician-exit movement code still controls speed, positions and paths. The ineffective `IsWalking` Animator parameter calls were removed; the supplied Ren controller does not have that parameter and the scene Animator fields were unassigned.
- CharacterMotion never translates/rotates the actor root. It disables Animator root motion while owning animation, preserves the imported animation-root local transform during clip evaluation, and restores Animator states when giving up control. Large discontinuities/teleports are excluded from stride distance. Procedural feet are relative to the imported skeleton frame, retaining model corrections and offsets.
- Before ProceduralDrummer caches the seated reference, CharacterMotion suspends and restores its original bone reference. Its PlayableGraph is destroyed and it stops writing bones. Release restores the procedural drummer's reference and returns ownership to idle/walk when active. OnDisable/OnEnable and repeated Seat/Release calls respect this ownership. The musician drumstick prefab, grip calibration and seated arm/leg IK are retained.

## Validation actually performed

No APK was built, installed or uninstalled. No commit or push was made. Saved XR settings, packages, Android version/signing settings and existing APKs were not changed.

The final code compiled in Unity 6000.3.20f1 in an isolated clone of the current project. A separate Roslyn compilation against the project's Unity references passed with `UNITY_EDITOR` defines removed, exercising the player code path without building a player/APK.

The isolated Play Mode integration check passed **74 assertions**, including:

- Actual imported Maya/Mika/Ren skeletons and the saved scene references; Ren selected compatible clips and Maya/Mika selected the named-bone fallback.
- Lowered idle arms, no actor-root motion during idle, walk activation from measured translation, return to idle after stopping, and preservation of Ren's imported Animator-root transform. Idle/walk camera renders for all three were visually inspected.
- Six complete record → Save button → performance → Delete button cycles, three for Mika and three for Ren. InputSystem-generated right XR controller states drove real XRRayInteractor/XRUIInputModule UI clicks, rather than calling the Pause/Delete listeners directly.
- Pause and Resume on every cycle; the seated hand pose stayed fixed during a paused interval, with standing motion suspended.
- Recovery from deliberately disabled performance Button/CanvasGroup state, disabled ray trigger/value actions and disabled tracked raycasting before entry. Duplicate Save/transition attempts were rejected.
- Recording UI raycasts disabled during every performance; Help blocked controls while open and restored them on close; original listener counts stayed constant; exactly two menu rays and one XR UI module remained after six cycles.

The test selected musician state directly and shortened fade delays. It did not execute the complete authored introduction/route UI sequence or listen to headset audio. Synthetic controller input, accelerated transitions and camera renders do not verify physical Quest bindings, final stride/foot sliding, mesh deformation, comfort or the original hardware-only bug. The hardware checklist below remains required.

## Quest checklist

Run **three complete cycles with Mika**, then restart/select **Ren and run three complete cycles again**. Do not treat a successful first performance as sufficient.

| Cycle for each musician | Required checks |
| --- | --- |
| 1 | Record several drums and pedal, review and Save using the controller ray. During performance Pause, wait, Resume, then Delete & Re-record. Confirm both buttons hover/click, the pose/audio resume together, and recording controls/player sticks return. |
| 2 | Record a different beat and Save again. Pause and Resume; open/close Help and confirm scrolling, normal environment brightness and blocked controls behind Help. Pause once more and Delete while paused. Confirm return to recording and no stuck overlay, disabled ray, duplicate sticks or unexpected sounds. |
| 3 | Record/Save again, including a hit near the loop boundary. Pause/Resume several times across strikes and gaps; try a rapid repeated Save/Delete click and verify only one transition. Delete & Re-record and confirm a further take can still start. |

For each selection, watch Ms Maya's entrance, initial approach, follow route, waypoint pause, dialogue and final approach. Check that all three stand with relaxed arms while waiting, that walking occurs only during actual translation, and that Mika/Ren leave with alternating steps and return to idle when stopped. Check feet against the floor, knee bends, opposite arm swing, body orientation/scale and speed; refine procedural amplitude or clip blend only after headset observation.

For every performance entry, confirm the selected musician sits in the preserved pose with exactly two sticks, no walk/idle motion overwrites drumming, Pause freezes the hands, Resume continues the beat, and Delete removes the musician sticks under blackout. Confirm player/controller sticks, A/B/X controls, recording hit count/timing, preview playback and selected musician remain correct throughout all six cycles. Capture Unity/adb logs if the original second-performance failure recurs; the Editor result does not rule out a device-specific input interruption.

# Procedural musician performance

Implemented in `Assets/Scripts/ProceduralDrummer.cs`, integrated into the existing `PerformanceSequence` and `DrumRecorder`. No downloaded animation, Animator Controller parameter, animation package, or paid asset is required.

## Scene and Inspector setup

Open `Assets/MusicRoom.unity` and select **Performance Manager → PerformanceSequence → Procedural Drumming**. The scene already assigns both rig profiles to **DrumSeatView → Musician Seat Point (calibrate hips)** and assigns the existing drum hit zones. The component is created on the selected movement root at runtime; do not add a second animation component.

The seat marker starts 0.58 m below DrumSeatView, as an estimate. In Scene view, place it at the desired **hips bone pivot** above the stool cushion, with its blue Z arrow pointing toward the kit. This is a hips marker, not a feet, floor, or headset marker. Keep it outside both musicians. Do not move DrumSeatView itself: the player return flow uses that point. For different pelvis-to-cushion proportions, use each rig's `Hips Offset` in seat axes, or duplicate the marker and assign separate Mika/Ren seat points. Both must represent the same stool.

Keep the existing Main References, Player, Transition, Performance UI, Mika, and Ren references:

| Existing field | Assignment |
| --- | --- |
| Mika | `Mika Movement Root` |
| Mika Rotation Target | The imported child Transform already assigned (GLB root containing the orientation correction) |
| Mika Performance Point | Existing `Mika Performance Point` |
| Ren | `Ren Root` |
| Ren Rotation Target | Imported `Ren` child of `Ren Root` |
| Ren Performance Point | Existing `Ren Performance Point` |

Do not reset imported children to identity or zero their local positions. Mika's GLB has approximately -90° X / -180.693° Z correction; Ren's imported child has a large local translation. Existing placement runs first; procedural seating then adds yaw to the movement root and translates it by the **actual hips-to-seat difference**, preserving child corrections and offsets.

### Bones and Animators

Each profile has its own `Animator` and bone fields. Explicit bone assignments take priority. Otherwise, the code searches inactive children, uses `GetBoneTransform` only when the avatar is present, valid and Humanoid, and falls back to the exact imported naming patterns below. The current scene can therefore leave bone fields blank. For explicit setup or a changed import, drag these bones from the selected character's own hierarchy:

| Profile field | Mika bone | Ren bone |
| --- | --- | --- |
| Hips | `J_Bip_C_Hips_124` | `Hips` |
| Spine | `J_Bip_C_Spine_75` | `Spine` |
| Left Upper Arm | `J_Bip_L_UpperArm_52` | `Left arm` |
| Left Forearm | `J_Bip_L_LowerArm_51` | `Left elbow` |
| Left Hand | `J_Bip_L_Hand_50` | `Left wrist` |
| Right Upper Arm | `J_Bip_R_UpperArm_71` | `Right arm` |
| Right Forearm | `J_Bip_R_LowerArm_70` | `Right elbow` |
| Right Hand | `J_Bip_R_Hand_69` | `Right wrist` |
| Left Thigh | `J_Bip_L_UpperLeg_99` | `Left leg` |
| Left Shin | `J_Bip_L_LowerLeg_98` | `Left knee` |
| Left Foot | `J_Bip_L_Foot_97` | `Left ankle` |
| Right Thigh | `J_Bip_R_UpperLeg_123` | `Right leg` |
| Right Shin | `J_Bip_R_LowerLeg_122` | `Right knee` |
| Right Foot | `J_Bip_R_Foot_121` | `Right ankle` |

Mika's hierarchy is `Mika Movement Root → imported GLB hierarchy → Armature_130 → … → Root_125 → J_Bip_C_Hips_124`. Ren is `Ren Root → Ren → Armature → Hips`. Do not assign shoulder bones as upper arms or finger bones as hands.

Both original Animator fields on PerformanceSequence are unassigned in the scene. They may remain blank. In each procedural profile, optionally assign the Animator found inside that character; otherwise it is discovered automatically. Mika's GLB contains zero animation clips; its glTFast Mecanim import does not supply a Humanoid avatar, so named/manual transforms are the supported path. Ren's actual FBX was imported in an isolated Unity 6000.3.20f1 project using its existing .meta settings: it creates an Animator with a present, valid Humanoid avatar. Runtime validity is still checked rather than assumed. The Console reports whether the selected model uses a valid Humanoid Animator or explicit/named transforms. If validation reports missing bones or invalid chains, fill in the corresponding profile using the table.

All Animators on/under the movement root and on its ancestors are disabled while the pose is owned by the procedural component. Their prior enabled states are restored when the musician is released. No Animator speed or `IsPerforming` parameter is required. If you add other scripts or constraints that write these bones, disable those separately during performance.

### Seat and feet

`Seat Point` is required for each profile and is already assigned. `Left Foot Point` and `Right Foot Point` are optional stationary world markers outside the musician. Leave them blank for an automatically bent seated knee with the foot beneath it. For pedal placement, put each marker at the corresponding ankle pivot, not the shoe sole. An unreachable marker is clamped to the limb's reach. Foot orientation retains the imported standing orientation after facing the kit.

### Drum targets

The scene uses the existing hit-zone Transforms as initial tip contact targets:

| Sound Name (AudioClip.name) | Target | Hand |
| --- | --- | --- |
| `snare` | `SnareHitZone` | Alternate |
| `hihat` | `HihatHitZone` | Right |
| `high tom` | `HighTomHitZone` | Alternate |
| `mid tom` | `MidTomHitZone` | Alternate |
| `floor tom` | `FloorTomHitZone` | Alternate |
| `cymbal` | `CymbalHitZone` | Alternate |
| `pedal` | None | Pedal |

These are **positions**, not collider surface calculations. Inspect the zone pivots: if one is below/above the playable surface, create a small empty child at the desired stick-tip contact and assign that child instead. Do not move existing collision hit zones to correct visual animation. Mapping ignores name case and surrounding whitespace; otherwise names must match the recorded clip names. An unmapped sound logs once when preparing the take and still plays its original audio. Pedal hits preserve their audio and do not incorrectly swing a stick; foot-pedal animation is not implemented.

### Drumsticks and motion tuning

The saved MusicRoom scene assigns `Assets/Prefabs/MusicianDrumstickVisual.prefab` to **both** rig profiles. This new visual-only wrapper reuses the mesh and material from `Assets/lowpoly_drumstick.glb`. It contains only Transforms, a MeshFilter and a MeshRenderer: no colliders, Rigidbody, XR interaction, hit/recording scripts or AudioSource. The player/controller stick instances and their hit scripts are unchanged.

The wrapper has its palm grip at local origin and its tip at local +Z, 0.34 m from the grip, matching the existing animation's `Stick Length`. The original mesh is uniformly scaled to 0.44 m overall, leaving 0.10 m behind the palm. The `Tip (visual reference only)` child is an inspection marker, not a hit zone. Keep the prefab's mesh correction intact; tune the rig grip fields rather than moving player sticks or changing the mesh asset. If changing `Stick Length`, adjust the visual wrapper's grip-to-tip distance to match.

Runtime seating creates independent **Left/Right musician drumstick grip** children on the resolved hand bones, and instantiates a separate visual beneath each. Local position/rotation stays attached to the hand throughout the existing animation; scale compensation keeps the visual in world metres and handles mirrored import scale. The component still sanitises custom visuals before activating their inactive grip: all MonoBehaviours, colliders and AudioSources are disabled; any Rigidbody is made kinematic with gravity and collisions disabled. The cylinder remains a fallback only for other scenes/profiles with no visual assigned.

The current skeletons were inspected in Unity: Mika uses `J_Bip_L_Hand_50` / `J_Bip_R_Hand_69`, Ren uses `Left wrist` / `Right wrist`; both have unit world hand scale. Mika's middle-finger bases are approximately 0.066 m from the wrist; Ren's are approximately 0.060 m. The following separate starting calibrations place the grip inside the palm, slightly toward the thumb and below the finger-base plane:

| Profile | Left grip offset | Right grip offset | Left / right grip Euler |
| --- | --- | --- | --- |
| Mika | (-0.004, 0.040, -0.009) | (0.004, 0.040, -0.009) | (0, 0, -8) / (0, 0, 8) |
| Ren | (-0.003, 0.036, -0.008) | (0.003, 0.036, -0.008) | (0, 0, -4) / (0, 0, 4) |

Offsets use hand-local units; Euler values are degrees relative to the existing automatic wrist-axis calibration. The mirrored roll adjustments are initial visual tuning, not hardware-verified grip fits. Neither profile changes hips/seat/feet settings, target assignments, stroke timing or IK logic. Fingers retain their existing pose; no finger-closing animation is added.

**No additional Inspector assignments are needed in the saved scene.** On **Performance Manager → PerformanceSequence → Procedural Drumming**, check both `Drumstick Prefab` fields reference the musician visual. In Play Mode inspect each hand's generated grip to judge palm intersection and tip direction; change the corresponding rig's offsets/Eulers for persistent tuning. Runtime-created grip edits do not persist after leaving Play Mode.

Sticks appear with seating under the existing performance blackout, remain visible while paused, and are deactivated immediately on Release/OnDisable before deferred destruction. Starting performance again creates a fresh pair with no visible duplicate pair. Audio recording, playback clock, pause/resume, controls and return transition are unchanged.

Defaults: `Lift Height = 0.18` m, `Strike Duration = 0.09` s, `Recovery Duration = 0.18` s. The stroke anticipates the timestamp, reaches contact at the recorded hit, then recovers. Closely spaced strokes compress these phases. Alternate targets alternate hands across the take; fixed-handed targets do not advance the alternating sequence. One hand cannot represent two distinct simultaneous hits; configure left/right assignments for common chords if necessary. Targets and seating are sampled at performance preparation and remain stationary during the loop.

## Timing and lifecycle

DrumRecorder still uses its existing coroutine, `Time.deltaTime`, hit dispatch and `AudioSource.PlayOneShot`. The animation evaluates **that same playbackTime**, using the recorded hit list and the same safe loop duration; it has no Update timer and produces no audio. Audio scheduling, clip names, saved JSON, review playback and recording are unchanged. As before, playback is frame-dispatched, not DSP/sample-accurate.

During Save's existing full blackout, the selected character is activated, placed, seated and given sticks. Performance begins at the existing point after fade-in. Pausing skips the playback clock and pose evaluation and pauses the existing audio source. Stop freezes the pose. Delete & Record Again stops playback immediately, holds the last seated pose during fade-out, and releases the procedural rig and restores its original bones/Animator state only once the screen is black. The original return-to-recording transition continues.

Every frame restores a cached seated bone pose before solving the arms. IK uses actual segment directions and explicit bend hints, not assumed bone-local Euler axes. The movement root is held at its seated position/rotation throughout evaluation. Disabling the musician also releases the pose and restores previous Animator enabled states.

## Verification and remaining Unity visual testing

The project scripts compile with the installed Unity 6000.3.20f1 compiler and the project's actual references. An isolated Unity batch validation passed hips alignment, bent knees/feet beneath knees, alternating left/right stick contact, visible recovery, loop seam continuity, 1,000 repeated evaluations without drift, and original pose/root/Animator restoration on synthetic rigs with different model corrections and offsets. The actual imported Ren FBX also passed bone resolution and seating checks with its valid Humanoid avatar. Pause freezes by skipping evaluation in the recorder coroutine; full UI pause/resume remains part of the visual integration pass. These checks do not establish the final visual fit of the imported meshes.

In Unity, test both selections separately:

1. Record snare, hihat, each tom, cymbal and pedal, including quick patterns, gaps, simultaneous hits and a hit near the loop boundary. Test review playback, retake and Save.
2. Confirm seating is hidden by blackout. Calibrate the seat marker/hips offsets against the actual stool cushion; verify facing, bent knees, ankle positions, shoe-floor contact and no seat/mesh clipping for both models.
3. Verify the generated/imported sticks sit in the palms, wrist roll is natural, elbows bend outward, forearms visibly reach, and stick tips meet the intended surfaces. Adjust individual target markers, grip offsets/Eulers, or seat distance if targets exceed reach. IK clamps reach rather than stretching bones.
4. Let several loops run and check for root drift, limb twist, loop discontinuities and duplicate drum audio. Confirm no animation affects preview playback or recording.
5. Pause during a strike and during recovery, wait, then resume; arms and audio must resume together. Delete while playing and while paused; confirm sticks disappear under blackout, only the chosen character was visible, and the player returns to recording with working controller sticks and menus.
6. Repeat Save/Delete for both musicians and test the final flow on the Quest. Mesh deformation, exact physical seat fit, wrist calibration and VR appearance require this visual pass; they cannot be verified by compilation or synthetic-rig checks.

## Musician-stick validation (19 October 2026)

No APK was built. An isolated clone of the current project compiled and passed Unity Play Mode checks using both actual imported character skeletons and the saved MusicRoom rig profiles. Each selected musician had exactly two enabled mesh visuals parented through grips to the correct hands. The prefab contained no physics, XR, MonoBehaviour, or audio components; mesh/material references resolved and the material shader was supported. Actual mesh tips matched the existing 0.34 m grip-to-tip reach. One thousand evaluations per rig preserved the seated root and leg pose. Five consecutive Seat calls kept exactly two active grips; Release and OnDisable hid all sticks immediately, restored the original bone/root pose, and allowed restart. Full-body and palm camera renders for both musicians were inspected.

This establishes attachment, mesh visibility and lifecycle in Editor Play Mode, not final headset ergonomics or all drum-surface contacts. On Quest, visually check both left/right palms from the normal performance viewpoint, imported finger pose, wrist roll, every drum target and fast/alternating strokes; then pause/resume and repeat Delete/Record/Save. Verify sticks remain attached while paused, disappear on return, never create duplicate sounds/recordings, and the player's original sticks and controls still behave normally. The recording/playback/control scripts were not changed by this work.

## Standing/walking animation ownership

CharacterMotion now owns standing idle/walking and explicitly yields before ProceduralDrummer caches the seated pose. Release returns ownership to standing animation when the actor is active. See `RepeatCyclesAndCharacterMotion.md` for the repeated-cycle interaction repair, clip/fallback choices, validation and six-cycle Quest checklist. Existing seat points, grip settings, drum target mappings and playback-clock integration are unchanged.

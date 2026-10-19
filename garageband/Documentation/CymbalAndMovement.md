# Cymbal contact and standing motion

The v9 build and the follow-up natural-strike correction are audited in [CymbalDeploymentAudit.md](CymbalDeploymentAudit.md). The 20 mm rearm and contact-frame speed gating described below document the earlier implementation, superseded by that follow-up.

Implemented in MusicRoom, 19 October 2026. No APK was built, installed, committed or pushed.

## Recording indicator

The user confirmed that the headset-only red icon is Quest's casting indicator. Application recording UI was left unchanged. Inspection found the existing Record image and Delete/Re-record image, no duplicate Record graphic or scene composition-layer component. OpenXR composition-layer support being enabled does not itself create an application overlay.

## Verified cymbal findings and changes

- Both player tips already had kinematic Rigidbodies, gravity disabled and Continuous Speculative collision detection. The drum trigger layer is enabled in the collision matrix. The cymbal AudioSource is enabled, unmuted and at full volume. These were not the failure.
- The old code only handled OnTriggerEnter, with a shared 100 ms cooldown across every drum per stick. Discrete trigger entry cannot reliably follow fast, transform-driven tip motion or repeat contacts while remaining inside an oversized zone.
- Tip collider radii were approximately 160 mm and 200 mm in world space; the left tip was also offset from the visual end. Both now use a 12 mm radius at the mesh endpoint (parent local Z = 4.86). The player's meshes and controller alignment remain unchanged.
- The old cymbal trigger was a world-aligned box roughly 0.58 x 0.55 x 0.93 m. The visible cymbal is a tilted disc of approximately 0.253 m radius, centred near (216.5513, -24.31078, -120.81725), with upward normal (-0.33714, 0.77423, -0.53564). The trigger and new CymbalStrikeSurface follow that disc rather than enlarging the old volume. A shallow raised-centre profile follows the mesh's bell.
- CymbalHitZone incorrectly referenced the hi-hat audio clip. It now references the existing cymbal.wav. The recorder already includes that clip, and the musician animation target already maps the name `cymbal`.

DrumstickHit sweeps the actual tip between rendered poses. CymbalStrikeSurface requires a downward surface crossing at at least 0.08 m/s toward the surface normal, inside the visible disc. Each stick must lift 20 mm above its contact height to rearm; the existing 100 ms cooldown is now per drum and per stick. Tracking jumps of 1 m or more in one frame are ignored. Other drums retain trigger-entry fallback, combined with swept contacts in a single nearest-contact decision; upward recovery does not play another drum. The two paths cannot double-play the same contact. Only player tips carry this component. Musician visual sticks remain without hit scripts or enabled colliders.

All accepted strikes still call DrumSound.PlaySound and DrumRecorder.RecordHit. Recording and replay clocks, saved data format and playback scheduler were not changed. DrumstickHit's optional `Diagnostic Logging` flag is off by default; it adds log output only. No HUD or input action was added.

## Mika and Ms Maya

Available NewWalk/NewIdle clips are Humanoid clips used by Ren. Mika and Ms Maya's existing generic named-bone GLB rigs do not have compatible animation clips, so CharacterMotion continues to use the existing procedural fallback for them. Ren's clip-based motion is unchanged.

Separate standing styles soften Mika's arm swing and give Ms Maya a slightly quieter cadence. Both keep a natural elbow bend, hanging arms clear of the torso, restrained torso counter-rotation, slower weight shifts and subtle breathing. Vertical standing axes use gravity rather than the imported hip/spine lean, preventing arms from resting behind the body. Reduced pelvis lowering and bounce avoid a crouched idle. Swing feet lift 45 mm, and stance feet remain at world-space anchors while walking; phase still follows distance travelled. Standing feet retain their original rest height. The existing 220 ms blend releases foot plants on stopping. Suspension, disable/reactivation and tracking jumps reset plants.

IntroSequence remains the only owner of translation, routes, speed and actor orientation. CharacterMotion still yields all bones to ProceduralDrummer before seating, restores ownership after release, and keeps Animator root motion disabled while it owns the pose. No path, seat, model scale/import correction, Help layout, XR settings, recording timing or Quest input code was changed.

## Validation and remaining device checks

See CymbalAndMovementValidation.txt for the actual isolated Unity checks. Tests use a cloned project and synthetic tracked tips/controller input; they do not establish headset audio output, physical tracking behaviour or visual quality on Quest. Front and side Editor renders were reviewed for Mika and Ms Maya. The new cymbal and motion changes are wired and require no Inspector setup. The isolated run also logged two pre-existing missing menu-scroll input-action errors because DrumModeVisuals has no Menu Input Actions reference. That field and XR settings were left unchanged; synthetic Help open/close passed, but physical Quest scrolling still needs verification.

Quest checklist for the next authorised build:

1. With each player stick, strike the cymbal centre and rim slowly and quickly, using natural downward strokes. Check clear cymbal sound, one sound per strike, silent upward recovery/resting contact, and reliable lift/re-strike. Alternate both sticks rapidly. Check nearby toms, hi-hat and snare still work and do not sound during cymbal-only playing.
2. Record a recognisable cymbal/snare pattern, replay it, then Save. Confirm the cymbal sound, order and spacing match, and musician sticks cause no extra sounds or recorded hits.
3. Complete at least three Record → Save → performance → Delete/Re-record cycles with Mika, then three with Ren. Pause and Resume on every cycle. Confirm no duplicate sticks, correct seated drumming, clean return to idle/walking and responsive buttons each time.
4. Watch Mika and Ms Maya during entrances, dialogue/waiting, route turns and final approaches. Check relaxed shoulders/elbows, subtle breathing, feet at floor height and little stance sliding. Watch musician exits and the transition into seated drumming. Assess clothing/hand clearance through the full walk, not only the sampled poses.
5. Open Help in recording and performance states. Check scrolling and close input, normal environment brightness and blocked controls behind Help. Confirm recording/replay timing, B pedal and Quest controls still behave as before. The Quest casting indicator remains controlled by the headset.

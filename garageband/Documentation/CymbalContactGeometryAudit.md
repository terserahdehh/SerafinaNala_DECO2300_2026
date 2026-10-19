# Cymbal-only contact geometry correction

Follow-up: the user authorised a narrow player-facing tolerance extension with unchanged visuals. See [CymbalReachTolerance.md](CymbalReachTolerance.md) for current collider geometry and testing limits.

19 October 2026. No build, install, commit or push. A local file/hash checkpoint was taken before changing the scene. Snare, other drums, character animation, UI and all gameplay scripts are unchanged.

## Device observations before the change

The connected Quest runs package com.DefaultCompany.VRTemplate, version code 11. The user performed the requested physical test and reported:

| Instrument | Left stick | Right stick |
|---|---:|---:|
| Snare | 5/5 heard | 5/5 heard |
| Cymbal | 0/5 heard | 0/5 heard |

Replay results were not reported, so no inference is made about how many cymbal events were recorded.

ADB capture was started before the test against the running app process. It captured app lifecycle, OpenXR focus/session and audio-stream activation events. It captured **no managed gameplay HIT or RECORDED HIT messages**. It therefore does not show the player-tip poses, trigger contacts, cooldown decisions, source submissions or recorded events for individual physical strokes. Absent gameplay messages must not be interpreted as proof of absent contact. A full on-device per-stage trace remains unavailable in this installed APK; no diagnostic APK was built under the current instruction.

## Comparison and verified local cause

Compared with the earlier responsive build, the player-tip sphere radii are 12 mm rather than approximately 160/200 mm, and the cymbal collider was changed from a large world-aligned box to a tilted box 35 mm thick. Both are retained build-input facts, not live Inspector readings from Quest. The restored shared script is the original immediate OnTriggerEnter → DrumSound.PlaySound path with a 100 ms per-stick cooldown. There are no shared nearest-contact, velocity, sweep or rearm filters remaining. Player tips retain their enabled solid sphere colliders and kinematic Rigidbodies; the cymbal is an enabled trigger on the same enabled Default physics layer. Its actual object is CymbalHitZone and its AudioSource references the existing cymbal.wav, not hi-hat. The original cymbal WAV has nonzero audio immediately (first-250-ms peak 0.9885); local playback also produces nonzero listener samples.

Geometry inspection and real physics queries established a specific defect: the highest visible cymbal bell is 43.70764 mm above the collider transform origin along its normal, but the old box's upper face is only 17.5 mm above that origin. With a 12 mm tip radius, a sphere touching the visible bell can still be separated from the trigger. A probe with 1.2 mm of valid sphere/surface overlap at the visible bell reported **old box: no contact**. This is a proven geometry gap. It is not a measured on-device trajectory or proof that every reported Quest strike landed at the bell.

The snare test was successful, so its collider and behaviour were deliberately left untouched. The global tip dimensions were also left unchanged to preserve the other working drums.

## Small targeted change

Only the cymbal's collider is replaced:

- MusicRoom's existing CymbalHitZone now uses a convex trigger MeshCollider in place of its BoxCollider. The same component file ID, GameObject, transform, layer, material/default collision filtering, enabled state, AudioSource and DrumSound are preserved.
- Assets/Physics/CymbalContactMesh.asset contains the cymbal component extracted from the existing drum-kit mesh: 520 source vertices and 892 triangles. Convex physics cooking succeeded. It follows the actual disc and raised bell; no new visible object or paid asset is introduced.
- The physical contact shape covers the bell and disc. It does not enlarge the lateral footprint. Eight points outside the disc were rejected, including the old square box's empty surrounding space.
- No script, audio dispatch, recording code, control, cooldown, snare setting or character setting changed. Sounds still require the existing real trigger event. There is no timer-based or proximity-based sound fallback.

## Checks

In an isolated project copy:

1. The original box failed the visible-bell contact test; the convex mesh passed at the same position with the same 12 mm tip.
2. Eight playable surface samples passed, while eight points outside the visible disc failed contact.
3. The original scene player-tip component, real Unity physics, original DrumSound and real DrumRecorder were exercised at the visible bell. The cymbal produced the correct recorded hit and nonzero AudioListener output.
4. The full seven-sound regression passed 37 assertions, including all other drums, the unchanged Editor B-pedal branch, duplicate-overlap suppression and replay scheduler traversal. All seven produced nonzero listener DSP samples. This is local audio-engine evidence, **not headset audibility**.
5. Unity imported and cooked the collider successfully. No production C# changed. The saved scene diff contains only the cymbal collider block; hashes of all pre-existing scripts, settings and other scene blocks remain unchanged.

Retained geometry and sound-path test code/results are in Validation/CymbalGeometry. Tests belong in an isolated project copy, not the working project's Assets.

## Remaining Quest checks

The headset still runs unchanged v11 and does not contain this collider correction. After a separately authorised update build, test five centre/bell hits and five outer-disc hits with each stick, while recording, then listen to replay. Check cymbal sound and recording order, and repeat a short snare/other-drum check. Check slower controlled contacts and normal faster strikes separately: this change fixes the demonstrated geometry gap, but does not establish that fast trigger tunnelling or all physical trajectories are resolved. If misses remain, obtain a diagnostic build with per-stage telemetry before changing shared detection again.

No Inspector wiring is required; the new mesh reference and convex/trigger flags are already saved in MusicRoom. If Unity prompts about an externally changed open scene, reload the saved scene before saving over it.

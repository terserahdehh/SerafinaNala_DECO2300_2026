# Restore drum sound before further cymbal work

Follow-up: the user confirmed sound recovery and reliable snare hits. A cymbal-only geometry correction is documented in [CymbalContactGeometryAudit.md](CymbalContactGeometryAudit.md); shared detection and audio code remain as restored here.

19 October 2026. No APK built or installed; no commit, push, hard reset or broad discard. This supersedes the natural-strike enhancement described in CymbalDeploymentAudit.md.

## What is established, and what is not

The connected Quest reports `com.DefaultCompany.VRTemplate`, version code 10. Its installed APK SHA-256 matches the local GarageBandVR_CymbalStrikeFix.apk exactly (`c9a57fc59647c49dfe61bd1eb2a1e7b9f066b702d958bce2e5c1c9556ed6e880`). The user reports all struck drums, the B pedal and saved playback are silent. The retained v9 and v10 build copies have identical MusicRoom scene, audio clips/references, AudioManager settings, DrumRecorder, DrumModeVisuals and PerformanceSequence code. The newest shared audio change was a new TryPlaySound result/gate before all struck-drum sound dispatch. The new cymbal approach/rearm processing also runs ahead of every drum's nearest-contact decision.

The exact cause of the complete Quest silence is **not established**. There were no retained Unity messages for the running app when queried. Quest media audio was unmuted and master mute was off at inspection, but a system snapshot does not prove audible app playback during the reported test. Saved playback uses DrumRecorder's direct PlayOneShot path, which does not go through DrumstickHit or TryPlaySound. Therefore cymbal filtering alone cannot explain confirmed playback silence. An empty/new recording, source/output state or another device/runtime condition cannot be distinguished from the available evidence. The new shared AudioSource gate was removed as a regression rollback, not because an on-headset trace proved it was false.

A separate, exact shared detection defect **was reproduced** with Unity physics: a real trigger callback queued one snare contact; with no new tip displacement, LateUpdate cleared that queue without playing or recording. Results: queued contacts 1, recorded hits before 0, recorded hits after 0, source active/enabled with clip, listener unpaused and volume 1. The callback was suppressed before audio dispatch. The inherited detector also rejected queued contacts unless sampled movement was downward and within its distance gate, and selected one nearest candidate before applying cooldown; it could lose another valid candidate. Cymbal state was isolated per tip/surface, but the common detector still evaluated it before handling any other drum.

That queue defect is present in both v9 and v10. It is a real lost-contact mechanism, **not proof of the version-to-version all-audio regression**. Earlier tests drove tip movement into a surface on a render frame, so they did not expose the delayed/stationary physics callback case. They also did not verify every drum's listener audio output. Those coverage limits are corrected here, without claiming simulated checks establish Quest functionality.

## Narrow restoration

Only three production files changed in this follow-up:

- **DrumstickHit.cs:** restored byte for byte from the pre-cymbal working source retained by the repeat-cycle/walk-idle validation. OnTriggerEnter resolves DrumSound, checks the original 100 ms per-stick cooldown, and calls PlaySound immediately. There is no render-frame queue, nearest-contact filter, movement-direction/speed gate, sweep, cymbal lookup or rearm state in this path.
- **DrumSound.cs:** restored byte for byte from the v9 build inputs (also identical to the earlier working source). A valid source/clip calls PlayOneShot and then RecordHit through the original method. The new TryPlaySound/isActiveAndEnabled rejection gate and new diagnostic dispatch wrapper are removed. The B-pedal input code is unchanged.
- **CymbalStrikeSurface.cs:** reverted to its v9 source. It remains on the scene object for geometry inspection, but DrumstickHit no longer calls it, so its surface logic cannot gate drum sound. No further cymbal enhancement is active in shared detection.

MusicRoom, collider dimensions/positions, correct cymbal clip, Rigidbody/layer setup, character movement, seated drumming/sticks, Help, XR input/configuration, recording clock, playback, and repeat-cycle controls were left unchanged. No keystore, package/version setting or previous APK was changed. The restoration deliberately trades away the experimental cymbal sweep/rearm behaviour while sound recovery is the priority.

## Verification and limits

See Validation/DrumSoundRecovery for retained test code and filtered output. Tests ran only in an isolated saved-project copy.

For each of Cymbal, Floor Tom, High Tom, Hi-hat, Mid Tom and Snare, the test uses the original scene player tip, a real Unity physics trigger, the existing DrumSound and actual clip, and the real DrumRecorder. It checks immediate recording of the correct sound name when no later render movement is provided; nonzero decoded clip PCM; nonzero AudioListener.GetOutputData samples; and no duplicate hit during sustained overlap. Kick uses the unchanged Editor B input branch, then the same playback/recording and listener-output checks. The virtual keyboard is manually advanced in the test harness; this is not a test of a physical Quest B button.

Observed listener output peaks (linear sample amplitude):

| Sound | Peak |
|---|---:|
| Cymbal | 0.0225455 |
| Floor Tom | 0.995361 |
| High Tom | 0.411957 |
| Hi-hat | 0.0739441 |
| Kick pedal | 0.993897 |
| Mid Tom | 0.995422 |
| Snare | 0.122345 |

These are actual nonzero Unity DSP samples, stronger than merely checking `isPlaying` or that PlayOneShot was invoked. They do **not** prove sound was heard through Quest speakers/headphones. The test verifies all seven recorded entries traverse the existing replay scheduler. It does not measure physical strike latency, headset tracking delivery or acoustic output.

The focused sound-path run passed 37 assertions. Editor and player-script C# compilation passed; the separate six-cycle synthetic XR UI regression passed 74 assertions, including Pause/Resume/Delete for three Mika cycles and three Ren cycles. Results are recorded in the validation output. The pre-existing missing menu-scroll input-action configuration remains untouched and is still logged during the UI test; that is not a newly fixed or verified physical Quest scroll path.

## Required Quest verification after a later authorised build

The connected headset still has v10. This source restoration cannot be tested there without a subsequent build/install, which was prohibited for this task.

Before claiming recovery, use an update build with matching package/signing and a higher version code, then, while the app has VR focus:

1. Strike Snare, Floor Tom, High Tom, Mid Tom, Hi-hat and Cymbal individually with each player stick. For each one, verify the trigger/accepted hit, the **heard** sound, and a corresponding recorded event. Check the B pedal separately.
2. Stop and replay a recording containing one clearly spaced hit from every drum. Listen for all seven correct sounds in order and verify recording times/order. Repeat with an existing saved take so an empty take cannot mask playback failure.
3. Repeat at least three Record → Save → performance → Pause/Resume → Delete/Re-record cycles for Mika and three for Ren. Check attached visual sticks cause no extra hits, Help and Quest controls work, and live/pedal/replay audio returns each time.
4. If every source remains silent, capture a live app trace and audio/output/focus state during that exact test. Do not infer an audio-output fix from the Editor results or silently reintroduce cymbal filtering.

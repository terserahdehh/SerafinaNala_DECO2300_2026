# Cymbal deployment audit and natural-strike correction

Historical enhancement report: the subsequent all-drum sound regression and selective restoration are documented in [DrumSoundRecovery.md](DrumSoundRecovery.md). The shared sweep/rearm and TryPlaySound changes below are no longer active in current source.

19 October 2026. Scope narrowed at the user's request to cymbal detection. Mika and Ms Maya's movement is unchanged. No APK build, installation, commit or push was performed during this follow-up.

## Deployment evidence, collected before changing code

Connected Quest 2: `1WMHH8406G1165`.

| Evidence | Result |
|---|---|
| Installed package | `com.DefaultCompany.VRTemplate` |
| Installed version code / name | `9` / `1.0` |
| Local GarageBandVR_CymbalAndMotion.apk code / name | `9` / `1.0` |
| Installed base.apk SHA-256, pulled directly from Quest | `679e199cef00014ef223141b6f0575f5fdf5b3995ed05b9aa604e24d106f6c88` |
| Local APK SHA-256 | Identical |
| APK metadata | Contains DrumstickHit, CymbalStrikeSurface and CharacterMotion types |
| Retained build source snapshot | MusicRoom, DrumstickHit, CymbalStrikeSurface, CharacterMotion and IntroSequence match the files used for the previous change |
| Build log | `Rebuilding Data files because Assets/MusicRoom.unity is dirty (hash)` and `Level 0 'Assets/MusicRoom.unity'` |
| MusicRoom SHA-256 in the build snapshot and working project | `8130cd2a781e481d73063cf430c1ee5b9a87c2d16cf1a14a606e2c69a9eb91e8` |

The correct APK is installed. This proves artifact identity and build inputs, not that every runtime component behaved correctly. The app was closed when queried and useful Unity logs were not retained. Consequently no on-headset tip trajectory, rejection or audio event was observed during this audit.

## Runtime wiring and verified failure

In an isolated Unity run of MusicRoom, CymbalHitZone's component, GameObject and collider are enabled; it has the cymbal clip and expected surface normal. The actual player-tip component discovers this surface when activated. A regression drives the real scene tip by moving its controller parent (rather than replacing the hit detector with a mock). It verifies that tip movement reaches detection, audio submission and the recorder.

An untracked Editor run initially has inactive XR controller parents. That is not evidence that the Quest controllers are inactive: the Editor has no physical XR controller devices. The controlled test explicitly activates the original controller hierarchy with input drivers disabled so it can exercise the real tip deterministically.

Two failures were reproduced in the v9 implementation:

1. The speed gate examines only the frame that crosses the contact boundary. A normal fast approach followed by a braking contact at 0.0625 m/s fails the 0.08 m/s test. The next sample starts inside the surface, so it cannot recover that strike. The earlier tests used large, fast boundary crossings and missed this case.
2. A successful strike stays latched until the tip clears its contact height by 20 mm. A 10 mm rebound can visibly leave the cymbal and strike again yet remain latched, even after the 100 ms cooldown expires.

These are established algorithm failures compatible with the reported misses. Without live Quest diagnostics, they cannot be claimed as the explanation for every physical miss.

## Effective settings before / after

| Setting or behaviour | Installed v9 | Revised source |
|---|---|---|
| Cymbal footprint radii / bell height | 0.255 m / 0.043 m | Unchanged |
| Player tip radius | 0.012 m | Unchanged |
| Surface transform, normal, collider dimensions and scene | Previous fitted surface | Unchanged byte for byte |
| Minimum approach speed | 0.08 m/s | Unchanged |
| Approach assessment | Contact-frame speed only | Per-tip, per-surface approach remembered for 120 ms; final segment must still move toward the surface |
| Rearm | 20 mm above contact height | Actual separation with 3 mm hysteresis, plus the existing cooldown |
| Cooldown | 100 ms per tip/drum | Unchanged |
| Tracking jump rejection | >=1 m in one frame | Unchanged, also clears approach state |
| Surface discovery | Active surfaces only at tip enable | Includes inactive surfaces, with enabled/collider validation on every sample |
| Audio | Void PlaySound call | Result-returning TryPlaySound; inactive/missing source does not create a phantom recorded hit |
| Diagnostics | Optional accepted-hit log | Optional status, samples, contact, rejection reason, audio submission/failure |

No collider was enlarged. Geometry, clips, input actions, recording/replay clocks, UI, performance transitions and animation settings were not changed. CharacterMotion and IntroSequence remain byte-identical to the v9 build inputs. The original repeat-cycle and Quest input scripts also remain byte-identical.

## Optional diagnostics

Both `CymbalStrikeSurface.Diagnostic Logging` and `DrumstickHit.Diagnostic Logging` default to false. Enable the checkbox on **CymbalHitZone** to trace both player tips in Play Mode or in a later, explicitly authorised diagnostic build. No input or debug HUD is added. Turning on diagnostics cannot alter the installed v9 APK without a new build/install.

Read Unity logcat using `adb logcat -v brief Unity:I '*:S'` and filter `[DrumHit`:

- `STATUS`: effective surface enablement, position, normal and gating settings.
- `TIP_STATUS`: tip component, hierarchy and collider enablement, even when no movement samples arrive.
- `SAMPLE`: sampled movement count, accumulated travel, world tip position, radius and discovered surface count, once per second.
- `CONTACT`: geometry detected a contact boundary crossing.
- `REJECT`: exact reason, including NoRecentApproach, NotSeparated, Cooldown, AlreadyInside, OutsideDisc, MovingAway, SurfaceDisabled, TrackingJump or NearerContact.
- `AUDIO_REJECT`: missing source/clip or disabled source. No hit is recorded for these failures.
- `AUDIO_SUBMITTED`: PlayOneShot was called; includes clip, load state, mute, source volume and global listener pause/volume. This establishes submission, not audible output through headset hardware.
- `ACCEPT`: accepted strike and audio submission through the existing recorder path.

Normal air motion above the surface is not logged as a rejection on every frame. Existing ordinary HIT/RECORDED HIT logs remain as before.

## Checks and device work remaining

Editor compilation and player-script C# compilation with UNITY_EDITOR excluded passed. No APK/IL2CPP build was performed. 121 runtime assertions passed in the isolated project, including the real player-tip braking/rebound regressions, resting jitter rejection, approach expiry, diagnostic event distinctions, failed-audio recording suppression, cymbal recording/replay, and six synthetic XR UI cycles (three Mika, three Ren) with Pause/Resume/Delete each time. A separate deterministic sampling check covers braking contact at 72, 90 and 120 Hz. The retained harness and filtered results are under Validation/CymbalContact.

The test run still emits the previously documented missing menu-scroll input-action errors; that configuration was left untouched. Help open/close and XR button tests pass, but physical Quest scrolling is not established by these tests.

After a future authorised build, verify on Quest: natural braked strikes at centre/rim, shallow rebounds, alternating sticks, resting contact and nearby drums; confirm recorded cymbal spacing and replay audio; then repeat three complete recording/performance/delete cycles for each musician, with Pause/Resume each time. If misses remain, capture the diagnostic statuses, CONTACT/REJECT and AUDIO events for those exact strokes. That will separate tracking delivery, geometry/gating and audible playback. The current headset remains on v9 and does not contain this follow-up correction.

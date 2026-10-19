# Conservative tolerance for all struck drums

20 October 2026. The user confirmed that v13's cymbal is audible on Quest, then requested additional angle tolerance for all struck drums, especially snare and the two front toms. No APK build, installation, commit or push was performed in this change.

Before editing, a persistent local source checkpoint was saved outside the repository at `/Users/serafinanala/.codex/backups/GarageBandVR_WorkingV13_20261020/working-v13-source.tar.gz`, with a SHA-256 manifest, Git status and the working APK hash alongside it. This includes tracked and untracked source/assets/settings/documentation and excludes generated Library/build caches. It is a local rollback backup; it is not a remote Git backup. The existing v13 APK is preserved in Builds.

## Actual finding and scope

The current detector is the original immediate OnTriggerEnter → PlaySound → RecordHit path with a 100 ms per-stick cooldown. There is no angular filter. Misses can arise from a tip not entering a trigger, discrete physics missing a fast traversal, sustained overlap without another entry, or a previous hit consuming the cooldown. Those mechanisms are not established as the cause of the user's individual angle misses; no new on-device per-stage trace was collected. This change adds geometric tolerance only. It does not claim to fix rapid-repeat or tunnelling failures.

The old scene already has overlapping trigger pairs: floor tom/mid tom (penetration approximately 25.7 mm), high tom/snare (155.6 mm), and hi-hat/snare (153.9 mm). A blanket expansion would deepen those overlaps. Existing overlap is retained rather than silently redesigning the drum layout or shared detection.

## Geometry change

Each box-trigger face was tested for a 20 mm world-space extension. It is retained only if all collider pairs have no new overlap and no increased penetration beyond 0.1 mm numerical tolerance. Faces are applied sequentially and rechecked against the effective candidate scene, not independently against stale dimensions. The geometry test confirms all six struck instruments have increased contact volume:

| Trigger | Old world dimensions (m) | New world dimensions (m) | Blocked expansion |
|---|---|---|---|
| Floor tom | .4503 × .4098 × .7995 | .4703 × .4498 × .8395 | local -X toward mid tom |
| High tom | .2370 × 1.6422 × 1.0792 | .2570 × 1.6822 × 1.1192 | local -X toward snare |
| Hi-hat | .6045 × .5760 × .8489 | .6245 × .6160 × .8889 | local +X toward snare |
| Mid tom | .2370 × 1.6422 × 1.0792 | .2570 × 1.6822 × 1.1192 | local +X toward floor tom |
| Snare | .3804 × .6288 × .3799 | .3804 × .6688 × .4199 | both local X faces |

Dimensions describe existing trigger volumes, not visible instrument diameters. The two front toms were already tall triggers; the retained values intentionally preserve those working settings.

The cymbal retains the v13 player-facing reach hull and adds 20 mm vertical tolerance above and below its existing points using a new `CymbalAllDrumsTolerance.asset` mesh. It remains one convex trigger; visual geometry and horizontal reach are unchanged. The original mesh assets are preserved. All pairwise penetration checks passed after cymbal expansion as well.

Only five BoxCollider size/centre fields and the cymbal MeshCollider mesh reference changed in MusicRoom. All transforms, visible meshes, other collider properties, Rigidbody/layer configuration, hit/audio scripts, cooldown, pedal, recording timing, UI, character motion, seated drumming and musician sticks remain unchanged. No proximity-based or timer-based sound was added. Physical trigger entry remains required.

## Validation and limits

Unity imported/cooked the new convex shape successfully. The candidate passed all fifteen drum-pair checks against the original overlap depths. The saved diff is restricted to the five box blocks and cymbal mesh reference. All existing production scripts/settings and original assets match the pre-change checkpoint.

47 isolated Editor runtime assertions passed, including actual player-tip trigger callbacks, accepted/recorded sound names, nonzero listener DSP output for all seven sounds, sustained-overlap duplicate suppression, ten assisted cymbal contacts (five per stick), and unchanged replay scheduling. Test code and filtered output are retained in Validation/AllDrumTolerance. Those checks do not verify physical controller angles, tracking, audible Quest output or subjective responsiveness. The broader UI/animation suite was not repeated because no related code or scene data changed.

After a separately authorised update build, compare controlled angled strokes on snare and both front toms, five per hand at the centre and near edges. Check that striking one drum never produces the neighboring drum instead. Test hi-hat, floor tom, cymbal reach, pedal and recorded playback. Try rapid rolls separately: the shared 100 ms cooldown is unchanged, so these should not be treated as validation of the added geometry. If angle misses persist, capture contact/dispatch evidence rather than continuing to enlarge all volumes.

No Inspector wiring is required. Reload the externally changed scene before saving over it from an open Unity editor.

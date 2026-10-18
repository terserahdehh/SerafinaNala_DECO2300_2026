# Help panel layout fix

`HelpMenu.Start()` configures the world-space Help Canvas and resizes `Help Popup → Instruction Panel` to 900 × 650 UI units. Previously it did not lay out the panel's children: the authored instructions used a fixed 760 × 430 rectangle with unrestricted text overflow, and the 160 × 160 close button extended beyond the panel's header area. These mixed authored/runtime dimensions allowed text to overlap the heading and escape below the navy background.

`HelpMenu` now configures `HelpPanelLayout` on the existing Instruction Panel. It reuses the original Title, Instructions and Close Button, retaining the navy background Image, instruction wording, font asset, close artwork and persistent CloseHelp action. No controller mappings or modal open/close logic changed.

The header has dedicated space above the body: title begins 24 units below the top, the close button has a 64 × 64 target with 24-unit top/right insets, and the body starts 112 units below the top. Instructions remain 32-point white text, aligned top-left with consistent line/paragraph spacing. A RectMask2D clips the scroll content inside the background. The scrollbar and Up/Down buttons allow drag, wheel/scroll input, or controller-ray button clicks; the header and close button stay fixed. Help remeasures its content and returns to the top each time it opens. Setup reuses generated UI objects if called again.

## Inspector setup

No new assignments are required in the current scene. Keep `HelpMenu.helpPopup` pointing to the existing Help Popup and retain the original child names **Instruction Panel**, **Title**, **Instructions**, and **Close Button**. The helper component and scroll controls are installed at runtime. Do not add another Canvas, EventSystem, input module or replacement close button. Layout is established when entering Play Mode; changes to old child offsets in the Inspector will be replaced by the runtime layout.

## Verified

- Editor and player branches compile with the project's Unity references; no APK was built.
- An isolated Unity Play Mode session used the actual authored Help Canvas, original font/text, navy Image and close-button persistent listener.
- Assertions passed for heading/body separation, close-button separation, viewport bounds/mask, 32-point text without auto-shrinking, sufficient measured content height, scrolling, original CloseHelp action, open/close toggling, reopening at the top, and repeated setup without duplicate scroll areas/buttons.
- Top and bottom Unity camera renders were visually inspected: see `HelpLayoutTop.png` and `HelpLayoutBottom.png`. The body is clipped while the fixed heading, close button and footer remain inside the navy background.

## Still needs visual testing

Run Help during dialogue, recording review and performance in the complete MusicRoom scene. Check Editor mouse dragging/wheel, Up/Down and Close clicks; then check Quest controller-ray clicks/scrolling, stereo clipping, perceived font size and comfortable button targeting. Confirm reopening Help and returning to the underlying menus in each stage. These hardware/full-scene interactions were not established by the isolated layout checks.

## Quest controller-thumbstick scrolling

Both controllers are supported: point the **left ray** at the instruction body and tilt the **left thumbstick**, or point the **right ray** there and tilt the **right thumbstick**. Down reveals later instructions; up returns toward the beginning. No trigger or thumbstick click is required. Only the vertical axis scrolls. The thumbstick on the other controller does not drive the hovered ray. The fixed heading/background outside the instruction viewport is not a scroll target. Up/Down buttons and scrollbar dragging remain available.

The existing input asset already contains `XRI Left Interaction/UI Scroll` and `XRI Right Interaction/UI Scroll`, bound to each controller's `Primary2DAxis`. Previously, the runtime menu-ray prefab's UI Scroll reader had no action reference. `DrumModeVisuals.Menu Input Actions` is now assigned to **XRI Default Input Actions** in MusicRoom, and each runtime `QuestControllerMenuRay` clones the appropriate existing action, preserving its bindings/processors. The clone has its own enable/disable/dispose lifecycle because the existing ControllerInputActionManagers remain disabled to prevent teleport/movement mode conflicts. Shared action assets are not enabled/disabled by these clones.

The ray sends vertical, unscaled-frame-time-adjusted scroll deltas through its XRRayInteractor UI scroll reader into the existing XR UI Input Module and hovered ScrollRect. It does not call the ScrollRect directly. The current tracked-scroll multiplier is 5, the separate mouse multiplier is 6, and Help's scroll sensitivity is 40. Full stick deflection produces approximately 600 panel units/second with these defaults. Tune speed only after checking physical Quest use. Manual LocomotionProviders remain disabled by QuestExperienceControls, and the scripted movement/teleport paths are unchanged. Editor mode still uses its mouse-only UI path and the existing mouse ScrollWheel binding.

No additional Inspector setup is required in the saved MusicRoom scene. If **Menu Input Actions** appears empty in an already-open/stale scene, reload the saved scene or assign the existing `Assets/Samples/XR Interaction Toolkit/3.4.1/Starter Assets/XRI Default Input Actions.inputactions` asset to that field on DrumModeVisuals. Do not re-enable ControllerInputActionManager or locomotion to make scrolling work.

An isolated Unity Play Mode integration check passed using the actual Help Canvas, ray prefab and input asset with simulated left/right XR controller states. It verified scrolling through each hand, separation of left/right input, hover gating, action re-enable, untouched shared action enable state, and Editor mouse-wheel scrolling. Editor/player compilation also passed. On Quest, still verify physical controller binding, direction/speed, neutral-stick drift/deadzone, stable hover, stopping when pointing away, reopen behaviour, and that thumbstick scrolling never moves/turns/teleports the player. No APK was built.

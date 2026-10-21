# Editor testing after removing the desktop helper

`EditorExperienceTesting.cs` has been removed. The old keyboard/mouse setup
and its previous validation results no longer describe the current project.
The custom helper no longer activates the XR Device Simulator, creates its
input bindings, or temporarily changes XR startup settings in Play Mode.

The existing XR Device Simulator and all of its saved scene settings have
been left unchanged. No simulator was enabled, disabled, added or removed.
Do not expect the previous desktop simulation controls to work automatically.

`QuestExperienceControls` still handles Quest A/X input and keeps its existing
Editor keyboard A/X shortcuts. Other existing Editor input paths were not
changed. This removal does not add new mouse support.

On Quest, controller-ray trigger clicks, dialogue A, Help scrolling, recording,
playback and performance still use their existing gameplay scripts. The
removed helper was entirely Editor-only and never ran in the Quest app.
`Assets/Editor/ExcludeSimulatorFromBuild.cs` is retained: it removes simulator
objects from build scene copies without editing the saved scenes.

Recording buttons now get non-clickable hover labels from
`RecordingButtonHover`, installed by `DrumRecorder` at runtime. These labels
use Quest tracked-pointer enter/exit events and need no Inspector assignment.

## Quest checks still needed

- Point at dialogue buttons and press the trigger; also test controller A.
- Open Help and click its scroll buttons with the trigger.
- Hover Record before and during recording; check Start/Stop wording.
- Hover Playback during play and pause; check Play/Pause wording changes.
- Check Retake and Save labels, then test their existing click actions.
- Leave each button, close the panel and open Help; labels should disappear.
- Repeat recording, playback and musician performance with Mika and Ren.

No APK was built for these changes. C# compilation alone does not verify
headset layout, pointer behaviour, reach or perceived readability.

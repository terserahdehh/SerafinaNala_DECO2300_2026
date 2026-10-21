/*
 * Remembers which drums the player hit and when each hit happened.
 * It saves sound names and times, not microphone audio. DrumSound sends hits
 * here. A starts/stops recording; buttons let the player listen, retake or
 * save.
 * Saving asks PerformanceSequence to show Mika or Ren performing the take.
 * The same playback time controls drum sounds and ProceduralDrummer's arms.
 * This is active game code and runs in Editor Play Mode too. DrumProximity
 * still shows/hides the recording canvas according to distance from the kit.
 * An AudioClip is a sound file; an AudioSource is the Unity component playing
 * it. A Canvas holds UI text/buttons. A raycaster checks which button a pointer
 * touches. A coroutine is a sequence that can wait while the game keeps
 * running. JSON is readable text used to save the hit list and take length.
 * UI means the buttons and text the player sees and uses.
 * The Inspector is Unity's settings panel for assigning objects and values.
 * A frame is one game update/image. Time.deltaTime gives seconds since the
 * previous update, so movement can use speed per second.
 */
// Use sequences that can wait while the game keeps running.
using System.Collections;
// Use a List to keep the player's recorded drum hits in order.
using System.Collections.Generic;
// Use file paths and JSON recording-file read/write operations.
using System.IO;
// Use Unity objects, positions and the methods Unity calls during the game.
using UnityEngine;
// Use Unity canvas graphics, buttons and scrolling controls.
using UnityEngine.UI;
// Use the text labels shown in the experience menus.
using TMPro;
// Use XR Origin, used for the player rig and headset camera.
using Unity.XR.CoreUtils;
// Use XR canvas raycasters and the headset UI input module.
using UnityEngine.XR.Interaction.Toolkit.UI;

// Remembers which drums the player hit and when each hit happened.
public class DrumRecorder : MonoBehaviour
{
    // Let DrumSound find the recorder that remembers this scene's take.
    public static DrumRecorder Instance { get; private set; }

    // Keep one recorded drum sound name and its time since the take began.
    [System.Serializable]
    public class DrumHit
    {
        // Keep the AudioClip name used to match a recorded hit to its sound.
        public string soundName;
        // Keep this hit's time in seconds since recording started.
        public float time;

        // Remember one hit. soundName identifies the drum's sound file; time is
        // seconds since this take started.
        public DrumHit(string soundName, float time)
        {
            // Save the supplied clip name on this hit, so replay can look it up
            // later.
            this.soundName = soundName;
            // Save its relative time so replay can reproduce the original
            // rhythm.
            this.time = time;
        }
    }

    // Group the recorded hits and take length into the data written to JSON.
    [System.Serializable]
    private class RecordingData
    {
        // Keep the recorded list of sound-name/time pairs.
        public List<DrumHit> hits;
        // Keep the take duration written into the save-data container.
        public float duration;
    }

    // Keep the audio output shared by preview and performance playback.
    [Header("Audio")]
    [SerializeField] private AudioSource playbackAudioSource;
    // Keep the clip list used to resolve recorded sound names during replay.
    [SerializeField] private List<AudioClip> drumSounds =
        new List<AudioClip>();

    // Keep the existing Record button object.
    [Header("Record Button")]
    [SerializeField] private GameObject recordButton;
    // Keep the existing image whose recording icon can change.
    [SerializeField] private Image recordButtonImage;
    // Keep the icon used when the recorder is ready.
    [SerializeField] private Sprite normalRecordIcon;
    // Keep the icon used while a take is being recorded.
    [SerializeField] private Sprite activeRecordIcon;

    // Keep the existing button for discarding the take and recording again.
    [Header("Retake Button")]
    [SerializeField] private GameObject retakeButton;

    // Keep the existing button for starting or pausing the preview.
    [Header("Playback Button")]
    [SerializeField] private GameObject playbackButton;
    // Keep the preview image whose play/pause icon can change.
    [SerializeField] private Image playButtonImage;
    // Keep the artwork offering playback or resume.
    [SerializeField] private Sprite playIcon;
    // Keep the artwork offering pause.
    [SerializeField] private Sprite pauseIcon;

    // Keep the existing button for saving the take and starting performance.
    [Header("Save Button")]
    [SerializeField] private GameObject saveButton;

    // Keep the scene coordinator for performer placement and viewing position
    // changes.
    [Header("Performance")]
    [SerializeField] private PerformanceSequence performanceSequence;

    // Remember whether recording is active, so new hits can be added to the
    // current take.
    [Header("Debug")]
    [SerializeField] private bool isRecording;
    // Remember whether preview playback is active, so the single review
    // timeline is running.
    [SerializeField] private bool isReplaying;
    // Remember whether preview playback is paused, so the preview clock must
    // not advance.
    [SerializeField] private bool isPlaybackPaused;
    // Remember whether the take contains at least one hit, so preview/save can
    // use real recorded sounds.
    [SerializeField] private bool hasRecording;
    // Remember whether looped musician playback is active, so performance
    // controls own playback.
    [SerializeField] private bool isPerformanceMode;
    // Remember whether the musician performance is paused, so both audio and
    // pose time stay frozen.
    [SerializeField] private bool isPerformancePaused;
    // Remember whether a performance transition is active, so record/review
    // controls must not intercept it.
    [SerializeField] private bool performanceTransitionActive;
    // Keep the Inspector-visible count of captured hits.
    [SerializeField] private int recordedHitCount;
    // Keep the length of the completed take in seconds.
    [SerializeField] private float recordingDuration;

    // Keep the drum sound names and hit times in the order the player recorded
    // them.
    private readonly List<DrumHit> recordedHits =
        new List<DrumHit>();

    // Keep the recording canvas found above the existing Record button.
    private Canvas recordingCanvas;
    // Keep the game-created panel holding review buttons.
    private GameObject reviewPanel;
    // Keep the small game-created panel for recording status.
    private GameObject recordStatusPanel;
    // Keep the headset status label for starting or stopping recording.
    private TMP_Text recordingStatusText;
    // Keep the text describing whether the completed take contains hits.
    private TMP_Text reviewTitle;
    // Remember whether a take has been stopped, so the review screen, including
    // empty takes, is relevant.
    private bool hasCompletedTake;
    // Keep the Help controller used to respect its open state.
    private HelpMenu help;
    // Keep the controller/stick and menu-ray switcher.
    private DrumModeVisuals drumModeVisuals;
    // Keep the recording-review dimmer canvas, separate from Help.
    private Canvas reviewOverlayCanvas;
    // Report whether the player is reviewing a stopped take, rather than
    // recording or watching a performance.
    public bool IsReviewing => hasCompletedTake && !isRecording && !performanceTransitionActive && !isPerformanceMode;
    // Allow A recording only while its canvas is active, outside review,
    // performance and transitions.
    public bool CanToggleRecording => recordingCanvas != null && recordingCanvas.gameObject.activeInHierarchy &&
        !isPerformanceMode && !performanceTransitionActive && !hasCompletedTake;

    // Keep the Unity game time when the latest take started.
    private float recordingStartTime;
    // Remember the running preview so StopReplay can stop that exact sequence.
    private Coroutine replayCoroutine;
    // Remember the running musician playback so StopPerformance can stop it.
    private Coroutine performanceCoroutine;

    // Return whether new hit events are currently being collected.
    public bool IsRecording => isRecording;
    // Return whether a single preview timeline is running.
    public bool IsReplaying => isReplaying;
    // Return whether that preview timeline is paused.
    public bool IsPlaybackPaused => isPlaybackPaused;
    // Return whether looped musician playback is active.
    public bool IsPerformanceMode => isPerformanceMode;
    // Return whether the shared musician playback clock is paused.
    public bool IsPerformancePaused => isPerformancePaused;

    // Let DrumSound find this recorder through Instance. Warn if a second
    // recorder exists. This warning does not disable that second component.
    private void Awake()
    {
        // Detect a second recorder instead of replacing the shared reference
        // silently.
        if (Instance != null && Instance != this)
        {
            // Warn in Unity's Console if a second recorder tries to become the
            // shared recorder.
            Debug.LogWarning(
                "More than one DrumRecorder exists in the scene."
            );

            // Leave this Awake call after warning; the existing shared recorder
            // stays selected.
            return;
        }

        // Make this recorder available to DrumSound through the shared Instance
        // property.
        Instance = this;
    }

    // Begin with an empty take and no playback. Set up the recording menus
    // before refreshing the buttons.
    private void Start()
    {
        // Stop collecting new hits; captured events remain until explicitly
        // cleared.
        isRecording = false;
        // Mark preview playback stopped so another preview may start cleanly.
        isReplaying = false;
        // Clear preview pause so a new or resumed preview can advance.
        isPlaybackPaused = false;
        // Mark the take as empty until new hit events are collected.
        hasRecording = false;
        // Leave looping performer mode so normal recording controls can return.
        isPerformanceMode = false;
        // Clear the old performance pause flag before another loop or recording
        // cycle.
        isPerformancePaused = false;
        // Release the transition gate so the next valid recording/playback
        // action can proceed.
        performanceTransitionActive = false;

        // Reset the Inspector hit counter for the fresh recording state.
        recordedHitCount = 0;
        // Clear the old take length before another recording begins.
        recordingDuration = 0f;

        // Create/reuse the headset recording and review layout before updating
        // its icons.
        PrepareRecordingUI();
        // Refresh the Record artwork to match the current recording state.
        UpdateRecordButtonIcon();
        // Refresh the review Play/Pause artwork to match preview state.
        UpdatePlayButtonIcon();
        // Apply the current recorder state to visible buttons and UI ray
        // surfaces.
        UpdateButtonVisibility();
    }

    // Arrange the player's recording status and the Preview, Retake and Save
    // menu. Reuse the existing button objects and their click events.
    private void PrepareRecordingUI()
    {
        // Skip repeated UI construction or setup without the existing Record
        // button.
        if (recordingCanvas != null || recordButton == null) return;
        // Find the Record button's canvas, even if proximity at first hid its
        // parent.
        recordingCanvas = recordButton.GetComponentInParent<Canvas>(true);
        // Find the scene player rig for this UI or controller setup.
        var origin = FindFirstObjectByType<XROrigin>();
        // Prefer the XR camera, falling back to the main camera for desktop
        // use.
        var eye = origin != null ? origin.Camera : Camera.main;
        // Stop layout without both a canvas and viewing camera.
        if (recordingCanvas == null || eye == null) return;
        // Configure the recording canvas size, overlay priority and headset
        // pointer-checking component.
        QuestUI.ConfigureCanvas(recordingCanvas, eye, new Vector2(1100f, 700f), 150);
        // Attach the ready/recording UI to the camera so status follows the
        // player's view.
        recordingCanvas.transform.SetParent(eye.transform, false);
        // Place the UI relative to its parent, such as the headset or
        // containing panel.
        recordingCanvas.transform.localPosition = new Vector3(0f, -.05f, 1f);
        // Set local scale explicitly so inherited layout changes do not stretch
        // this UI.
        recordingCanvas.transform.localScale = Vector3.one * .001f;
        // Find Help so this script can respect its current visibility and input
        // block.
        help = FindFirstObjectByType<HelpMenu>();
        // Prefer the DrumModeVisuals component on this same kit object.
        drumModeVisuals = GetComponent<DrumModeVisuals>();
        // Fall back to the scene's mode switcher if none is on this object.
        if (drumModeVisuals == null) drumModeVisuals = FindFirstObjectByType<DrumModeVisuals>();
        // Create the separate full-view dimmer used only during take review,
        // not Help.
        reviewOverlayCanvas = QuestUI.CreateCanvas("Recording Full Screen Overlay", eye, new Vector2(4000f, 4000f), 140);
        // Parent the review dimmer to the headset so it covers the current
        // view.
        reviewOverlayCanvas.transform.SetParent(eye.transform, false);
        // Place the UI relative to its parent, such as the headset or
        // containing panel.
        reviewOverlayCanvas.transform.localPosition = new Vector3(0f, 0f, .32f);
        // Set local scale explicitly so inherited layout changes do not stretch
        // this UI.
        reviewOverlayCanvas.transform.localScale = Vector3.one * .001f;
        // Add the large translucent dark image for the review backdrop.
        QuestUI.Background("Full View Dimmer", reviewOverlayCanvas.transform,
            new Vector2(4000f, 4000f), new Color(0f, 0f, 0f, .8f));
        // Keep that backdrop hidden until the player stops and reviews a take.
        reviewOverlayCanvas.gameObject.SetActive(false);

        // Create the smaller status background on the recording canvas.
        recordStatusPanel = QuestUI.Background("Recording Status Panel", recordingCanvas.transform,
            new Vector2(650f, 100f), new Color(.04f, .06f, .1f, .92f)).gameObject;
        // Place that status panel below the centre so it does not dominate the
        // drum view.
        recordStatusPanel.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -230f);
        // Set the position and size of the Record button inside its parent
        // panel.
        QuestUI.Layout(recordButton.GetComponent<RectTransform>(), recordStatusPanel.transform,
            new Vector2(-245f, 0f), new Vector2(64f, 64f));
        // Show the player this menu text: "A  •  Start recording".
        recordingStatusText = QuestUI.Text("Recording Status", recordStatusPanel.transform,
            "A  •  Start recording", new Vector2(45f, 0f), new Vector2(480f, 70f), 26f, Color.white);

        // Create a container for the existing review buttons and their title.
        reviewPanel = new GameObject("Recording Review Panel", typeof(RectTransform));
        // Set the position and size of the recording review panel inside its
        // parent panel.
        QuestUI.Layout(reviewPanel.GetComponent<RectTransform>(), recordingCanvas.transform,
            Vector2.zero, new Vector2(950f, 330f));
        // Show the player this menu text: "RECORDING READY".
        reviewTitle = QuestUI.Text("Review Title", reviewPanel.transform, "RECORDING READY",
            new Vector2(0f, 114f), new Vector2(850f, 45f), 32f, Color.white);
        // Place Preview in the centre of the review row.
        LayoutReviewButton(playbackButton, 0f);
        // Place Retake on the left so a new attempt is easy to find.
        LayoutReviewButton(retakeButton, -280f);
        // Place Save on the right to continue to performance.
        LayoutReviewButton(saveButton, 280f);
        // Show the player this menu text: "Point your controller ray at a
        // button and press the trigger".
        QuestUI.Text("Review Ray Hint", reviewPanel.transform,
            "Point your controller ray at a button and press the trigger",
            new Vector2(0f, -116f), new Vector2(900f, 40f), 23f, Color.white);
        // Add labels to the existing buttons without changing their clicks.
        RecordingButtonHover.Attach(recordButton, this,
            RecordingButtonHover.Action.Record, new Vector2(0f, 75f));
        // Put review labels below the row and its trigger-use hint.
        RecordingButtonHover.Attach(retakeButton, this,
            RecordingButtonHover.Action.Retake, new Vector2(0f, -180f));
        // Let the preview label follow its current Play or Pause action.
        RecordingButtonHover.Attach(playbackButton, this,
            RecordingButtonHover.Action.Playback, new Vector2(0f, -180f));
        // Explain Save while keeping the existing Save click unchanged.
        RecordingButtonHover.Attach(saveButton, this,
            RecordingButtonHover.Action.Save, new Vector2(0f, -180f));
        // Draw this UI over room geometry using the shared overlay shader
        // setup.
        QuestUI.ApplyOverlay(recordingCanvas.gameObject);
        // Hide the game-created panel holding review buttons.
        reviewPanel.SetActive(false);
    }

    // Fit one review button into the row without stretching its image.
    // buttonObject is the existing button; x is its left/right position on the
    // panel.
    private void LayoutReviewButton(GameObject buttonObject, float x)
    {
        // Skip an optional missing review button rather than trying to resize
        // it.
        if (buttonObject == null) return;
        // Read the button's existing Image so its artwork can determine
        // proportions.
        var image = buttonObject.GetComponent<Image>();
        // Begin with a square fallback size for buttons without usable artwork.
        var size = new Vector2(150f, 150f);
        // Measure artwork proportions only when a button picture is available.
        if (image != null && image.sprite != null)
        {
            // Calculate width divided by height so sizing keeps the button
            // artwork proportional.
            float aspect = image.sprite.rect.width / image.sprite.rect.height;
            // Fit wide/tall art into the review row without stretching its
            // original aspect ratio.
            size = aspect >= 1f ? new Vector2(210f, 210f / aspect) : new Vector2(150f * aspect, 150f);
            // Keep the source picture proportions instead of stretching its
            // artwork.
            image.preserveAspect = true;
        }
        // Set the position and size of this review button inside its parent
        // panel.
        QuestUI.Layout(buttonObject.GetComponent<RectTransform>(), reviewPanel.transform,
            new Vector2(x, 0f), size);
        // Find this existing button's interaction component after moving its
        // rectangle.
        var button = buttonObject.GetComponent<Button>();
        // Disable automatic button automatic button selection if present;
        // controller rays provide clicks.
        if (button != null) button.navigation = new Navigation { mode = Navigation.Mode.None };
    }

    // Refresh recording buttons after other game scripts have changed the
    // current experience state.
    private void LateUpdate()
    {
        // Apply the current recorder state to visible buttons and UI ray
        // surfaces.
        UpdateButtonVisibility();
    }

    // Clear Instance if it still points to this recorder, so DrumSound cannot
    // keep using a destroyed recorder.
    private void OnDestroy()
    {
        // Only clear the shared recorder variable if it still points to this
        // component.
        if (Instance == this)
            // Remove this recorder from the shared variable when it is
            // destroyed.
            Instance = null;
    }


    // Use A to stop the current take or start a fresh one. Ignore the press
    // when Help or the current game stage blocks recording.
    public void ToggleRecording()
    {
        // Ignore A when recording is unavailable or Help is covering the
        // experience controls.
        if (!CanToggleRecording || (help != null && help.IsOpen)) return;
        // Check whether recording is active before taking this branch.
        if (isRecording)
            // Finish the running take on this accepted A press.
            StopRecording();
        // Start a fresh take when the player is not already recording.
        else
            // Start a fresh take if the recorder is ready rather than already
            // collecting hits.
            StartRecording();
    }

    // Clear the last take and remember when the new recording begins. Later
    // drum hits save their time relative to this start.
    public void StartRecording()
    {
        // Reject duplicate start requests and starts during performance or its
        // transition.
        if (isRecording || performanceTransitionActive || isPerformanceMode) return;
        // Stop any preview first so the next recorder state has no overlapping
        // playback.
        StopReplay();

        // Discard earlier hit events so a fresh take does not mix with the
        // previous one.
        recordedHits.Clear();
        // Reset the Inspector hit counter for the fresh recording state.
        recordedHitCount = 0;
        // Clear the old take length before another recording begins.
        recordingDuration = 0f;

        // Mark the take as empty until new hit events are collected.
        hasRecording = false;
        // Leave completed-take review so A can begin a fresh recording.
        hasCompletedTake = false;
        // Collect new DrumSound hits in this take.
        isRecording = true;

        // Capture the current game time as zero for all new relative hit times.
        recordingStartTime = Time.time;
        // Return the small recording status to attached to the headset
        // placement.
        AttachRecordingStatusToHeadset();
        // Restore player sticks if recording was entered from a menu state.
        if (drumModeVisuals != null) drumModeVisuals.EnableDrumMode();

        // Refresh the Record artwork to match the current recording state.
        UpdateRecordButtonIcon();
        // Apply the current recorder state to visible buttons and UI ray
        // surfaces.
        UpdateButtonVisibility();

        // Log this state change for checking the experience sequence during
        // testing.
        Debug.Log("DRUM RECORDING STARTED");
    }

    // Finish the take and show review controls. Keep empty takes in review too,
    // so the player can choose Retake.
    public void StopRecording()
    {
        // Only continue this branch when recording is active is false.
        if (!isRecording)
            // Leave the take unchanged because recording is not currently
            // running.
            return;

        // Measure the full take duration from recording start to the player's
        // stop press.
        recordingDuration =
            Time.time - recordingStartTime;

        // Stop collecting new hits; captured events remain until explicitly
        // cleared.
        isRecording = false;
        // Refresh the hit counter after stopping the take.
        recordedHitCount = recordedHits.Count;
        // Mark whether the completed take actually contains sound events.
        hasRecording = recordedHits.Count > 0;
        // Offer review controls after stopping, even when the take contains no
        // hits.
        hasCompletedTake = true;
        // Freeze the review panel only when its canvas and viewing camera are
        // available.
        if (recordingCanvas != null && recordingCanvas.worldCamera != null)
        {
            // Find the scene player rig for this UI or controller setup.
            var origin = FindFirstObjectByType<XROrigin>();
            // Detach from head movement under XR Origin while keeping the
            // current world placement.
            recordingCanvas.transform.SetParent(origin != null ? origin.transform : null, true);
            // Place review controls one metre ahead of the current level view
            // for steady ray aiming.
            QuestUI.PlaceInFront(recordingCanvas, recordingCanvas.worldCamera, new Vector3(0f, 0f, 1f));
        }
        // Show menu rays for interacting with Preview, Retake and Save.
        if (drumModeVisuals != null) drumModeVisuals.SetMenuMode(true);
        // Keep the existing controller ray-line visuals visible while
        // reviewing.
        if (help != null) help.SetLaserVisuals(true);

        // Refresh the Record artwork to match the current recording state.
        UpdateRecordButtonIcon();
        // Apply the current recorder state to visible buttons and UI ray
        // surfaces.
        UpdateButtonVisibility();

        // Check whether the take contains at least one hit before taking this
        // branch.
        if (hasRecording)
        {
            // Log this state change for checking the experience sequence during
            // testing.
            Debug.Log(
                "DRUM RECORDING STOPPED. HITS: "
                + recordedHits.Count
                + ". DURATION: "
                + recordingDuration
            );
        }
        // Warn the player if the stopped take contains no drum hits.
        else
        {
            // Warn that the player stopped recording without any drum hits.
            Debug.LogWarning(
                "RECORDING STOPPED, BUT NO HITS WERE RECORDED"
            );
        }
    }

    // Discard the take being reviewed and show the ready-to-record screen. The
    // next A press starts a new take.
    public void RetakeRecording()
    {
        // Do not discard a take while it is in a performance or performance
        // transition.
        if (performanceTransitionActive || isPerformanceMode) return;
        // Stop any preview first so the next recorder state has no overlapping
        // playback.
        StopReplay();
        // Discard earlier hit events so a fresh take does not mix with the
        // previous one.
        recordedHits.Clear();
        // Reset the Inspector hit counter for the fresh recording state.
        recordedHitCount = 0;
        // Clear the old take length before another recording begins.
        recordingDuration = 0f;
        // Clear the old start time when resetting the completed take.
        recordingStartTime = 0f;
        // Stop collecting new hits; captured events remain until explicitly
        // cleared.
        isRecording = false;
        // Mark the take as empty until new hit events are collected.
        hasRecording = false;
        // Leave completed-take review so A can begin a fresh recording.
        hasCompletedTake = false;
        // Return the small recording status to attached to the headset
        // placement.
        AttachRecordingStatusToHeadset();
        // Return the player to sticks after leaving the review menu for a
        // retake.
        if (drumModeVisuals != null) drumModeVisuals.EnableDrumMode();
        // Refresh the Record artwork to match the current recording state.
        UpdateRecordButtonIcon();
        // Refresh the review Play/Pause artwork to match preview state.
        UpdatePlayButtonIcon();
        // Apply the current recorder state to visible buttons and UI ray
        // surfaces.
        UpdateButtonVisibility();
        // Log this state change for checking the experience sequence during
        // testing.
        Debug.Log("RETAKE: INITIAL RECORDING SCREEN RESTORED. PRESS A TO START.");
    }

    // Remember a sound played by DrumSound, but only while recording. drumSound
    // is that drum's sound file; store its name and time since recording began.
    public void RecordHit(AudioClip drumSound)
    {
        // Ignore this sound when recording is off or its sound file is missing.
        if (!isRecording || drumSound == null)
            // Skip this sound because recording is off or no valid sound file
            // was supplied.
            return;

        // Measure this hit's seconds from the same start used for the take
        // duration.
        float hitTime =
            Time.time - recordingStartTime;

        // Append the new event without changing the recorded order.
        recordedHits.Add(
            new DrumHit(drumSound.name, hitTime)
        );

        // Keep the Inspector event count in sync after adding a hit.
        recordedHitCount = recordedHits.Count;

        // Log this state change for checking the experience sequence during
        // testing.
        Debug.Log(
            "RECORDED HIT: "
            + drumSound.name
            + " AT "
            + hitTime
        );
    }


    // Use the review Play/Pause button to start, pause or resume one preview.
    // Do not preview while recording or watching the musician.
    public void TogglePlayback()
    {
        // Reject preview input during recording or performance, or without a
        // saved take.
        if (
            isRecording ||
            !hasRecording ||
            isPerformanceMode
        )
        {
            // Ignore preview input while recording, while the take is empty, or
            // during musician performance.
            return;
        }

        // Only continue this branch when preview playback is active is false.
        if (!isReplaying)
        {
            // Start the single preview if no preview timeline is currently
            // running.
            PlayRecording();
            // Finish after starting the preview so this same click does not
            // also pause it.
            return;
        }

        // Check whether preview playback is paused before taking this branch.
        if (isPlaybackPaused)
            // Resume the paused preview at its existing playback position.
            ResumeReplay();
        // Pause the preview if it is already playing.
        else
            // Pause the running preview instead of starting a second copy.
            PauseReplay();
    }

    // Check that the take has hits, then start one preview from its beginning.
    public void PlayRecording()
    {
        // Check whether recording is active before taking this branch.
        if (isRecording)
        {
            // Warn that the player must stop recording before listening to a
            // preview.
            Debug.LogWarning(
                "CANNOT PLAY WHILE RECORDING"
            );

            // Do not preview while new hits are being recorded.
            return;
        }

        // Reject preview without both the recorded-state flag and a non-empty
        // hit list.
        if (!hasRecording || recordedHits.Count == 0)
        {
            // Warn that there is no recorded beat to preview.
            Debug.LogWarning(
                "NO DRUM RECORDING TO PLAY"
            );

            // Do not start a preview without any recorded hits to play.
            return;
        }

        // Stop any preview first so the next recorder state has no overlapping
        // playback.
        StopReplay();

        // Mark the single preview timeline as running.
        isReplaying = true;
        // Clear preview pause so a new or resumed preview can advance.
        isPlaybackPaused = false;

        // Refresh the review Play/Pause artwork to match preview state.
        UpdatePlayButtonIcon();

        // Start the preview and remember it so a later button press can stop
        // it.
        replayCoroutine = StartCoroutine(
            ReplayRecording()
        );

        // Log this state change for checking the experience sequence during
        // testing.
        Debug.Log(
            "DRUM RECORDING STARTED PLAYING"
        );
    }

    // Run one preview from start to finish. While paused, wait without
    // advancing its playback time.
    private IEnumerator ReplayRecording()
    {
        // Start preview time at zero so the rhythm plays from the beginning of
        // the take.
        float playbackTime = 0f;
        // Start at the first unplayed hit in the list.
        int nextHitIndex = 0;

        // Choose a positive preview duration that leaves room for the final
        // hit.
        float previewDuration =
            GetSafeRecordingDuration();

        // Keep preview running until its measured duration has passed.
        while (playbackTime < previewDuration)
        {
            // Check whether preview playback is paused before taking this
            // branch.
            if (isPlaybackPaused)
            {
                // Wait for the next game update so this sequence does not block
                // the rest of the game.
                yield return null;
                // Wait for another update without moving preview time forward
                // while paused.
                continue;
            }

            // Add seconds since the last update to preview time, unless paused.
            playbackTime += Time.deltaTime;

            // Play the hits due by this preview time and advance the next-hit
            // index.
            PlayHitsAtTime(
                playbackTime,
                ref nextHitIndex
            );

            // Wait for the next game update so this sequence does not block the
            // rest of the game.
            yield return null;
        }

        // Mark preview playback stopped so another preview may start cleanly.
        isReplaying = false;
        // Clear preview pause so a new or resumed preview can advance.
        isPlaybackPaused = false;
        // Forget the preview sequence now that it has finished.
        replayCoroutine = null;

        // Refresh the review Play/Pause artwork to match preview state.
        UpdatePlayButtonIcon();

        // Log this state change for checking the experience sequence during
        // testing.
        Debug.Log(
            "DRUM RECORDING FINISHED PLAYING"
        );
    }

    // Stop the preview time and pause its current sound until the player
    // resumes.
    private void PauseReplay()
    {
        // Only pause a preview that is running and not already paused.
        if (!isReplaying || isPlaybackPaused)
            // Do nothing when there is no running preview that can be paused.
            return;

        // Freeze the preview clock until ResumeReplay is requested.
        isPlaybackPaused = true;

        // Only control replay audio if its shared output source exists.
        if (playbackAudioSource != null)
            // Pause the current output alongside the playback clock.
            playbackAudioSource.Pause();

        // Refresh the review Play/Pause artwork to match preview state.
        UpdatePlayButtonIcon();

        // Log this state change for checking the experience sequence during
        // testing.
        Debug.Log("DRUM RECORDING PAUSED");
    }

    // Continue the same preview time and audio, without starting another
    // preview.
    private void ResumeReplay()
    {
        // Only resume a preview that is running but currently paused.
        if (!isReplaying || !isPlaybackPaused)
            // Do nothing when no paused preview is waiting to continue.
            return;

        // Clear preview pause so a new or resumed preview can advance.
        isPlaybackPaused = false;

        // Only control replay audio if its shared output source exists.
        if (playbackAudioSource != null)
            // Resume the paused output without restarting the recorded
            // timeline.
            playbackAudioSource.UnPause();

        // Refresh the review Play/Pause artwork to match preview state.
        UpdatePlayButtonIcon();

        // Log this state change for checking the experience sequence during
        // testing.
        Debug.Log("DRUM RECORDING RESUMED");
    }

    // Stop the running preview and its sound. Clear the remembered
    // preview/pause state for the next action.
    private void StopReplay()
    {
        // Only stop a preview sequence if one is still remembered.
        if (replayCoroutine != null)
        {
            // Stop the existing timed sequence so it cannot keep sending hits
            // after replay ends.
            StopCoroutine(replayCoroutine);
            // Forget the stopped preview so the next take can start a fresh
            // one.
            replayCoroutine = null;
        }

        // Mark preview playback stopped so another preview may start cleanly.
        isReplaying = false;
        // Clear preview pause so a new or resumed preview can advance.
        isPlaybackPaused = false;

        // Only control replay audio if its shared output source exists.
        if (playbackAudioSource != null)
            // Stop any sounds still playing through the shared
            // preview/performance source.
            playbackAudioSource.Stop();

        // Refresh the review Play/Pause artwork to match preview state.
        UpdatePlayButtonIcon();
    }


    // Stop old playback and hide recording controls before the player moves to
    // the watching position.
    public void BeginPerformanceTransition()
    {
        // Stop any preview first so the next recorder state has no overlapping
        // playback.
        StopReplay();
        // Stop the old performance timed sequence/audio before changing
        // playback state.
        StopPerformance();

        // Hide recording/review interaction while the viewing position
        // transition runs.
        performanceTransitionActive = true;

        // Apply the current recorder state to visible buttons and UI ray
        // surfaces.
        UpdateButtonVisibility();
    }

    // Clear the transition state and stop playback if the performance sequence
    // is cancelled.
    public void CancelPerformanceTransition()
    {
        // Release the transition gate so the next valid recording/playback
        // action can proceed.
        performanceTransitionActive = false;
        // Stop any preview first so the next recorder state has no overlapping
        // playback.
        StopReplay();
        // Stop the old performance timed sequence/audio before changing
        // playback state.
        StopPerformance();
    }

    // Start the musician's repeating performance if the take contains hits. Use
    // one playback time for both sound and arm movement.
    public void StartLoopedPerformance()
    {
        // Require real recorded hits before starting the musician loop.
        if (!hasRecording || recordedHits.Count == 0)
        {
            // Warn that musician playback needs a recorded beat first.
            Debug.LogWarning(
                "NO RECORDING AVAILABLE FOR PERFORMANCE"
            );

            // Leave performance stopped because there are no recorded hits to
            // play.
            return;
        }

        // Stop any preview first so the next recorder state has no overlapping
        // playback.
        StopReplay();
        // Stop the old performance timed sequence/audio before changing
        // playback state.
        StopPerformance();

        // Release the transition gate so the next valid recording/playback
        // action can proceed.
        performanceTransitionActive = false;
        // Make the looping performer playback the active recorder mode.
        isPerformanceMode = true;
        // Clear the old performance pause flag before another loop or recording
        // cycle.
        isPerformancePaused = false;

        // Remember the single performance timed sequence so repeated
        // transitions can stop it safely.
        performanceCoroutine = StartCoroutine(
            LoopPerformance()
        );

        // Apply the current recorder state to visible buttons and UI ray
        // surfaces.
        UpdateButtonVisibility();

        // Log this state change for checking the experience sequence during
        // testing.
        Debug.Log(
            "LOOPED MUSICIAN PERFORMANCE STARTED"
        );
    }

    // Repeat the take. Each repetition starts at time zero and plays every hit
    // again. Pausing stops both the sound timing and musician movement.
    private IEnumerator LoopPerformance()
    {
        // Choose a positive length for one repeated performance, including the
        // final hit.
        float loopDuration =
            GetSafeRecordingDuration();

        // Prepare the musician's arm movements if PerformanceSequence is
        // available.
        if (performanceSequence != null)
            // Give the selected drummer hit targets and the loop length before
            // playback advances.
            performanceSequence.PrepareDrumming(recordedHits, loopDuration);

        // Repeat only while performance mode remains active.
        while (isPerformanceMode)
        {
            // Start this repetition's playback time at zero.
            float playbackTime = 0f;
            // Start at the first hit again for this new repetition.
            int nextHitIndex = 0;

            // Keep playing this repetition until its duration ends or
            // performance stops.
            while (
                playbackTime < loopDuration &&
                isPerformanceMode
            )
            {
                // Check whether the musician performance is paused before
                // taking this branch.
                if (isPerformancePaused)
                {
                    // Wait for the next game update so this sequence does not
                    // block the rest of the game.
                    yield return null;
                    // Keep the sound and arm clocks still while the performance
                    // is paused.
                    continue;
                }

                // Advance the shared sound/arm time only while the performance
                // is not paused.
                playbackTime += Time.deltaTime;

                // Emit all audio events newly due at this clock time.
                PlayHitsAtTime(
                    playbackTime,
                    ref nextHitIndex
                );

                // Update the musician's arms to this playback time if
                // PerformanceSequence exists.
                if (performanceSequence != null)
                    // Calculate the musician at the audio clock time; no
                    // separate animation clock runs here.
                    performanceSequence.EvaluateDrumming(playbackTime);

                // Wait for the next game update so this sequence does not block
                // the rest of the game.
                yield return null;
            }
        }

        // Forget the musician playback sequence after its loop ends.
        performanceCoroutine = null;
    }

    // Pause or resume the same musician performance. Keep the current time
    // rather than restarting the beat.
    public void TogglePerformancePause()
    {
        // Only continue this branch when looped musician playback is active is
        // false.
        if (!isPerformanceMode)
            // Ignore Pause when the recorder is not playing the musician loop.
            return;

        // Set performance pause to the opposite of its current state.
        isPerformancePaused =
            !isPerformancePaused;

        // Only control replay audio if its shared output source exists.
        if (playbackAudioSource != null)
        {
            // Check whether the musician performance is paused before taking
            // this branch.
            if (isPerformancePaused)
                // Pause the current output alongside the playback clock.
                playbackAudioSource.Pause();
            // Resume paused sounds when the player presses Resume.
            else
                // Resume the paused output without restarting the recorded
                // timeline.
                playbackAudioSource.UnPause();
        }

        // Log this state change for checking the experience sequence during
        // testing.
        Debug.Log(
            isPerformancePaused
                ? "PERFORMANCE PAUSED"
                : "PERFORMANCE RESUMED"
        );
    }

    // Stop the repeating beat and clear the old pause state. Refresh the
    // recording UI for whichever stage follows.
    public void StopPerformance()
    {
        // Only stop a musician playback sequence if one is still remembered.
        if (performanceCoroutine != null)
        {
            // Stop the loop timed sequence so no later frame can play stale
            // performance hits.
            StopCoroutine(performanceCoroutine);
            // Forget the stopped playback so the next performance can start a
            // fresh one.
            performanceCoroutine = null;
        }

        // Leave looping performer mode so normal recording controls can return.
        isPerformanceMode = false;
        // Clear the old performance pause flag before another loop or recording
        // cycle.
        isPerformancePaused = false;

        // Only control replay audio if its shared output source exists.
        if (playbackAudioSource != null)
            // Stop any sounds still playing through the shared
            // preview/performance source.
            playbackAudioSource.Stop();

        // Apply the current recorder state to visible buttons and UI ray
        // surfaces.
        UpdateButtonVisibility();
    }

    // Delete the saved take and clear its remembered hits. Return to a clean
    // recording state so the player can record another beat.
    public void ResetAfterPerformanceDelete()
    {
        // Stop any preview first so the next recorder state has no overlapping
        // playback.
        StopReplay();
        // Stop the old performance timed sequence/audio before changing
        // playback state.
        StopPerformance();

        // Discard earlier hit events so a fresh take does not mix with the
        // previous one.
        recordedHits.Clear();

        // Reset the Inspector hit counter for the fresh recording state.
        recordedHitCount = 0;
        // Clear the old take length before another recording begins.
        recordingDuration = 0f;
        // Clear the old start time when resetting the completed take.
        recordingStartTime = 0f;

        // Stop collecting new hits; captured events remain until explicitly
        // cleared.
        isRecording = false;
        // Mark preview playback stopped so another preview may start cleanly.
        isReplaying = false;
        // Clear preview pause so a new or resumed preview can advance.
        isPlaybackPaused = false;
        // Mark the take as empty until new hit events are collected.
        hasRecording = false;
        // Leave completed-take review so A can begin a fresh recording.
        hasCompletedTake = false;
        // Return the small recording status to attached to the headset
        // placement.
        AttachRecordingStatusToHeadset();

        // Release the transition gate so the next valid recording/playback
        // action can proceed.
        performanceTransitionActive = false;
        // Leave looping performer mode so normal recording controls can return.
        isPerformanceMode = false;
        // Clear the old performance pause flag before another loop or recording
        // cycle.
        isPerformancePaused = false;

        // Remove the saved JSON take as part of Delete and Re-record.
        DeleteSavedRecordingFile();

        // Refresh the Record artwork to match the current recording state.
        UpdateRecordButtonIcon();
        // Refresh the review Play/Pause artwork to match preview state.
        UpdatePlayButtonIcon();
        // Apply the current recorder state to visible buttons and UI ray
        // surfaces.
        UpdateButtonVisibility();

        // Log this state change for checking the experience sequence during
        // testing.
        Debug.Log(
            "SAVED PERFORMANCE DELETED"
        );
    }

    // Play every hit due by playbackTime, measured in seconds into this replay.
    // nextHitIndex remembers the next unplayed hit; ref lets this method update
    // it for the next call.
    private void PlayHitsAtTime(
        float playbackTime,
        ref int nextHitIndex
    )
    {
        // Play all recorded hits whose saved times have now been reached.
        while (
            nextHitIndex < recordedHits.Count &&
            playbackTime >=
            recordedHits[nextHitIndex].time
        )
        {
            // Read the next due hit from the recorded list.
            DrumHit hit =
                recordedHits[nextHitIndex];

            // Find the actual sound file whose name was saved for this hit.
            AudioClip clip =
                FindDrumSound(hit.soundName);

            // Only play when both the saved sound and the speaker component
            // exist.
            if (
                clip != null &&
                playbackAudioSource != null
            )
            {
                // Play this hit's sound once, letting other recent hits keep
                // sounding.
                playbackAudioSource.PlayOneShot(clip);
            }
            // Warn about the missing sound or speaker instead of trying to play
            // it.
            else
            {
                // Warn which saved sound could not be replayed.
                Debug.LogWarning(
                    "COULD NOT REPLAY SOUND: "
                    + hit.soundName
                );
            }

            // Advance even after a missing sound, so one invalid entry cannot
            // stall the timeline.
            nextHitIndex++;
        }
    }

    // Return a positive take length that includes the final hit and a short
    // tail. This is shared by preview and musician playback.
    private float GetSafeRecordingDuration()
    {
        // Use zero as the last-hit fallback when the take contains no events.
        float lastHitTime = 0f;

        // Only index the final hit when at least one captured event exists.
        if (recordedHits.Count > 0)
        {
            // Remember when the last captured hit occurred.
            lastHitTime =
                recordedHits[
                    recordedHits.Count - 1
                ].time;
        }

        // Return the longest of the take duration, the last hit plus a short
        // tail, and a positive minimum.
        return Mathf.Max(
            recordingDuration,
            lastHitTime + 0.1f,
            0.1f
        );
    }

    // Find the sound file matching soundName from the recorded hit. Return that
    // file, or no clip if the configured list has no match.
    private AudioClip FindDrumSound(
        string soundName
    )
    {
        // Return the first configured clip satisfying the following name match,
        // or missing if none does.
        return drumSounds.Find(
            sound =>
                sound != null &&
                sound.name == soundName
        );
    }


    // Show the controls for recording or review, and hide them while the player
    // watches the musician. Also stop hidden recording UI from taking pointer
    // clicks.
    private void UpdateButtonVisibility()
    {
        // Hide recording controls while the musician plays or the player is
        // moving to/from that view.
        bool hideRecordingButtons =
            performanceTransitionActive ||
            isPerformanceMode;

        // Only change recording UI when its canvas reference is available.
        if (recordingCanvas != null)
        {
            // Hide the recording canvas drawing during performance; proximity
            // still controls its object.
            recordingCanvas.enabled = !hideRecordingButtons;
            // Find the recording canvas's pointer checker, which could
            // otherwise catch performance clicks.
            var tracked = recordingCanvas.GetComponent<TrackedDeviceGraphicRaycaster>();
            // Turn that checker off for performance/transitions and back on for
            // recording.
            if (tracked != null) tracked.enabled = !hideRecordingButtons;
// Compile the following part only for testing inside the Unity Editor.
#if UNITY_EDITOR
            // Find the recording canvas's mouse pointer checker for desktop
            // testing.
            var desktop = recordingCanvas.GetComponent<GraphicRaycaster>();
            // Apply the same hide/show interaction rule to desktop mouse
            // clicks.
            if (desktop != null) desktop.enabled = !hideRecordingButtons;
// End the Editor-specific choice; the following shared code is kept for both.
#endif
        }

        // Show review buttons only for a stopped take outside recording,
        // performance and transitions.
        bool showReviewButtons =
            !hideRecordingButtons &&
            hasCompletedTake &&
            !isRecording;

        // Show the review container only in that completed-take review state.
        SetButtonActive(reviewPanel, showReviewButtons);
        // Check that the review dimmer exists before changing its visibility.
        if (reviewOverlayCanvas != null)
            // Show the review dimmer only while review is visible and
            // proximity's canvas is active.
            SetButtonActive(reviewOverlayCanvas.gameObject, showReviewButtons && recordingCanvas.gameObject.activeInHierarchy);
        // Show the small recording status only when neither review nor
        // performance controls are needed.
        SetButtonActive(recordStatusPanel, !hideRecordingButtons && !showReviewButtons);
        // Check that the recording label exists before showing its current
        // status.
        if (recordingStatusText != null)
            // Tell the player whether A will stop an active take or start a new
            // one.
            recordingStatusText.text = isRecording ? "●  RECORDING   •   A to stop" : "A  •  Start recording";
        // Check that the review heading exists before describing the completed
        // take.
        if (reviewTitle != null)
            // Offer a ready title for real hits, otherwise explain that the
            // empty take needs a retake.
            reviewTitle.text = hasRecording ? "RECORDING READY" : "NO HITS RECORDED — CHOOSE RETAKE";
        // Permit preview clicks only when the completed take contains sound
        // events.
        SetReviewButtonInteractable(playbackButton, hasRecording);
        // Permit saving only when the take contains hits that can be performed.
        SetReviewButtonInteractable(saveButton, hasRecording);

        // Apply the ready/recording-state visibility to the existing Record
        // object.
        SetButtonActive(
            recordButton,
            !hideRecordingButtons &&
            !showReviewButtons
        );

        // Apply completed-take review visibility to the existing Retake object.
        SetButtonActive(
            retakeButton,
            showReviewButtons
        );

        // Apply the same review-stage visibility to the existing Preview
        // button.
        SetButtonActive(
            playbackButton,
            showReviewButtons
        );

        // Keep Save with the other review controls rather than the live
        // recording screen.
        SetButtonActive(
            saveButton,
            showReviewButtons
        );
    }

    // Make the small status panel follow the player's view again after leaving
    // the steady review menu.
    private void AttachRecordingStatusToHeadset()
    {
        // Skip headset attachment without both the recording canvas and its
        // viewing camera.
        if (recordingCanvas == null || recordingCanvas.worldCamera == null) return;
        // Parent the status UI back to its own viewing camera without retaining
        // a review offset.
        recordingCanvas.transform.SetParent(recordingCanvas.worldCamera.transform, false);
        // Place the UI relative to its parent, such as the headset or
        // containing panel.
        recordingCanvas.transform.localPosition = new Vector3(0f, -.05f, 1f);
        // Set local scale explicitly so inherited layout changes do not stretch
        // this UI.
        recordingCanvas.transform.localScale = Vector3.one * .001f;
    }

    // Let a review button accept clicks only when selectable object is true.
    // buttonObject is the existing Preview or Save button.
    private void SetReviewButtonInteractable(GameObject buttonObject, bool interactable)
    {
        // Only change interaction when the supplied object actually has a
        // Button component.
        if (buttonObject != null && buttonObject.GetComponent<Button>() != null)
            // Apply the requested clickability without replacing that button's
            // listeners.
            buttonObject.GetComponent<Button>().interactable = interactable;
    }

    // Show/hide buttonObject only if its current state differs from
    // shouldBeActive. This avoids repeating the same change every update.
    private void SetButtonActive(
        GameObject buttonObject,
        bool shouldBeActive
    )
    {
        // Only change a button object if it exists and needs a different
        // visibility.
        if (
            buttonObject != null &&
            buttonObject.activeSelf != shouldBeActive
        )
        {
            // Update the GameObject's visibility only after those guards have
            // passed.
            buttonObject.SetActive(
                shouldBeActive
            );
        }
    }

    // Show the recording image during a take and the normal image when ready.
    private void UpdateRecordButtonIcon()
    {
        // Check for a missing recording image before trying to change its
        // picture.
        if (recordButtonImage == null)
            // Leave the recording icon alone if its Image component is missing.
            return;

        // Use the recording-in-progress picture when recording and that picture
        // exists.
        if (
            isRecording &&
            activeRecordIcon != null
        )
        {
            // Assign the recording-state artwork to the existing Record Image.
            recordButtonImage.sprite =
                activeRecordIcon;
        }
        // Otherwise prefer the normal icon when that artwork was assigned.
        else if (normalRecordIcon != null)
        {
            // Assign the ready-state artwork without replacing the button
            // object.
            recordButtonImage.sprite =
                normalRecordIcon;
        }
    }

    // Show Pause while the preview runs, or Play when it is stopped/paused.
    private void UpdatePlayButtonIcon()
    {
        // Check for a missing preview image before trying to change its
        // picture.
        if (playButtonImage == null)
            // Leave the preview icon alone if its Image component is missing.
            return;

        // Show the Pause picture when the preview is playing and not paused.
        if (
            isReplaying &&
            !isPlaybackPaused &&
            pauseIcon != null
        )
        {
            // Update the existing Preview Image with the offered Pause action.
            playButtonImage.sprite =
                pauseIcon;
        }
        // Otherwise show Play when the normal artwork exists.
        else if (playIcon != null)
        {
            // Update the existing Preview Image with the Play/Resume action.
            playButtonImage.sprite =
                playIcon;
        }
    }


    // Save the take's hit names, times and length as JSON. Then ask
    // PerformanceSequence to show the selected musician playing it.
    public void SaveRecording()
    {
        // Ignore repeated Save requests during a performance or its ongoing
        // transition.
        if (performanceTransitionActive || isPerformanceMode) return;
        // Check whether recording is active before taking this branch.
        if (isRecording)
        {
            // Warn that recording must be stopped before the player can save
            // the take.
            Debug.LogWarning(
                "STOP RECORDING BEFORE SAVING"
            );

            // Leave without saving or starting a performance when the current
            // take/state is invalid.
            return;
        }

        // Do not save an empty list even if UI interaction reaches this method
        // directly.
        if (!hasRecording || recordedHits.Count == 0)
        {
            // Warn that there are no recorded hits to save.
            Debug.LogWarning(
                "NO DRUM RECORDING TO SAVE"
            );

            // Leave without saving or starting a performance when the current
            // take/state is invalid.
            return;
        }

        // Copy this take's hits and duration into the data object that will be
        // saved.
        RecordingData recordingData =
            new RecordingData
            {
                hits =
                    new List<DrumHit>(
                        recordedHits
                    ),

                duration = recordingDuration
            };

        // Convert that recording data to readable JSON text.
        string json =
            JsonUtility.ToJson(
                recordingData,
                true
            );

        // Find the recording filename in this app's writable save folder.
        string savePath =
            GetSavePath();

        // Write that JSON text to the recording file on this device.
        File.WriteAllText(
            savePath,
            json
        );

        // Log this state change for checking the experience sequence during
        // testing.
        Debug.Log(
            "DRUM RECORDING SAVED: "
            + savePath
        );

        // Start the chosen musician's performance if PerformanceSequence is
        // assigned.
        if (performanceSequence != null)
        {
            // Start the selected musician performance after the file has been
            // written.
            performanceSequence.BeginPerformance();
        }
        // Warn that the musician performance cannot start without
        // PerformanceSequence.
        else
        {
            // Warn that the saved beat cannot start a performance without
            // PerformanceSequence.
            Debug.LogWarning(
                "PERFORMANCE SEQUENCE HAS NOT BEEN ASSIGNED"
            );
        }
    }

    // Remove this experience's saved recording file if it exists. Project
    // assets and APK files are not involved.
    private void DeleteSavedRecordingFile()
    {
        // Find the same application-data filename used when saving the take.
        string savePath = GetSavePath();

        // Delete only if this recording file currently exists.
        if (File.Exists(savePath))
            // Remove that saved take; this does not remove APKs or project
            // assets.
            File.Delete(savePath);
    }

    // Return the full recording filename in the app's writable data folder on
    // this device.
    private string GetSavePath()
    {
        // Return a platform-correct combined folder and recording filename.
        return Path.Combine(
            Application.persistentDataPath,
            "drum-recording.json"
        );
    }
}

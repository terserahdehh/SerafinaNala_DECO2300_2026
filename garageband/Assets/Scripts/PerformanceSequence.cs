/*
 * Shows the selected musician performing after the player saves a beat.
 * It fades the view, places Mika or Ren at the kit and moves the player to
 * the watching position. Pause changes DrumRecorder's playback state.
 * Delete and Re-record removes the take and returns the player to the seat.
 * DrumRecorder supplies one time for sounds and ProceduralDrummer's arms.
 * This is active game code, including Editor Play Mode. IntroSequence supplies
 * the musician choice; DrumModeVisuals switches player sticks/menu pointers.
 * A Transform stores an object's position, turn and size. Scene markers give
 * destinations. XR Origin groups the tracked camera/controllers. A CanvasGroup
 * controls a UI group's visibility/clicks. A coroutine can wait between steps
 * while the rest of the game continues. A Quaternion stores a rotation.
 * UI means the buttons and text the player sees and uses.
 * The Inspector is Unity's settings panel for assigning objects and values.
 */
// Use sequences that can wait while the game keeps running.
using System.Collections;
// Use Unity objects, positions and the methods Unity calls during the game.
using UnityEngine;
// Use Unity canvas graphics, buttons and scrolling controls.
using UnityEngine.UI;
// Use XR Origin, used for the player rig and headset camera.
using Unity.XR.CoreUtils;

// Shows the selected musician performing after the player saves a beat.
public class PerformanceSequence : MonoBehaviour
{
    // Keep the onboarding coordinator that stores the musician choice.
    [Header("Main References")]
    [SerializeField] private IntroSequence introSequence;
    // Keep the recorder that remembers hits and controls the musician's
    // playback time.
    [SerializeField] private DrumRecorder drumRecorder;
    // Keep the controller/stick and menu-ray switcher.
    [SerializeField] private DrumModeVisuals drumModeVisuals;

    // Keep the player XR Origin used for headset position and facing.
    [Header("Player")]
    [SerializeField] private XROrigin xrOrigin;
    // Keep the scene marker from which the player watches the musician.
    [SerializeField] private Transform performanceViewPoint;
    // Keep the scene marker for the automatic drum-seat viewing position.
    [SerializeField] private Transform drumSeatTarget;
    // Keep the assigned manual movement component.
    [SerializeField] private Behaviour moveProvider;

    // Keep the fade overlay and its UI input-blocking state.
    [Header("Transition")]
    [SerializeField] private CanvasGroup blackoutCanvasGroup;
    // Keep the short pause after saving before the performance fade.
    [SerializeField] private float delayAfterSave = 0.5f;
    // Keep seconds spent fading to a fully black view.
    [SerializeField] private float fadeToBlackDuration = 1f;
    // Keep seconds spent holding black during placement.
    [SerializeField] private float blackoutHoldDuration = 0.5f;
    // Keep seconds spent restoring the normal view.
    [SerializeField] private float fadeFromBlackDuration = 1f;
    // Keep the extra hold before the performance is revealed.
    [SerializeField] private float delayBeforePerformance = 1f;

    // Keep the existing Pause and Delete/Re-record UI objects.
    [Header("Performance UI")]
    [SerializeField] private GameObject performanceControls;
    // Keep the image that offers pause or resume during performance.
    [SerializeField] private Image pauseButtonImage;
    // Keep the artwork offering pause.
    [SerializeField] private Sprite pauseIcon;
    // Keep the artwork offering playback or resume.
    [SerializeField] private Sprite playIcon;

    // Keep Mika's whole character object.
    [Header("Mika")]
    [SerializeField] private Transform mika;
    // Keep Mika's model transform, including its imported turn adjustment for
    // the imported model.
    [SerializeField] private Transform mikaRotationTarget;
    // Keep Mika's placement marker for the performance.
    [SerializeField] private Transform mikaPerformancePoint;
    // Keep the Animator associated with Mika's connected body parts.
    [SerializeField] private Animator mikaAnimator;

    // Keep Ren's whole character object.
    [Header("Ren")]
    [SerializeField] private Transform ren;
    // Keep Ren's model transform, including its imported turn adjustment for
    // the imported model.
    [SerializeField] private Transform renRotationTarget;
    // Keep Ren's placement marker for the performance.
    [SerializeField] private Transform renPerformancePoint;
    // Keep the Animator associated with Ren's connected body parts.
    [SerializeField] private Animator renAnimator;

    // Keep Mika's sitting bones, grip corrections and stick settings.
    [Header("Procedural Drumming")]
    [SerializeField] private ProceduralDrummer.Rig mikaRig = new ProceduralDrummer.Rig();
    // Keep Ren's sitting bones, grip corrections and stick settings.
    [SerializeField] private ProceduralDrummer.Rig renRig = new ProceduralDrummer.Rig();
    // Keep the sound-name mappings to drum surface animation markers.
    [SerializeField] private ProceduralDrummer.DrumTarget[] drumTargets;
    // Keep the current performer's procedural sitting animation component.
    private ProceduralDrummer selectedDrummer;

    // Keep Mika's original model rotation relative to the whole character
    // object.
    private Quaternion initialMikaLocalRotation;
    // Keep Ren's original model rotation relative to the whole character
    // object.
    private Quaternion initialRenLocalRotation;

    // Keep the Animator belonging to the selected performer.
    private Animator selectedAnimator;
    // Keep the selected whole character object used for performance placement.
    private Transform selectedMusician;

    // Keep the dedicated game-created canvas for performance controls.
    private Canvas performanceCanvas;
    // Keep the shared visibility and ray-interaction controls for that canvas.
    private CanvasGroup performanceUIGroup;
    // Keep the active transition handle, preventing overlapping transitions.
    private Coroutine transitionCoroutine;

    // Remember whether a viewing position transition is running, so overlapping
    // performance requests are blocked.
    private bool sequenceRunning;
    // Remember whether the performance sequence is active, so Pause/Delete may
    // operate the current performance.
    private bool performanceActive;

    // Remember Mika's and Ren's model turn adjustments. Set up the watching
    // controls but leave them hidden until performance starts.
    private void Start()
    {
        // Hide the performance controls at startup if their object is assigned.
        if (performanceControls != null)
            // Hide the existing Pause and Delete/Re-record UI objects.
            performanceControls.SetActive(false);

        // Resolve the scene player rig only when the Inspector did not supply
        // it.
        if (xrOrigin == null)
            // Find the active scene XR Origin for headset-aware placement.
            xrOrigin = FindFirstObjectByType<XROrigin>();

        // Save Mika's model angle only if her model object exists.
        if (mikaRotationTarget != null)
        {
            // Save Mika's model turn relative to her whole object before
            // performance changes her placement.
            initialMikaLocalRotation =
                mikaRotationTarget.localRotation;
        }

        // Save Ren's model angle only if his model object exists.
        if (renRotationTarget != null)
        {
            // Save Ren's model turn separately for his later performance
            // placement.
            initialRenLocalRotation =
                renRotationTarget.localRotation;
        }

        // Ensure the dedicated performance canvas exists before restoring
        // controls.
        PreparePerformanceUI();
        // Offer Pause at first because the new performance is not paused.
        UpdatePauseButtonIcon(false);
    }

    // Arrange the existing Pause and Delete/Re-record buttons on a separate
    // canvas. Keep their Inspector click events.
    private void PreparePerformanceUI()
    {
        // Avoid duplicate performance canvases and stop setup without the
        // controls or tracked view.
        if (performanceCanvas != null || performanceControls == null || xrOrigin == null || xrOrigin.Camera == null) return;
        // Create the dedicated room canvas used only by performance controls.
        performanceCanvas = QuestUI.CreateCanvas("Performance Controls Canvas", xrOrigin.Camera,
            new Vector2(950f, 430f), 150);
        // Attach the performance canvas under the player group so automatic
        // travel carries it too.
        performanceCanvas.transform.SetParent(xrOrigin.transform, false);
        // Add one group for visibility, interaction and pointer blocking on
        // this canvas.
        performanceUIGroup = performanceCanvas.gameObject.AddComponent<CanvasGroup>();
        // Hide and disable performance controls during setup, transitions or
        // shutdown.
        SetPerformanceUI(false);
        // Set the position and size of the Pause and Delete/Re-record controls
        // inside its parent panel.
        QuestUI.Layout(performanceControls.GetComponent<RectTransform>(), performanceCanvas.transform,
            Vector2.zero, new Vector2(950f, 430f));
        // Collect existing buttons instead of replacing their events with new
        // objects.
        var buttons = performanceControls.GetComponentsInChildren<Button>(true);
        // Arrange each existing button, preserving its existing click
        // callbacks.
        foreach (var button in buttons)
        {
            // Identify Pause by its assigned image object so the other button
            // can use a wider layout.
            bool pause = pauseButtonImage != null && button.gameObject == pauseButtonImage.gameObject;
            // Read this button's artwork before deciding its width/height.
            var image = button.GetComponent<Image>();
            // Give Pause a square area and Delete/Re-record a wider area.
            Vector2 size = pause ? new Vector2(150f, 150f) : new Vector2(310f, 150f);
            // Measure artwork proportions only when a button picture is
            // available.
            if (image != null && image.sprite != null)
            {
                // Calculate width divided by height so sizing keeps the button
                // artwork proportional.
                float aspect = image.sprite.rect.width / image.sprite.rect.height;
                // Calculate height from artwork proportions at the selected
                // width.
                size = new Vector2(size.x, size.x / aspect);
                // Scale down anything taller than the allowed row height
                // without stretching it.
                if (size.y > 150f) size *= 150f / size.y;
                // Keep the source picture proportions instead of stretching its
                // artwork.
                image.preserveAspect = true;
            }
            // Set the position and size of this menu button inside its parent
            // panel.
            QuestUI.Layout(button.GetComponent<RectTransform>(), performanceControls.transform,
                new Vector2(pause ? 230f : -230f, 0f), size);
            // Use pointer/trigger clicks instead of automatic gamepad button
            // automatic button selection.
            button.navigation = new Navigation { mode = Navigation.Mode.None };
        }
        // Draw this UI over room geometry using the shared overlay shader
        // setup.
        QuestUI.ApplyOverlay(performanceControls);
    }

    // After Save, check the needed objects and start one fade-and-performance
    // sequence. Ignore duplicate starts.
    public void BeginPerformance()
    {
        // Ignore overlapping starts while a transition or existing performance
        // is still active.
        if (sequenceRunning || performanceActive || transitionCoroutine != null)
            // Do not start another sequence when a transition/performance is
            // active or required setup is missing.
            return;

        // Only start if the introduction, recorder, blackout, player and view
        // marker exist.
        if (
            introSequence == null ||
            drumRecorder == null ||
            blackoutCanvasGroup == null ||
            xrOrigin == null ||
            performanceViewPoint == null
        )
        {
            // Warn that the objects needed to start the musician performance
            // are missing.
            Debug.LogWarning(
                "PERFORMANCE SEQUENCE REFERENCES ARE MISSING"
            );

            // Do not start another sequence when a transition/performance is
            // active or required setup is missing.
            return;
        }

        // Block other performance requests while this transition is underway.
        sequenceRunning = true;

        // Tell the recorder to hide and disable its UI and stop old playback
        // during this fade.
        drumRecorder.BeginPerformanceTransition();

        // Remember this transition timed sequence so duplicate requests can be
        // blocked and cancellation can stop it.
        transitionCoroutine = StartCoroutine(BeginPerformanceRoutine());
    }

    // Fade the view, seat the chosen musician and move the player to watch.
    // Start the repeating beat, then make the performance buttons usable.
    private IEnumerator BeginPerformanceRoutine()
    {
        // Hide and disable performance controls during setup, transitions or
        // shutdown.
        SetPerformanceUI(false);

        // Pause this sequence for the configured delay while Unity keeps
        // running other updates.
        yield return new WaitForSeconds(
            delayAfterSave
        );

        // Block UI pointer events through the blackout while placement changes.
        blackoutCanvasGroup.blocksRaycasts = true;

        // Wait for the blackout fade to finish before moving to the next
        // transition step.
        yield return FadeBlackout(
                blackoutCanvasGroup.alpha,
                1f,
                fadeToBlackDuration
            );

        // Only switch sticks/menu rays when the mode controller is available.
        if (drumModeVisuals != null)
            // Show menu/controller visuals instead of player sticks during
            // performance.
            drumModeVisuals.DisableDrumMode();

        // Place and seat the musician chosen during onboarding while the screen
        // is black.
        PlaceSelectedMusician();

        // Use the shared headset-aware placement helper for this viewing
        // position marker.
        MovePlayerToPoint(
            performanceViewPoint
        );

        // Update physics with the new transforms before the next interaction
        // frame.
        Physics.SyncTransforms();
        // Wait for the next game update so this sequence does not block the
        // rest of the game.
        yield return null;

        // Pause this sequence for the configured delay while Unity keeps
        // running other updates.
        yield return new WaitForSeconds(
            blackoutHoldDuration + delayBeforePerformance
        );

        // Wait for the blackout fade to finish before moving to the next
        // transition step.
        yield return FadeBlackout(
                1f,
                0f,
                fadeFromBlackDuration
            );

        // Release the blackout input blocker after the transition is finished.
        blackoutCanvasGroup.blocksRaycasts = false;

        // Start the recorder's one repeated audio/pose timeline after the fade
        // is complete.
        drumRecorder.StartLoopedPerformance();
        // Use the recorder's actual performance-mode result rather than
        // assuming playback started.
        performanceActive = drumRecorder.IsPerformanceMode;
        // Allow future performance controls now that the transition is
        // finished.
        sequenceRunning = false;
        // Forget the finished view transition so another one can start later.
        transitionCoroutine = null;
        // Restore canvas/ray interaction on every entry, including the second
        // and later cycles.
        RestorePerformanceInteraction();
        // Offer Pause at first because the new performance is not paused.
        UpdatePauseButtonIcon(false);

        // Log this state change for checking the experience sequence during
        // testing.
        Debug.Log(
            "MUSICIAN PERFORMANCE STARTED"
        );
    }

    // Ask DrumRecorder to pause/resume the current beat, then update the button
    // image to offer the next action.
    public void TogglePerformancePause()
    {
        // Ignore Pause while performance is inactive, moving views or missing
        // its recorder.
        if (
            !performanceActive ||
            sequenceRunning ||
            drumRecorder == null
        )
        {
            // Ignore Pause outside a ready performance or without the recorder.
            return;
        }

        // Let the recorder change the audio/pose pause state rather than
        // running another clock here.
        drumRecorder.TogglePerformancePause();

        // Prepare the current recorder pause flag for the button artwork
        // update. Read that flag after toggling so the icon offers the next
        // available action.
        bool isPaused =
            drumRecorder.IsPerformancePaused;

        // Show Play for a paused performance or Pause for a running one.
        UpdatePauseButtonIcon(isPaused);
    }

    // Accept one Delete/Re-record request during performance. Start the return
    // to the player's drum seat.
    public void DeleteAndRecordAgain()
    {
        // Ignore another delete request while inactive or already changing
        // views.
        if (
            !performanceActive ||
            sequenceRunning || transitionCoroutine != null
        )
        {
            // Ignore another delete request outside performance or while a
            // transition is running.
            return;
        }

        // Block other performance requests while this transition is underway.
        sequenceRunning = true;

        // Hide and disable the current performance controls before returning or
        // shutting down.
        performanceActive = false;
        // Start and remember the return-to-recording sequence so another cannot
        // overlap it.
        transitionCoroutine = StartCoroutine(DeleteAndReturnRoutine());
    }

    // Stop the beat, fade to black and remove the musician's sitting
    // movements/sticks. Return the player to recording with the take cleared.
    private IEnumerator DeleteAndReturnRoutine()
    {
        // Hide and disable performance controls during setup, transitions or
        // shutdown.
        SetPerformanceUI(false);

        // Stop the recorder's loop and audio before returning to recording.
        drumRecorder.StopPerformance();
        // Tell the recorder to hide and disable its UI and stop old playback
        // during this fade.
        drumRecorder.BeginPerformanceTransition();


        // Block UI pointer events through the blackout while placement changes.
        blackoutCanvasGroup.blocksRaycasts = true;

        // Wait for the blackout fade to finish before moving to the next
        // transition step.
        yield return FadeBlackout(
                blackoutCanvasGroup.alpha,
                1f,
                fadeToBlackDuration
            );

        // Release sitting bones and musician-only sticks before changing
        // performance state.
        if (selectedDrummer != null) selectedDrummer.Release();
        // Hide the chosen musician if their character still exists when
        // returning to record.
        if (selectedMusician != null)
            // Hide the performer only after its bones and visual sticks have
            // been released.
            selectedMusician.gameObject.SetActive(false);

        // Use the shared headset-aware placement helper for this viewing
        // position marker.
        MovePlayerToPoint(
            drumSeatTarget
        );

        // Only touch the optional manual movement component when it was
        // assigned.
        if (moveProvider != null)
            // Keep manual movement disabled while scripts control the player
            // viewing position.
            moveProvider.enabled = false;

        // Only switch sticks/menu rays when the mode controller is available.
        if (drumModeVisuals != null)
            // Show player sticks and leave menu pointing mode for sitting
            // drumming.
            drumModeVisuals.EnableDrumMode();

        // Clear the take, saved file and playback flags so another cycle can
        // start.
        drumRecorder.ResetAfterPerformanceDelete();

        // Update physics with the new transforms before the next interaction
        // frame.
        Physics.SyncTransforms();
        // Wait for the next game update so this sequence does not block the
        // rest of the game.
        yield return null;

        // Pause this sequence for the configured delay while Unity keeps
        // running other updates.
        yield return new WaitForSeconds(
            blackoutHoldDuration
        );

        // Wait for the blackout fade to finish before moving to the next
        // transition step.
        yield return FadeBlackout(
                1f,
                0f,
                fadeFromBlackDuration
            );

        // Release the blackout input blocker after the transition is finished.
        blackoutCanvasGroup.blocksRaycasts = false;

        // Hide and disable the current performance controls before returning or
        // shutting down.
        performanceActive = false;
        // Allow future performance controls now that the transition is
        // finished.
        sequenceRunning = false;
        // Forget the finished return transition so the next take can use a
        // fresh one.
        transitionCoroutine = null;

        // Log this state change for checking the experience sequence during
        // testing.
        Debug.Log(
            "RETURNED TO DRUM SEAT. PRESS A TO RECORD"
        );
    }

    // Show the watching controls and allow clicks when visible is true.
    // Otherwise hide them and stop them taking pointer input.
    private void SetPerformanceUI(bool visible)
    {
        // Show/hide the existing controls object if assigned, without replacing
        // its buttons.
        if (performanceControls != null) performanceControls.SetActive(visible);
        // Leave the button settings alone if their CanvasGroup is missing.
        if (performanceUIGroup == null) return;
        // Set canvas-group visibility to match the requested control
        // visibility.
        performanceUIGroup.alpha = visible ? 1f : 0f;
        // Enable clicks only while the performance controls should be visible.
        performanceUIGroup.interactable = visible;
        // Enable the same group's pointer blocking only while those controls
        // are visible.
        performanceUIGroup.blocksRaycasts = visible;
    }

    // Make the existing buttons and pointers work again every time a
    // performance begins. Keep them blocked if Help is already open.
    private void RestorePerformanceInteraction()
    {
        // Ensure the dedicated performance canvas exists before restoring
        // controls.
        PreparePerformanceUI();
        // Make the blackout fully see-through so the normal room view is
        // visible.
        blackoutCanvasGroup.alpha = 0f;
        // Release the blackout input blocker after the transition is finished.
        blackoutCanvasGroup.blocksRaycasts = false;
        // Keep the fade overlay itself from behaving like an interactive menu.
        blackoutCanvasGroup.interactable = false;
        // Switch back to controller/menu visuals if the scene mode switcher
        // exists.
        if (drumModeVisuals != null) drumModeVisuals.DisableDrumMode();
        // Stop restoring the performance menu if its canvas could not be found.
        if (performanceCanvas == null) return;
        // Activate the existing performance canvas again on each entry.
        performanceCanvas.gameObject.SetActive(true);
        // Restore its Canvas component as well as the GameObject active state.
        performanceCanvas.enabled = true;
        // Restore its XR canvas/pointer-checking component settings, which
        // other experience stages may have changed.
        QuestUI.ConfigureCanvas(performanceCanvas, xrOrigin.Camera, new Vector2(950f, 430f), 150);
        // Place controls ahead of the current watching view, steady for
        // controller aiming.
        QuestUI.PlaceInFront(performanceCanvas, xrOrigin.Camera, new Vector3(0f, 0f, 1.1f));
        // Visit the existing performance buttons, including at first hidden
        // ones.
        foreach (var button in performanceControls.GetComponentsInChildren<Button>(true))
        {
            // Make each button clickable again after earlier Help/transition
            // interaction changes.
            button.interactable = true;
            // Restore the target graphic's area that receives pointer clicks if
            // that graphic exists.
            if (button.targetGraphic != null) button.targetGraphic.raycastTarget = true;
        }
        // Show controls only if the recorder actually entered performance mode.
        SetPerformanceUI(performanceActive);
        // Find Help to ensure this restoration does not bypass an already-open
        // popup.
        var help = FindFirstObjectByType<HelpMenu>();
        // If Help is open, keep this canvas group hidden and non-interactive.
        if (help != null && help.IsOpen && performanceUIGroup != null)
        {
            // Hide performance controls behind Help rather than drawing them
            // through the popup.
            performanceUIGroup.alpha = 0f;
            // Keep their buttons disabled until Help closes.
            performanceUIGroup.interactable = false;
            // Keep their ray surfaces from intercepting Help interaction.
            performanceUIGroup.blocksRaycasts = false;
        }
    }

    // Stop any unfinished viewing position change and playback. Hide controls,
    // release the musician and remove any leftover black image/input block.
    private void OnDisable()
    {
        // Stop any in-progress transition if this sequence component is
        // disabled.
        if (transitionCoroutine != null) StopCoroutine(transitionCoroutine);
        // Forget the stopped transition when this component becomes inactive.
        transitionCoroutine = null;
        // Allow future performance controls now that the transition is
        // finished.
        sequenceRunning = false;
        // Hide and disable the current performance controls before returning or
        // shutting down.
        performanceActive = false;
        // Hide and disable performance controls during setup, transitions or
        // shutdown.
        SetPerformanceUI(false);
        // Release sitting bones and musician-only sticks before changing
        // performance state.
        if (selectedDrummer != null) selectedDrummer.Release();
        // Clear the recorder transition/playback state if a recorder is
        // available.
        if (drumRecorder != null) drumRecorder.CancelPerformanceTransition();
        // Clear the blackout image if it exists when this component becomes
        // inactive.
        if (blackoutCanvasGroup != null)
        // Remove a leftover blackout and its input block during sequence
        // cancellation.
        { blackoutCanvasGroup.alpha = 0f; blackoutCanvasGroup.blocksRaycasts = false; }
    }

    // Read the player's Mika/Ren choice. Show that musician and set up their
    // sitting body movements and visual sticks.
    private void PlaceSelectedMusician()
    {
        // Prepare the stored onboarding choice used for performer placement.
        // Read that choice directly from IntroSequence, preserving the player's
        // selection.
        string selectedName =
            introSequence.SelectedMusician;

        // Use Mika's placement, model correction and rig when she was selected.
        if (selectedName == "MIKA")
        {
            // Hide Ren during Mika's performance only if Ren's character is
            // assigned.
            if (ren != null)
                // Hide Ren so only the selected musician performs.
                ren.gameObject.SetActive(false);

            // Remember Mika's whole character object as the active performer.
            selectedMusician = mika;
            // Remember the Animator belonging to Mika for rig setup.
            selectedAnimator = mikaAnimator;

            // Place Mika using the saved model correction and her own
            // performance marker.
            PlaceMusician(
                mika,
                mikaRotationTarget,
                initialMikaLocalRotation,
                mikaPerformancePoint,
                false
            );
        }
        // Use Ren's own placement and rig when he was selected instead.
        else if (selectedName == "REN")
        {
            // Hide Mika during Ren's performance only if Mika's character is
            // assigned.
            if (mika != null)
                // Hide Mika so Ren is the only active performer.
                mika.gameObject.SetActive(false);

            // Remember Ren's whole character object for this performance.
            selectedMusician = ren;
            // Keep Ren's Animator as the selected rig lookup source.
            selectedAnimator = renAnimator;

            // Place Ren and request compensation for his visible model's offset
            // inside the whole character object.
            PlaceMusician(
                ren,
                renRotationTarget,
                initialRenLocalRotation,
                renPerformancePoint,
                true
            );
        }
        // Warn when the saved musician choice is neither Mika nor Ren.
        else
        {
            // Warn that the player has not chosen Mika or Ren for this
            // performance.
            Debug.LogWarning(
                "NO MUSICIAN HAS BEEN SELECTED"
            );
        }
        // Show the selected musician only if a valid character was found.
        if (selectedMusician != null)
        {
            // Pick the selected character's own rig so grip offsets and bones
            // stay independent.
            var rig = selectedName == "MIKA" ? mikaRig : renRig;
            // Use the selected Animator only if the rig did not already assign
            // one explicitly.
            if (rig.animator == null) rig.animator = selectedAnimator;
            // Reuse a sitting drummer already attached by an earlier
            // performance cycle.
            selectedDrummer = selectedMusician.GetComponent<ProceduralDrummer>();
            // Create the component only when missing so repeated performances
            // do not duplicate it.
            if (selectedDrummer == null) selectedDrummer = selectedMusician.gameObject.AddComponent<ProceduralDrummer>();
            // Validate and apply the sitting bones/sticks using the selected
            // character's rig settings.
            selectedDrummer.Seat(selectedMusician, rig);
        }
    }

    // Send the recorded hits and duration in seconds to the selected musician,
    // with the drum-marker list for their hand targets.
    public void PrepareDrumming(System.Collections.Generic.IReadOnlyList<DrumRecorder.DrumHit> hits, float duration)
    {
        // Forward this take's events, length and drum-surface mappings if a
        // drummer exists.
        if (selectedDrummer != null) selectedDrummer.PrepareRecording(hits, duration, drumTargets);
    }

    // Move the musician's arms to the pose for playbackTime. The recorder
    // supplies this time; this method does not run a second clock.
    public void EvaluateDrumming(float playbackTime)
    {
        // Forward the recorder's current loop time; this method does not
        // advance another clock.
        if (selectedDrummer != null) selectedDrummer.Evaluate(playbackTime);
    }

    // Place musician at performancePoint. Keep initialLocalRotation on
    // rotationTarget, the visible model. If alignModelToPoint is true, also
    // compensate for the model's position inside its whole object.
    private void PlaceMusician(
        Transform musician,
        Transform rotationTarget,
        Quaternion initialLocalRotation,
        Transform performancePoint,
        bool alignModelToPoint
    )
    {
        // Leave the musician in place if the musician or performance marker is
        // missing.
        if (
            musician == null ||
            performancePoint == null
        )
        {
            // Warn that the chosen musician or their performance position
            // marker is missing.
            Debug.LogWarning(
                "MUSICIAN OR PERFORMANCE POINT IS MISSING"
            );

            // Leave placement unchanged because the musician or destination
            // marker is missing.
            return;
        }

        // Activate this musician for the performance before configuring its
        // sitting pose.
        musician.gameObject.SetActive(true);

        // Set the whole character object to the marker's position in the room
        // and facing together.
        musician.SetPositionAndRotation(
            performancePoint.position,
            performancePoint.rotation
        );

        // Restore model corrections only if a separate rotation target exists.
        if (rotationTarget != null)
        {
            // Restore the model's saved local rotation, keeping its imported
            // turn adjustment for the imported model.
            rotationTarget.localRotation =
                initialLocalRotation;
        }

        // Compensate the visible-model offset only for the placement path that
        // requested it.
        if (alignModelToPoint && rotationTarget != null)
        {
            // Shift the whole character object by the amount needed to align
            // its child model with the marker.
            musician.position +=
                performancePoint.position -
                rotationTarget.position;
        }
    }

    // Put the headset at targetPoint and face the marker's forward direction.
    // Account for the camera's position inside XR Origin.
    private void MovePlayerToPoint(
        Transform targetPoint
    )
    {
        // Leave the player in place if the player group or target marker is
        // missing.
        if (
            xrOrigin == null ||
            targetPoint == null
        )
        {
            // Do not move the player without both the rig and viewing position
            // marker.
            return;
        }

        // Rotate XR Origin so the tracked camera faces the destination while
        // remaining upright.
        xrOrigin.MatchOriginUpCameraForward(
            Vector3.up,
            targetPoint.forward
        );

        // Move XR Origin by the amount needed to put its tracked camera on the
        // destination.
        xrOrigin.MoveCameraToWorldLocation(
            targetPoint.position
        );
    }

    // When isPaused is true, show Play to offer resume. Otherwise show Pause.
    private void UpdatePauseButtonIcon(
        bool isPaused
    )
    {
        // Check for a missing Pause image before trying to change its picture.
        if (pauseButtonImage == null)
            // Leave the Pause icon alone if its Image component is missing.
            return;

        // Offer Resume artwork only when paused and a Play icon has been
        // assigned.
        if (isPaused && playIcon != null)
        {
            // Assign that Play icon to the existing performance Pause/Resume
            // Image.
            pauseButtonImage.sprite =
                playIcon;
        }
        // Otherwise offer Pause when its artwork exists.
        else if (pauseIcon != null)
        {
            // Assign that Pause icon without replacing its button or listeners.
            pauseButtonImage.sprite =
                pauseIcon;
        }
    }

    // Fade the black image from startAlpha to endAlpha over duration seconds.
    // Visibility is 0 for clear and 1 for fully black.
    private IEnumerator FadeBlackout(
        float startAlpha,
        float endAlpha,
        float duration
    )
    {
        // Handle an instant fade without dividing by zero or waiting for
        // another frame.
        if (duration <= 0f)
        {
            // Set the black image to its requested end visibility for an
            // instant fade.
            blackoutCanvasGroup.alpha =
                endAlpha;

            // Finish this instant fade; the requested end visibility has
            // already been applied.
            yield break;
        }

        // Start measuring this fade from zero seconds since recording began.
        float elapsedTime = 0f;

        // Keep updating the fade until its configured duration has passed.
        while (elapsedTime < duration)
        {
            // Add this frame's game time so fade duration is measured in
            // seconds.
            elapsedTime += Time.deltaTime;

            // Calculate how far through the fade we are, from 0 at the start to
            // 1 at the end.
            float progress =
                Mathf.Clamp01(
                    elapsedTime / duration
                );

            // Apply the visibility between the fade's start and end for this
            // update.
            blackoutCanvasGroup.alpha =
                Mathf.Lerp(
                    startAlpha,
                    endAlpha,
                    progress
                );

            // Wait for the next game update so this sequence does not block the
            // rest of the game.
            yield return null;
        }

        // Finish exactly at the requested fade visibility after its time is
        // complete.
        blackoutCanvasGroup.alpha =
            endAlpha;
    }
}
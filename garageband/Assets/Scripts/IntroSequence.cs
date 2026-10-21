/*
 * Guides the player through the start of the experience in MusicRoom.
 * Ms Maya enters and speaks, the player follows her to Mika and Ren, and the
 * player chooses a musician. Both musicians leave. Maya then explains how
 * to record a beat. After her final words, the view fades to black and the
 * player moves automatically to the drum seat. No kit click is needed.
 * This is active game code and also runs in Editor Play Mode. CharacterMotion
 * moves the characters' arms/legs; this file decides where they travel.
 * QuestExperienceControls sends A presses here for dialogue.
 * PerformanceSequence
 * later reads the chosen musician. A Transform stores position, turn and size.
 * Scene markers are objects used as destinations. XR Origin groups the player
 * camera/controllers. A Canvas holds UI; a CanvasGroup controls fade visibility
 * and clicks. Coroutines let these sequences wait without stopping the game.
 * UI means the buttons and text the player sees and uses.
 * The Inspector is Unity's settings panel for assigning objects and values.
 */
// Use sequences that can wait while the game keeps running.
using System.Collections;
// Use the text labels shown in the experience menus.
using TMPro;
// Use Unity objects, positions and the methods Unity calls during the game.
using UnityEngine;
// Use XR Origin, used for the player rig and headset camera.
using Unity.XR.CoreUtils;
// Use Unity canvas graphics, buttons and scrolling controls.
using UnityEngine.UI;
// Use XR canvas raycasters and the headset UI input module.
using UnityEngine.XR.Interaction.Toolkit.UI;

// Guides the player through the start of the experience in MusicRoom.
public class IntroSequence : MonoBehaviour
{
    // Keep the existing button that begins onboarding.
    [Header("UI")]
    [SerializeField] private GameObject startExperienceButton;
    // Keep the existing conversation panel.
    [SerializeField] private GameObject dialoguePanel;
    // Keep the text showing the current spoken line.
    [SerializeField] private TMP_Text dialogueText;
    // Keep the text showing the current speaker.
    [SerializeField] private TMP_Text characterNameText;
    // Keep the existing Mika/Ren selection panel.
    [SerializeField] private GameObject musicianChoicePanel;
    // Keep the fade overlay and its UI input-blocking state.
    [SerializeField] private CanvasGroup blackoutCanvasGroup;
    // Keep the recording UI canvas, whose GameObject is controlled by
    // proximity.
    [SerializeField] private GameObject recordingCanvas;

    // Keep the existing opening dialogue lines for Ms Maya.
    [Header("Intro Dialogue")]
    [SerializeField]
    [TextArea(2, 5)]
    private string[] dialogues;

    // Keep the existing group conversation before musician selection.
    [Header("Musicians Dialogue")]
    [SerializeField]
    [TextArea(2, 5)]
    private string[] musiciansDialogues;

    // Keep the optional compatible walking animation clip.
    [Header("Standing and Walking Animation")]
    [SerializeField] private AnimationClip humanoidWalk;
    // Keep the optional compatible standing animation clip.
    [SerializeField] private AnimationClip humanoidIdle;

    // Give Maya, Mika and Ren their standing/walking body movements.
    // CharacterMotion handles their arms/legs; this script still decides where
    // they go.
    private void Awake()
    {
        // Give Ms Maya her own standing/walking movement with the Maya style.
        CharacterMotion.Attach(character, characterAnimator, humanoidWalk, humanoidIdle, CharacterMotion.StandingStyle.Maya);
        // Give Mika her own standing/walking movement with the Mika style.
        CharacterMotion.Attach(mika, mikaAnimator, humanoidWalk, humanoidIdle, CharacterMotion.StandingStyle.Mika);
        // Give Ren the default standing/walking style using the same optional
        // clips.
        CharacterMotion.Attach(ren, renAnimator, humanoidWalk, humanoidIdle);
    }

    // Keep Ms Maya's whole character object.
    [Header("Ms Maya")]
    [SerializeField] private Transform character;
    // Keep the scene marker Maya walks to before greeting the player.
    [SerializeField] private Transform greetingPoint;
    // Keep the intermediate marker on the route to the musicians.
    [SerializeField] private Transform musiciansWaypoint;
    // Keep Maya's destination marker beside the musicians.
    [SerializeField] private Transform musiciansPoint;
    // Keep Maya's marker before the final recording instructions.
    [SerializeField] private Transform mayaFinalPoint;
    // Keep the tracked camera transform for facing and attached to the headset
    // UI.
    [SerializeField] private Transform playerCamera;
    // Keep the Animator associated with Ms Maya's model.
    [SerializeField] private Animator characterAnimator;

    // Keep Mika's whole character object.
    [Header("Mika and Ren")]
    [SerializeField] private Transform mika;
    // Keep Ren's whole character object.
    [SerializeField] private Transform ren;
    // Keep Mika's model transform, including its imported turn adjustment for
    // the imported model.
    [SerializeField] private Transform mikaRotationTarget;
    // Keep Ren's model transform, including its imported turn adjustment for
    // the imported model.
    [SerializeField] private Transform renRotationTarget;
    // Keep the Animator associated with Mika's connected body parts.
    [SerializeField] private Animator mikaAnimator;
    // Keep the Animator associated with Ren's connected body parts.
    [SerializeField] private Animator renAnimator;
    // Keep the existing name label beside Mika.
    [SerializeField] private GameObject mikaNameLabel;
    // Keep the existing name label beside Ren.
    [SerializeField] private GameObject renNameLabel;
    // Keep the scene marker that sets the musicians' exit direction.
    [SerializeField] private Transform musiciansExitPoint;

    // Keep the pause in seconds before the musicians leave.
    [Header("Musician Exit Settings")]
    [SerializeField] private float pauseBeforeMusiciansExit = 1.2f;
    // Keep Mika's exit movement speed in metres per second.
    [SerializeField] private float mikaWalkSpeed = 1.3f;
    // Keep Ren's exit movement speed in metres per second.
    [SerializeField] private float renWalkSpeed = 1.15f;
    // Keep how far Mika travels before Ren begins following.
    [SerializeField] private float musicianFollowGap = 2f;
    // Keep how far each musician walks before being hidden.
    [SerializeField] private float disappearAfterDistance = 4f;
    // Keep the blend rate when turning the musician models.
    [SerializeField] private float musicianRotationSpeed = 8f;

    // Keep the player whole character object that follows Maya automatically.
    [Header("Player Automatic Movement")]
    [SerializeField] private Transform playerRig;
    // Keep the player object moved automatically while following Ms Maya.
    [SerializeField] private Transform playerStopPoint;
    // Keep the scene marker for the automatic drum-seat viewing position.
    [SerializeField] private Transform drumSeatTarget;
    // Keep the player XR Origin used for headset position and facing.
    [SerializeField] private XROrigin xrOrigin;
    // Keep the assigned manual movement component.
    [SerializeField] private Behaviour moveProvider;
    // Keep the script that switches player sticks and menu pointers.
    [SerializeField] private DrumModeVisuals drumModeVisuals;
    // Keep the allowed horizontal gap before the player follows Maya.
    [SerializeField] private float followDistance = 4f;
    // Keep the automatic player travel speed in metres per second.
    [SerializeField] private float playerFollowSpeed = 2f;

    // Keep the pause before Maya's entrance starts.
    [Header("Movement Settings")]
    [SerializeField] private float startDelay = 2f;
    // Keep Maya's movement speed in metres per second.
    [SerializeField] private float walkSpeed = 1.5f;
    // Keep Maya's facing blend rate.
    [SerializeField] private float rotationSpeed = 8f;
    // Keep how close Maya/player travel must get before finishing exactly on a
    // marker.
    [SerializeField] private float stoppingDistance = 0.1f;
    // Keep the pause at the intermediate route marker.
    [SerializeField] private float waypointPause = 0.3f;

    // Keep the pause before Maya takes her final greeting step.
    [Header("Initial Final Approach")]
    [SerializeField] private float pauseBeforeApproach = 0.5f;
    // Keep the distance of Maya's final step toward the player.
    [SerializeField] private float finalApproachDistance = 0.5f;

    // Keep the pause before Maya moves for her final dialogue.
    [Header("Final Maya Sequence")]
    [SerializeField] private float pauseBeforeMayaMoves = 0.02f;

    // Keep seconds spent fading to a fully black view.
    [Header("Blackout Transition")]
    [SerializeField] private float fadeToBlackDuration = 1f;
    // Keep seconds spent holding black during placement.
    [SerializeField] private float blackoutHoldDuration = 0.5f;
    // Keep seconds spent restoring the normal view.
    [SerializeField] private float fadeFromBlackDuration = 1f;

    // Expose the stored MIKA or REN choice used by PerformanceSequence.
    public string SelectedMusician { get; private set; }
    // Remember the player's MIKA/REN choice so PerformanceSequence can use it
    // later.
    public bool IsDialogueActive => dialogueIsActive;
    // Let the Quest A-button handler check whether dialogue is currently open.
    public bool IsAtDrumSeat { get; private set; }

    // Remember whether onboarding has begun, so Start cannot launch a second
    // entrance sequence.
    private bool hasStarted;
    // Remember whether a dialogue is open, so A advances speech instead of
    // starting recording.
    private bool dialogueIsActive;
    // Remember whether the musician choice has been accepted, so duplicate
    // clicks cannot launch another exit.
    private bool musicianHasBeenChosen;
    // Remember whether the final seat transition has started, so the last
    // dialogue cannot start it twice.
    private bool finalTransitionStarted;

    // Keep the current line index inside the active conversation.
    private int currentDialogueIndex;
    // Keep the numbered conversation stage used to select the next step.
    private int dialogueStage;

    // Remember which line of the current dialogue the player is reading.
    private string[] activeDialogues;
    // Remember the conversation stage: opening, musicians, or final recording
    // instructions.
    private Canvas conversationCanvas;

    // Keep the A-button hint that changes from Next to Close.
    private TMP_Text advanceLabel;
    // Keep the game-created canvas holding the Start menu.
    private Canvas startCanvas;

    // Arrange the existing dialogue and Mika/Ren choice panels for the headset.
    // Make the shared canvas only once.
    private void PrepareConversationCanvas()
    {
        // Avoid duplicating the conversation canvas or creating it without a
        // player view.
        if (conversationCanvas != null || playerCamera == null) return;
        // Read the Camera component from the known tracked view transform.
        var eye = playerCamera.GetComponent<Camera>();
        // Create the shared room canvas for dialogue and the musician-choice
        // panel.
        conversationCanvas = QuestUI.CreateCanvas("Conversation Canvas", eye, new Vector2(1100f, 650f), 120);
        // Set the position and size of Maya and the musicians' dialogue panel
        // inside its parent panel.
        QuestUI.Layout(dialoguePanel.GetComponent<RectTransform>(), conversationCanvas.transform,
            Vector2.zero, new Vector2(900f, 250f));
        // Set the position and size of the Mika/Ren choice panel inside its
        // parent panel.
        QuestUI.Layout(musicianChoicePanel.GetComponent<RectTransform>(), conversationCanvas.transform,
            Vector2.zero, new Vector2(950f, 430f));

        // Find even inactive existing dialogue buttons before placing their
        // artwork.
        foreach (var button in dialoguePanel.GetComponentsInChildren<Button>(true))
        {
            // Set the position and size of this menu button inside its parent
            // panel.
            QuestUI.Layout(button.GetComponent<RectTransform>(), dialoguePanel.transform,
                new Vector2(365f, -87f), new Vector2(60f, 60f));
            // Activate each existing dialogue button without replacing its
            // Inspector events.
            button.gameObject.SetActive(true);
        }
        // Set the position and size of the current dialogue text inside its
        // parent panel.
        QuestUI.Layout(dialogueText.rectTransform, dialoguePanel.transform, new Vector2(0f, 5f), new Vector2(820f, 125f));
        // Set the visible UI colour, including transparency where specified.
        dialogueText.color = QuestUI.Ink;
        // Choose whether this label fits its font size to the available
        // rectangle.
        dialogueText.enableAutoSizing = true;
        // Keep automatically fitted text from shrinking below this readable
        // minimum.
        dialogueText.fontSizeMin = 22f;
        // Cap automatic text growth so the label stays within its layout.
        dialogueText.fontSizeMax = 28f;
        // Show the speaker name only if its text object is assigned.
        if (characterNameText != null)
        {
            // Set the position and size of the speaker name inside its parent
            // panel.
            QuestUI.Layout(characterNameText.rectTransform, dialoguePanel.transform,
                new Vector2(0f, 94f), new Vector2(820f, 34f));
            // Set the visible UI colour, including transparency where
            // specified.
            characterNameText.color = QuestUI.Ink;
            // Set the label's text size in panel units to keep the message
            // readable.
            characterNameText.fontSize = 25f;
            // Align the text within its rectangle for this part of the menu.
            characterNameText.alignment = TextAlignmentOptions.Left;
        }
        // Show the player this menu text: "RIGHT CONTROLLER".
        QuestUI.Text("Dialogue Controller Hint", dialoguePanel.transform, "RIGHT CONTROLLER",
            new Vector2(-200f, -91f), new Vector2(400f, 34f), 20f, QuestUI.Ink);
        // Create the A Next/Close hint below the spoken dialogue.
        advanceLabel = QuestUI.Text("Dialogue A Hint", dialoguePanel.transform, "A  •  Next",
            new Vector2(225f, -91f), new Vector2(210f, 36f), 23f, QuestUI.Ink);
        // Draw this UI over room geometry using the shared overlay shader
        // setup.
        QuestUI.ApplyOverlay(dialoguePanel);
        // Draw this UI over room geometry using the shared overlay shader
        // setup.
        QuestUI.ApplyOverlay(musicianChoicePanel);
    }

    // Keep dialogue near the player's view. Keep the musician-choice menu
    // steady when the player aims at its buttons.
    private void PositionConversationCanvas()
    {
        // Create conversation UI if needed before trying to position or show
        // it.
        PrepareConversationCanvas();
        // Skip positioning until both the generated conversation canvas and
        // tracked view exist.
        if (conversationCanvas == null || playerCamera == null) return;
        // Check whether a dialogue is open before taking this branch.
        if (dialogueIsActive)
        {
            // Attach active dialogue to the headset so subtitles follow the
            // view.
            conversationCanvas.transform.SetParent(playerCamera, false);
            // Place the UI relative to its parent, such as the headset or
            // containing panel.
            conversationCanvas.transform.localPosition = new Vector3(0f, -.31f, .85f);
            // Set the local UI rotation without changing its parent's facing.
            conversationCanvas.transform.localRotation = Quaternion.identity;
            // Set local scale explicitly so inherited layout changes do not
            // stretch this UI.
            conversationCanvas.transform.localScale = Vector3.one * .001f;
        }
        // Attach the choice menu to the player group so it stays steady for
        // aiming.
        else
        {
            // Detach the choice menu from head motion under the player rig,
            // keeping its placement.
            conversationCanvas.transform.SetParent(xrOrigin != null ? xrOrigin.transform : null, true);
            // Place choices ahead of the current level view so ray targets stay
            // steady while aiming.
            QuestUI.PlaceInFront(conversationCanvas, playerCamera.GetComponent<Camera>(), new Vector3(0f, 0f, 1.1f));
        }
    }

    // Show the Start menu in front of the player using the existing Start
    // button.
    private void PrepareStartCanvas()
    {
        // Skip Start setup if its button/camera is missing or this menu was
        // already created.
        if (startExperienceButton == null || playerCamera == null || startCanvas != null) return;
        // Read the tracked view's Camera component for the Start canvas.
        var eye = playerCamera.GetComponent<Camera>();
        // Create the small attached to the headset Start canvas using the
        // shared UI helper.
        startCanvas = QuestUI.CreateCanvas("Start Experience Canvas", eye, new Vector2(700f, 300f), 120);
        // Keep Start attached to the headset while tracking establishes the
        // initial eye height.
        startCanvas.transform.SetParent(playerCamera, false);
        // Place the UI relative to its parent, such as the headset or
        // containing panel.
        startCanvas.transform.localPosition = new Vector3(0f, 0f, 1.1f);
        // Set the local UI rotation without changing its parent's facing.
        startCanvas.transform.localRotation = Quaternion.identity;
        // Set local scale explicitly so inherited layout changes do not stretch
        // this UI.
        startCanvas.transform.localScale = Vector3.one * .001f;
        // Add the dark Start-menu panel, separate from the Help background
        // behaviour.
        QuestUI.Background("Start Backdrop", startCanvas.transform, new Vector2(700f, 300f), new Color(.06f, .08f, .12f, .94f));
        // Show the player this menu text: "GARAGEBAND".
        QuestUI.Text("Start Title", startCanvas.transform, "GARAGEBAND", new Vector2(0f, 92f), new Vector2(620f, 50f), 36f, Color.white);
        // Set the position and size of the Start button inside its parent
        // panel.
        QuestUI.Layout(startExperienceButton.GetComponent<RectTransform>(), startCanvas.transform,
            Vector2.zero, new Vector2(480f, 90f));
        // Find its original Image so the artwork can retain its proportions.
        var image = startExperienceButton.GetComponent<Image>();
        // Preserve the Start picture's aspect ratio when that Image exists.
        if (image != null) image.preserveAspect = true;
        // Show the player this menu text: "Point your controller ray and press
        // the trigger".
        QuestUI.Text("Start Ray Hint", startCanvas.transform, "Point your controller ray and press the trigger",
            new Vector2(0f, -94f), new Vector2(650f, 45f), 22f, Color.white);
        // Draw this UI over room geometry using the shared overlay shader
        // setup.
        QuestUI.ApplyOverlay(startExperienceButton);
    }

    // Prepare the large black image that follows the headset during automatic
    // viewing position changes.
    private void PrepareBlackoutCanvas()
    {
        // Skip fade setup without both the existing blackout group and viewing
        // camera.
        if (blackoutCanvasGroup == null || playerCamera == null) return;
        // Create the large black image canvas attached to the player's view for
        // transitions.
        var fade = QuestUI.CreateCanvas("Headset Fade Canvas", playerCamera.GetComponent<Camera>(), new Vector2(4000f, 4000f), 1000);
        // Attach that fade canvas to the tracked view so it always covers the
        // headset image.
        fade.transform.SetParent(playerCamera, false);
        // Place the UI relative to its parent, such as the headset or
        // containing panel.
        fade.transform.localPosition = new Vector3(0f, 0f, .3f);
        // Set local scale explicitly so inherited layout changes do not stretch
        // this UI.
        fade.transform.localScale = Vector3.one * .001f;
        // Set the position and size of the black transition image inside its
        // parent panel.
        QuestUI.Layout(blackoutCanvasGroup.GetComponent<RectTransform>(), fade.transform,
            Vector2.zero, new Vector2(4000f, 4000f));
        // Draw this UI over room geometry using the shared overlay shader
        // setup.
        QuestUI.ApplyOverlay(blackoutCanvasGroup.gameObject);
    }

    // Hide later menus, find the player's camera and prepare Start, dialogue
    // and fade UI.
    private void Start()
    {
        // Hide the conversation panel at startup if it is assigned.
        if (dialoguePanel != null)
            // Hide the existing conversation panel.
            dialoguePanel.SetActive(false);

        // Hide the musician choices at startup if their panel is assigned.
        if (musicianChoicePanel != null)
            // Hide the existing Mika/Ren selection panel.
            musicianChoicePanel.SetActive(false);

        // Reset the blackout image at startup if its controls are assigned.
        if (blackoutCanvasGroup != null)
        {
            // Make the blackout fully see-through so the normal room view is
            // visible.
            blackoutCanvasGroup.alpha = 0f;
            // Keep the fade overlay itself from behaving like an interactive
            // menu.
            blackoutCanvasGroup.interactable = false;
            // Release the blackout input blocker after the transition is
            // finished.
            blackoutCanvasGroup.blocksRaycasts = false;
        }

        // Resolve the scene player rig only when the Inspector did not supply
        // it.
        if (xrOrigin == null)
            // Find the active scene XR Origin for headset-aware placement.
            xrOrigin = FindFirstObjectByType<XROrigin>();
        // Prefer the XR Origin's own camera when it is available.
        if (xrOrigin != null && xrOrigin.Camera != null)
            // Use that camera transform for character facing and attached to
            // the headset UI.
            playerCamera = xrOrigin.Camera.transform;
        // Otherwise use the scene main camera if no player-camera reference was
        // assigned.
        else if (playerCamera == null && Camera.main != null)
            // Keep that fallback camera's transform as the view reference.
            playerCamera = Camera.main.transform;

        // Keep the optional manual movement provider disabled during automatic
        // travel.
        if (moveProvider != null) moveProvider.enabled = false;
        // Create conversation UI if needed before trying to position or show
        // it.
        PrepareConversationCanvas();
        // Create the initial Start-menu presentation using the resolved player
        // view.
        PrepareStartCanvas();
        // Prepare the headset-covering transition overlay before onboarding can
        // finish.
        PrepareBlackoutCanvas();
    }

    // Accept Start once. Hide its menu and begin Maya's entrance.
    public void StartExperience()
    {
        // Check whether onboarding has begun before taking this branch.
        if (hasStarted)
            // Ignore another Start request because onboarding already began.
            return;

        // Remember that Start has been accepted, preventing another entrance
        // sequence.
        hasStarted = true;
        // Hide the generated Start canvas as soon as the Start request is
        // accepted.
        if (startCanvas != null) startCanvas.gameObject.SetActive(false);

        // Hide the Start button if it exists, because onboarding is now
        // beginning.
        if (startExperienceButton != null)
            // Hide the existing button that begins onboarding.
            startExperienceButton.SetActive(false);

        // Launch Maya's timed entrance without blocking the game update loop.
        StartCoroutine(BeginCharacterEntrance());
    }

    // Use one A press to show the next dialogue line. After the last line,
    // continue to the next part of the experience.
    public void NextDialogue()
    {
        // Only continue this branch when a dialogue is open is false.
        if (!dialogueIsActive)
            // Ignore the A press when no dialogue is open.
            return;

        // Move to the next line within this conversation, one press at a time.
        currentDialogueIndex++;

        // Show another line only if the dialogue list has a next line.
        if (
            activeDialogues != null &&
            currentDialogueIndex < activeDialogues.Length
        )
        {
            // Update the dialogue label with the newly selected line.
            dialogueText.text =
                activeDialogues[currentDialogueIndex];

            // Show the speaker associated with this dialogue stage and line.
            UpdateSpeakerName();
            // Refresh the A-button hint for the next line or the end of the
            // conversation.
            UpdateAdvanceLabel();
        }
        // Finish this conversation when there are no more lines to show.
        else
        {
            // Continue the experience flow after the current group has no more
            // lines.
            CompleteCurrentDialogue();
        }
    }

    // Pass the Mika button choice to the shared selection method.
    public void ChooseMika()
    {
        // Store Mika through the shared one-choice-only selection path.
        SelectMusician("MIKA");
    }

    // Pass the Ren button choice to the shared selection method.
    public void ChooseRen()
    {
        // Store Ren through the same selection path.
        SelectMusician("REN");
    }

    // Remember musicianName as MIKA or REN. Accept the choice only once, hide
    // choice UI and let the musicians leave.
    private void SelectMusician(string musicianName)
    {
        // Check whether the musician choice has been accepted before taking
        // this branch.
        if (musicianHasBeenChosen)
            // Keep the choice already accepted instead of starting the exit
            // sequence again.
            return;

        // Remember the musician choice so repeated button clicks do not replay
        // the exit.
        musicianHasBeenChosen = true;
        // Keep the exact MIKA or REN value that PerformanceSequence later
        // reads.
        SelectedMusician = musicianName;

        // Hide the choices after the player has picked Mika or Ren, if the
        // panel exists.
        if (musicianChoicePanel != null)
            // Hide the existing Mika/Ren selection panel.
            musicianChoicePanel.SetActive(false);

        // Only change Mika's optional name label if it exists.
        if (mikaNameLabel != null)
            // Hide Mika's name label once selection no longer needs it.
            mikaNameLabel.SetActive(false);

        // Only change Ren's optional name label if it exists.
        if (renNameLabel != null)
            // Hide Ren's name label once selection no longer needs it.
            renNameLabel.SetActive(false);

        // Log this state change for checking the experience sequence during
        // testing.
        Debug.Log(
            "Selected musician: " + SelectedMusician
        );

        // Start the musicians' ordered exit after the choice UI is hidden.
        StartCoroutine(MusiciansExitSequence());
    }

    // Walk Mika out first. Ren follows after a gap. Once both are hidden, start
    // Maya's final dialogue sequence.
    private IEnumerator MusiciansExitSequence()
    {
        // Stop the exit sequence if either musician or the exit marker is
        // missing.
        if (
            mika == null ||
            ren == null ||
            musiciansExitPoint == null
        )
        {
            // Warn that a musician or the exit marker is missing from the scene
            // setup.
            Debug.LogWarning(
                "Mika, Ren, or Musicians Exit Point is missing."
            );

            // End this sequence because the required setup or direction is
            // missing.
            yield break;
        }

        // Pause this sequence for the configured delay while Unity keeps
        // running other updates.
        yield return new WaitForSeconds(
            pauseBeforeMusiciansExit
        );

        // Wait for Mika and Ren to finish turning before they start walking
        // out.
        yield return StartCoroutine(
            TurnMusiciansTowardsExit()
        );

        // Save Mika's starting position so her exit distance can be measured
        // from here.
        Vector3 mikaStartPosition = mika.position;
        // Save Ren's own starting position for his delayed exit.
        Vector3 renStartPosition = ren.position;

        // Point from Mika's current place toward the exit marker.
        Vector3 mikaWalkDirection =
            musiciansExitPoint.position - mika.position;

        // Point from Ren's own place toward that same exit marker.
        Vector3 renWalkDirection =
            musiciansExitPoint.position - ren.position;

        // Remove height from Mika's exit direction so she walks across the
        // floor.
        mikaWalkDirection.y = 0f;
        // Remove height from Ren's exit direction too.
        renWalkDirection.y = 0f;

        // Stop if the exit marker is too close to give a useful walking
        // direction.
        if (
            mikaWalkDirection.sqrMagnitude <= 0.001f ||
            renWalkDirection.sqrMagnitude <= 0.001f
        )
        {
            // Warn that the exit marker is too close to choose a walking
            // direction.
            Debug.LogWarning(
                "Musicians Exit Point is too close to Mika or Ren."
            );

            // End this sequence because the required setup or direction is
            // missing.
            yield break;
        }

        // Give Mika's exit direction length one, so her speed sets how far she
        // moves.
        mikaWalkDirection.Normalize();
        // Give Ren's direction length one too, keeping his own speed
        // independent.
        renWalkDirection.Normalize();

        // Start with Mika still visible and walking out.
        bool mikaHasDisappeared = false;
        // Keep Ren waiting until Mika has moved far enough ahead.
        bool renStartedWalking = false;
        // Start with Ren still visible, ready for his delayed exit.
        bool renHasDisappeared = false;

        // Keep updating the exit until both musicians have disappeared.
        while (
            !mikaHasDisappeared ||
            !renHasDisappeared
        )
        {
            // Move and measure Mika only while she remains visible.
            if (!mikaHasDisappeared)
            {
                // Move Mika along her exit direction at her speed, scaled by
                // seconds since the last update.
                mika.position +=
                    mikaWalkDirection *
                    mikaWalkSpeed *
                    Time.deltaTime;

                // Measure Mika's distance from her saved exit start, not from
                // the exit marker.
                float mikaTravelledDistance =
                    Vector3.Distance(
                        mikaStartPosition,
                        mika.position
                    );

                // Let Ren start after Mika has walked far enough to leave a
                // gap.
                if (
                    !renStartedWalking &&
                    mikaTravelledDistance >= musicianFollowGap
                )
                {
                    // Release Ren's waiting state so his exit can begin.
                    renStartedWalking = true;
                }

                // Hide Mika once she has walked the chosen exit distance.
                if (
                    mikaTravelledDistance >=
                    disappearAfterDistance
                )
                {
                    // Remember that Mika's exit is finished so later frames
                    // stop moving her.
                    mikaHasDisappeared = true;

                    // Hide Mika only after she has walked the intended
                    // distance.
                    mika.gameObject.SetActive(false);

                    // Ensure Ren is not left waiting if Mika disappears before
                    // the gap was reached.
                    if (!renStartedWalking)
                    {
                        // Start Ren in that fallback case so the exit sequence
                        // can still complete.
                        renStartedWalking = true;
                    }
                }
            }

            // Move Ren only after his turn to leave has started and while he is
            // visible.
            if (
                renStartedWalking &&
                !renHasDisappeared
            )
            {
                // Move Ren along his own exit direction and speed for this
                // update.
                ren.position +=
                    renWalkDirection *
                    renWalkSpeed *
                    Time.deltaTime;

                // Measure how far Ren has travelled from his separate starting
                // point.
                float renTravelledDistance =
                    Vector3.Distance(
                        renStartPosition,
                        ren.position
                    );

                // Hide Ren once he has walked the chosen exit distance.
                if (
                    renTravelledDistance >=
                    disappearAfterDistance
                )
                {
                    // Mark Ren's exit complete so this loop can finish when
                    // both are gone.
                    renHasDisappeared = true;

                    // Hide Ren after his own distance threshold has been
                    // reached.
                    ren.gameObject.SetActive(false);
                }
            }

            // Wait for the next game update so this sequence does not block the
            // rest of the game.
            yield return null;
        }

        // Log this state change for checking the experience sequence during
        // testing.
        Debug.Log(
            "Mika and Ren have left the scene."
        );

        // Start Maya's final movement/dialogue only after both exit flags are
        // complete.
        StartCoroutine(MayaFinalSequence());
    }

    // Walk Maya to her final marker. Have her ask the player to record and save
    // a beat for the chosen musician.
    private IEnumerator MayaFinalSequence()
    {
        // Pause this sequence for the configured delay while Unity keeps
        // running other updates.
        yield return new WaitForSeconds(
            pauseBeforeMayaMoves
        );

        // Stop Maya's final sequence if either her whole character object or
        // final scene marker is missing.
        if (character == null || mayaFinalPoint == null)
        {
            // Warn that Maya or her final dialogue marker is missing.
            Debug.LogWarning(
                "Ms Maya or Maya Final Point is missing."
            );

            // End this sequence because the required setup or direction is
            // missing.
            yield break;
        }

        // Use Maya's final scene marker as the next walking destination.
        Vector3 destination = mayaFinalPoint.position;
        // Keep Maya at her current height so this route moves across the floor.
        destination.y = character.position.y;

        // Wait for Maya to face her final speaking position before walking
        // there.
        yield return StartCoroutine(
            TurnTowards(destination)
        );

        // Wait for Maya to reach her final speaking position.
        yield return StartCoroutine(
            MoveCharacter(destination)
        );

        // Use the player-facing step only if the tracked camera transform is
        // known.
        if (playerCamera != null)
        {
            // Wait for Maya to face the player before her final dialogue.
            yield return StartCoroutine(
                TurnTowards(playerCamera.position)
            );
        }

        // Get Mika or Ren's readable name for Maya's final recording
        // instructions.
        string musicianDisplayName =
            GetSelectedMusicianDisplayName();

        // Build Maya's final message asking the player to record and save a
        // beat.
        string finalDialogue =
            "Perfect, you've made your choice! " +
            "We still need a beat for the performance, " +
            "so please create and record one, and don't forget to save it. " +
            musicianDisplayName +
            " will perform it exactly as you played it!";

        // Put that one complete message into the dialogue list format used by
        // OpenDialogue.
        string[] finalDialogueLines =
        {
            finalDialogue
        };

        // Open this as stage two; finishing it will trigger the automatic
        // blackout/seat move.
        OpenDialogue(finalDialogueLines, 2);
    }

    // Return Mika or Ren for the final dialogue, or 'Your musician' if no
    // recognised choice is stored.
    private string GetSelectedMusicianDisplayName()
    {
        // Recognise the stored uppercase Mika choice.
        if (SelectedMusician == "MIKA")
            // Return the readable name Mika for the dialogue.
            return "Mika";

        // Recognise the stored uppercase Ren choice.
        if (SelectedMusician == "REN")
            // Return the readable name Ren for the dialogue.
            return "Ren";

        // Return a neutral fallback if a valid musician choice was not stored.
        return "Your musician";
    }

    // Turn Mika and Ren toward the exit before they walk. Keep each imported
    // model's existing turn adjustments.
    private IEnumerator TurnMusiciansTowardsExit()
    {
        // Turn Mika's visible model if assigned; otherwise turn her whole
        // object.
        Transform actualMikaRotationTarget =
            mikaRotationTarget != null
                ? mikaRotationTarget
                : mika;

        // Choose Ren's visible model or his whole object in the same way.
        Transform actualRenRotationTarget =
            renRotationTarget != null
                ? renRotationTarget
                : ren;

        // Find the direction from Mika to the exit before turning her.
        Vector3 mikaDirection =
            musiciansExitPoint.position - mika.position;

        // Find Ren's direction to the same exit.
        Vector3 renDirection =
            musiciansExitPoint.position - ren.position;

        // Remove height from Mika's desired facing so this is a horizontal
        // turn.
        mikaDirection.y = 0f;
        // Flatten Ren's facing for the same floor-level turn.
        renDirection.y = 0f;

        // Work out Mika's exit-facing turn while keeping her model's existing
        // adjustments.
        Quaternion mikaTargetRotation =
            CreateYawTargetRotation(
                actualMikaRotationTarget,
                mikaDirection
            );

        // Work out Ren's turn separately for his model.
        Quaternion renTargetRotation =
            CreateYawTargetRotation(
                actualRenRotationTarget,
                renDirection
            );

        // Start with Mika's turn unfinished so she receives gradual rotation
        // updates.
        bool mikaFinishedTurning = false;
        // Start with Ren's turn unfinished as well.
        bool renFinishedTurning = false;

        // Keep turning until both musicians face the exit direction.
        while (
            !mikaFinishedTurning ||
            !renFinishedTurning
        )
        {
            // Update Mika only while her turn is still unfinished.
            if (!mikaFinishedTurning)
            {
                // Turn Mika gradually toward her calculated exit-facing
                // rotation.
                actualMikaRotationTarget.rotation =
                    Quaternion.Slerp(
                        actualMikaRotationTarget.rotation,
                        mikaTargetRotation,
                        musicianRotationSpeed * Time.deltaTime
                    );

                // Finish Mika's turn when her angle is within one degree of the
                // target.
                if (
                    Quaternion.Angle(
                        actualMikaRotationTarget.rotation,
                        mikaTargetRotation
                    ) <= 1f
                )
                {
                    // Finish Mika's last small turn exactly on the target
                    // rotation.
                    actualMikaRotationTarget.rotation =
                        mikaTargetRotation;

                    // Mark Mika complete so the loop stops rotating her
                    // unnecessarily.
                    mikaFinishedTurning = true;
                }
            }

            // Update Ren only until his own turn finishes.
            if (!renFinishedTurning)
            {
                // Turn Ren gradually toward his calculated exit-facing
                // rotation.
                actualRenRotationTarget.rotation =
                    Quaternion.Slerp(
                        actualRenRotationTarget.rotation,
                        renTargetRotation,
                        musicianRotationSpeed * Time.deltaTime
                    );

                // Finish Ren's turn when his angle is within one degree of the
                // target.
                if (
                    Quaternion.Angle(
                        actualRenRotationTarget.rotation,
                        renTargetRotation
                    ) <= 1f
                )
                {
                    // Finish Ren exactly at his own target rotation.
                    actualRenRotationTarget.rotation =
                        renTargetRotation;

                    // Mark Ren complete; the loop ends only after both turns
                    // finish.
                    renFinishedTurning = true;
                }
            }

            // Wait for the next game update so this sequence does not block the
            // rest of the game.
            yield return null;
        }
    }

    // Return a level facing turn without losing the model's existing turn
    // adjustments. rotationTarget is the visible model to turn;
    // desiredDirection points toward its destination.
    private Quaternion CreateYawTargetRotation(
        Transform rotationTarget,
        Vector3 desiredDirection
    )
    {
        // Remove vertical direction before calculating a horizontal rotation
        // change.
        desiredDirection.y = 0f;

        // Skip target directions too short to define a reliable facing.
        if (desiredDirection.sqrMagnitude <= 0.001f)
            // Keep the existing model rotation because a usable horizontal turn
            // was not available.
            return rotationTarget.rotation;

        // Read the model's current forward arrow before comparing it with the
        // wanted direction.
        Vector3 currentForward =
            rotationTarget.forward;

        // Ignore up/down tilt while calculating this horizontal turn.
        currentForward.y = 0f;

        // Keep the old rotation if that flattened facing is also unusable.
        if (currentForward.sqrMagnitude <= 0.001f)
            // Keep the existing model rotation because a usable horizontal turn
            // was not available.
            return rotationTarget.rotation;

        // Set the current direction's length to one so only its facing matters.
        currentForward.Normalize();
        // Set the wanted direction's length to one for the same comparison.
        desiredDirection.Normalize();

        // Calculate the horizontal turn between the current and wanted facing
        // directions.
        Quaternion yawDifference =
            Quaternion.FromToRotation(
                currentForward,
                desiredDirection
            );

        // Apply that turn to the existing rotation, preserving the model's
        // other facing offsets.
        return yawDifference * rotationTarget.rotation;
    }

    // Let Maya enter, face the player and take the short greeting step. Then
    // open the first dialogue.
    private IEnumerator BeginCharacterEntrance()
    {
        // Pause this sequence for the configured delay while Unity keeps
        // running other updates.
        yield return new WaitForSeconds(startDelay);

        // Stop the entrance if Maya or the greeting marker was not assigned.
        if (character == null || greetingPoint == null)
        {
            // Warn that Maya or her greeting marker has not been assigned.
            Debug.LogWarning(
                "Character or Greeting Point has not been assigned."
            );

            // End this sequence because the required setup or direction is
            // missing.
            yield break;
        }

        // Use the greeting marker for Maya's first walking destination.
        Vector3 destination = greetingPoint.position;
        // Keep Maya at her current height so this route moves across the floor.
        destination.y = character.position.y;

        // Wait for Maya to face the greeting marker before walking towards it.
        yield return StartCoroutine(
            TurnTowards(destination)
        );

        // Wait for Maya to reach the greeting marker.
        yield return StartCoroutine(
            MoveCharacter(destination)
        );

        // Use the player-facing step only if the tracked camera transform is
        // known.
        if (playerCamera != null)
        {
            // Wait for Maya to face the player before approaching.
            yield return StartCoroutine(
                TurnTowards(playerCamera.position)
            );

            // Pause this sequence for the configured delay while Unity keeps
            // running other updates.
            yield return new WaitForSeconds(
                pauseBeforeApproach
            );

            // Find the floor direction from Maya toward the player for her
            // short final approach.
            Vector3 approachDirection =
                playerCamera.position - character.position;

            // Make the final greeting approach horizontal.
            approachDirection.y = 0f;

            // Only make an approach step if the horizontal player direction is
            // usable.
            if (approachDirection.sqrMagnitude > 0.001f)
            {
                // Choose a point one configured approach distance ahead of Maya
                // toward the player.
                Vector3 approachDestination =
                    character.position
                    + approachDirection.normalized
                    * finalApproachDistance;

                // Keep the endpoint at Maya's existing floor height.
                approachDestination.y =
                    character.position.y;

                // Wait for Maya to finish her short approach towards the
                // player.
                yield return StartCoroutine(
                    MoveCharacter(approachDestination)
                );

                // Wait for Maya to face the player again before she starts
                // speaking.
                yield return StartCoroutine(
                    TurnTowards(playerCamera.position)
                );
            }
        }

        // Start the existing onboarding dialogue as stage zero.
        OpenDialogue(dialogues, 0);
    }

    // Show dialogueLines from the first line. newDialogueStage identifies what
    // should happen when this conversation ends.
    private void OpenDialogue(
        string[] dialogueLines,
        int newDialogueStage
    )
    {
        // Do not open dialogue without its panel, text or at least one spoken
        // line.
        if (
            dialoguePanel == null ||
            dialogueText == null ||
            dialogueLines == null ||
            dialogueLines.Length == 0
        )
        {
            // Warn that the dialogue panel, text or spoken lines are missing.
            Debug.LogWarning(
                "Dialogue UI or dialogue lines have not been assigned."
            );

            // Do not open dialogue without its required panel, text and at
            // least one line.
            return;
        }

        // Remember which group of dialogue strings is now active.
        activeDialogues = dialogueLines;
        // Store the stage so completion can choose the correct next experience
        // step.
        dialogueStage = newDialogueStage;
        // Begin this dialogue group at its first line.
        currentDialogueIndex = 0;

        // Display the initial line from the newly active conversation.
        dialogueText.text =
            activeDialogues[currentDialogueIndex];

        // Show the speaker associated with this dialogue stage and line.
        UpdateSpeakerName();

        // Let A advance this dialogue instead of recording.
        dialogueIsActive = true;
        // Place the active dialogue or choice menu using its current
        // interaction style.
        PositionConversationCanvas();
        // Activate the existing conversation panel.
        dialoguePanel.SetActive(true);
        // Refresh the A-button hint for the next line or the end of the
        // conversation.
        UpdateAdvanceLabel();
        // Log this state change for checking the experience sequence during
        // testing.
        Debug.Log("Dialogue opened. Press A on the right controller to continue/close.");
    }

    // Tell the player whether A will show another line or close this
    // conversation.
    private void UpdateAdvanceLabel()
    {
        // Update the A-button hint if its text label exists.
        if (advanceLabel != null)
            // Tell the player A closes the final line; otherwise A shows the
            // next line.
            advanceLabel.text = currentDialogueIndex == activeDialogues.Length - 1 ? "A  •  Close" : "A  •  Next";
    }

    // Show Maya, Mika or Ren as the speaker for the current conversation line.
    private void UpdateSpeakerName()
    {
        // Skip speaker-label updates if that optional text reference is
        // missing.
        if (characterNameText == null)
            // Stop this update once the missing label or Maya-only stage has
            // been handled.
            return;

        // Use Maya as the speaker for both the opening and final
        // recording-instruction stages.
        if (dialogueStage == 0 || dialogueStage == 2)
        {
            // Display Ms Maya as the speaker for this dialogue line.
            characterNameText.text = "MS MAYA";
            // Stop this update once the missing label or Maya-only stage has
            // been handled.
            return;
        }

        // For the group conversation, choose the speaker by the existing line
        // order.
        switch (currentDialogueIndex)
        {
            // The first group-conversation line belongs to Maya.
            case 0:
                // Display Ms Maya as the speaker for this dialogue line.
                characterNameText.text = "MS MAYA";
                // Finish choosing this speaker's name before updating the
                // label.
                break;

            // The second group-conversation line belongs to Mika.
            case 1:
                // Display Mika as the speaker for this group-conversation line.
                characterNameText.text = "MIKA";
                // Finish choosing this speaker's name before updating the
                // label.
                break;

            // The third group-conversation line belongs to Ren.
            case 2:
                // Display Ren as the speaker for this group-conversation line.
                characterNameText.text = "REN";
                // Finish choosing this speaker's name before updating the
                // label.
                break;

            // Handle later/unrecognised line numbers without inventing a
            // speaker.
            default:
                // Clear the speaker label for dialogue line numbers without a
                // named speaker.
                characterNameText.text = "";
                // Finish choosing this speaker's name before updating the
                // label.
                break;
        }
    }

    // After the last line, walk to the musicians, show their choice menu, or
    // start the automatic move to the drum seat.
    private void CompleteCurrentDialogue()
    {
        // Stop dialogue input before choosing the next automatic experience
        // step.
        dialogueIsActive = false;

        // Hide the dialogue panel at the end of the conversation if it exists.
        if (dialoguePanel != null)
            // Hide the existing conversation panel.
            dialoguePanel.SetActive(false);

        // After the opening conversation, begin the automatic walk to the
        // musicians.
        if (dialogueStage == 0)
        {
            // Start the walk to Mika and Ren after Maya's opening dialogue
            // ends.
            StartCoroutine(
                WalkToMusiciansAutomatically()
            );
        }
        // After the group conversation, offer the two musician choices instead.
        else if (dialogueStage == 1)
        {
            // Place the active dialogue or choice menu using its current
            // interaction style.
            PositionConversationCanvas();
            // Show the musician choices after their conversation if the panel
            // exists.
            if (musicianChoicePanel != null)
                // Show the Mika/Ren choice panel after their conversation is
                // finished.
                musicianChoicePanel.SetActive(true);

            // Log this state change for checking the experience sequence during
            // testing.
            Debug.Log("Choose Mika or Ren.");
        }
        // Try the next condition only if the previous branch did not apply.
        else if (
            dialogueStage == 2 &&
            !finalTransitionStarted
        )
        {
            // Remember the final fade/seat transition so it starts only once.
            finalTransitionStarted = true;

            // Begin the blackout and automatic drum-seat move after the final
            // dialogue.
            StartCoroutine(
                FadeAndTeleportToDrumSeat()
            );
        }
    }

    // Fade to black before moving the player to the drum seat. Hide Maya, show
    // sticks/recording UI, then reveal the room again.
    private IEnumerator FadeAndTeleportToDrumSeat()
    {
        // Stop the seat transition if the blackout, player group or seat marker
        // is missing.
        if (
            blackoutCanvasGroup == null ||
            xrOrigin == null ||
            drumSeatTarget == null
        )
        {
            // Warn that the blackout image, player group or drum-seat marker is
            // missing.
            Debug.LogWarning(
                "Blackout Canvas Group, XR Origin, or Drum Seat Target is missing."
            );

            // End this sequence because the required setup or direction is
            // missing.
            yield break;
        }

        // Block UI pointer events through the blackout while placement changes.
        blackoutCanvasGroup.blocksRaycasts = true;

        // Wait until the view is fully black before moving the player to the
        // seat.
        yield return StartCoroutine(
            FadeBlackout(
                blackoutCanvasGroup.alpha,
                1f,
                fadeToBlackDuration
            )
        );

        // Hide Maya during the blackout if her character object exists.
        if (character != null)
            // Hide Maya while the view is fully black so her disappearance is
            // not visible.
            character.gameObject.SetActive(false);

        // Face the player's tracked camera along the drum-seat marker's forward
        // arrow.
        xrOrigin.MatchOriginUpCameraForward(
            Vector3.up,
            drumSeatTarget.forward
        );

        // Move XR Origin enough to put the camera at the drum-seat marker.
        xrOrigin.MoveCameraToWorldLocation(
            drumSeatTarget.position
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

        // Only change recording UI when its canvas reference is available.
        if (recordingCanvas != null)
            // Activate the recording UI GameObject; proximity will keep
            // checking its range.
            recordingCanvas.SetActive(true);

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

        // Wait for the black image to fade away and reveal the drum-seat view.
        yield return StartCoroutine(
            FadeBlackout(
                1f,
                0f,
                fadeFromBlackDuration
            )
        );

        // Release the blackout input blocker after the transition is finished.
        blackoutCanvasGroup.blocksRaycasts = false;

        // Remember that seating is complete so Quest A can control recording.
        IsAtDrumSeat = true;
        // Log this state change for checking the experience sequence during
        // testing.
        Debug.Log(
            "Player moved automatically to the drum seat. Ms Maya is hidden."
        );
    }

    // Change the black image gradually. startAlpha and endAlpha are its
    // starting/ending visibility, from 0 clear to 1 black. duration is how many
    // seconds the fade takes.
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
            // Finish at the exact requested visibility, including instant
            // fades.
            blackoutCanvasGroup.alpha = endAlpha;
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

            // Find the fade progress between 0 at its start and 1 at its end.
            float progress = Mathf.Clamp01(
                elapsedTime / duration
            );

            // Set the black image's visibility between its start/end values
            // using that progress.
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

        // Finish at the exact requested visibility, including instant fades.
        blackoutCanvasGroup.alpha = endAlpha;
    }

    // Move Maya through the existing route markers while the player follows.
    // Finish at the musician conversation positions.
    private IEnumerator WalkToMusiciansAutomatically()
    {
        // Only start the route when Maya, the player and all required markers
        // exist.
        if (
            character == null ||
            musiciansWaypoint == null ||
            musiciansPoint == null ||
            playerRig == null ||
            playerStopPoint == null
        )
        {
            // Warn that a character or route reference needed for this walk is
            // missing.
            Debug.LogWarning(
                "A character or movement reference is missing."
            );

            // End this sequence because the required setup or direction is
            // missing.
            yield break;
        }

        // Only touch the optional manual movement component when it was
        // assigned.
        if (moveProvider != null)
            // Keep manual movement disabled while scripts control the player
            // viewing position.
            moveProvider.enabled = false;

        // Read the existing intermediate marker as Maya's first route
        // destination.
        Vector3 waypointDestination =
            musiciansWaypoint.position;

        // Keep the waypoint at Maya's existing height.
        waypointDestination.y =
            character.position.y;

        // Wait for Maya to face the first section of the walking route.
        yield return StartCoroutine(
            TurnTowards(waypointDestination)
        );

        // Move Maya and the player along the first section, then wait for them.
        yield return StartCoroutine(
            MoveCharacterAndPlayer(
                waypointDestination
            )
        );

        // Pause this sequence for the configured delay while Unity keeps
        // running other updates.
        yield return new WaitForSeconds(
            waypointPause
        );

        // Read the existing musicians marker as her final route destination.
        Vector3 finalDestination =
            musiciansPoint.position;

        // Keep this route endpoint at Maya's current height so she stays on the
        // floor.
        finalDestination.y =
            character.position.y;

        // Wait for Maya to face the final section of the route.
        yield return StartCoroutine(
            TurnTowards(finalDestination)
        );

        // Move Maya and the player along the final section, then wait for them.
        yield return StartCoroutine(
            MoveCharacterAndPlayer(
                finalDestination
            )
        );

        // Wait for the player to reach the conversation stop marker.
        yield return StartCoroutine(
            MovePlayerToStopPoint()
        );

        // Use the player-facing step only if the tracked camera transform is
        // known.
        if (playerCamera != null)
        {
            // Wait for Maya to face the player before the musician
            // conversation.
            yield return StartCoroutine(
                TurnTowards(playerCamera.position)
            );
        }

        // Only touch the optional manual movement component when it was
        // assigned.
        if (moveProvider != null)
            // Keep manual movement disabled while scripts control the player
            // viewing position.
            moveProvider.enabled = false;

        // Open the existing musician conversation as stage one after everyone
        // is positioned.
        OpenDialogue(musiciansDialogues, 1);
    }

    // Walk Maya toward destination in the room. Update the player's following
    // position as she moves.
    private IEnumerator MoveCharacterAndPlayer(
        Vector3 destination
    )
    {
        // Keep moving Maya and the player until Maya is close enough to the
        // target.
        while (
            Vector3.Distance(
                character.position,
                destination
            ) > stoppingDistance
        )
        {
            // Move Maya one frame toward the destination without stepping past
            // it.
            character.position = Vector3.MoveTowards(
                character.position,
                destination,
                walkSpeed * Time.deltaTime
            );

            // Bring the player along only if Maya is beyond the allowed
            // following gap.
            MovePlayerBehindCharacter();

            // Wait for the next game update so this sequence does not block the
            // rest of the game.
            yield return null;
        }

        // Finish exactly on the destination so small stopping-tolerance errors
        // do not accumulate.
        character.position = destination;
    }

    // Move the player closer only when Maya is farther away than the allowed
    // following gap.
    private void MovePlayerBehindCharacter()
    {
        // Skip following when either the player whole character object or Maya
        // is missing.
        if (playerRig == null || character == null)
            // Leave the player where they are if following is unavailable or
            // the gap is already small enough.
            return;

        // Read Maya's room position before measuring the floor gap to the
        // player.
        Vector3 flatCharacterPosition =
            character.position;

        // Read the player object's room position for the other end of that gap.
        Vector3 flatPlayerPosition =
            playerRig.position;

        // Ignore Maya's height when comparing floor positions.
        flatCharacterPosition.y = 0f;
        // Ignore the player's height in that comparison as well.
        flatPlayerPosition.y = 0f;

        // Measure the horizontal player-to-Maya gap before deciding whether to
        // follow.
        float currentDistance = Vector3.Distance(
            flatPlayerPosition,
            flatCharacterPosition
        );

        // Do not move the player if the configured following distance is
        // already satisfied.
        if (currentDistance <= followDistance)
            // Leave the player where they are if following is unavailable or
            // the gap is already small enough.
            return;

        // Choose a following point behind Maya at the configured gap distance.
        Vector3 targetPosition =
            character.position
            - character.forward * followDistance;

        // Keep the player object's height unchanged while following her.
        targetPosition.y = playerRig.position.y;

        // Move the player gradually toward that following point at the
        // configured speed.
        playerRig.position = Vector3.MoveTowards(
            playerRig.position,
            targetPosition,
            playerFollowSpeed * Time.deltaTime
        );
    }

    // Move the player to the existing conversation stop marker without changing
    // the player object's height.
    private IEnumerator MovePlayerToStopPoint()
    {
        // Read the existing player stop marker as the end of automatic
        // following.
        Vector3 destination =
            playerStopPoint.position;

        // Keep the player's current height while moving to the stop marker.
        destination.y =
            playerRig.position.y;

        // Keep moving the player until they are close enough to the stop
        // marker.
        while (
            Vector3.Distance(
                playerRig.position,
                destination
            ) > stoppingDistance
        )
        {
            // Move the player gradually toward that stop point without stepping
            // past it.
            playerRig.position = Vector3.MoveTowards(
                playerRig.position,
                destination,
                playerFollowSpeed * Time.deltaTime
            );

            // Wait for the next game update so this sequence does not block the
            // rest of the game.
            yield return null;
        }

        // Finish exactly at the player's stop destination after the smooth
        // travel.
        playerRig.position = destination;
    }

    // Walk Maya toward destination at her configured speed. CharacterMotion
    // responds to that travel with body movement.
    private IEnumerator MoveCharacter(
        Vector3 destination
    )
    {
        // Keep Maya walking until she is close enough to her target marker.
        while (
            Vector3.Distance(
                character.position,
                destination
            ) > stoppingDistance
        )
        {
            // Move Maya one frame toward the destination without stepping past
            // it.
            character.position = Vector3.MoveTowards(
                character.position,
                destination,
                walkSpeed * Time.deltaTime
            );

            // Wait for the next game update so this sequence does not block the
            // rest of the game.
            yield return null;
        }

        // Finish exactly on the destination so small stopping-tolerance errors
        // do not accumulate.
        character.position = destination;
    }

    // Turn Maya toward targetPosition, a point in the room. Keep the turn level
    // rather than tilting her toward the player's head height.
    private IEnumerator TurnTowards(
        Vector3 targetPosition
    )
    {
        // Point from Maya's current position toward the place she should face.
        Vector3 direction =
            targetPosition - character.position;

        // Remove height difference so this facing turn only rotates around room
        // up.
        direction.y = 0f;

        // Skip a turn toward an almost-identical horizontal position.
        if (direction.sqrMagnitude <= 0.001f)
            // End this sequence because the required setup or direction is
            // missing.
            yield break;

        // Make the turn that faces along that floor direction.
        Quaternion targetRotation =
            Quaternion.LookRotation(
                direction.normalized
            );

        // Keep turning Maya until she is within one degree of the target
        // direction.
        while (
            Quaternion.Angle(
                character.rotation,
                targetRotation
            ) > 1f
        )
        {
            // Turn Maya a little closer to that facing during this update.
            character.rotation = Quaternion.Slerp(
                character.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );

            // Wait for the next game update so this sequence does not block the
            // rest of the game.
            yield return null;
        }

        // Finish exactly at the target after the gradual turn is within
        // tolerance.
        character.rotation = targetRotation;
    }

}

using System.Collections;
using TMPro;
using UnityEngine;
using Unity.XR.CoreUtils;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

public class IntroSequence : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject startExperienceButton;
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private TMP_Text characterNameText;
    [SerializeField] private GameObject musicianChoicePanel;
    [SerializeField] private CanvasGroup blackoutCanvasGroup;
    [SerializeField] private GameObject recordingCanvas;

    [Header("Intro Dialogue")]
    [SerializeField]
    [TextArea(2, 5)]
    private string[] dialogues;

    [Header("Musicians Dialogue")]
    [SerializeField]
    [TextArea(2, 5)]
    private string[] musiciansDialogues;

    [Header("Ms Maya")]
    [SerializeField] private Transform character;
    [SerializeField] private Transform greetingPoint;
    [SerializeField] private Transform musiciansWaypoint;
    [SerializeField] private Transform musiciansPoint;
    [SerializeField] private Transform mayaFinalPoint;
    [SerializeField] private Transform playerCamera;
    [SerializeField] private Animator characterAnimator;

    [Header("Mika and Ren")]
    [SerializeField] private Transform mika;
    [SerializeField] private Transform ren;
    [SerializeField] private Transform mikaRotationTarget;
    [SerializeField] private Transform renRotationTarget;
    [SerializeField] private Animator mikaAnimator;
    [SerializeField] private Animator renAnimator;
    [SerializeField] private GameObject mikaNameLabel;
    [SerializeField] private GameObject renNameLabel;
    [SerializeField] private Transform musiciansExitPoint;

    [Header("Musician Exit Settings")]
    [SerializeField] private float pauseBeforeMusiciansExit = 1.2f;
    [SerializeField] private float mikaWalkSpeed = 1.3f;
    [SerializeField] private float renWalkSpeed = 1.15f;
    [SerializeField] private float musicianFollowGap = 2f;
    [SerializeField] private float disappearAfterDistance = 4f;
    [SerializeField] private float musicianRotationSpeed = 8f;

    [Header("Player Automatic Movement")]
    [SerializeField] private Transform playerRig;
    [SerializeField] private Transform playerStopPoint;
    [SerializeField] private Transform drumSeatTarget;
    [SerializeField] private XROrigin xrOrigin;
    [SerializeField] private Behaviour moveProvider;
    [SerializeField] private DrumModeVisuals drumModeVisuals;
    [SerializeField] private float followDistance = 4f;
    [SerializeField] private float playerFollowSpeed = 2f;

    [Header("Movement Settings")]
    [SerializeField] private float startDelay = 2f;
    [SerializeField] private float walkSpeed = 1.5f;
    [SerializeField] private float rotationSpeed = 8f;
    [SerializeField] private float stoppingDistance = 0.1f;
    [SerializeField] private float waypointPause = 0.3f;

    [Header("Initial Final Approach")]
    [SerializeField] private float pauseBeforeApproach = 0.5f;
    [SerializeField] private float finalApproachDistance = 0.5f;

    [Header("Final Maya Sequence")]
    [SerializeField] private float pauseBeforeMayaMoves = 0.02f;

    [Header("Blackout Transition")]
    [SerializeField] private float fadeToBlackDuration = 1f;
    [SerializeField] private float blackoutHoldDuration = 0.5f;
    [SerializeField] private float fadeFromBlackDuration = 1f;

    public string SelectedMusician { get; private set; }
    public bool IsDialogueActive => dialogueIsActive;
    public bool IsAtDrumSeat { get; private set; }

    private bool hasStarted;
    private bool dialogueIsActive;
    private bool musicianHasBeenChosen;
    private bool finalTransitionStarted;

    private int currentDialogueIndex;
    private int dialogueStage;

    private string[] activeDialogues;
    private Canvas conversationCanvas;

    private TMP_Text advanceLabel;
    private Canvas startCanvas;

    private void PrepareConversationCanvas()
    {
        if (conversationCanvas != null || playerCamera == null) return;
        var eye = playerCamera.GetComponent<Camera>();
        conversationCanvas = QuestUI.CreateCanvas("Conversation Canvas", eye, new Vector2(1100f, 650f), 120);
        QuestUI.Layout(dialoguePanel.GetComponent<RectTransform>(), conversationCanvas.transform,
            Vector2.zero, new Vector2(900f, 250f));
        QuestUI.Layout(musicianChoicePanel.GetComponent<RectTransform>(), conversationCanvas.transform,
            Vector2.zero, new Vector2(950f, 430f));

        // Keep the original Unity button artwork, aligned with the dialogue footer.
        foreach (var button in dialoguePanel.GetComponentsInChildren<Button>(true))
        {
            QuestUI.Layout(button.GetComponent<RectTransform>(), dialoguePanel.transform,
                new Vector2(365f, -87f), new Vector2(60f, 60f));
            button.gameObject.SetActive(true);
        }
        QuestUI.Layout(dialogueText.rectTransform, dialoguePanel.transform, new Vector2(0f, 5f), new Vector2(820f, 125f));
        dialogueText.color = QuestUI.Ink;
        dialogueText.enableAutoSizing = true;
        dialogueText.fontSizeMin = 22f;
        dialogueText.fontSizeMax = 28f;
        if (characterNameText != null)
        {
            QuestUI.Layout(characterNameText.rectTransform, dialoguePanel.transform,
                new Vector2(0f, 94f), new Vector2(820f, 34f));
            characterNameText.color = QuestUI.Ink;
            characterNameText.fontSize = 25f;
            characterNameText.alignment = TextAlignmentOptions.Left;
        }
        QuestUI.Text("Dialogue Controller Hint", dialoguePanel.transform, "RIGHT CONTROLLER",
            new Vector2(-200f, -91f), new Vector2(400f, 34f), 20f, QuestUI.Ink);
        advanceLabel = QuestUI.Text("Dialogue A Hint", dialoguePanel.transform, "A  •  Next",
            new Vector2(225f, -91f), new Vector2(210f, 36f), 23f, QuestUI.Ink);
        QuestUI.ApplyOverlay(dialoguePanel);
        QuestUI.ApplyOverlay(musicianChoicePanel);
    }

    private void PositionConversationCanvas()
    {
        PrepareConversationCanvas();
        if (conversationCanvas == null || playerCamera == null) return;
        if (dialogueIsActive)
        {
            // Subtitle-sized UI below the face, close to the eyes and rendered over geometry.
            conversationCanvas.transform.SetParent(playerCamera, false);
            conversationCanvas.transform.localPosition = new Vector3(0f, -.31f, .85f);
            conversationCanvas.transform.localRotation = Quaternion.identity;
            conversationCanvas.transform.localScale = Vector3.one * .001f;
        }
        else
        {
            conversationCanvas.transform.SetParent(xrOrigin != null ? xrOrigin.transform : null, true);
            QuestUI.PlaceInFront(conversationCanvas, playerCamera.GetComponent<Camera>(), new Vector3(0f, 0f, 1.1f));
        }
    }

    private void PrepareStartCanvas()
    {
        if (startExperienceButton == null || playerCamera == null || startCanvas != null) return;
        var eye = playerCamera.GetComponent<Camera>();
        startCanvas = QuestUI.CreateCanvas("Start Experience Canvas", eye, new Vector2(700f, 300f), 120);
        // Tracking can establish eye height after Start; keep this menu eye-relative.
        startCanvas.transform.SetParent(playerCamera, false);
        startCanvas.transform.localPosition = new Vector3(0f, 0f, 1.1f);
        startCanvas.transform.localRotation = Quaternion.identity;
        startCanvas.transform.localScale = Vector3.one * .001f;
        QuestUI.Background("Start Backdrop", startCanvas.transform, new Vector2(700f, 300f), new Color(.06f, .08f, .12f, .94f));
        QuestUI.Text("Start Title", startCanvas.transform, "GARAGEBAND", new Vector2(0f, 92f), new Vector2(620f, 50f), 36f, Color.white);
        QuestUI.Layout(startExperienceButton.GetComponent<RectTransform>(), startCanvas.transform,
            Vector2.zero, new Vector2(480f, 90f));
        var image = startExperienceButton.GetComponent<Image>();
        if (image != null) image.preserveAspect = true;
        QuestUI.Text("Start Ray Hint", startCanvas.transform, "Point your controller ray and press the trigger",
            new Vector2(0f, -94f), new Vector2(650f, 45f), 22f, Color.white);
        QuestUI.ApplyOverlay(startExperienceButton);
    }

    private void PrepareBlackoutCanvas()
    {
        if (blackoutCanvasGroup == null || playerCamera == null) return;
        var fade = QuestUI.CreateCanvas("Headset Fade Canvas", playerCamera.GetComponent<Camera>(), new Vector2(4000f, 4000f), 1000);
        fade.transform.SetParent(playerCamera, false);
        fade.transform.localPosition = new Vector3(0f, 0f, .3f);
        fade.transform.localScale = Vector3.one * .001f;
        QuestUI.Layout(blackoutCanvasGroup.GetComponent<RectTransform>(), fade.transform,
            Vector2.zero, new Vector2(4000f, 4000f));
        QuestUI.ApplyOverlay(blackoutCanvasGroup.gameObject);
    }

    private void Start()
    {
        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);

        if (musicianChoicePanel != null)
            musicianChoicePanel.SetActive(false);

        if (blackoutCanvasGroup != null)
        {
            blackoutCanvasGroup.alpha = 0f;
            blackoutCanvasGroup.interactable = false;
            blackoutCanvasGroup.blocksRaycasts = false;
        }

        if (xrOrigin == null)
            xrOrigin = FindFirstObjectByType<XROrigin>();
        if (xrOrigin != null && xrOrigin.Camera != null)
            playerCamera = xrOrigin.Camera.transform;
        else if (playerCamera == null && Camera.main != null)
            playerCamera = Camera.main.transform;

        if (moveProvider != null) moveProvider.enabled = false;
        PrepareConversationCanvas();
        PrepareStartCanvas();
        PrepareBlackoutCanvas();
    }

    public void StartExperience()
    {
        if (hasStarted)
            return;

        hasStarted = true;
        if (startCanvas != null) startCanvas.gameObject.SetActive(false);

        if (startExperienceButton != null)
            startExperienceButton.SetActive(false);

        StartCoroutine(BeginCharacterEntrance());
    }

    public void NextDialogue()
    {
        if (!dialogueIsActive)
            return;

        currentDialogueIndex++;

        if (
            activeDialogues != null &&
            currentDialogueIndex < activeDialogues.Length
        )
        {
            dialogueText.text =
                activeDialogues[currentDialogueIndex];

            UpdateSpeakerName();
            UpdateAdvanceLabel();
        }
        else
        {
            CompleteCurrentDialogue();
        }
    }

    public void ChooseMika()
    {
        SelectMusician("MIKA");
    }

    public void ChooseRen()
    {
        SelectMusician("REN");
    }

    private void SelectMusician(string musicianName)
    {
        if (musicianHasBeenChosen)
            return;

        musicianHasBeenChosen = true;
        SelectedMusician = musicianName;

        if (musicianChoicePanel != null)
            musicianChoicePanel.SetActive(false);

        if (mikaNameLabel != null)
            mikaNameLabel.SetActive(false);

        if (renNameLabel != null)
            renNameLabel.SetActive(false);

        Debug.Log(
            "Selected musician: " + SelectedMusician
        );

        StartCoroutine(MusiciansExitSequence());
    }

    private IEnumerator MusiciansExitSequence()
    {
        if (
            mika == null ||
            ren == null ||
            musiciansExitPoint == null
        )
        {
            Debug.LogWarning(
                "Mika, Ren, or Musicians Exit Point is missing."
            );

            yield break;
        }

        yield return new WaitForSeconds(
            pauseBeforeMusiciansExit
        );

        yield return StartCoroutine(
            TurnMusiciansTowardsExit()
        );

        Vector3 mikaStartPosition = mika.position;
        Vector3 renStartPosition = ren.position;

        Vector3 mikaWalkDirection =
            musiciansExitPoint.position - mika.position;

        Vector3 renWalkDirection =
            musiciansExitPoint.position - ren.position;

        mikaWalkDirection.y = 0f;
        renWalkDirection.y = 0f;

        if (
            mikaWalkDirection.sqrMagnitude <= 0.001f ||
            renWalkDirection.sqrMagnitude <= 0.001f
        )
        {
            Debug.LogWarning(
                "Musicians Exit Point is too close to Mika or Ren."
            );

            yield break;
        }

        mikaWalkDirection.Normalize();
        renWalkDirection.Normalize();

        bool mikaHasDisappeared = false;
        bool renStartedWalking = false;
        bool renHasDisappeared = false;

        SetMusicianWalking(mikaAnimator, true);
        SetMusicianWalking(renAnimator, false);

        while (
            !mikaHasDisappeared ||
            !renHasDisappeared
        )
        {
            if (!mikaHasDisappeared)
            {
                mika.position +=
                    mikaWalkDirection *
                    mikaWalkSpeed *
                    Time.deltaTime;

                float mikaTravelledDistance =
                    Vector3.Distance(
                        mikaStartPosition,
                        mika.position
                    );

                if (
                    !renStartedWalking &&
                    mikaTravelledDistance >= musicianFollowGap
                )
                {
                    renStartedWalking = true;

                    SetMusicianWalking(
                        renAnimator,
                        true
                    );
                }

                if (
                    mikaTravelledDistance >=
                    disappearAfterDistance
                )
                {
                    mikaHasDisappeared = true;

                    SetMusicianWalking(
                        mikaAnimator,
                        false
                    );

                    mika.gameObject.SetActive(false);

                    if (!renStartedWalking)
                    {
                        renStartedWalking = true;

                        SetMusicianWalking(
                            renAnimator,
                            true
                        );
                    }
                }
            }

            if (
                renStartedWalking &&
                !renHasDisappeared
            )
            {
                ren.position +=
                    renWalkDirection *
                    renWalkSpeed *
                    Time.deltaTime;

                float renTravelledDistance =
                    Vector3.Distance(
                        renStartPosition,
                        ren.position
                    );

                if (
                    renTravelledDistance >=
                    disappearAfterDistance
                )
                {
                    renHasDisappeared = true;

                    SetMusicianWalking(
                        renAnimator,
                        false
                    );

                    ren.gameObject.SetActive(false);
                }
            }

            yield return null;
        }

        Debug.Log(
            "Mika and Ren have left the scene."
        );

        StartCoroutine(MayaFinalSequence());
    }

    private IEnumerator MayaFinalSequence()
    {
        yield return new WaitForSeconds(
            pauseBeforeMayaMoves
        );

        if (character == null || mayaFinalPoint == null)
        {
            Debug.LogWarning(
                "Ms Maya or Maya Final Point is missing."
            );

            yield break;
        }

        Vector3 destination = mayaFinalPoint.position;
        destination.y = character.position.y;

        yield return StartCoroutine(
            TurnTowards(destination)
        );

        SetWalking(true);

        yield return StartCoroutine(
            MoveCharacter(destination)
        );

        SetWalking(false);

        if (playerCamera != null)
        {
            yield return StartCoroutine(
                TurnTowards(playerCamera.position)
            );
        }

        string musicianDisplayName =
            GetSelectedMusicianDisplayName();

        string finalDialogue =
            "Perfect, you've made your choice! " +
            "We still need a beat for the performance, " +
            "so please create and record one, and don't forget to save it. " +
            musicianDisplayName +
            " will perform it exactly as you played it!";

        string[] finalDialogueLines =
        {
            finalDialogue
        };

        OpenDialogue(finalDialogueLines, 2);
    }

    private string GetSelectedMusicianDisplayName()
    {
        if (SelectedMusician == "MIKA")
            return "Mika";

        if (SelectedMusician == "REN")
            return "Ren";

        return "Your musician";
    }

    private IEnumerator TurnMusiciansTowardsExit()
    {
        Transform actualMikaRotationTarget =
            mikaRotationTarget != null
                ? mikaRotationTarget
                : mika;

        Transform actualRenRotationTarget =
            renRotationTarget != null
                ? renRotationTarget
                : ren;

        Vector3 mikaDirection =
            musiciansExitPoint.position - mika.position;

        Vector3 renDirection =
            musiciansExitPoint.position - ren.position;

        mikaDirection.y = 0f;
        renDirection.y = 0f;

        Quaternion mikaTargetRotation =
            CreateYawTargetRotation(
                actualMikaRotationTarget,
                mikaDirection
            );

        Quaternion renTargetRotation =
            CreateYawTargetRotation(
                actualRenRotationTarget,
                renDirection
            );

        bool mikaFinishedTurning = false;
        bool renFinishedTurning = false;

        while (
            !mikaFinishedTurning ||
            !renFinishedTurning
        )
        {
            if (!mikaFinishedTurning)
            {
                actualMikaRotationTarget.rotation =
                    Quaternion.Slerp(
                        actualMikaRotationTarget.rotation,
                        mikaTargetRotation,
                        musicianRotationSpeed * Time.deltaTime
                    );

                if (
                    Quaternion.Angle(
                        actualMikaRotationTarget.rotation,
                        mikaTargetRotation
                    ) <= 1f
                )
                {
                    actualMikaRotationTarget.rotation =
                        mikaTargetRotation;

                    mikaFinishedTurning = true;
                }
            }

            if (!renFinishedTurning)
            {
                actualRenRotationTarget.rotation =
                    Quaternion.Slerp(
                        actualRenRotationTarget.rotation,
                        renTargetRotation,
                        musicianRotationSpeed * Time.deltaTime
                    );

                if (
                    Quaternion.Angle(
                        actualRenRotationTarget.rotation,
                        renTargetRotation
                    ) <= 1f
                )
                {
                    actualRenRotationTarget.rotation =
                        renTargetRotation;

                    renFinishedTurning = true;
                }
            }

            yield return null;
        }
    }

    private Quaternion CreateYawTargetRotation(
        Transform rotationTarget,
        Vector3 desiredDirection
    )
    {
        desiredDirection.y = 0f;

        if (desiredDirection.sqrMagnitude <= 0.001f)
            return rotationTarget.rotation;

        Vector3 currentForward =
            rotationTarget.forward;

        currentForward.y = 0f;

        if (currentForward.sqrMagnitude <= 0.001f)
            return rotationTarget.rotation;

        currentForward.Normalize();
        desiredDirection.Normalize();

        Quaternion yawDifference =
            Quaternion.FromToRotation(
                currentForward,
                desiredDirection
            );

        return yawDifference * rotationTarget.rotation;
    }

    private void SetMusicianWalking(
        Animator musicianAnimator,
        bool isWalking
    )
    {
        if (musicianAnimator != null)
        {
            musicianAnimator.SetBool(
                "IsWalking",
                isWalking
            );
        }
    }

    private IEnumerator BeginCharacterEntrance()
    {
        yield return new WaitForSeconds(startDelay);

        if (character == null || greetingPoint == null)
        {
            Debug.LogWarning(
                "Character or Greeting Point has not been assigned."
            );

            yield break;
        }

        Vector3 destination = greetingPoint.position;
        destination.y = character.position.y;

        yield return StartCoroutine(
            TurnTowards(destination)
        );

        SetWalking(true);

        yield return StartCoroutine(
            MoveCharacter(destination)
        );

        SetWalking(false);

        if (playerCamera != null)
        {
            yield return StartCoroutine(
                TurnTowards(playerCamera.position)
            );

            yield return new WaitForSeconds(
                pauseBeforeApproach
            );

            Vector3 approachDirection =
                playerCamera.position - character.position;

            approachDirection.y = 0f;

            if (approachDirection.sqrMagnitude > 0.001f)
            {
                Vector3 approachDestination =
                    character.position
                    + approachDirection.normalized
                    * finalApproachDistance;

                approachDestination.y =
                    character.position.y;

                SetWalking(true);

                yield return StartCoroutine(
                    MoveCharacter(approachDestination)
                );

                SetWalking(false);

                yield return StartCoroutine(
                    TurnTowards(playerCamera.position)
                );
            }
        }

        OpenDialogue(dialogues, 0);
    }

    private void OpenDialogue(
        string[] dialogueLines,
        int newDialogueStage
    )
    {
        if (
            dialoguePanel == null ||
            dialogueText == null ||
            dialogueLines == null ||
            dialogueLines.Length == 0
        )
        {
            Debug.LogWarning(
                "Dialogue UI or dialogue lines have not been assigned."
            );

            return;
        }

        activeDialogues = dialogueLines;
        dialogueStage = newDialogueStage;
        currentDialogueIndex = 0;

        dialogueText.text =
            activeDialogues[currentDialogueIndex];

        UpdateSpeakerName();

        dialogueIsActive = true;
        PositionConversationCanvas();
        dialoguePanel.SetActive(true);
        UpdateAdvanceLabel();
        Debug.Log("Dialogue opened. Press A on the right controller to continue/close.");
    }

    private void UpdateAdvanceLabel()
    {
        if (advanceLabel != null)
            advanceLabel.text = currentDialogueIndex == activeDialogues.Length - 1 ? "A  •  Close" : "A  •  Next";
    }

    private void UpdateSpeakerName()
    {
        if (characterNameText == null)
            return;

        if (dialogueStage == 0 || dialogueStage == 2)
        {
            characterNameText.text = "MS MAYA";
            return;
        }

        switch (currentDialogueIndex)
        {
            case 0:
                characterNameText.text = "MS MAYA";
                break;

            case 1:
                characterNameText.text = "MIKA";
                break;

            case 2:
                characterNameText.text = "REN";
                break;

            default:
                characterNameText.text = "";
                break;
        }
    }

    private void CompleteCurrentDialogue()
    {
        dialogueIsActive = false;

        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);

        if (dialogueStage == 0)
        {
            StartCoroutine(
                WalkToMusiciansAutomatically()
            );
        }
        else if (dialogueStage == 1)
        {
            PositionConversationCanvas();
            if (musicianChoicePanel != null)
                musicianChoicePanel.SetActive(true);

            Debug.Log("Choose Mika or Ren.");
        }
        else if (
            dialogueStage == 2 &&
            !finalTransitionStarted
        )
        {
            finalTransitionStarted = true;

            StartCoroutine(
                FadeAndTeleportToDrumSeat()
            );
        }
    }

    private IEnumerator FadeAndTeleportToDrumSeat()
    {
        if (
            blackoutCanvasGroup == null ||
            xrOrigin == null ||
            drumSeatTarget == null
        )
        {
            Debug.LogWarning(
                "Blackout Canvas Group, XR Origin, or Drum Seat Target is missing."
            );

            yield break;
        }

        blackoutCanvasGroup.blocksRaycasts = true;

        yield return StartCoroutine(
            FadeBlackout(
                blackoutCanvasGroup.alpha,
                1f,
                fadeToBlackDuration
            )
        );

        // Screen is fully black, so Maya can disappear invisibly.
        if (character != null)
            character.gameObject.SetActive(false);

        xrOrigin.MatchOriginUpCameraForward(
            Vector3.up,
            drumSeatTarget.forward
        );

        xrOrigin.MoveCameraToWorldLocation(
            drumSeatTarget.position
        );

        if (moveProvider != null)
            moveProvider.enabled = false;

        if (drumModeVisuals != null)
            drumModeVisuals.EnableDrumMode();

        if (recordingCanvas != null)
            recordingCanvas.SetActive(true);

        Physics.SyncTransforms();

        yield return null;

        yield return new WaitForSeconds(
            blackoutHoldDuration
        );

        yield return StartCoroutine(
            FadeBlackout(
                1f,
                0f,
                fadeFromBlackDuration
            )
        );

        blackoutCanvasGroup.blocksRaycasts = false;

        IsAtDrumSeat = true;
        Debug.Log(
            "Player moved automatically to the drum seat. Ms Maya is hidden."
        );
    }

    private IEnumerator FadeBlackout(
        float startAlpha,
        float endAlpha,
        float duration
    )
    {
        if (duration <= 0f)
        {
            blackoutCanvasGroup.alpha = endAlpha;
            yield break;
        }

        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;

            float progress = Mathf.Clamp01(
                elapsedTime / duration
            );

            blackoutCanvasGroup.alpha =
                Mathf.Lerp(
                    startAlpha,
                    endAlpha,
                    progress
                );

            yield return null;
        }

        blackoutCanvasGroup.alpha = endAlpha;
    }

    private IEnumerator WalkToMusiciansAutomatically()
    {
        if (
            character == null ||
            musiciansWaypoint == null ||
            musiciansPoint == null ||
            playerRig == null ||
            playerStopPoint == null
        )
        {
            Debug.LogWarning(
                "A character or movement reference is missing."
            );

            yield break;
        }

        if (moveProvider != null)
            moveProvider.enabled = false;

        Vector3 waypointDestination =
            musiciansWaypoint.position;

        waypointDestination.y =
            character.position.y;

        yield return StartCoroutine(
            TurnTowards(waypointDestination)
        );

        SetWalking(true);

        yield return StartCoroutine(
            MoveCharacterAndPlayer(
                waypointDestination
            )
        );

        SetWalking(false);

        yield return new WaitForSeconds(
            waypointPause
        );

        Vector3 finalDestination =
            musiciansPoint.position;

        finalDestination.y =
            character.position.y;

        yield return StartCoroutine(
            TurnTowards(finalDestination)
        );

        SetWalking(true);

        yield return StartCoroutine(
            MoveCharacterAndPlayer(
                finalDestination
            )
        );

        SetWalking(false);

        yield return StartCoroutine(
            MovePlayerToStopPoint()
        );

        if (playerCamera != null)
        {
            yield return StartCoroutine(
                TurnTowards(playerCamera.position)
            );
        }

        if (moveProvider != null)
            moveProvider.enabled = false;

        OpenDialogue(musiciansDialogues, 1);
    }

    private IEnumerator MoveCharacterAndPlayer(
        Vector3 destination
    )
    {
        while (
            Vector3.Distance(
                character.position,
                destination
            ) > stoppingDistance
        )
        {
            character.position = Vector3.MoveTowards(
                character.position,
                destination,
                walkSpeed * Time.deltaTime
            );

            MovePlayerBehindCharacter();

            yield return null;
        }

        character.position = destination;
    }

    private void MovePlayerBehindCharacter()
    {
        if (playerRig == null || character == null)
            return;

        Vector3 flatCharacterPosition =
            character.position;

        Vector3 flatPlayerPosition =
            playerRig.position;

        flatCharacterPosition.y = 0f;
        flatPlayerPosition.y = 0f;

        float currentDistance = Vector3.Distance(
            flatPlayerPosition,
            flatCharacterPosition
        );

        if (currentDistance <= followDistance)
            return;

        Vector3 targetPosition =
            character.position
            - character.forward * followDistance;

        targetPosition.y = playerRig.position.y;

        playerRig.position = Vector3.MoveTowards(
            playerRig.position,
            targetPosition,
            playerFollowSpeed * Time.deltaTime
        );
    }

    private IEnumerator MovePlayerToStopPoint()
    {
        Vector3 destination =
            playerStopPoint.position;

        destination.y =
            playerRig.position.y;

        while (
            Vector3.Distance(
                playerRig.position,
                destination
            ) > stoppingDistance
        )
        {
            playerRig.position = Vector3.MoveTowards(
                playerRig.position,
                destination,
                playerFollowSpeed * Time.deltaTime
            );

            yield return null;
        }

        playerRig.position = destination;
    }

    private IEnumerator MoveCharacter(
        Vector3 destination
    )
    {
        while (
            Vector3.Distance(
                character.position,
                destination
            ) > stoppingDistance
        )
        {
            character.position = Vector3.MoveTowards(
                character.position,
                destination,
                walkSpeed * Time.deltaTime
            );

            yield return null;
        }

        character.position = destination;
    }

    private IEnumerator TurnTowards(
        Vector3 targetPosition
    )
    {
        Vector3 direction =
            targetPosition - character.position;

        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.001f)
            yield break;

        Quaternion targetRotation =
            Quaternion.LookRotation(
                direction.normalized
            );

        while (
            Quaternion.Angle(
                character.rotation,
                targetRotation
            ) > 1f
        )
        {
            character.rotation = Quaternion.Slerp(
                character.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );

            yield return null;
        }

        character.rotation = targetRotation;
    }

    private void SetWalking(bool isWalking)
    {
        if (characterAnimator != null)
        {
            characterAnimator.SetBool(
                "IsWalking",
                isWalking
            );
        }
    }
}

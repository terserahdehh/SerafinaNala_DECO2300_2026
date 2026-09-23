using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.XR.CoreUtils;

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

    private bool hasStarted;
    private bool dialogueIsActive;
    private bool previousXButtonState;
    private bool musicianHasBeenChosen;
    private bool finalTransitionStarted;

    private int currentDialogueIndex;
    private int dialogueStage;

    private string[] activeDialogues;

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
    }

    private void Update()
    {
        if (!dialogueIsActive)
            return;

        bool keyboardXPressed =
            Keyboard.current != null &&
            Keyboard.current.xKey.wasPressedThisFrame;

        UnityEngine.XR.InputDevice leftController =
            UnityEngine.XR.InputDevices.GetDeviceAtXRNode(
                UnityEngine.XR.XRNode.LeftHand
            );

        bool currentXButtonState = false;

        leftController.TryGetFeatureValue(
            UnityEngine.XR.CommonUsages.primaryButton,
            out currentXButtonState
        );

        bool questXPressed =
            currentXButtonState &&
            !previousXButtonState;

        if (keyboardXPressed || questXPressed)
            NextDialogue();

        previousXButtonState = currentXButtonState;
    }

    public void StartExperience()
    {
        if (hasStarted)
            return;

        hasStarted = true;

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

        dialoguePanel.SetActive(true);
        dialogueIsActive = true;
        previousXButtonState = false;
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
        previousXButtonState = false;

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
            moveProvider.enabled = true;

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
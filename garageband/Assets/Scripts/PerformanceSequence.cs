using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Unity.XR.CoreUtils;

public class PerformanceSequence : MonoBehaviour
{
    [Header("Main References")]
    [SerializeField] private IntroSequence introSequence;
    [SerializeField] private DrumRecorder drumRecorder;
    [SerializeField] private DrumModeVisuals drumModeVisuals;

    [Header("Player")]
    [SerializeField] private XROrigin xrOrigin;
    [SerializeField] private Transform performanceViewPoint;
    [SerializeField] private Transform drumSeatTarget;
    [SerializeField] private Behaviour moveProvider;

    [Header("Transition")]
    [SerializeField] private CanvasGroup blackoutCanvasGroup;
    [SerializeField] private float delayAfterSave = 0.5f;
    [SerializeField] private float fadeToBlackDuration = 1f;
    [SerializeField] private float blackoutHoldDuration = 0.5f;
    [SerializeField] private float fadeFromBlackDuration = 1f;
    [SerializeField] private float delayBeforePerformance = 1f;

    [Header("Performance UI")]
    [SerializeField] private GameObject performanceControls;
    [SerializeField] private Image pauseButtonImage;
    [SerializeField] private Sprite pauseIcon;
    [SerializeField] private Sprite playIcon;

    [Header("Mika")]
    [SerializeField] private Transform mika;
    [SerializeField] private Transform mikaRotationTarget;
    [SerializeField] private Transform mikaPerformancePoint;
    [SerializeField] private Animator mikaAnimator;

    [Header("Ren")]
    [SerializeField] private Transform ren;
    [SerializeField] private Transform renRotationTarget;
    [SerializeField] private Transform renPerformancePoint;
    [SerializeField] private Animator renAnimator;

    [Header("Procedural Drumming")]
    [SerializeField] private ProceduralDrummer.Rig mikaRig = new ProceduralDrummer.Rig();
    [SerializeField] private ProceduralDrummer.Rig renRig = new ProceduralDrummer.Rig();
    [SerializeField] private ProceduralDrummer.DrumTarget[] drumTargets;
    private ProceduralDrummer selectedDrummer;

    private Quaternion initialMikaLocalRotation;
    private Quaternion initialRenLocalRotation;

    private Animator selectedAnimator;
    private Transform selectedMusician;

    private Canvas performanceCanvas;

    private bool sequenceRunning;
    private bool performanceActive;

    private void Start()
    {
        if (performanceControls != null)
            performanceControls.SetActive(false);

        if (xrOrigin == null)
            xrOrigin = FindFirstObjectByType<XROrigin>();

        if (mikaRotationTarget != null)
        {
            initialMikaLocalRotation =
                mikaRotationTarget.localRotation;
        }

        if (renRotationTarget != null)
        {
            initialRenLocalRotation =
                renRotationTarget.localRotation;
        }

        PreparePerformanceUI();
        UpdatePauseButtonIcon(false);
    }

    private void PreparePerformanceUI()
    {
        if (performanceControls == null || xrOrigin == null || xrOrigin.Camera == null) return;
        performanceCanvas = QuestUI.CreateCanvas("Performance Controls Canvas", xrOrigin.Camera,
            new Vector2(950f, 430f), 150);
        performanceCanvas.transform.SetParent(xrOrigin.transform, false);
        QuestUI.Layout(performanceControls.GetComponent<RectTransform>(), performanceCanvas.transform,
            Vector2.zero, new Vector2(950f, 430f));
        var buttons = performanceControls.GetComponentsInChildren<Button>(true);
        foreach (var button in buttons)
        {
            bool pause = pauseButtonImage != null && button.gameObject == pauseButtonImage.gameObject;
            var image = button.GetComponent<Image>();
            Vector2 size = pause ? new Vector2(150f, 150f) : new Vector2(310f, 150f);
            if (image != null && image.sprite != null)
            {
                float aspect = image.sprite.rect.width / image.sprite.rect.height;
                size = new Vector2(size.x, size.x / aspect);
                if (size.y > 150f) size *= 150f / size.y;
                image.preserveAspect = true;
            }
            QuestUI.Layout(button.GetComponent<RectTransform>(), performanceControls.transform,
                new Vector2(pause ? 230f : -230f, 0f), size);
            button.navigation = new Navigation { mode = Navigation.Mode.None };
        }
        QuestUI.ApplyOverlay(performanceControls);
    }

    public void BeginPerformance()
    {
        if (sequenceRunning || performanceActive)
            return;

        if (
            introSequence == null ||
            drumRecorder == null ||
            blackoutCanvasGroup == null ||
            xrOrigin == null ||
            performanceViewPoint == null
        )
        {
            Debug.LogWarning(
                "PERFORMANCE SEQUENCE REFERENCES ARE MISSING"
            );

            return;
        }

        sequenceRunning = true;

        drumRecorder.BeginPerformanceTransition();

        StartCoroutine(
            BeginPerformanceRoutine()
        );
    }

    private IEnumerator BeginPerformanceRoutine()
    {
        if (performanceControls != null)
            performanceControls.SetActive(false);

        yield return new WaitForSeconds(
            delayAfterSave
        );

        blackoutCanvasGroup.blocksRaycasts = true;

        yield return FadeBlackout(
                blackoutCanvasGroup.alpha,
                1f,
                fadeToBlackDuration
            );

        if (drumModeVisuals != null)
            drumModeVisuals.DisableDrumMode();

        PlaceSelectedMusician();

        MovePlayerToPoint(
            performanceViewPoint
        );

        Physics.SyncTransforms();
        yield return null;

        yield return new WaitForSeconds(
            blackoutHoldDuration + delayBeforePerformance
        );

        yield return FadeBlackout(
                1f,
                0f,
                fadeFromBlackDuration
            );

        blackoutCanvasGroup.blocksRaycasts = false;

        performanceActive = true;
        sequenceRunning = false;

        if (performanceCanvas != null)
            QuestUI.PlaceInFront(performanceCanvas, xrOrigin.Camera, new Vector3(0f, 0f, 1.1f));
        if (performanceControls != null)
            performanceControls.SetActive(true);

        drumRecorder.StartLoopedPerformance();

        UpdatePauseButtonIcon(false);

        Debug.Log(
            "MUSICIAN PERFORMANCE STARTED"
        );
    }

    public void TogglePerformancePause()
    {
        if (
            !performanceActive ||
            drumRecorder == null
        )
        {
            return;
        }

        drumRecorder.TogglePerformancePause();

        bool isPaused =
            drumRecorder.IsPerformancePaused;

        UpdatePauseButtonIcon(isPaused);
    }

    public void DeleteAndRecordAgain()
    {
        if (
            !performanceActive ||
            sequenceRunning
        )
        {
            return;
        }

        sequenceRunning = true;

        StartCoroutine(
            DeleteAndReturnRoutine()
        );
    }

    private IEnumerator DeleteAndReturnRoutine()
    {
        if (performanceControls != null)
            performanceControls.SetActive(false);

        drumRecorder.StopPerformance();
        drumRecorder.BeginPerformanceTransition();


        blackoutCanvasGroup.blocksRaycasts = true;

        yield return FadeBlackout(
                blackoutCanvasGroup.alpha,
                1f,
                fadeToBlackDuration
            );

        if (selectedDrummer != null) selectedDrummer.Release();
        if (selectedMusician != null)
            selectedMusician.gameObject.SetActive(false);

        MovePlayerToPoint(
            drumSeatTarget
        );

        if (moveProvider != null)
            moveProvider.enabled = false;

        if (drumModeVisuals != null)
            drumModeVisuals.EnableDrumMode();

        drumRecorder.ResetAfterPerformanceDelete();

        Physics.SyncTransforms();
        yield return null;

        yield return new WaitForSeconds(
            blackoutHoldDuration
        );

        yield return FadeBlackout(
                1f,
                0f,
                fadeFromBlackDuration
            );

        blackoutCanvasGroup.blocksRaycasts = false;

        performanceActive = false;
        sequenceRunning = false;

        Debug.Log(
            "RETURNED TO DRUM SEAT. PRESS A TO RECORD"
        );
    }

    private void PlaceSelectedMusician()
    {
        string selectedName =
            introSequence.SelectedMusician;

        if (selectedName == "MIKA")
        {
            if (ren != null)
                ren.gameObject.SetActive(false);

            selectedMusician = mika;
            selectedAnimator = mikaAnimator;

            PlaceMusician(
                mika,
                mikaRotationTarget,
                initialMikaLocalRotation,
                mikaPerformancePoint,
                false
            );
        }
        else if (selectedName == "REN")
        {
            if (mika != null)
                mika.gameObject.SetActive(false);

            selectedMusician = ren;
            selectedAnimator = renAnimator;

            PlaceMusician(
                ren,
                renRotationTarget,
                initialRenLocalRotation,
                renPerformancePoint,
                true
            );
        }
        else
        {
            Debug.LogWarning(
                "NO MUSICIAN HAS BEEN SELECTED"
            );
        }
        if (selectedMusician != null)
        {
            var rig = selectedName == "MIKA" ? mikaRig : renRig;
            if (rig.animator == null) rig.animator = selectedAnimator;
            selectedDrummer = selectedMusician.GetComponent<ProceduralDrummer>();
            if (selectedDrummer == null) selectedDrummer = selectedMusician.gameObject.AddComponent<ProceduralDrummer>();
            selectedDrummer.Seat(selectedMusician, rig);
        }
    }

    public void PrepareDrumming(System.Collections.Generic.IReadOnlyList<DrumRecorder.DrumHit> hits, float duration)
    {
        if (selectedDrummer != null) selectedDrummer.PrepareRecording(hits, duration, drumTargets);
    }

    public void EvaluateDrumming(float playbackTime)
    {
        if (selectedDrummer != null) selectedDrummer.Evaluate(playbackTime);
    }

    private void PlaceMusician(
        Transform musician,
        Transform rotationTarget,
        Quaternion initialLocalRotation,
        Transform performancePoint,
        bool alignModelToPoint
    )
    {
        if (
            musician == null ||
            performancePoint == null
        )
        {
            Debug.LogWarning(
                "MUSICIAN OR PERFORMANCE POINT IS MISSING"
            );

            return;
        }

        musician.gameObject.SetActive(true);

        musician.SetPositionAndRotation(
            performancePoint.position,
            performancePoint.rotation
        );

        if (rotationTarget != null)
        {
            rotationTarget.localRotation =
                initialLocalRotation;
        }

        // Ren's visible model is offset inside Ren Root.
        // Shift the root so the Ren child reaches the point.
        if (alignModelToPoint && rotationTarget != null)
        {
            musician.position +=
                performancePoint.position -
                rotationTarget.position;
        }
    }

    private void MovePlayerToPoint(
        Transform targetPoint
    )
    {
        if (
            xrOrigin == null ||
            targetPoint == null
        )
        {
            return;
        }

        xrOrigin.MatchOriginUpCameraForward(
            Vector3.up,
            targetPoint.forward
        );

        xrOrigin.MoveCameraToWorldLocation(
            targetPoint.position
        );
    }

    private void UpdatePauseButtonIcon(
        bool isPaused
    )
    {
        if (pauseButtonImage == null)
            return;

        if (isPaused && playIcon != null)
        {
            pauseButtonImage.sprite =
                playIcon;
        }
        else if (pauseIcon != null)
        {
            pauseButtonImage.sprite =
                pauseIcon;
        }
    }

    private IEnumerator FadeBlackout(
        float startAlpha,
        float endAlpha,
        float duration
    )
    {
        if (duration <= 0f)
        {
            blackoutCanvasGroup.alpha =
                endAlpha;

            yield break;
        }

        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;

            float progress =
                Mathf.Clamp01(
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

        blackoutCanvasGroup.alpha =
            endAlpha;
    }
}
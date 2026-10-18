using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.XR.CoreUtils;

public class DrumRecorder : MonoBehaviour
{
    public static DrumRecorder Instance { get; private set; }

    [System.Serializable]
    public class DrumHit
    {
        public string soundName;
        public float time;

        public DrumHit(string soundName, float time)
        {
            this.soundName = soundName;
            this.time = time;
        }
    }

    [System.Serializable]
    private class RecordingData
    {
        public List<DrumHit> hits;
        public float duration;
    }

    [Header("Audio")]
    [SerializeField] private AudioSource playbackAudioSource;
    [SerializeField] private List<AudioClip> drumSounds =
        new List<AudioClip>();

    [Header("Record Button")]
    [SerializeField] private GameObject recordButton;
    [SerializeField] private Image recordButtonImage;
    [SerializeField] private Sprite normalRecordIcon;
    [SerializeField] private Sprite activeRecordIcon;

    [Header("Retake Button")]
    [SerializeField] private GameObject retakeButton;

    [Header("Playback Button")]
    [SerializeField] private GameObject playbackButton;
    [SerializeField] private Image playButtonImage;
    [SerializeField] private Sprite playIcon;
    [SerializeField] private Sprite pauseIcon;

    [Header("Save Button")]
    [SerializeField] private GameObject saveButton;

    [Header("Performance")]
    [SerializeField] private PerformanceSequence performanceSequence;

    [Header("Debug")]
    [SerializeField] private bool isRecording;
    [SerializeField] private bool isReplaying;
    [SerializeField] private bool isPlaybackPaused;
    [SerializeField] private bool hasRecording;
    [SerializeField] private bool isPerformanceMode;
    [SerializeField] private bool isPerformancePaused;
    [SerializeField] private bool performanceTransitionActive;
    [SerializeField] private int recordedHitCount;
    [SerializeField] private float recordingDuration;

    private readonly List<DrumHit> recordedHits =
        new List<DrumHit>();

    private Canvas recordingCanvas;
    private GameObject reviewPanel;
    private GameObject recordStatusPanel;
    private TMP_Text recordingStatusText;
    private TMP_Text reviewTitle;
    private bool hasCompletedTake;
    private HelpMenu help;
    private DrumModeVisuals drumModeVisuals;
    private Canvas reviewOverlayCanvas;
    public bool IsReviewing => hasCompletedTake && !isRecording && !performanceTransitionActive && !isPerformanceMode;
    public bool CanToggleRecording => recordingCanvas != null && recordingCanvas.gameObject.activeInHierarchy &&
        !isPerformanceMode && !performanceTransitionActive && !hasCompletedTake;

    private float recordingStartTime;
    private Coroutine replayCoroutine;
    private Coroutine performanceCoroutine;

    public bool IsRecording => isRecording;
    public bool IsReplaying => isReplaying;
    public bool IsPlaybackPaused => isPlaybackPaused;
    public bool HasRecording => hasRecording;
    public bool IsPerformanceMode => isPerformanceMode;
    public bool IsPerformancePaused => isPerformancePaused;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning(
                "More than one DrumRecorder exists in the scene."
            );

            return;
        }

        Instance = this;
    }

    private void Start()
    {
        isRecording = false;
        isReplaying = false;
        isPlaybackPaused = false;
        hasRecording = false;
        isPerformanceMode = false;
        isPerformancePaused = false;
        performanceTransitionActive = false;

        recordedHitCount = 0;
        recordingDuration = 0f;

        PrepareRecordingUI();
        UpdateRecordButtonIcon();
        UpdatePlayButtonIcon();
        UpdateButtonVisibility();
    }

    private void PrepareRecordingUI()
    {
        if (recordingCanvas != null || recordButton == null) return;
        recordingCanvas = recordButton.GetComponentInParent<Canvas>(true);
        var origin = FindFirstObjectByType<XROrigin>();
        var eye = origin != null ? origin.Camera : Camera.main;
        if (recordingCanvas == null || eye == null) return;
        QuestUI.ConfigureCanvas(recordingCanvas, eye, new Vector2(1100f, 700f), 150);
        recordingCanvas.transform.SetParent(eye.transform, false);
        recordingCanvas.transform.localPosition = new Vector3(0f, -.05f, 1f);
        recordingCanvas.transform.localScale = Vector3.one * .001f;
        help = FindFirstObjectByType<HelpMenu>();
        drumModeVisuals = GetComponent<DrumModeVisuals>();
        if (drumModeVisuals == null) drumModeVisuals = FindFirstObjectByType<DrumModeVisuals>();
        reviewOverlayCanvas = QuestUI.CreateCanvas("Recording Full Screen Overlay", eye, new Vector2(4000f, 4000f), 140);
        reviewOverlayCanvas.transform.SetParent(eye.transform, false);
        reviewOverlayCanvas.transform.localPosition = new Vector3(0f, 0f, .32f);
        reviewOverlayCanvas.transform.localScale = Vector3.one * .001f;
        QuestUI.Background("Full View Dimmer", reviewOverlayCanvas.transform,
            new Vector2(4000f, 4000f), new Color(0f, 0f, 0f, .8f));
        reviewOverlayCanvas.gameObject.SetActive(false);

        recordStatusPanel = QuestUI.Background("Recording Status Panel", recordingCanvas.transform,
            new Vector2(650f, 100f), new Color(.04f, .06f, .1f, .92f)).gameObject;
        recordStatusPanel.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -230f);
        QuestUI.Layout(recordButton.GetComponent<RectTransform>(), recordStatusPanel.transform,
            new Vector2(-245f, 0f), new Vector2(64f, 64f));
        recordingStatusText = QuestUI.Text("Recording Status", recordStatusPanel.transform,
            "A  •  Start recording", new Vector2(45f, 0f), new Vector2(480f, 70f), 26f, Color.white);

        reviewPanel = new GameObject("Recording Review Panel", typeof(RectTransform));
        QuestUI.Layout(reviewPanel.GetComponent<RectTransform>(), recordingCanvas.transform,
            Vector2.zero, new Vector2(950f, 330f));
        reviewTitle = QuestUI.Text("Review Title", reviewPanel.transform, "RECORDING READY",
            new Vector2(0f, 114f), new Vector2(850f, 45f), 32f, Color.white);
        LayoutReviewButton(playbackButton, 0f);
        LayoutReviewButton(retakeButton, -280f);
        LayoutReviewButton(saveButton, 280f);
        QuestUI.Text("Review Ray Hint", reviewPanel.transform,
            "Point your controller ray at a button and press the trigger",
            new Vector2(0f, -116f), new Vector2(900f, 40f), 23f, Color.white);
        QuestUI.ApplyOverlay(recordingCanvas.gameObject);
        reviewPanel.SetActive(false);
    }

    private void LayoutReviewButton(GameObject buttonObject, float x)
    {
        if (buttonObject == null) return;
        var image = buttonObject.GetComponent<Image>();
        var size = new Vector2(150f, 150f);
        if (image != null && image.sprite != null)
        {
            float aspect = image.sprite.rect.width / image.sprite.rect.height;
            size = aspect >= 1f ? new Vector2(210f, 210f / aspect) : new Vector2(150f * aspect, 150f);
            image.preserveAspect = true;
        }
        QuestUI.Layout(buttonObject.GetComponent<RectTransform>(), reviewPanel.transform,
            new Vector2(x, 0f), size);
        var button = buttonObject.GetComponent<Button>();
        if (button != null) button.navigation = new Navigation { mode = Navigation.Mode.None };
    }

    private void LateUpdate()
    {
        UpdateButtonVisibility();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // RECORDING

    public void ToggleRecording()
    {
        if (!CanToggleRecording || (help != null && help.IsOpen)) return;
        if (isRecording)
            StopRecording();
        else
            StartRecording();
    }

    public void StartRecording()
    {
        StopReplay();

        recordedHits.Clear();
        recordedHitCount = 0;
        recordingDuration = 0f;

        hasRecording = false;
        hasCompletedTake = false;
        isRecording = true;

        recordingStartTime = Time.time;
        AttachRecordingStatusToHeadset();
        if (drumModeVisuals != null) drumModeVisuals.EnableDrumMode();

        UpdateRecordButtonIcon();
        UpdateButtonVisibility();

        Debug.Log("DRUM RECORDING STARTED");
    }

    public void StopRecording()
    {
        if (!isRecording)
            return;

        recordingDuration =
            Time.time - recordingStartTime;

        isRecording = false;
        recordedHitCount = recordedHits.Count;
        hasRecording = recordedHits.Count > 0;
        hasCompletedTake = true;
        if (recordingCanvas != null && recordingCanvas.worldCamera != null)
        {
            // Freeze the review panel in front of the current seated view for ray aiming.
            var origin = FindFirstObjectByType<XROrigin>();
            recordingCanvas.transform.SetParent(origin != null ? origin.transform : null, true);
            QuestUI.PlaceInFront(recordingCanvas, recordingCanvas.worldCamera, new Vector3(0f, 0f, 1f));
        }
        if (drumModeVisuals != null) drumModeVisuals.SetMenuMode(true);
        if (help != null) help.SetLaserVisuals(true);

        UpdateRecordButtonIcon();
        UpdateButtonVisibility();

        if (hasRecording)
        {
            Debug.Log(
                "DRUM RECORDING STOPPED. HITS: "
                + recordedHits.Count
                + ". DURATION: "
                + recordingDuration
            );
        }
        else
        {
            Debug.LogWarning(
                "RECORDING STOPPED, BUT NO HITS WERE RECORDED"
            );
        }
    }

    public void RetakeRecording()
    {
        if (performanceTransitionActive || isPerformanceMode) return;
        StopReplay();
        recordedHits.Clear();
        recordedHitCount = 0;
        recordingDuration = 0f;
        recordingStartTime = 0f;
        isRecording = false;
        hasRecording = false;
        hasCompletedTake = false;
        AttachRecordingStatusToHeadset();
        if (drumModeVisuals != null) drumModeVisuals.EnableDrumMode();
        UpdateRecordButtonIcon();
        UpdatePlayButtonIcon();
        UpdateButtonVisibility();
        Debug.Log("RETAKE: INITIAL RECORDING SCREEN RESTORED. PRESS A TO START.");
    }

    public void RecordHit(AudioClip drumSound)
    {
        if (!isRecording || drumSound == null)
            return;

        float hitTime =
            Time.time - recordingStartTime;

        recordedHits.Add(
            new DrumHit(drumSound.name, hitTime)
        );

        recordedHitCount = recordedHits.Count;

        Debug.Log(
            "RECORDED HIT: "
            + drumSound.name
            + " AT "
            + hitTime
        );
    }

    // PREVIEW PLAYBACK

    public void TogglePlayback()
    {
        if (
            isRecording ||
            !hasRecording ||
            isPerformanceMode
        )
        {
            return;
        }

        if (!isReplaying)
        {
            PlayRecording();
            return;
        }

        if (isPlaybackPaused)
            ResumeReplay();
        else
            PauseReplay();
    }

    public void PlayRecording()
    {
        if (isRecording)
        {
            Debug.LogWarning(
                "CANNOT PLAY WHILE RECORDING"
            );

            return;
        }

        if (!hasRecording || recordedHits.Count == 0)
        {
            Debug.LogWarning(
                "NO DRUM RECORDING TO PLAY"
            );

            return;
        }

        StopReplay();

        isReplaying = true;
        isPlaybackPaused = false;

        UpdatePlayButtonIcon();

        replayCoroutine = StartCoroutine(
            ReplayRecording()
        );

        Debug.Log(
            "DRUM RECORDING STARTED PLAYING"
        );
    }

    private IEnumerator ReplayRecording()
    {
        float playbackTime = 0f;
        int nextHitIndex = 0;

        float previewDuration =
            GetSafeRecordingDuration();

        while (playbackTime < previewDuration)
        {
            if (isPlaybackPaused)
            {
                yield return null;
                continue;
            }

            playbackTime += Time.deltaTime;

            PlayHitsAtTime(
                playbackTime,
                ref nextHitIndex
            );

            yield return null;
        }

        isReplaying = false;
        isPlaybackPaused = false;
        replayCoroutine = null;

        UpdatePlayButtonIcon();

        Debug.Log(
            "DRUM RECORDING FINISHED PLAYING"
        );
    }

    private void PauseReplay()
    {
        if (!isReplaying || isPlaybackPaused)
            return;

        isPlaybackPaused = true;

        if (playbackAudioSource != null)
            playbackAudioSource.Pause();

        UpdatePlayButtonIcon();

        Debug.Log("DRUM RECORDING PAUSED");
    }

    private void ResumeReplay()
    {
        if (!isReplaying || !isPlaybackPaused)
            return;

        isPlaybackPaused = false;

        if (playbackAudioSource != null)
            playbackAudioSource.UnPause();

        UpdatePlayButtonIcon();

        Debug.Log("DRUM RECORDING RESUMED");
    }

    private void StopReplay()
    {
        if (replayCoroutine != null)
        {
            StopCoroutine(replayCoroutine);
            replayCoroutine = null;
        }

        isReplaying = false;
        isPlaybackPaused = false;

        if (playbackAudioSource != null)
            playbackAudioSource.Stop();

        UpdatePlayButtonIcon();
    }

    // LOOPED PERFORMANCE

    public void BeginPerformanceTransition()
    {
        StopReplay();
        StopPerformance();

        performanceTransitionActive = true;

        UpdateButtonVisibility();
    }

    public void StartLoopedPerformance()
    {
        if (!hasRecording || recordedHits.Count == 0)
        {
            Debug.LogWarning(
                "NO RECORDING AVAILABLE FOR PERFORMANCE"
            );

            return;
        }

        StopReplay();
        StopPerformance();

        performanceTransitionActive = false;
        isPerformanceMode = true;
        isPerformancePaused = false;

        performanceCoroutine = StartCoroutine(
            LoopPerformance()
        );

        UpdateButtonVisibility();

        Debug.Log(
            "LOOPED MUSICIAN PERFORMANCE STARTED"
        );
    }

    private IEnumerator LoopPerformance()
    {
        float loopDuration =
            GetSafeRecordingDuration();

        if (performanceSequence != null)
            performanceSequence.PrepareDrumming(recordedHits, loopDuration);

        while (isPerformanceMode)
        {
            float playbackTime = 0f;
            int nextHitIndex = 0;

            while (
                playbackTime < loopDuration &&
                isPerformanceMode
            )
            {
                if (isPerformancePaused)
                {
                    yield return null;
                    continue;
                }

                playbackTime += Time.deltaTime;

                PlayHitsAtTime(
                    playbackTime,
                    ref nextHitIndex
                );

                if (performanceSequence != null)
                    performanceSequence.EvaluateDrumming(playbackTime);

                yield return null;
            }
        }

        performanceCoroutine = null;
    }

    public void TogglePerformancePause()
    {
        if (!isPerformanceMode)
            return;

        isPerformancePaused =
            !isPerformancePaused;

        if (playbackAudioSource != null)
        {
            if (isPerformancePaused)
                playbackAudioSource.Pause();
            else
                playbackAudioSource.UnPause();
        }

        Debug.Log(
            isPerformancePaused
                ? "PERFORMANCE PAUSED"
                : "PERFORMANCE RESUMED"
        );
    }

    public void StopPerformance()
    {
        if (performanceCoroutine != null)
        {
            StopCoroutine(performanceCoroutine);
            performanceCoroutine = null;
        }

        isPerformanceMode = false;
        isPerformancePaused = false;

        if (playbackAudioSource != null)
            playbackAudioSource.Stop();

        UpdateButtonVisibility();
    }

    public void ResetAfterPerformanceDelete()
    {
        StopReplay();
        StopPerformance();

        recordedHits.Clear();

        recordedHitCount = 0;
        recordingDuration = 0f;
        recordingStartTime = 0f;

        isRecording = false;
        isReplaying = false;
        isPlaybackPaused = false;
        hasRecording = false;
        hasCompletedTake = false;
        AttachRecordingStatusToHeadset();

        performanceTransitionActive = false;
        isPerformanceMode = false;
        isPerformancePaused = false;

        DeleteSavedRecordingFile();

        UpdateRecordButtonIcon();
        UpdatePlayButtonIcon();
        UpdateButtonVisibility();

        Debug.Log(
            "SAVED PERFORMANCE DELETED"
        );
    }

    private void PlayHitsAtTime(
        float playbackTime,
        ref int nextHitIndex
    )
    {
        while (
            nextHitIndex < recordedHits.Count &&
            playbackTime >=
            recordedHits[nextHitIndex].time
        )
        {
            DrumHit hit =
                recordedHits[nextHitIndex];

            AudioClip clip =
                FindDrumSound(hit.soundName);

            if (
                clip != null &&
                playbackAudioSource != null
            )
            {
                playbackAudioSource.PlayOneShot(clip);
            }
            else
            {
                Debug.LogWarning(
                    "COULD NOT REPLAY SOUND: "
                    + hit.soundName
                );
            }

            nextHitIndex++;
        }
    }

    private float GetSafeRecordingDuration()
    {
        float lastHitTime = 0f;

        if (recordedHits.Count > 0)
        {
            lastHitTime =
                recordedHits[
                    recordedHits.Count - 1
                ].time;
        }

        return Mathf.Max(
            recordingDuration,
            lastHitTime + 0.1f,
            0.1f
        );
    }

    private AudioClip FindDrumSound(
        string soundName
    )
    {
        return drumSounds.Find(
            sound =>
                sound != null &&
                sound.name == soundName
        );
    }

    // BUTTON VISIBILITY

    private void UpdateButtonVisibility()
    {
        bool hideRecordingButtons =
            performanceTransitionActive ||
            isPerformanceMode;

        bool showReviewButtons =
            !hideRecordingButtons &&
            hasCompletedTake &&
            !isRecording;

        SetButtonActive(reviewPanel, showReviewButtons);
        if (reviewOverlayCanvas != null)
            SetButtonActive(reviewOverlayCanvas.gameObject, showReviewButtons && recordingCanvas.gameObject.activeInHierarchy);
        SetButtonActive(recordStatusPanel, !hideRecordingButtons && !showReviewButtons);
        if (recordingStatusText != null)
            recordingStatusText.text = isRecording ? "●  RECORDING   •   A to stop" : "A  •  Start recording";
        if (reviewTitle != null)
            reviewTitle.text = hasRecording ? "RECORDING READY" : "NO HITS RECORDED — CHOOSE RETAKE";
        SetReviewButtonInteractable(playbackButton, hasRecording);
        SetReviewButtonInteractable(saveButton, hasRecording);

        SetButtonActive(
            recordButton,
            !hideRecordingButtons &&
            !showReviewButtons
        );

        SetButtonActive(
            retakeButton,
            showReviewButtons
        );

        SetButtonActive(
            playbackButton,
            showReviewButtons
        );

        SetButtonActive(
            saveButton,
            showReviewButtons
        );
    }

    private void AttachRecordingStatusToHeadset()
    {
        if (recordingCanvas == null || recordingCanvas.worldCamera == null) return;
        recordingCanvas.transform.SetParent(recordingCanvas.worldCamera.transform, false);
        recordingCanvas.transform.localPosition = new Vector3(0f, -.05f, 1f);
        recordingCanvas.transform.localScale = Vector3.one * .001f;
    }

    private void SetReviewButtonInteractable(GameObject buttonObject, bool interactable)
    {
        if (buttonObject != null && buttonObject.GetComponent<Button>() != null)
            buttonObject.GetComponent<Button>().interactable = interactable;
    }

    private void SetButtonActive(
        GameObject buttonObject,
        bool shouldBeActive
    )
    {
        if (
            buttonObject != null &&
            buttonObject.activeSelf != shouldBeActive
        )
        {
            buttonObject.SetActive(
                shouldBeActive
            );
        }
    }

    private void UpdateRecordButtonIcon()
    {
        if (recordButtonImage == null)
            return;

        if (
            isRecording &&
            activeRecordIcon != null
        )
        {
            recordButtonImage.sprite =
                activeRecordIcon;
        }
        else if (normalRecordIcon != null)
        {
            recordButtonImage.sprite =
                normalRecordIcon;
        }
    }

    private void UpdatePlayButtonIcon()
    {
        if (playButtonImage == null)
            return;

        if (
            isReplaying &&
            !isPlaybackPaused &&
            pauseIcon != null
        )
        {
            playButtonImage.sprite =
                pauseIcon;
        }
        else if (playIcon != null)
        {
            playButtonImage.sprite =
                playIcon;
        }
    }

    // SAVE

    public void SaveRecording()
    {
        if (isRecording)
        {
            Debug.LogWarning(
                "STOP RECORDING BEFORE SAVING"
            );

            return;
        }

        if (!hasRecording || recordedHits.Count == 0)
        {
            Debug.LogWarning(
                "NO DRUM RECORDING TO SAVE"
            );

            return;
        }

        RecordingData recordingData =
            new RecordingData
            {
                hits =
                    new List<DrumHit>(
                        recordedHits
                    ),

                duration = recordingDuration
            };

        string json =
            JsonUtility.ToJson(
                recordingData,
                true
            );

        string savePath =
            GetSavePath();

        File.WriteAllText(
            savePath,
            json
        );

        Debug.Log(
            "DRUM RECORDING SAVED: "
            + savePath
        );

        if (performanceSequence != null)
        {
            performanceSequence.BeginPerformance();
        }
        else
        {
            Debug.LogWarning(
                "PERFORMANCE SEQUENCE HAS NOT BEEN ASSIGNED"
            );
        }
    }

    private void DeleteSavedRecordingFile()
    {
        string savePath = GetSavePath();

        if (File.Exists(savePath))
            File.Delete(savePath);
    }

    private string GetSavePath()
    {
        return Path.Combine(
            Application.persistentDataPath,
            "drum-recording.json"
        );
    }
}

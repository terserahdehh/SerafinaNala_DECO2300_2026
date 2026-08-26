using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

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
    }

    [Header("Audio")]
    [SerializeField] private AudioSource playbackAudioSource;
    [SerializeField] private List<AudioClip> drumSounds =
        new List<AudioClip>();

    [Header("Record Button")]
    [SerializeField] private Image recordButtonImage;
    [SerializeField] private Sprite normalRecordIcon;
    [SerializeField] private Sprite activeRecordIcon;

    [Header("Play Button")]
    [SerializeField] private Image playButtonImage;
    [SerializeField] private Sprite playIcon;
    [SerializeField] private Sprite pauseIcon;

    [Header("Debug")]
    [SerializeField] private bool isRecording;
    [SerializeField] private bool isReplaying;
    [SerializeField] private bool isPlaybackPaused;
    [SerializeField] private int recordedHitCount;

    private readonly List<DrumHit> recordedHits =
        new List<DrumHit>();

    private float recordingStartTime;
    private Coroutine replayCoroutine;

    public bool IsRecording => isRecording;
    public bool IsReplaying => isReplaying;
    public bool IsPlaybackPaused => isPlaybackPaused;

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
        UpdateRecordButtonIcon();
        UpdatePlayButtonIcon();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // RECORDING

    public void ToggleRecording()
    {
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

        recordingStartTime = Time.time;
        isRecording = true;

        UpdateRecordButtonIcon();

        Debug.Log("DRUM RECORDING STARTED");
    }

    public void StopRecording()
    {
        if (!isRecording)
            return;

        isRecording = false;

        UpdateRecordButtonIcon();

        Debug.Log(
            "DRUM RECORDING STOPPED. HITS: "
            + recordedHits.Count
        );
    }

    public void RecordHit(AudioClip drumSound)
    {
        if (!isRecording || drumSound == null)
            return;

        float hitTime = Time.time - recordingStartTime;

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

    // PLAYBACK

    public void TogglePlayback()
    {
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
        if (recordedHits.Count == 0)
        {
            Debug.LogWarning("NO DRUM RECORDING TO PLAY");
            return;
        }

        StopRecording();
        StopReplay();

        isReplaying = true;
        isPlaybackPaused = false;

        UpdatePlayButtonIcon();

        replayCoroutine = StartCoroutine(
            ReplayRecording()
        );

        Debug.Log("DRUM RECORDING STARTED PLAYING");
    }

    private IEnumerator ReplayRecording()
    {
        float playbackTime = 0f;
        int nextHitIndex = 0;

        while (nextHitIndex < recordedHits.Count)
        {
            if (isPlaybackPaused)
            {
                yield return null;
                continue;
            }

            playbackTime += Time.deltaTime;

            while (
                nextHitIndex < recordedHits.Count &&
                playbackTime >= recordedHits[nextHitIndex].time
            )
            {
                DrumHit hit = recordedHits[nextHitIndex];
                AudioClip clip = FindDrumSound(hit.soundName);

                if (clip != null && playbackAudioSource != null)
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

            yield return null;
        }

        isReplaying = false;
        isPlaybackPaused = false;
        replayCoroutine = null;

        UpdatePlayButtonIcon();

        Debug.Log("DRUM RECORDING FINISHED PLAYING");
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

    private AudioClip FindDrumSound(string soundName)
    {
        return drumSounds.Find(
            sound =>
                sound != null &&
                sound.name == soundName
        );
    }

    // BUTTON ICONS

    private void UpdateRecordButtonIcon()
    {
        if (recordButtonImage == null)
            return;

        if (isRecording && activeRecordIcon != null)
        {
            recordButtonImage.sprite = activeRecordIcon;
        }
        else if (!isRecording && normalRecordIcon != null)
        {
            recordButtonImage.sprite = normalRecordIcon;
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
            playButtonImage.sprite = pauseIcon;
        }
        else if (playIcon != null)
        {
            playButtonImage.sprite = playIcon;
        }
    }

    // SAVE

    public void SaveRecording()
    {
        if (recordedHits.Count == 0)
        {
            Debug.LogWarning("NO DRUM RECORDING TO SAVE");
            return;
        }

        RecordingData recordingData = new RecordingData
        {
            hits = new List<DrumHit>(recordedHits)
        };

        string json = JsonUtility.ToJson(
            recordingData,
            true
        );

        string savePath = Path.Combine(
            Application.persistentDataPath,
            "drum-recording.json"
        );

        File.WriteAllText(savePath, json);

        Debug.Log(
            "DRUM RECORDING SAVED: " + savePath
        );
    }
}
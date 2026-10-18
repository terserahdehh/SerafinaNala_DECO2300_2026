using UnityEngine;
using UnityEngine.InputSystem;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;

public class DrumModeVisuals : MonoBehaviour
{
    [Header("Controller Models")]
    [SerializeField] private GameObject leftControllerVisual;
    [SerializeField] private GameObject rightControllerVisual;

    [Header("Drumsticks")]
    [SerializeField] private GameObject leftDrumstick;
    [SerializeField] private GameObject rightDrumstick;

    [Header("Menu UI Input")]
    [SerializeField] private InputActionAsset menuInputActions;

    private bool drumModeActive;
    private bool menuModeActive;
    private GameObject leftMenuRay;
    private GameObject rightMenuRay;
    public bool IsDrumModeActive => drumModeActive;
    public bool IsUsingDrumsticks => drumModeActive && !menuModeActive;

    private void Start()
    {
        drumModeActive = false;

        if (leftControllerVisual != null)
            leftControllerVisual.SetActive(true);

        if (rightControllerVisual != null)
            rightControllerVisual.SetActive(true);

        if (leftDrumstick != null)
            leftDrumstick.SetActive(false);

        if (rightDrumstick != null)
            rightDrumstick.SetActive(false);

        SetMenuMode(true);
    }

    private void Update()
    {
#if UNITY_EDITOR
        // Existing desktop shortcut; Quest uses the normal experience transitions.
        if (Keyboard.current != null &&
            Keyboard.current.pKey.wasPressedThisFrame)
        {
            EnableDrumMode();
        }
#endif
    }

    private void LateUpdate()
    {
        ApplyCurrentMode();
    }

    public void EnableDrumMode()
    {
        drumModeActive = true;
        SetMenuMode(false);

        Debug.Log("DRUM MODE ENABLED");
    }

    public void DisableDrumMode()
    {
        drumModeActive = false;
        SetMenuMode(true);
        Debug.Log("DRUM MODE DISABLED");
    }

    // Review/Help override the drum visuals without losing the seated drum state.
    public void SetMenuMode(bool active)
    {
        menuModeActive = active;
        ApplyCurrentMode();
        var origin = FindFirstObjectByType<XROrigin>();
        if (origin == null) return;
        if (active && leftMenuRay == null)
        {
            leftMenuRay = CreateMenuRay(origin, true);
            rightMenuRay = CreateMenuRay(origin, false);
        }
        foreach (var ray in origin.GetComponentsInChildren<NearFarInteractor>(true))
            ray.enabled = !active;
        if (leftMenuRay != null) leftMenuRay.SetActive(active);
        if (rightMenuRay != null) rightMenuRay.SetActive(active);
    }

    private GameObject CreateMenuRay(XROrigin origin, bool left)
    {
        var prefab = Resources.Load<GameObject>("QuestMenuRay");
        if (prefab == null) { Debug.LogError("Quest menu ray prefab missing"); return null; }
        var ray = Instantiate(prefab, origin.CameraFloorOffsetObject.transform);
        ray.name = left ? "Left Controller Menu Ray" : "Right Controller Menu Ray";
        ray.AddComponent<QuestControllerMenuRay>().Initialize(left, menuInputActions);
        return ray;
    }

    private void ApplyCurrentMode()
    {
        bool showDrumsticks = IsUsingDrumsticks;

        if (leftControllerVisual != null)
            leftControllerVisual.SetActive(!showDrumsticks);

        if (rightControllerVisual != null)
            rightControllerVisual.SetActive(!showDrumsticks);

        if (leftDrumstick != null)
            leftDrumstick.SetActive(showDrumsticks);

        if (rightDrumstick != null)
            rightDrumstick.SetActive(showDrumsticks);
    }
}

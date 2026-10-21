/*
 * Removes the desktop XR Device Simulator from the scene copies used in a
 * player build. It leaves the saved scene and its simulator unchanged.
 * The custom keyboard/mouse testing helper was removed. This is a build tool,
 * not gameplay code and not an APK builder by itself.
 * Unity calls OnProcessScene while preparing build scenes. A BuildReport holds
 * information about that build. If the report is missing, this tool does
 * nothing. It finds even inactive simulators so they do not ship on Quest.
 */
// Use the interface Unity invokes for scene processing during a build.
using UnityEditor.Build;
// Use the build report used to recognise a real player build.
using UnityEditor.Build.Reporting;
// Use Unity objects, positions and the methods Unity calls during the game.
using UnityEngine;
// Use the scene-copy type passed to the custom build hook.
using UnityEngine.SceneManagement;
// Use the existing XR Device Simulator used for desktop testing.
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

// Removes the desktop XR Device Simulator from the scene copies used in a
// player build.
public sealed class ExcludeSimulatorFromBuild : IProcessSceneWithReport
{
    // Use build callback order zero; this property returns the processing
    // priority.
    public int callbackOrder => 0;
    // Remove simulators from scene, the scene copy being built. report
    // describes the current build; if missing, leave the scene alone. The saved
    // scene is not edited.
    public void OnProcessScene(Scene scene, BuildReport report)
    {
        // Ignore non-build scene processing; only a real build report
        // authorises this removal.
        if (report == null) return;
        // Visit every top-level object in the scene copy Unity is processing
        // for the build.
        foreach (var root in scene.GetRootGameObjects())
            // Find simulators even under inactive child objects so none ship
            // accidentally.
            foreach (var simulator in root.GetComponentsInChildren<XRDeviceSimulator>(true))
                // Remove each simulator GameObject from this build copy, not
                // the saved scene asset.
                if (simulator != null) Object.DestroyImmediate(simulator.gameObject);
    }
}

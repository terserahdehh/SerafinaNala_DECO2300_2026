using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

// Processes build scene copies only. Does not modify the saved scene or Play Mode scene.
public sealed class ExcludeSimulatorFromBuild : IProcessSceneWithReport
{
    public int callbackOrder => 0;
    public void OnProcessScene(Scene scene, BuildReport report)
    {
        if (report == null) return;
        foreach (var root in scene.GetRootGameObjects())
            foreach (var simulator in root.GetComponentsInChildren<XRDeviceSimulator>(true))
                if (simulator != null) Object.DestroyImmediate(simulator.gameObject);
    }
}

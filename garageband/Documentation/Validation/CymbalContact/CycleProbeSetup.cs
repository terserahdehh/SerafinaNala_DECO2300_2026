using UnityEditor; using UnityEditor.SceneManagement; using UnityEngine;
public static class CycleProbeSetup {
 public static void Run(){
 EditorSceneManager.OpenScene("Assets/MusicRoom.unity");
 foreach(var b in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include,FindObjectsSortMode.None))
 if(b!=null && !(b is UnityEngine.EventSystems.UIBehaviour) && !(b is TMPro.TMP_Text) && !(b is UnityEngine.XR.Interaction.Toolkit.XRInteractionManager) && !(b is DrumModeVisuals) && !(b is DrumRecorder) && !(b is PerformanceSequence) && !(b is HelpMenu) && !(b is DrumProximity)) b.enabled=false;
 var intro=Object.FindFirstObjectByType<IntroSequence>(FindObjectsInactive.Include);intro.gameObject.SetActive(false);
 foreach(var c in Object.FindObjectsByType<Collider>(FindObjectsInactive.Include,FindObjectsSortMode.None))c.enabled=false;
 var runner=new GameObject("Cycle Probe",typeof(CycleProbe));
 EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),"Assets/CycleProbe.unity");
 EditorApplication.EnterPlaymode();
 }
}
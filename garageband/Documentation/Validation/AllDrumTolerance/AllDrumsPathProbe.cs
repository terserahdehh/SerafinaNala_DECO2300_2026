using UnityEngine;using System;using System.Collections;using System.Collections.Generic;using System.Reflection;using System.Linq;using UnityEngine.InputSystem;using UnityEngine.InputSystem.LowLevel;
public class AllDrumsPathProbe:MonoBehaviour{
const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;T Get<T>(object o,string n)=>(T)o.GetType().GetField(n,F).GetValue(o);float watchdog;int passed;
void Update(){watchdog+=Time.unscaledDeltaTime;if(watchdog>50)Finish(2);}
void OnEnable(){Application.logMessageReceived+=OnLog;}void OnDisable(){Application.logMessageReceived-=OnLog;}
void OnLog(string m,string stack,LogType t){if(t==LogType.Exception)Finish(1);}
void Check(bool b,string name){Debug.Log("RESTORE "+(b?"PASS ":"FAIL ")+name);if(!b)throw new Exception(name);passed++;}
void Finish(int code){
#if UNITY_EDITOR
UnityEditor.EditorApplication.Exit(code);
#endif
}
IEnumerator Start(){yield return null;
var rec=FindFirstObjectByType<DrumRecorder>();var hits=Get<List<DrumRecorder.DrumHit>>(rec,"recordedHits");var tip=FindObjectsByType<DrumstickHit>(FindObjectsInactive.Include,FindObjectsSortMode.None).First();var controller=tip.transform.parent.parent;controller.gameObject.SetActive(false);tip.enabled=true;tip.GetComponent<SphereCollider>().enabled=true;tip.GetComponent<Rigidbody>().isKinematic=true;
rec.StartRecording();Get<Canvas>(rec,"recordingCanvas").gameObject.SetActive(true);
Check(!AudioListener.pause&&AudioListener.volume>0,"listener unpaused and unmuted");
var listeners=FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Where(a=>a.enabled).ToArray();Check(listeners.Length==1,"one active audio listener");
float[] pcm=new float[1024];var drums=FindObjectsByType<DrumSound>(FindObjectsSortMode.None).OrderBy(d=>d.name).ToArray();
foreach(var drum in drums){
 foreach(var c in FindObjectsByType<Collider>(FindObjectsInactive.Include,FindObjectsSortMode.None))c.enabled=false;
 tip.GetComponent<SphereCollider>().enabled=true;
 foreach(var a in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))a.Stop();
 var source=drum.GetComponent<AudioSource>();Check(source.enabled&&source.clip!=null&&!source.mute&&source.volume>0,drum.name+" source/clip enabled and unmuted");
 float[] clipData=new float[Mathf.Min(source.clip.samples*source.clip.channels,4096)];source.clip.LoadAudioData();Check(source.clip.GetData(clipData,0)&&clipData.Any(v=>Mathf.Abs(v)>.00001f),drum.name+" decoded clip has nonzero PCM");
 int before=hits.Count;var zone=drum.GetComponent<Collider>();
 if(zone!=null){
 controller.gameObject.SetActive(false);zone.enabled=true;Vector3 target=zone.bounds.center;
 if(drum.name=="CymbalHitZone"){
 var seq=FindFirstObjectByType<PerformanceSequence>(FindObjectsInactive.Include);var seat=Get<Transform>(seq,"drumSeatTarget");
 var near=zone.ClosestPoint(seat.position);target=near+(zone.bounds.center-near).normalized*.025f;
 Debug.Log("RESTORE CYMBAL player-facing tolerance contact distance="+Vector3.Distance(seat.position,target));
 }

 controller.position+=target-tip.transform.position;controller.gameObject.SetActive(true);
 var oldMode=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;Physics.SyncTransforms();Physics.Simulate(.02f);Physics.simulationMode=oldMode;
 Check(hits.Count==before+1&&hits.Last().soundName==source.clip.name,drum.name+" real physics trigger immediately accepts and records correct clip");
 }else{
 InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
 InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
 var oldUpdate=InputSystem.settings.updateMode;InputSystem.settings.updateMode=InputSettings.UpdateMode.ProcessEventsManually;
 var keyboard=InputSystem.AddDevice<Keyboard>();InputSystem.EnableDevice(keyboard);
 Debug.Log("RESTORE PEDAL component="+drum.enabled+" canvasActive="+Get<GameObject>(drum,"recordingCanvas").activeInHierarchy+" triggerB="+Get<bool>(drum,"triggerWithB"));
 InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();
 bool prime=keyboard.bKey.wasPressedThisFrame;
 InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.B));InputSystem.Update();keyboard.MakeCurrent();
 Debug.Log("RESTORE PEDAL keyboardPressed="+keyboard.bKey.wasPressedThisFrame+" held="+keyboard.bKey.isPressed+" enabled="+keyboard.enabled);
 typeof(DrumSound).GetMethod("Update",F).Invoke(drum,null);
 InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();InputSystem.settings.updateMode=oldUpdate;

 Check(hits.Count==before+1&&hits.Last().soundName==source.clip.name,drum.name+" existing B pedal input dispatches and records");
 }
 float max=0;for(float t=0;t<.25f;t+=.01f){AudioListener.GetOutputData(pcm,0);foreach(var sample in pcm)max=Mathf.Max(max,Mathf.Abs(sample));yield return new WaitForSecondsRealtime(.01f);}
 Debug.Log("RESTORE DSP "+drum.name+" max="+max.ToString("G6")+" sourcePlaying="+source.isPlaying);
 Check(max>.000001f,drum.name+" nonzero listener DSP output (does not establish headset audibility)");
 if(zone!=null){int count=hits.Count;Physics.SyncTransforms();var oldMode=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;Physics.Simulate(.02f);Physics.simulationMode=oldMode;Check(hits.Count==count,drum.name+" sustained overlap does not double-record");}
 controller.gameObject.SetActive(false);yield return new WaitForSecondsRealtime(.12f);
}

var cymbal=drums.First(d=>d.name=="CymbalHitZone");var cymCollider=cymbal.GetComponent<Collider>();cymCollider.enabled=true;Physics.SyncTransforms();
var perf=FindFirstObjectByType<PerformanceSequence>(FindObjectsInactive.Include);var seatTarget=Get<Transform>(perf,"drumSeatTarget");var edge=cymCollider.ClosestPoint(seatTarget.position);var assisted=edge+(cymCollider.bounds.center-edge).normalized*.025f;
foreach(var c in FindObjectsByType<Collider>(FindObjectsInactive.Include,FindObjectsSortMode.None))c.enabled=false;cymCollider.enabled=true;
foreach(var playerTip in FindObjectsByType<DrumstickHit>(FindObjectsInactive.Include,FindObjectsSortMode.None)){
 var parent=playerTip.transform.parent.parent;parent.gameObject.SetActive(false);playerTip.transform.parent.gameObject.SetActive(true);playerTip.enabled=true;parent.gameObject.SetActive(true);playerTip.GetComponent<SphereCollider>().enabled=true;
 for(int i=0;i<5;i++){
 parent.position+=assisted+Vector3.up*.3f-playerTip.transform.position;
 var previousMode=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;Physics.SyncTransforms();Physics.Simulate(.02f);Physics.simulationMode=previousMode;yield return new WaitForSecondsRealtime(.12f);
 int count=hits.Count;parent.position+=assisted-playerTip.transform.position;
 previousMode=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;Physics.SyncTransforms();Physics.Simulate(.02f);Physics.simulationMode=previousMode;
 Check(hits.Count==count+1&&hits.Last().soundName=="cymbal",playerTip.name+" assisted strike "+(i+1)+" records exactly one cymbal");
 yield return new WaitForSecondsRealtime(.12f);
 }
 playerTip.GetComponent<SphereCollider>().enabled=false;parent.gameObject.SetActive(false);
}
rec.StopRecording();int expected=hits.Count;var args=new object[]{100f,0};typeof(DrumRecorder).GetMethod("PlayHitsAtTime",F).Invoke(rec,args);Check((int)args[1]==expected,"all drum entries including ten assisted cymbal hits traverse unchanged replay scheduler");
Debug.Log("RESTORE ALL SOUND PATH CHECKS PASSED assertions="+passed);Finish(0);
}}

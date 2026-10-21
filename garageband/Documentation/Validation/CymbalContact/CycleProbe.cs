// Historical test of the retired cymbal detection system.
// Kept for reference only; this does not work with the current game.
using System;using System.Collections;using System.Collections.Generic;using System.Linq;using System.Reflection;using UnityEngine;using UnityEngine.UI;using UnityEngine.InputSystem;using UnityEngine.InputSystem.Controls;using UnityEngine.XR.Interaction.Toolkit;using UnityEngine.XR.Interaction.Toolkit.UI;using UnityEngine.XR.Interaction.Toolkit.Interactors;using Unity.XR.CoreUtils;
public class CycleProbe:MonoBehaviour{
 const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
 T Get<T>(object o,string n)=>(T)o.GetType().GetField(n,Flags).GetValue(o);
 void Set(object o,string n,object v)=>o.GetType().GetField(n,Flags).SetValue(o,v);
 void Check(bool v,string s){Debug.Log((v?"PROBE PASS ":"PROBE FAIL ")+s);if(!v)throw new Exception(s);}
 float watchdog; void Update(){watchdog+=Time.unscaledDeltaTime;if(watchdog>90f){
#if UNITY_EDITOR
UnityEditor.EditorApplication.Exit(2);
#endif
}}
 void OnEnable(){Application.logMessageReceived+=OnLog;}
 void OnDisable(){Application.logMessageReceived-=OnLog;}
 void OnLog(string message,string stack,LogType type){if(type==LogType.Exception){
#if UNITY_EDITOR
UnityEditor.EditorApplication.Exit(1);
#endif
}}
 IEnumerator Start(){yield return null;yield return Run();}
 IEnumerator Run(){
 var seq=FindFirstObjectByType<PerformanceSequence>(FindObjectsInactive.Include);var rec=FindFirstObjectByType<DrumRecorder>();var mode=FindFirstObjectByType<DrumModeVisuals>();var intro=FindFirstObjectByType<IntroSequence>(FindObjectsInactive.Include);var origin=FindFirstObjectByType<XROrigin>();
 typeof(IntroSequence).GetProperty("SelectedMusician").GetSetMethod(true).Invoke(intro,new object[]{"MIKA"});
 foreach(var n in new[]{"delayAfterSave","fadeToBlackDuration","blackoutHoldDuration","fadeFromBlackDuration","delayBeforePerformance"})Set(seq,n,.12f);
 var module=FindFirstObjectByType<XRUIInputModule>();module.enableXRInput=true;module.enableMouseInput=false;
 InputSystem.RegisterLayout(@"{""name"":""CycleTestController"",""extend"":""XRController"",""controls"":[{""name"":""pointerPosition"",""layout"":""Vector3""},{""name"":""pointerRotation"",""layout"":""Quaternion""},{""name"":""triggerPressed"",""layout"":""Button""},{""name"":""trigger"",""layout"":""Axis""}]}");
 var device=InputSystem.AddDevice("CycleTestController");InputSystem.SetDeviceUsage(device,UnityEngine.InputSystem.CommonUsages.RightHand);
 var cam=Get<Canvas>(rec,"recordingCanvas");cam.gameObject.SetActive(true);var controls=Get<GameObject>(seq,"performanceControls");var pause=Get<Image>(seq,"pauseButtonImage").GetComponent<Button>();var delete=controls.GetComponentsInChildren<Button>(true).First(b=>b!=pause);
 origin.MoveCameraToWorldLocation(Get<Transform>(seq,"drumSeatTarget").position);
 var clip=Get<List<AudioClip>>(rec,"drumSounds")[0];
 typeof(IntroSequence).GetMethod("Awake",Flags).Invoke(intro,null);
 foreach(string key in new[]{"character","mika","ren"}){
  var actor=Get<Transform>(intro,key);actor.gameObject.SetActive(true);var motion=actor.GetComponent<CharacterMotion>();
  Check(motion!=null,key+" standing/walking component wired");Check(motion.UsesClips==(key=="ren"),key+" uses compatible clips or named-bone fallback");
  var before=actor.position;var rotation=actor.rotation;var imports=actor.GetComponentsInChildren<Animator>(true).Select(a=>(a.transform,a.transform.localPosition,a.transform.localRotation)).ToArray();
  var rig=new ProceduralDrummer.Rig();ProceduralDrummer.ResolveBones(rig,actor);
  yield return new WaitForSeconds(.3f);Check(Vector3.Distance(before,actor.position)<.00001f&&Quaternion.Angle(rotation,actor.rotation)<.001f,key+" idle has no root motion");
  Capture(actor,key+"-idle"); CaptureSide(actor,key+"-side");
  if(!motion.UsesClips){
   float elbow=Vector3.Angle(rig.leftForearm.position-rig.leftUpperArm.position,rig.leftHand.position-rig.leftForearm.position);
   Check(elbow>12&&elbow<30,key+" relaxed elbow bend");
   Debug.Log("PROBE ARM "+key+" upper="+rig.leftUpperArm.position+" elbow="+rig.leftForearm.position+" hand="+rig.leftHand.position);
   bool sampled=false;var travel=Vector3.Cross(rig.rightThigh.position-rig.leftThigh.position,rig.spine.position-rig.hips.position).normalized;
   for(float t=0;t<1.5f;t+=Time.deltaTime){
    var plant=Get<object>(motion,"leftPlant");bool planted=(bool)plant.GetType().GetField("planted").GetValue(plant);var foot=rig.leftFoot.position;
    actor.position+=travel*(.7f*Time.deltaTime);yield return null;
    var afterPlant=Get<object>(motion,"leftPlant");bool after=(bool)afterPlant.GetType().GetField("planted").GetValue(afterPlant);
    if(planted&&after){Check(Vector3.Distance(foot,rig.leftFoot.position)<.005f,key+" planted stance foot remains grounded without sliding");sampled=true;break;}
   }
   Check(sampled,key+" world-space stance plant exercised");
   yield return new WaitForSeconds(.3f);
  }
  Check(Vector3.Dot((rig.leftForearm.position-rig.leftUpperArm.position).normalized,Vector3.down)>.65f,key+" idle arms are lowered");
  for(float elapsed=0;elapsed<.45f;elapsed+=Time.deltaTime){actor.position+=Vector3.forward*(1.2f*Time.deltaTime);yield return null;}
  Capture(actor,key+"-walk");
  Check(motion.WalkingWeight>.9f,key+" walking follows actual displacement");var stopped=actor.position;
  yield return new WaitForSeconds(.35f);Check(motion.WalkingWeight<.01f&&Vector3.Distance(stopped,actor.position)<.00001f,key+" walking blends to idle without drift");
  foreach(var imp in imports)Check(Vector3.Distance(imp.Item2,imp.Item1.localPosition)<.0001f&&Quaternion.Angle(imp.Item3,imp.Item1.localRotation)<.001f,key+" preserves imported model transform");
 }
 yield return NaturalCymbalChecks(rec);
 yield return CymbalChecks(rec);
 int pauseListeners=pause.onClick.GetPersistentEventCount(),deleteListeners=delete.onClick.GetPersistentEventCount();
 for(int cycle=1;cycle<=6;cycle++){
 typeof(IntroSequence).GetProperty("SelectedMusician").GetSetMethod(true).Invoke(intro,new object[]{cycle<=3?"MIKA":"REN"});
 if(cycle==2||cycle==5){var stale=controls.GetComponentInParent<Canvas>(true).GetComponent<CanvasGroup>();stale.interactable=false;stale.blocksRaycasts=false;pause.interactable=false;}

 mode.EnableDrumMode();rec.StartRecording();rec.RecordHit(clip);yield return new WaitForSeconds(.1f);rec.StopRecording();yield return Click(device,origin,Get<GameObject>(rec,"saveButton").GetComponent<Button>());
 var transition=Get<Coroutine>(seq,"transitionCoroutine");rec.SaveRecording();seq.BeginPerformance();Check(Get<Coroutine>(seq,"transitionCoroutine")==transition,"cycle "+cycle+" overlapping Save/transition rejected");
 if(cycle==2||cycle==5){
  foreach(var r in FindObjectsByType<QuestControllerMenuRay>(FindObjectsInactive.Include,FindObjectsSortMode.None)){
   Get<InputAction>(r,"trigger").Disable();Get<InputAction>(r,"triggerValue").Disable();
  }
  controls.GetComponentInParent<Canvas>(true).GetComponent<TrackedDeviceGraphicRaycaster>().enabled=false;
 }

 yield return new WaitForSeconds(.9f);
 Debug.Log("PROBE STATE cycle="+cycle+" seqActive="+Get<bool>(seq,"performanceActive")+" running="+Get<bool>(seq,"sequenceRunning")+" recPerformance="+rec.IsPerformanceMode+" pauseInteractable="+pause.IsInteractable()+" blackout="+Get<CanvasGroup>(seq,"blackoutCanvasGroup").blocksRaycasts);
 yield return Click(device,origin,pause);
 Check(rec.IsPerformancePaused,"cycle "+cycle+" pause click");
 var musician=Get<Transform>(seq,"selectedMusician");var motion=musician.GetComponent<CharacterMotion>();Check(motion.IsSuspended,"cycle "+cycle+" standing motion yields to drumming");
 var rig=new ProceduralDrummer.Rig();ProceduralDrummer.ResolveBones(rig,musician);var hand=rig.leftHand.localRotation;
 var grips=musician.GetComponentsInChildren<Transform>(true).Where(t=>t.name.EndsWith("musician drumstick grip")).ToArray();
 Check(grips.Length==2,"cycle "+cycle+" exactly two attached musician sticks");
 Check(grips.All(g=>g.GetComponentsInChildren<Collider>(true).All(c=>!c.enabled)&&g.GetComponentsInChildren<DrumstickHit>(true).Length==0&&g.GetComponentsInChildren<DrumSound>(true).Length==0),"cycle "+cycle+" visual sticks cannot trigger audio or recording");
 yield return new WaitForSeconds(.15f);Check(Quaternion.Angle(hand,rig.leftHand.localRotation)<.001f,"cycle "+cycle+" pause freezes drummer with no idle writer");
 if(cycle==2||cycle==5){var help=FindFirstObjectByType<HelpMenu>();help.OpenHelp();Check(!pause.IsInteractable(),"Help blocks performance controls");help.CloseHelp();Check(pause.IsInteractable(),"Help close restores performance controls");}
 Check(!cam.enabled&&!cam.GetComponent<TrackedDeviceGraphicRaycaster>().enabled,"cycle "+cycle+" recording UI cannot intercept performance rays");
 var savedHandle=Get<Coroutine>(seq,"transitionCoroutine");rec.SaveRecording();Check(Get<Coroutine>(seq,"transitionCoroutine")==savedHandle,"cycle "+cycle+" duplicate Save ignored");
 yield return Click(device,origin,pause);Check(!rec.IsPerformancePaused,"cycle "+cycle+" resume click");
 yield return Click(device,origin,delete);yield return new WaitForSeconds(.7f);Check(!rec.IsPerformanceMode&&!Get<bool>(seq,"performanceActive"),"cycle "+cycle+" delete click");
 }
 Check(pause.onClick.GetPersistentEventCount()==pauseListeners&&delete.onClick.GetPersistentEventCount()==deleteListeners,"listeners do not accumulate");
 Check(FindObjectsByType<QuestControllerMenuRay>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length==2,"exactly two menu rays after six cycles");
 Check(FindObjectsByType<XRUIInputModule>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length==1,"one XR UI input module");
 Debug.Log("PROBE ALL CYCLES AND LOCOMOTION PASSED");
 #if UNITY_EDITOR
 UnityEditor.EditorApplication.Exit(0);
 #endif
 }


 IEnumerator NaturalCymbalChecks(DrumRecorder rec){
 var surface=FindFirstObjectByType<CymbalStrikeSurface>();surface.enabled=true;surface.GetComponent<Collider>().enabled=true;
 var sound=surface.GetComponent<DrumSound>();typeof(DrumSound).GetMethod("Awake",Flags).Invoke(sound,null);
 var audio=sound.GetComponent<AudioSource>();var logs=new List<string>();Application.LogCallback capture=(m,st,t)=>logs.Add(m);Application.logMessageReceived+=capture;
 var tip=FindObjectsByType<DrumstickHit>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(t=>t.name.StartsWith("left"));
 var controller=tip.transform.parent.parent;var oldPos=controller.position;var oldActive=controller.gameObject.activeSelf;float oldCapture=Time.captureDeltaTime;Time.captureDeltaTime=.02f;
 controller.gameObject.SetActive(false);tip.GetComponent<SphereCollider>().enabled=true;tip.enabled=true;
 var centre=surface.transform.position;var n=surface.Normal;float bell=.043f,r=.012f;
 Action<float> place=h=>controller.position+=centre+n*(bell+r+h)-tip.transform.TransformPoint(tip.GetComponent<SphereCollider>().center);
 place(.16f);controller.gameObject.SetActive(true);rec.StartRecording();yield return null;
 Check(tip.isActiveAndEnabled&&Get<CymbalStrikeSurface[]>(tip,"cymbals").Contains(surface),"actual player tip active and references cymbal surface");
 var hits=Get<List<DrumRecorder.DrumHit>>(rec,"recordedHits");
 place(.0005f);yield return null;Check(hits.Count==0,"fast approach above surface does not play early");
 place(-.0005f);yield return null;yield return null;
 Check(hits.Count==1&&hits[0].soundName=="cymbal","braked natural strike accepted at 0.05m/s final contact after fast approach (v9 rejected)");
 place(.010f);yield return null;yield return new WaitForSeconds(.12f);place(-.001f);yield return null;yield return null;
 Check(hits.Count==2,"10mm rebound rearms a real player tip (v9 stayed latched)");
 yield return new WaitForSeconds(.12f);
 for(int i=0;i<6;i++){place(i%2==0?.001f:-.001f);yield return null;}
 Check(hits.Count==2,"1mm resting jitter cannot produce extra cymbal sounds");
 place(.010f);yield return null;yield return new WaitForSeconds(.2f);
 for(float h=.010f;h>-.001f;h-=.0004f){place(h);yield return null;}
 Check(hits.Count==2,"unprepared slow drift is still rejected");
 Set(surface,"diagnosticLogging",true);place(.16f);yield return null;place(.0005f);yield return null;yield return new WaitForSeconds(.2f);place(-.0005f);yield return null;yield return null;
 Check(hits.Count==2&&logs.Any(l=>l.Contains("reason=NoRecentApproach")),"expired approach rejected with explicit reason");
 place(.16f);yield return null;place(-.02f);yield return null;yield return null;
 Check(hits.Count==3&&logs.Any(l=>l.StartsWith("[DrumHit CONTACT]"))&&logs.Any(l=>l.StartsWith("[DrumHit AUDIO_SUBMITTED]"))&&logs.Any(l=>l.StartsWith("[DrumHit SAMPLE]")),"diagnostics distinguish real-tip sampling, contact and audio submission");
 place(.16f);yield return null;yield return new WaitForSeconds(.12f);audio.enabled=false;place(-.02f);yield return null;yield return null;
 Check(hits.Count==3&&logs.Any(l=>l.Contains("AUDIO_REJECT")&&l.Contains("SourceInactiveOrDisabled")),"disabled audio source rejected explicitly without phantom recorded hit");
 audio.enabled=true;Set(surface,"diagnosticLogging",false);Application.logMessageReceived-=capture;
 rec.StopRecording();tip.enabled=false;controller.position=oldPos;controller.gameObject.SetActive(oldActive);Time.captureDeltaTime=oldCapture;
 Check(!Get<bool>(surface,"diagnosticLogging")&&!Get<bool>(tip,"diagnosticLogging"),"all diagnostics disabled by default and restored after test");
 Debug.Log("PROBE NATURAL CYMBAL REGRESSIONS PASSED");
 }
 IEnumerator CymbalChecks(DrumRecorder rec){
 var surface=FindFirstObjectByType<CymbalStrikeSurface>();Check(surface!=null,"cymbal surface wired");
 var sound=surface.GetComponent<DrumSound>();sound.enabled=true;typeof(DrumSound).GetMethod("Awake",Flags).Invoke(sound,null);
 Check(sound.GetComponent<AudioSource>().clip.name=="cymbal","cymbal uses cymbal clip, not hi-hat");
 Check(Get<List<AudioClip>>(rec,"drumSounds").Contains(sound.GetComponent<AudioSource>().clip),"cymbal clip available to replay lookup");
 surface.enabled=true;surface.GetComponent<Collider>().enabled=true;
 foreach(var d in FindObjectsByType<DrumSound>(FindObjectsSortMode.None))if(d.GetComponent<Collider>()!=null)d.GetComponent<Collider>().enabled=true;
 Physics.SyncTransforms();
 var pos=surface.transform.position;var n=surface.Normal;
 Check(surface.Crosses(pos+Vector3.up*.18f,pos-Vector3.up*.12f,.012f,.02f,out _),"natural gravity-down strike crosses tilted cymbal");
 Check(surface.Crosses(pos+surface.transform.right*.24f+n*.18f,pos+surface.transform.right*.24f-n*.1f,.012f,.02f,out _),"visible cymbal edge is playable");
 Check(!surface.Crosses(pos+n*.18f,pos-n*.1f,.012f,10f,out _),"sub-threshold drift is silent");
 Check(!surface.Crosses(pos+surface.transform.right*.2f+n*.025f,pos-surface.transform.right*.2f+n*.025f,.012f,.02f,out _),"horizontal brush is silent");
 Check(n.y>.7f&&Vector3.Distance(pos,new Vector3(216.5513f,-24.31078f,-120.81725f))<.01f,"hit surface fitted to tilted cymbal mesh");
 foreach(var tip in FindObjectsByType<DrumstickHit>(FindObjectsInactive.Include,FindObjectsSortMode.None)){
 Check(Mathf.Abs(tip.GetComponent<SphereCollider>().radius-.3f)<.001f&&Vector3.Distance(tip.transform.localPosition,new Vector3(0,0,4.86f))<.001f,"player tip at mesh end with 12mm radius");
 tip.enabled=false;
 }
 var go=new GameObject("Synthetic tracked tip");go.transform.position=pos+n*.18f;
 var sphere=go.AddComponent<SphereCollider>();sphere.radius=.012f;sphere.isTrigger=true;
 var body=go.AddComponent<Rigidbody>();body.isKinematic=true;body.useGravity=false;
 go.AddComponent<DrumstickHit>();
 rec.StartRecording();yield return null;
 go.transform.position=pos-n*.09f;yield return null;yield return null;
 var hits=Get<List<DrumRecorder.DrumHit>>(rec,"recordedHits");Check(hits.Count==1&&hits[0].soundName=="cymbal","fast downward strike records exactly one cymbal hit");
 Check(Mathf.Abs(hits[0].time-(Time.time-Get<float>(rec,"recordingStartTime")))<.15f,"strike uses recorder clock");
 yield return new WaitForSeconds(.12f);go.transform.position=pos-n*.04f;yield return null;
 go.transform.position=pos-n*.08f;yield return null;Check(hits.Count==1,"contact below surface cannot retrigger after cooldown");
 go.transform.position=pos+n*.18f;yield return null;Check(hits.Count==1,"upward recovery is silent");
 yield return new WaitForSeconds(.12f);go.transform.position=pos-n*.06f;yield return null;yield return null;
 Check(hits.Count==2,"lift and second downward strike rearm");
 go.transform.position=pos-surface.transform.right*.4f+n*.18f;yield return null;yield return new WaitForSeconds(.12f);
 go.transform.position=pos-surface.transform.right*.4f-n*.06f;yield return null;yield return null;
 Check(hits.Count==2,"outside visible disc does not hit cymbal or nearby drums");
 go.transform.position=pos+n*.18f;yield return null;yield return new WaitForSeconds(.12f);
 go.transform.position=pos-n*.06f;yield return null;yield return null;
 var second=new GameObject("Second tracked tip");second.transform.position=pos+n*.18f;var c2=second.AddComponent<SphereCollider>();c2.radius=.012f;c2.isTrigger=true;var b2=second.AddComponent<Rigidbody>();b2.isKinematic=true;b2.useGravity=false;second.AddComponent<DrumstickHit>();yield return null;
 second.transform.position=pos-n*.06f;yield return null;yield return null;Check(hits.Count==4,"both sticks can strike independently inside shared cooldown interval");
 go.SetActive(false);second.SetActive(false);Destroy(go);Destroy(second);
 rec.StopRecording();var args=new object[]{100f,0};typeof(DrumRecorder).GetMethod("PlayHitsAtTime",Flags).Invoke(rec,args);
 Check((int)args[1]==4,"all cymbal hits traverse the existing replay scheduler");
 Check(Get<AudioSource>(rec,"playbackAudioSource").isPlaying,"replay AudioSource accepts cymbal audio");
 foreach(var collider in FindObjectsByType<Collider>(FindObjectsInactive.Include,FindObjectsSortMode.None))collider.enabled=false;
 Debug.Log("PROBE CYMBAL CHECKS PASSED");
 }
 IEnumerator Click(InputDevice d,XROrigin origin,Button button){
 var offset=origin.CameraFloorOffsetObject.transform;var start=origin.Camera.transform.position;var target=button.transform.position;
 InputSystem.QueueDeltaStateEvent(d.GetChildControl<Vector3Control>("pointerPosition"),offset.InverseTransformPoint(start));InputSystem.QueueDeltaStateEvent(d.GetChildControl<QuaternionControl>("pointerRotation"),Quaternion.Inverse(offset.rotation)*Quaternion.LookRotation(target-start));InputSystem.Update();
 yield return null;yield return null;yield return null;
 var ray=FindObjectsByType<QuestControllerMenuRay>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(r=>r.name.StartsWith("Right"));var xr=ray.GetComponent<XRRayInteractor>();
 bool found=moduleModel(xr,out var model);Debug.Log("PROBE RAY "+button.name+" active="+ray.gameObject.activeInHierarchy+" enabled="+xr.enabled+" registered="+found+" pointerTarget="+(xr.TryGetCurrentUIRaycastResult(out var hit)?hit.gameObject.name:"none"));
 InputSystem.QueueDeltaStateEvent(d.GetChildControl<ButtonControl>("triggerPressed"),(byte)1);InputSystem.QueueDeltaStateEvent(d.GetChildControl<AxisControl>("trigger"),1f);InputSystem.Update();yield return null;yield return null;
 InputSystem.QueueDeltaStateEvent(d.GetChildControl<ButtonControl>("triggerPressed"),(byte)0);InputSystem.QueueDeltaStateEvent(d.GetChildControl<AxisControl>("trigger"),0f);InputSystem.Update();yield return null;yield return null;
 }
 void Capture(Transform actor,string filename){
 var rig=new ProceduralDrummer.Rig();ProceduralDrummer.ResolveBones(rig,actor);var forward=Vector3.Cross(rig.rightThigh.position-rig.leftThigh.position,rig.spine.position-rig.hips.position).normalized;
 var go=new GameObject("QA capture",typeof(Camera));var camera=go.GetComponent<Camera>();camera.enabled=false;camera.transform.position=rig.hips.position+forward*2.6f+Vector3.up*.1f;camera.transform.LookAt(rig.hips.position);camera.fieldOfView=43;
 var target=new RenderTexture(800,1000,24);camera.targetTexture=target;camera.Render();var old=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(800,1000,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,800,1000),0,0);image.Apply();System.IO.File.WriteAllBytes(System.IO.Path.Combine(Application.dataPath,"../../"+filename+".png"),image.EncodeToPNG());RenderTexture.active=old;Destroy(go);Destroy(image);Destroy(target);
 }
 void CaptureSide(Transform actor,string filename){
 var rig=new ProceduralDrummer.Rig();ProceduralDrummer.ResolveBones(rig,actor);var side=(rig.rightThigh.position-rig.leftThigh.position).normalized;var forward=Vector3.Cross(side,rig.spine.position-rig.hips.position).normalized;
 var go=new GameObject("QA side capture",typeof(Camera));var c=go.GetComponent<Camera>();c.enabled=false;c.transform.position=rig.hips.position+side*2.3f+forward*1.3f+Vector3.up*.1f;c.transform.LookAt(rig.hips.position);c.fieldOfView=43;
 var target=new RenderTexture(800,1000,24);c.targetTexture=target;c.Render();var old=RenderTexture.active;RenderTexture.active=target;var im=new Texture2D(800,1000,TextureFormat.RGB24,false);im.ReadPixels(new Rect(0,0,800,1000),0,0);im.Apply();System.IO.File.WriteAllBytes(System.IO.Path.Combine(Application.dataPath,"../../"+filename+".png"),im.EncodeToPNG());RenderTexture.active=old;Destroy(go);Destroy(im);Destroy(target);
 }
 bool moduleModel(XRRayInteractor ray,out TrackedDeviceModel model)=>FindFirstObjectByType<XRUIInputModule>().GetTrackedDeviceModel(ray,out model);
}

using System;using System.Linq;using System.Collections.Generic;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;
public static class AllDrumToleranceCandidate{
static Collider[] zones;static Dictionary<string,float> baseline=new Dictionary<string,float>();
static string Key(int i,int j)=>i+":"+j;
static float Depth(Collider a,Collider b)=>Physics.ComputePenetration(a,a.transform.position,a.transform.rotation,b,b.transform.position,b.transform.rotation,out _,out float d)?d:0;
static bool Safe(){Physics.SyncTransforms();for(int i=0;i<zones.Length;i++)for(int j=i+1;j<zones.Length;j++)if(Depth(zones[i],zones[j])>baseline[Key(i,j)]+.0001f)return false;return true;}
public static void Run(){try{
EditorSceneManager.OpenScene("Assets/MusicRoom.unity");zones=UnityEngine.Object.FindObjectsByType<DrumSound>(FindObjectsSortMode.None).Select(d=>d.GetComponent<Collider>()).Where(c=>c!=null).OrderBy(c=>c.name).ToArray();Physics.SyncTransforms();for(int i=0;i<zones.Length;i++)for(int j=i+1;j<zones.Length;j++)baseline[Key(i,j)]=Depth(zones[i],zones[j]);
foreach(var b in zones.OfType<BoxCollider>()){
var initial=b.size;var sc=b.transform.lossyScale;for(int axis=0;axis<3;axis++)foreach(int sign in new[]{-1,1}){var size=b.size;var center=b.center;var nextSize=size;var nextCenter=center;float local=.02f/Mathf.Abs(sc[axis]);nextSize[axis]+=local;nextCenter[axis]+=sign*local*.5f;b.size=nextSize;b.center=nextCenter;if(!Safe()){b.size=size;b.center=center;Debug.Log("TOLERANCE SKIP "+b.name+" axis="+axis+" sign="+sign+" reason=would deepen overlap");}else Debug.Log("TOLERANCE ADD "+b.name+" axis="+axis+" sign="+sign+" 20mm");}
if(b.size==initial)throw new Exception("No safe tolerance increase for "+b.name);Debug.Log("TOLERANCE BOX "+b.name+" oldSize="+Vector3.Scale(initial,sc).ToString("F4")+" newSize="+Vector3.Scale(b.size,sc).ToString("F4"));}
var cym=zones.OfType<MeshCollider>().Single();var original=cym.sharedMesh;var mesh=UnityEngine.Object.Instantiate(original);mesh.name="Cymbal reach with vertical tolerance";var vertices=original.vertices;var up=cym.transform.InverseTransformVector(Vector3.up*.02f);mesh.vertices=vertices.Select(v=>v+up).Concat(vertices.Select(v=>v-up)).ToArray();mesh.triangles=original.triangles.Concat(original.triangles.Select(i=>i+vertices.Length)).ToArray();mesh.RecalculateBounds();cym.sharedMesh=mesh;Physics.BakeMesh(mesh.GetInstanceID(),true);if(!Safe())throw new Exception("Cymbal vertical tolerance increases cross-drum overlap");
AssetDatabase.CreateAsset(mesh,"Assets/Physics/CymbalAllDrumsTolerance.asset");AssetDatabase.SaveAssets();Debug.Log("TOLERANCE CYMBAL added 20mm vertical each way; reach unchanged");
for(int i=0;i<zones.Length;i++)for(int j=i+1;j<zones.Length;j++)Debug.Log("TOLERANCE PAIR "+zones[i].name+" / "+zones[j].name+" before="+baseline[Key(i,j)].ToString("F5")+" after="+Depth(zones[i],zones[j]).ToString("F5"));
EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),"Assets/AllDrumToleranceCandidate.unity");Debug.Log("TOLERANCE ALL SAFE");EditorApplication.Exit(0);
}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}

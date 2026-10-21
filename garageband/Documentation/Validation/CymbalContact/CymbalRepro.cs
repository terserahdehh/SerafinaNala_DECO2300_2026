// Historical test of the retired cymbal detection system.
// Kept for reference only; this does not work with the current game.
using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine;
public static class CymbalRepro{public static void Run(){EditorSceneManager.OpenScene("Assets/MusicRoom.unity");var s=Object.FindFirstObjectByType<CymbalStrikeSurface>();var p=s.transform.position;var n=s.Normal;float r=.012f,bell=.043f;
foreach(int hz in new[]{72,90,120}){
float dt=1f/hz;var state=new CymbalStrikeSurface.StrikeState();Vector3 from=p+n*(bell+r+.02f),above=p+n*(bell+r+dt*.02f),below=p+n*(bell+r-dt*.02f);
if(s.EvaluateStrike(state,from,above,r,dt,dt,out _,out _,out _))throw new System.Exception("early hit");
if(!s.EvaluateStrike(state,above,below,r,dt,2*dt,out _,out _,out var reason))throw new System.Exception("braking strike rejected "+reason);
if(s.Crosses(above,below,r,dt,out _))throw new System.Exception("stateless speed gate should reject this final sample");
Debug.Log("RATE PASS "+hz+"Hz braked 0.04m/s final contact accepted with approach history; isolated final sample rejected");}
EditorApplication.Exit(0);}}

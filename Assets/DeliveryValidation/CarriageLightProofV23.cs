using System;using System.IO;using System.Linq;using System.Collections;using System.Collections.Generic;using UnityEngine;
[DefaultExecutionOrder(5000)]public sealed class CarriageLightProofV23:MonoBehaviour {
 static string Dir => Path.Combine(Application.dataPath,"../../Validation/CarriageLightProofV23");
 SteamJourney j;StudioSpatialRefineV19 ui;MesaDayNightV13 sky;RenderTexture target;int errors;readonly List<string> log=new List<string>();
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Launch(){if(!Application.isEditor&&Environment.GetCommandLineArgs().Contains("-carriage23-proof"))new GameObject("Carriage lamp verification").AddComponent<CarriageLightProofV23>();}
 void Awake(){j=FindObjectOfType<SteamJourney>();j.suppressPersistence=true;j.verificationInputLock=true;Application.runInBackground=true;Directory.CreateDirectory(Dir);Application.logMessageReceived+=OnLog;target=new RenderTexture(1440,900,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB){antiAliasing=2};target.Create();j.view.targetTexture=target;j.view.aspect=1.6f;}
 void LateUpdate(){if(target)j.view.Render();}
 void OnDestroy(){Application.logMessageReceived-=OnLog;}
 void OnLog(string s,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception){errors++;log.Add(s+"\n"+trace);}}
 void Check(bool ok,string s){log.Add((ok?"PASS ":"FAIL ")+s);if(!ok)errors++;}
 Material[] WindowMaterials()=>ui.passengerWindows.Select(r=>r.sharedMaterial).Distinct().ToArray();
 void CheckWindows(string phase,bool night){
  Check(ui.passengerWindows.Length==30,phase+": 30 window renderers");
  for(int i=0;i<j.coaches.Length;i++){
   var windows=ui.passengerWindows.Where(r=>r.transform.IsChildOf(j.coaches[i])).ToArray();
   var block=new MaterialPropertyBlock();
   Check(windows.Length==10&&windows.All(r=>r.enabled&&r.gameObject.activeInHierarchy&&r.sharedMaterial.IsKeywordEnabled("_EMISSION")),phase+": coach "+(i+1)+" has 10 active emissive windows");
   Check(windows.All(r=>{r.GetPropertyBlock(block);float e=block.GetColor("_EmissionColor").r;return night?e>2:e<.15f;}),phase+": coach "+(i+1)+" correct "+(night?"night":"day")+" emission");
  }
 }
 IEnumerator Start(){
  while(!j.Complete)j.InstallCurrent();j.SendMessage("CancelCompletion");j.SetMode(true);yield return new WaitForSeconds(3);
  ui=j.GetComponent<StudioSpatialRefineV19>();sky=FindObjectOfType<MesaDayNightV13>(true);sky.automatic=false;sky.SetTime(.9f);j.routeDistance=0;j.throttle=0;j.brake=1;
  // Look over the physical driving console, which occludes this side at eye level.
  j.ReviewView(j.coaches[1].position+Vector3.up*.30f,7.5f,0,35);yield return new WaitForSeconds(1);CheckWindows("Initial night",true);
  var mats=WindowMaterials();foreach(var m in mats)m.DisableKeyword("_EMISSION");yield return new WaitForEndOfFrame();var off=Capture("01-before-emission-off");
  foreach(var m in mats)m.EnableKeyword("_EMISSION");yield return new WaitForEndOfFrame();var on=Capture("02-night-fixed");CheckPixels(off,on,"front");Destroy(off);Destroy(on);
  j.ReviewView(j.coaches[1].position+Vector3.up*.30f,7.5f,180,12);yield return new WaitForSeconds(.6f);
  foreach(var m in mats)m.DisableKeyword("_EMISSION");yield return new WaitForEndOfFrame();off=Capture("03-opposite-before");foreach(var m in mats)m.EnableKeyword("_EMISSION");yield return new WaitForEndOfFrame();on=Capture("04-opposite-night-fixed");CheckPixels(off,on,"back");Destroy(off);Destroy(on);
  sky.SetTime(.4f);yield return new WaitForSeconds(.4f);CheckWindows("Daylight",false);yield return new WaitForEndOfFrame();Destroy(Capture("05-daylight"));
  // Sample the phase densely: compressing half a day into 120 frames skips the dusk curve.
  float previous=ui.WindowGlow,maxStep=0;for(int i=0;i<=600;i++){sky.SetTime(Mathf.Lerp(.4f,.9f,i/600f));yield return new WaitForEndOfFrame();maxStep=Mathf.Max(maxStep,Mathf.Abs(previous-ui.WindowGlow));previous=ui.WindowGlow;}
  Check(maxStep<.1f,"Smooth dusk emission across 600 phase samples, maximum per-step change="+maxStep.ToString("F3"));
  for(int i=0;i<2;i++){j.SetMode(false);yield return new WaitForSeconds(2.2f);j.SetMode(true);sky.SetTime(.9f);yield return new WaitForSeconds(2.3f);CheckWindows("Mode return "+(i+1),true);}
  j.throttle=.55f;j.brake=0;float start=j.routeDistance;yield return new WaitForSeconds(5);Check(j.routeDistance!=start,"Train still drives");CheckWindows("Moving at night",true);
  j.ReviewView(new Vector3(0,3.5f,-3),43,12,24);yield return new WaitForSeconds(.6f);yield return new WaitForEndOfFrame();Destroy(Capture("06-night-overview"));
  var bgm=j.GetComponent<JourneyBackgroundMusic>();Check(bgm&&bgm.Playing,"BGM remains playing");log.Add("Errors="+errors);File.WriteAllLines(Dir+"/verification.txt",log);Application.Quit(errors==0?0:2);
 }
 // Compare actual rendered window centers, not merely the requested light values.
 void CheckPixels(Texture2D off,Texture2D on,string side){
  for(int k=0;k<j.coaches.Length;k++){
   var windows=ui.passengerWindows.Where(r=>r.transform.IsChildOf(j.coaches[k])&&Vector3.Dot(r.transform.forward,j.view.transform.position-r.bounds.center)<0).ToArray();int measured=0;float delta=0;
   foreach(var r in windows){var p=j.view.WorldToScreenPoint(r.bounds.center);int x=Mathf.RoundToInt(p.x),y=Mathf.RoundToInt(p.y);if(p.z<=0||x<3||y<3||x>=on.width-3||y>=on.height-3)continue;
    float d=0;for(int a=-2;a<=2;a++)for(int b=-2;b<=2;b++)d+=on.GetPixel(x+a,y+b).r-off.GetPixel(x+a,y+b).r;delta+=d/25;measured++;
   }
   delta/=Mathf.Max(1,measured);Check(measured==5&&delta>.10f,"Rendered "+side+" coach "+(k+1)+" windows="+measured+" mean red brightness gain="+delta.ToString("F3"));
  }
 }
 Texture2D Capture(string name){var old=RenderTexture.active;RenderTexture.active=target;var t=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,t.width,t.height),0,0);t.Apply();File.WriteAllBytes(Dir+"/"+name+".png",t.EncodeToPNG());RenderTexture.active=old;return t;}
}

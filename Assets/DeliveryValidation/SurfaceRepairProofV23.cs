using System;using System.IO;using System.Linq;using System.Collections;using System.Collections.Generic;using UnityEngine;
[DefaultExecutionOrder(5000)]public sealed class SurfaceRepairProofV23:MonoBehaviour {
 static string Dir => Path.Combine(Application.dataPath,"../../Validation/SurfaceRepairProofV23");
 SteamJourney j;int errors;RenderTexture proofTarget;readonly List<string> log=new List<string>();
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Launch(){if(!Application.isEditor&&Environment.GetCommandLineArgs().Any(a=>a=="-surface23-proof"||a=="-surface23-preview"))new GameObject("Surface repair proof V23").AddComponent<SurfaceRepairProofV23>();}
 void Awake(){j=FindObjectOfType<SteamJourney>();j.suppressPersistence=true;Application.runInBackground=true;Application.logMessageReceived+=OnLog;Directory.CreateDirectory(Dir);if(Environment.GetCommandLineArgs().Contains("-surface23-proof")){proofTarget=new RenderTexture(1440,900,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB){antiAliasing=2};proofTarget.Create();j.view.targetTexture=proofTarget;j.view.aspect=1.6f;}}
 void OnDestroy(){Application.logMessageReceived-=OnLog;}
 // A hidden Windows player does not present its swap chain. Render every simulation frame
 // into the persistent target, rather than re-rendering only when taking a screenshot.
 void LateUpdate(){if(proofTarget&&j&&j.view)j.view.Render();}
 void OnLog(string s,string trace,LogType t){if(t==LogType.Error||t==LogType.Exception){errors++;log.Add("ERROR "+s+" "+trace);}}
 void Check(bool b,string s){log.Add((b?"PASS ":"FAIL ")+s);if(!b)errors++;Debug.Log(log.Last());}
 void WorkshopView(){j.cutaway=true;j.inspectionMode=2;j.running=false;j.paused=true;j.ReviewView(new Vector3(.5f,2.3f,0),9.5f,25,24);j.SendMessage("ApplyCamera",1f);}
 IEnumerator Start(){
  bool proof=Environment.GetCommandLineArgs().Contains("-surface23-proof");j.verificationInputLock=proof;
  while(!j.Complete)j.InstallCurrent();j.SendMessage("CancelCompletion");WorkshopView();
  int[] frames={0,1,3,7,15,40,90};int f=0;
  for(int k=0;k<=90;k++){yield return new WaitForEndOfFrame();if(k==frames[f]){Capture("startup-"+k.ToString("D3"));Sample("startup "+k);if(f<frames.Length-1)f++;}}
  var arrival=j.GetComponent<JourneySceneArrival>();arrival.manual=true;arrival.Request();yield return new WaitForEndOfFrame();arrival.SetProgress(1);yield return new WaitForEndOfFrame();Capture("arrival-material-before-restore");arrival.Cancel();yield return new WaitForEndOfFrame();Capture("arrival-material-after-restore");arrival.manual=false;
  j.SetMode(true);yield return new WaitForSeconds(3);var sky=FindObjectOfType<MesaDayNightV13>(true);sky.automatic=false;sky.SetTime(.4f);j.ReviewView(new Vector3(0,3.5f,-3),49,12,25);
  if(!proof){j.throttle=.55f;j.brake=0;yield break;}
  yield return new WaitForSeconds(1);yield return Shot("01-overview");
  // Freeze the train and sky for repeatable low-angle surface checks.
  j.throttle=0;j.brake=1;
  long triangles=j.sandboxRoot.GetComponentsInChildren<MeshFilter>().Where(x=>x.sharedMesh&&x.GetComponent<Renderer>()&&x.GetComponent<Renderer>().enabled).Sum(x=>Enumerable.Range(0,x.sharedMesh.subMeshCount).Where(s=>x.sharedMesh.GetTopology(s)==UnityEngine.MeshTopology.Triangles).Sum(s=>(long)x.sharedMesh.GetIndexCount(s))/3);
  log.Add("Miniature triangles="+triangles);Check(triangles>=1000000&&triangles<=1300000,"Miniature within the 1M to 1.3M triangle budget");Check(!j.sandboxRoot.GetComponentsInChildren<LODGroup>().Any(g=>g.enabled),"No LOD switching");
  var root=j.sandboxRoot.transform.Find("Mesa original canyon · miniature railway V12");
  // Both long sides and both short sides, seen from low eye height.
  float[] angles={12,100,190,280};for(int i=0;i<4;i++){j.ReviewView(new Vector3(0,2.1f,-3),44,angles[i],12);yield return new WaitForSeconds(.7f);yield return Shot("02-edge-orbit-"+i);}
  Vector3[] targets={new Vector3(-13.9f,1.5f,9.5f),new Vector3(13.9f,1.5f,-1),new Vector3(8.5f,1.5f,-7.7f),new Vector3(10.98f,1.5f,-7.68f),new Vector3(-9.1f,1.4f,-1.4f)};
  for(int i=0;i<targets.Length;i++){j.ReviewView(root.TransformPoint(targets[i]),9,25+i*35,13);yield return new WaitForSeconds(.6f);yield return Shot("03-rock-close-"+i);}
  j.ReviewView(new Vector3(0,3.5f,-3),49,12,25);j.throttle=.7f;j.brake=0;float start=j.routeDistance;yield return new WaitForSeconds(5);Check(j.speed>0&&Mathf.Abs(j.routeDistance-start)>1,"Existing train route runs");sky.SetTime(.9f);yield return new WaitForSeconds(1);yield return Shot("04-night");
  j.SetMode(false);WorkshopView();for(int k=0;k<150;k++){yield return new WaitForEndOfFrame();if(k==0||k==1||k==7||k==40||k==90||k==149){Capture("return-"+k.ToString("D3"));Sample("return "+k);}}
  Check(!arrival.Active,"Scene scan completes and releases temporary materials");var mood=FindObjectOfType<StudioWorkshopMood>();Check(mood.key.enabled&&mood.fill.enabled&&mood.rim.enabled&&RenderSettings.customReflectionTexture==mood.workshopReflection,"Workshop lighting and reflection restored");
  var bgm=j.GetComponent<JourneyBackgroundMusic>();Check(bgm&&bgm.Playing,"BGM remains playing across mode switches");
  var times=new List<float>();for(int k=0;k<150;k++){yield return null;times.Add(Time.unscaledDeltaTime*1000);}times.Sort();log.Add("Workshop frame ms median="+times[75].ToString("F2")+" p95="+times[142].ToString("F2"));log.Add("GPU="+SystemInfo.graphicsDeviceName);log.Add("Errors="+errors);File.WriteAllLines(Dir+"/verification.txt",log);Application.Quit(errors==0?0:2);
 }
 void Sample(string prefix){var m=FindObjectOfType<StudioWorkshopMood>();log.Add(prefix+" key="+m.key.intensity+" fill="+m.fill.intensity+" rim="+m.rim.intensity+" ambient="+RenderSettings.ambientProbe[0,0]+" reflection="+RenderSettings.customReflectionTexture.name+" intensity="+RenderSettings.reflectionIntensity);}
 IEnumerator Shot(string name){yield return new WaitForEndOfFrame();Capture(name);}
 void Capture(string name){if(!proofTarget)return;var previous=RenderTexture.active;RenderTexture.active=proofTarget;var t=new Texture2D(proofTarget.width,proofTarget.height,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,t.width,t.height),0,0);t.Apply();File.WriteAllBytes(Dir+"/"+name+".png",t.EncodeToPNG());RenderTexture.active=previous;Destroy(t);}
}

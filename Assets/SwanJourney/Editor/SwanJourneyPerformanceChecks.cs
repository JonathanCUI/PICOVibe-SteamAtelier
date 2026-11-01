using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ByteDance.PICO.SpatialAdapter;
using ByteDance.PICO.SpatialAdapter.Component;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SwanJourney.Editor
{
    public static class SwanJourneyPerformanceChecks
    {
        [Serializable] sealed class Report { public bool passed, nativePerformanceValidated; public List<string> checks=new List<string>(); }
        public static void Run()
        {
            var report=new Report();
            var main=EditorSceneManager.OpenScene(SwanJourneyBuild.ScenePath);
            var journey=UnityEngine.Object.FindObjectOfType<SteamJourney>(true);
            var fps=UnityEngine.Object.FindObjectOfType<SwanAtelier.SwanFpsMeshText>(true);
            Check(fps && fps.variants.Length==33,"FPS keeps all digit variants after scene generation");
            Check(fps.GetComponent<MeshFilter>().sharedMesh==AssetDatabase.LoadAssetAtPath<Mesh>(SwanJourneyBuild.Generated+"/FpsFixed.asset"),
                "Fixed FPS letters retain their original stroke mesh, never a decimated surface");
            for(int i=0;i<33;i++)
                Check(fps.variants[i] && fps.variants[i].GetComponent<MeshFilter>().sharedMesh==AssetDatabase.LoadAssetAtPath<Mesh>(SwanJourneyBuild.Generated+"/FpsGlyph"+(i%11)+".asset"),
                    "FPS slot "+(i/11)+" glyph "+(i%11)+" retains its complete original stroke mesh");
            var cpuDiagnostics=UnityEngine.Object.FindObjectOfType<SwanJourneyCpuDiagnostics>(true);
            Check(cpuDiagnostics && cpuDiagnostics.journey==journey && cpuDiagnostics.interaction,
                "CPU diagnostics bind the same Journey and input owner without changing content");
            Check(!SwanJourneyCpuTimings.Enabled,"CPU timings do not activate in the editor");
            var timingOwner=new object();var otherOwner=new object();
            try
            {
                Check(SwanJourneyCpuTimings.BeginSession(timingOwner),"One diagnostic session can own callback timing");
                Check(!SwanJourneyCpuTimings.BeginSession(otherOwner),"A second diagnostic owner is rejected");
                using(SwanJourneyCpuTimings.Measure(SwanJourneyCpuTimings.Stage.Input)) { }
                Check(SwanJourneyCpuTimings.Read(SwanJourneyCpuTimings.Stage.Input).calls==1,"Measured input callback is accounted once");
                try { using(SwanJourneyCpuTimings.Measure(SwanJourneyCpuTimings.Stage.NativeUi)) { throw new InvalidOperationException("timing fixture"); } }
                catch(InvalidOperationException) { }
                Check(SwanJourneyCpuTimings.Read(SwanJourneyCpuTimings.Stage.NativeUi).calls==1,"Exceptions close diagnostic scopes without swallowing the callback failure");
                var stale=SwanJourneyCpuTimings.Measure(SwanJourneyCpuTimings.Stage.TransformFilter);
                SwanJourneyCpuTimings.Reset();stale.Dispose();
                Check(SwanJourneyCpuTimings.Read(SwanJourneyCpuTimings.Stage.TransformFilter).calls==0,"A reset excludes a stale in-flight scope");
                SwanJourneyCpuTimings.EndSession(otherOwner);
                Check(SwanJourneyCpuTimings.Enabled,"Non-owner cannot stop diagnostic collection");
            }
            finally { SwanJourneyCpuTimings.EndSession(timingOwner); }
            using(SwanJourneyCpuTimings.Measure(SwanJourneyCpuTimings.Stage.Input)) { }
            Check(!SwanJourneyCpuTimings.Enabled && SwanJourneyCpuTimings.Read(SwanJourneyCpuTimings.Stage.Input).calls==0,"Disabled scopes leave counters empty");
            var physicsBudget=UnityEngine.Object.FindObjectOfType<SwanJourneyPhysicsBudget>(true);
            Check(physicsBudget && physicsBudget.validatedStaticPhysics && physicsBudget.journey==journey,"Only the derived Swan scene owns the static-physics policy");
            Check(main.GetRootGameObjects().All(r=>!SwanJourneyPhysicsBudget.HasDynamics(r)),"Workshop has no Unity rigid body, articulation, character controller or cloth");
            var canyonPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(SwanJourneyBuild.EnvironmentPrefabPath);
            Check(canyonPrefab && !SwanJourneyPhysicsBudget.HasDynamics(canyonPrefab),"Deferred canyon also has no Unity dynamics");
            bool previousSimulation=Physics.autoSimulation;
            int collidersBefore=main.GetRootGameObjects().Sum(r=>r.GetComponentsInChildren<Collider>(true).Count(c=>c.enabled));
            var blocker=new GameObject("Physics policy regression fixture");
            try
            {
                var body=blocker.AddComponent<Rigidbody>();
                Check(!physicsBudget.TryEnable() && Physics.autoSimulation==previousSimulation,"An unexpected rigid body blocks the optimization without changing physics");
                UnityEngine.Object.DestroyImmediate(body);
                Check(physicsBudget.TryEnable() && !Physics.autoSimulation,"Validated Swan scene skips automatic Unity simulation");
                Check(main.GetRootGameObjects().Sum(r=>r.GetComponentsInChildren<Collider>(true).Count(c=>c.enabled))==collidersBefore,"Native hit-test colliders remain enabled and unchanged");
                var second=blocker.AddComponent<SwanJourneyPhysicsBudget>();second.journey=journey;second.validatedStaticPhysics=true;
                Check(!second.TryEnable(),"A second policy cannot take ownership of global physics settings");
                second.Restore();Check(!Physics.autoSimulation,"A non-owner cannot restore another owner's policy");
                physicsBudget.Restore();Check(Physics.autoSimulation==previousSimulation,"Scene shutdown restores the original Unity simulation setting");
                physicsBudget.validatedStaticPhysics=false;
                Check(!physicsBudget.TryEnable(),"Unvalidated content cannot disable simulation");
                physicsBudget.validatedStaticPhysics=true;
            }
            finally { physicsBudget.Restore();UnityEngine.Object.DestroyImmediate(blocker);Physics.autoSimulation=previousSimulation; }
            var collisionReport=JsonUtility.FromJson<SwanJourneyColliderBudget.Report>(File.ReadAllText(SwanJourneyBuild.Output+"/collider-budget.json"));
            Check(collisionReport.passed && collisionReport.removed>0 && collisionReport.meshCollidersBefore-collisionReport.meshCollidersAfter==collisionReport.removed,"Redundant collision cleanup is nonempty and every removed volume is accounted for");
            var fixture=GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var box=fixture.GetComponent<BoxCollider>();
                var mesh=fixture.AddComponent<MeshCollider>();mesh.sharedMesh=fixture.GetComponent<MeshFilter>().sharedMesh;
                Check(SwanJourneyColliderBudget.Covers(mesh,box),"An existing same-shape primitive covers its redundant mesh collider");
                box.size*=.5f;
                Check(!SwanJourneyColliderBudget.Covers(mesh,box),"A smaller primitive must not erase the uncovered mesh hit region");box.size*=2;
                box.isTrigger=true;
                Check(!SwanJourneyColliderBudget.Covers(mesh,box),"Trigger and solid colliders are not interchangeable");box.isTrigger=false;
                box.enabled=false;
                Check(!SwanJourneyColliderBudget.Covers(mesh,box),"Disabled primitive cannot replace an enabled mesh collider");box.enabled=true;
                var proxy=new GameObject("Swan positive-scale hit proxy");proxy.transform.SetParent(fixture.transform,false);
                fixture.transform.localScale=new Vector3(-1,2,1);proxy.transform.localScale=new Vector3(-1,1,1);
                var positive=proxy.AddComponent<BoxCollider>();
                Check(SwanJourneyColliderBudget.Covers(mesh,positive),"Reflected visible mesh is covered by its positive-scale child hit proxy");
                proxy.SetActive(false);
                Check(!SwanJourneyColliderBudget.Covers(mesh,positive),"Inactive child hit proxy cannot replace the mesh");
            }
            finally { UnityEngine.Object.DestroyImmediate(fixture); }
            var legacyOwner=new GameObject("Legacy hit cleanup fixture");
            try
            {
                var refs=new HashSet<UnityEngine.Object>();
                var old=new GameObject("Old desktop mesh hit");old.transform.SetParent(legacyOwner.transform,false);
                var hit=old.AddComponent<MeshCollider>();
                Check(!SwanJourneyLegacyHitBudget.CanRemove(hit,legacyOwner.transform,refs),"An orphan hit is retained if its part has no current visible hit surface");
                var visible=GameObject.CreatePrimitive(PrimitiveType.Cube);visible.transform.SetParent(legacyOwner.transform,false);
                visible.AddComponent<SpatialAdapterHoverEffect>();
                Check(SwanJourneyLegacyHitBudget.CanRemove(hit,legacyOwner.transform,refs),"A rendererless unreferenced leaf can yield to current visible native hit geometry");
                visible.SetActive(false);
                Check(!SwanJourneyLegacyHitBudget.CanRemove(hit,legacyOwner.transform,refs),"Hidden replacement geometry cannot replace a legacy hit");visible.SetActive(true);
                var nested=visible.AddComponent<LocomotivePart>();
                Check(!SwanJourneyLegacyHitBudget.CanRemove(hit,legacyOwner.transform,refs),"Another movable part cannot supply the replacement hit surface");UnityEngine.Object.DestroyImmediate(nested);
                refs.Add(old.transform);
                Check(!SwanJourneyLegacyHitBudget.CanRemove(hit,legacyOwner.transform,refs),"Serialized transform references preserve the legacy leaf");refs.Clear();
                hit.sharedMesh=visible.GetComponent<MeshFilter>().sharedMesh;hit.convex=true;
                hit.isTrigger=true;
                Check(!SwanJourneyLegacyHitBudget.CanRemove(hit,legacyOwner.transform,refs),"Trigger meshes are never discarded by hit cleanup");hit.isTrigger=false;
                var child=new GameObject("Retained child");child.transform.SetParent(old.transform,false);
                Check(!SwanJourneyLegacyHitBudget.CanRemove(hit,legacyOwner.transform,refs),"A legacy hit with children is retained");UnityEngine.Object.DestroyImmediate(child);
                old.AddComponent<MeshRenderer>();
                Check(!SwanJourneyLegacyHitBudget.CanRemove(hit,legacyOwner.transform,refs),"Visible or behavioral components prevent orphan cleanup");
            }
            finally { UnityEngine.Object.DestroyImmediate(legacyOwner); }
            foreach(var group in journey.modules.Concat(journey.finishing))
            {
                Check(group.preview && group.preview.GetComponentsInChildren<SpatialAdapterHoverEffect>(true).Any(),"Preview retains hover: "+group.title);
                Check(group.objects.Where(g=>g).SelectMany(g=>g.GetComponentsInChildren<SpatialAdapterHoverEffect>(true)).Any(),"Installed group retains hover: "+group.title);
            }
            var engine=journey.engine;
            float phase=engine.phase;
            // First solve settles the existing mechanism's iterative pose inputs.
            for(int i=0;i<5;i++)JourneyMotion.Apply(engine);
            var objects=engine.locomotive.GetComponentsInChildren<Transform>(true);
            foreach(var t in objects)t.hasChanged=false;
            var positions=objects.Select(t=>t.localPosition).ToArray();var rotations=objects.Select(t=>t.localRotation).ToArray();
            JourneyMotion.Apply(engine);
            Check(objects.All(t=>!t.hasChanged),"Stationary mechanism does not dirty child transforms repeatedly");
            engine.phase+=15;JourneyMotion.Apply(engine);
            Check(objects.Any(t=>t.hasChanged),"A real phase change still updates the moving mechanism");
            engine.phase=phase;for(int i=0;i<5;i++)JourneyMotion.Apply(engine);
            Check(objects.Select((t,i)=>Vector3.Distance(t.localPosition,positions[i])<.0001f && Quaternion.Angle(t.localRotation,rotations[i])<.01f).All(v=>v),"Returning to original phase restores every mechanism transform");
            var stage=journey.spatialRoot.parent;
            var sync=stage.GetComponent<SwanJourneyTransformSyncBudget>();
            Check(sync,"Movable workshop has local transform sync filtering");
            sync.FilterNow();sync.FilterNow();
            var inherited=stage.GetComponentsInChildren<Transform>(true).Where(t=>t!=stage&&t.gameObject.activeInHierarchy).ToArray();
            var stageScale=stage.localScale;
            stage.localScale*=.9f;
            sync.FilterNow();
            Check(inherited.Length>0 && sync.SuppressedLastFrame==inherited.Length && stage.hasChanged && inherited.All(t=>!t.hasChanged),
                "Parent-only scaling retains root update and suppresses every unchanged active descendant, independent of scene size");
            var moving=engine.locomotive.transform;var initial=moving.localPosition;
            moving.localPosition+=Vector3.up*.01f;sync.FilterNow();
            Check(moving.hasChanged,"A real local animation is never suppressed");
            moving.localPosition=initial;stage.localScale=stageScale;
            var syncFixture=new GameObject("Transform filtering lifecycle fixture");
            try
            {
                var a=new GameObject("A").transform;a.SetParent(syncFixture.transform,false);
                var b=new GameObject("B").transform;b.SetParent(syncFixture.transform,false);
                var leaf=new GameObject("Leaf").transform;leaf.SetParent(a,false);
                var filter=syncFixture.AddComponent<SwanJourneyTransformSyncBudget>();
                filter.FilterNow();Check(leaf.hasChanged,"First transform observation remains unsuppressed");
                filter.FilterNow();Check(leaf.hasChanged,"Second observation retains activation warmup");
                syncFixture.transform.position+=Vector3.right;
                filter.FilterNow();Check(syncFixture.transform.hasChanged&&!leaf.hasChanged,"Root translation alone clears only unchanged descendants");
                leaf.localRotation=Quaternion.Euler(0,30,0);filter.FilterNow();
                Check(leaf.hasChanged,"Combined local pose read preserves a true rotation change");
                leaf.localScale=new Vector3(1,2,1);filter.FilterNow();
                Check(leaf.hasChanged,"Scale changes remain visible to native synchronization");
                leaf.SetParent(b,false);filter.FilterNow();
                Check(leaf.hasChanged,"Same-local-pose reparenting remains visible");
                b.gameObject.SetActive(false);filter.FilterNow();
                b.gameObject.SetActive(true);leaf.hasChanged=true;filter.FilterNow();
                Check(leaf.hasChanged,"Reactivation restarts unsuppressed warmup");
                filter.FilterNow();Check(leaf.hasChanged,"Reactivation retains its second warmup pass");
                filter.FilterNow();Check(!leaf.hasChanged,"Unchanged local pose can be filtered after reactivation warmup");
                syncFixture.transform.hasChanged=false;leaf.localPosition+=Vector3.up;
                filter.FilterNow();Check(leaf.hasChanged,"Idle-root fast path never clears a new child animation");
                UnityEngine.Object.DestroyImmediate(leaf.gameObject);
                syncFixture.transform.position+=Vector3.right;filter.FilterNow();
                Check(syncFixture.transform.hasChanged,"Destroyed cached children are skipped without blocking the parent update");
                var added=new GameObject("Runtime child").transform;added.SetParent(a,false);filter.FilterNow();
                Check(added.hasChanged,"New uncached objects retain initial native synchronization");
                filter.enabled=false;filter.enabled=true;filter.FilterNow();
                Check(added.hasChanged,"Re-enabled filter rebuilds its object cache with unsuppressed warmup");
            }
            finally { UnityEngine.Object.DestroyImmediate(syncFixture); }
            var boot=EditorSceneManager.OpenScene(SwanJourneyBootScene.Path,OpenSceneMode.Additive);
            try
            {
                var roots=boot.GetRootGameObjects();
                var loader=roots.SelectMany(r=>r.GetComponentsInChildren<SwanJourneyBootstrap>(true)).Single();
                Check(loader.workshopScene==SwanJourneyBuild.ScenePath && loader.loading,"Boot loads only workshop via path, without canyon references");
                var dependencies=AssetDatabase.GetDependencies(boot.path,true);
                Check(!dependencies.Contains(SwanJourneyBuild.ScenePath)&&!dependencies.Contains(SwanJourneyBuild.EnvironmentScenePath),"Boot asset dependencies contain neither large scene");
                Check(roots.Sum(r=>r.GetComponentsInChildren<Transform>(true).Length)<30,"Boot scene has fewer than 30 nodes");
                Check(roots.SelectMany(r=>r.GetComponentsInChildren<SpatialCamera>(true)).Count()==1 && !main.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<SpatialCamera>(true)).Any(),"Exactly one Stage owner remains in boot; workshop cannot open another window");
                var textBridge=loader.GetComponent<SwanJourneyNativeUi>();
                Check(textBridge,"Boot has the same tested native TMP buffer guard as the workshop");
                // EditMode does not dispatch LateUpdate. Initialize discovery,
                // then explicitly run the buffer refresh after activation edits.
                textBridge.PrepareForExport();
                foreach(var label in roots.SelectMany(r=>r.GetComponentsInChildren<TMPro.TMP_Text>(true)))
                {
                    label.gameObject.SetActive(true);label.ForceMeshUpdate(true,true);
                    Check(label.textInfo.lineCount==1 && label.mesh.bounds.size.x<=.521f && label.mesh.bounds.size.y<=.031f,
                        "Loading label fits one horizontal line within 52 x 3.1 cm: "+label.text);
                }
                var view=loader.loading;view.Show(true);view.SetResourceProgress(.4f);
                Check(!SwanAtelier.SwanHeadPose.IsValid(new Pose(Vector3.zero,new Quaternion(0,0,0,0))),"Disconnected zero quaternion cannot position either HUD");
                Check(!SwanAtelier.SwanHeadPose.IsValid(new Pose(new Vector3(float.NaN,0,0),Quaternion.identity)),"Invalid head coordinates cannot move either HUD");
                var headPose=new Pose(new Vector3(2,1.7f,-3),Quaternion.Euler(15,70,0));
                view.ApplyHeadPose(headPose);
                var viewLocal=Quaternion.Inverse(headPose.rotation)*(view.transform.position-headPose.position);
                Check(Vector3.Distance(viewLocal,new Vector3(0,0,1.1f))<.0001f && Quaternion.Angle(view.transform.rotation,headPose.rotation)<.01f,"Loading panel stays centered 1.1 metres ahead after translated and rotated head poses");
                view.ApplyHeadPose(new Pose(Vector3.zero,new Quaternion(0,0,0,0)));
                Check(Vector3.Distance(view.transform.position,headPose.position+headPose.rotation*new Vector3(0,0,1.1f))<.0001f,"Tracking failure cannot snap loading text to floor origin");
                Check(view.surface.activeSelf&&view.canyonTitle.activeSelf&&!view.workshopTitle.activeSelf&&view.resourceLabel.activeSelf,"Canyon resource phase displays the correct title and label");
                Check(Mathf.Abs(view.fill.localScale.x-view.width*.2f)<.00001f,"Resource progress occupies its first half; native import never appears completed");
                view.PreparingSpace();Check(view.nativeLabel.activeSelf&&!view.resourceLabel.activeSelf&&view.fill.localScale.x<view.width,"Native preparation is a separate phase, not false completion");
                view.Fail();Check(view.failedLabel.activeSelf,"Failed load remains visibly distinct");
                view.Hide();Check(!view.surface.activeSelf,"Successful handoff hides the overlay without deleting its exported nodes");
                textBridge.RefreshNow();
                Check(roots.SelectMany(r=>r.GetComponentsInChildren<Collider>(true)).Count()==0,"Loading panel cannot intercept gaze or pinch input");
                Check(roots.SelectMany(r=>r.GetComponentsInChildren<TMPro.TMP_Text>(true)).All(t=>SwanJourneyNativeUi.HasUploadableMesh(t.mesh)),"All active and inactive phase labels retain uploadable glyph buffers");
                var response=new SwanJourneyMoveResponse();response.Begin(3);
                Check(response.Step(Vector3.right*.1f,3,.02f).x>.29f,"Distant exhibit moves about 30 cm for a 10 cm hand pull");
                for(int i=0;i<100;i++)response.Step(Vector3.zero,.5f,.02f);
                Check(response.Step(Vector3.right*.1f,.5f,.02f).x<.101f,"Close exhibit returns smoothly to one-to-one movement");
                Check(response.Step(Vector3.zero,3,.02f)==Vector3.zero,"Changing distance without hand motion never teleports the exhibit");
                var pose=Quaternion.identity;var point=new Vector3(1,0,1);
                for(int i=0;i<100;i++)pose=SwanJourneyMoveResponse.FacingPlayer(point,pose,Vector3.zero,.02f);
                Check(Quaternion.Angle(pose,Quaternion.Euler(0,45,0))<.1f,"Single-hand lateral move gradually turns the front toward the player without pitch");
                report.passed=true;
                Debug.Log("SWAN_JOURNEY_PERFORMANCE_CHECKS_PASS checks="+report.checks.Count);
            }
            finally
            {
                EditorSceneManager.CloseScene(boot,true);
                File.WriteAllText(SwanJourneyBuild.Output+"/performance-checks.json",JsonUtility.ToJson(report,true));
            }
            void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);report.checks.Add(message);}
        }
    }
}

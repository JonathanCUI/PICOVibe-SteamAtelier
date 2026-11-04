using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SwanJourney.Editor
{
    // Derive corrected display meshes without touching the imported models or colliders.
    public static class SwanSurfaceRepair
    {
        const string Folder = "Assets/SwanJourney/Generated/SurfaceRepairs";
        [Serializable] class Record { public string source, output; public int changedVertices, triangles, bindings; }
        [Serializable] class Report
        {
            public string scope = "Firebox foundation ring and running-board outer trim; preview and installed copies";
            public bool uvPreserved = true, indicesPreserved = true, materialSlotsPreserved = true;
            public float foundationOuterSeparation = .0044f, footplateOuterSeparation = .006f;
            public List<Record> meshes = new List<Record>();
        }

        public static void Apply(SteamJourney journey)
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/SwanJourney/Generated", "SurfaceRepairs");
            var report = new Report();
            var filters = UnityEngine.Object.FindObjectsOfType<MeshFilter>(true)
                .Where(f => f.gameObject.scene == journey.gameObject.scene).ToArray();

            Repair("Assets/AtelierJourney/StudioVisual/SurfaceV2/Meshes/Surface_0883.asset", 0,
                new[] { Vector3.zero }, 96, 1.04f, false);
            Repair("Assets/SwanJourney/Generated/RigidBatches/Rigid_0123.asset", 2,
                new[] { new Vector3(.018375f,-.6125f,.64f), new Vector3(.018375f,-.6125f,-.64f) }, 192, 1.04f, false);
            Repair("Assets/SwanJourney/Generated/RigidBatches/Rigid_0081.asset", 2,
                new[] { new Vector3(-1.33f,1.43f,-.64f) }, 96, 1.04f, false);
            // The cast support below the chequer plate shares both outside faces with the plate.
            // Inset the whole support cross-section, retaining its closed bevels and UV seams.
            Repair("Assets/SwanJourney/Generated/RigidBatches/Rigid_0133.asset", 10,
                new[] { Vector3.zero }, 374, (1.305f-.006f)/1.305f, true);
            Repair("Assets/SwanJourney/Generated/RigidBatches/Rigid_0003.asset", 10,
                new[] { Vector3.zero }, 374, (1.305f-.006f)/1.305f, true);
            Directory.CreateDirectory("Docs");
            File.WriteAllText("Docs/XR-Surface-Repair-20261104.json", JsonUtility.ToJson(report,true));
            AssetDatabase.SaveAssets();
            Debug.Log("XR_SURFACE_REPAIR_PASS meshes="+report.meshes.Count+" uv=true topology=true slots=true");

            void Repair(string path, int slot, Vector3[] centres, int expected, float zScale, bool entireSlot)
            {
                var source = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (!source) throw new InvalidOperationException("Missing surface repair source: "+path);
                string output = Folder+"/"+Path.GetFileNameWithoutExtension(path)+"_Separated.asset";
                var result = UnityEngine.Object.Instantiate(source);
                result.name = source.name+" · separated joint surfaces";
                var vertices = result.vertices;
                var normals = result.normals;
                var tangents = result.tangents;
                var candidates = new HashSet<int>(source.GetTriangles(slot));
                float centreX=0,xScale=1;
                if(entireSlot)
                {
                    float min=candidates.Min(i=>vertices[i].x),max=candidates.Max(i=>vertices[i].x);
                    centreX=(min+max)*.5f;xScale=(max-min-.012f)/(max-min);
                }
                int changed = 0;
                foreach (int i in candidates)
                    foreach (var centre in centres)
                    {
                        var p = vertices[i]-centre;
                        if (!entireSlot && (Mathf.Abs(p.x)>.6551f || Mathf.Abs(p.y)>.0326f || Mathf.Abs(p.z)>.1101f)) continue;
                        p.z *= zScale; vertices[i]=p+centre; changed++;
                        if(entireSlot) vertices[i].x=centreX+(vertices[i].x-centreX)*xScale;
                        if (normals.Length==vertices.Length) { var n=normals[i]; n.x/=xScale;n.z/=zScale; normals[i]=n.normalized; }
                        if (tangents.Length==vertices.Length)
                        {
                            var t=tangents[i];var xyz=new Vector3(t.x*xScale,t.y,t.z*zScale).normalized;
                            tangents[i]=new Vector4(xyz.x,xyz.y,xyz.z,t.w);
                        }
                        break;
                    }
                if (changed!=expected) throw new InvalidOperationException($"Repair selection changed for {path}: {changed}, expected {expected}");
                result.vertices=vertices;result.normals=normals;result.tangents=tangents;result.RecalculateBounds();
                for(int s=0;s<source.subMeshCount;s++)
                    if(!source.GetIndices(s).SequenceEqual(result.GetIndices(s))) throw new InvalidOperationException("Surface repair changed topology");
                for(int channel=0;channel<8;channel++)
                {
                    var a=new List<Vector4>();var b=new List<Vector4>();source.GetUVs(channel,a);result.GetUVs(channel,b);
                    if(!a.SequenceEqual(b)) throw new InvalidOperationException("Surface repair changed UVs");
                }
                var saved=AssetDatabase.LoadAssetAtPath<Mesh>(output);
                if(saved) { EditorUtility.CopySerialized(result,saved);UnityEngine.Object.DestroyImmediate(result);EditorUtility.SetDirty(saved); }
                else { saved=result;AssetDatabase.CreateAsset(saved,output); }
                int count=0;
                foreach(var f in filters)
                    if(f.sharedMesh==source || f.sharedMesh==saved) { f.sharedMesh=saved;EditorUtility.SetDirty(f);count++; }
                if(count==0) throw new InvalidOperationException("Unbound surface repair: "+path);
                report.meshes.Add(new Record{source=path,output=output,changedVertices=changed,triangles=source.triangles.Length/3,bindings=count});
            }
        }

        // Reproducible editor-only verification without an Android rebuild.
        public static void RepairAndAudit()
        {
            var scene=EditorSceneManager.OpenScene("Assets/SwanJourney/Scenes/SteamJourneyXRTrial.unity");
            Apply(UnityEngine.Object.FindObjectOfType<SteamJourney>(true));
            EditorSceneManager.SaveScene(scene);
            SwanSurfaceAudit.Export();
        }
    }
}

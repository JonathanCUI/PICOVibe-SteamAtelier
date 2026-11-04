using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SwanJourney.Editor
{
    internal static class SwanXrSketchSheetBuild
    {
        const string Folder="Assets/SwanJourney/Branding/SketchSheet/";
        internal static void Apply(StudioPartCards cards)
        {
            var frame=cards.spatialAnchor.GetComponentsInChildren<MeshRenderer>(true)
                .Single(r=>r.transform.parent==cards.spatialAnchor);
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"Firebox pencil study.png");
            var fibre=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"Paper fibre relief.png");
            var shader=AssetDatabase.LoadAssetAtPath<Shader>(Folder+"SketchSheet.shader");
            var open=ImportPaperSound("打开纸.mp3");
            var next=ImportPaperSound("翻过去.mp3");
            var previous=ImportPaperSound("翻回来.mp3");
            var close=ImportPaperSound("关闭纸.mp3");
            if(!texture || !fibre || !shader)throw new InvalidOperationException("Missing firebox paper artwork or fibre relief");
            int count=cards.journey.modules.Length+cards.journey.finishing.Length;
            if(count!=17)throw new InvalidOperationException("Sketchbook expects the 17-step workshop.");
            for(int index=1;index<count;index++){
                string path=Folder+"Resources/"+SwanSketchSheet.ResourcePath(index)+".png";
                if(!AssetDatabase.LoadAssetAtPath<Texture2D>(path))throw new InvalidOperationException("Missing workshop sketch: "+path);
            }
            var material=AssetDatabase.LoadAssetAtPath<Material>(Folder+"Firebox paper.mat");
            if(!material){material=new Material(shader);AssetDatabase.CreateAsset(material,Folder+"Firebox paper.mat");}
            material.SetTexture("_MainTex",texture);
            material.SetTexture("_PaperFibre",fibre);
            material.SetFloat("_SupportHeight",SwanSketchSheet.SupportHeight);
            EditorUtility.SetDirty(material);
            var workshop=cards.journey.workshopRoot.transform;
            var desk=cards.GetComponent<StudioSpatialConsoleV18>().console;
            if(!SwanJourneyPresentation.TryBounds(desk,workshop,out var deskBounds))
                throw new InvalidOperationException("Missing console bounds for the left-hand paper position.");
            // Authored +X is the player's left. Keep the paper outside the controls,
            // closer than the assembly platform, and tilted back like the approved B view.
            cards.spatialAnchor.position=workshop.TransformPoint(new Vector3(
                deskBounds.max.x+1.50f,deskBounds.center.y+.85f,deskBounds.center.z+.35f));
            cards.spatialAnchor.rotation=workshop.rotation*Quaternion.Euler(22,165,-4);
            var page=new GameObject("Workshop sketch sheet",typeof(MeshFilter),typeof(MeshRenderer),typeof(BoxCollider),
                typeof(AudioSource),typeof(SwanJourneyTarget),typeof(SwanSketchSheet));
            page.transform.SetParent(cards.spatialAnchor,false);
            page.transform.localPosition=new Vector3(0,0,-.06f);page.transform.localScale=Vector3.one*2.65f;
            page.GetComponent<MeshFilter>().sharedMesh=PageMesh();
            var surface=page.GetComponent<MeshRenderer>();
            surface.sharedMaterial=material;surface.shadowCastingMode=ShadowCastingMode.Off;surface.receiveShadows=false;
            surface.lightProbeUsage=LightProbeUsage.Off;surface.reflectionProbeUsage=ReflectionProbeUsage.Off;
            page.GetComponent<BoxCollider>().size=new Vector3(1,1.5f,.045f);
            page.GetComponent<SwanJourneyTarget>().kind=SwanJourneyTargetKind.SketchSheet;
            cards.sketchSheet=page.GetComponent<SwanSketchSheet>();cards.sketchSheet.originalFrame=frame.gameObject;
            cards.sketchSheet.openPaper=open;cards.sketchSheet.turnNext=next;
            cards.sketchSheet.turnPrevious=previous;cards.sketchSheet.closePaper=close;
            var audio=page.GetComponent<AudioSource>();audio.playOnAwake=false;audio.loop=false;
            audio.spatialBlend=.75f;audio.minDistance=.45f;audio.maxDistance=3;audio.dopplerLevel=0;
            audio.rolloffMode=AudioRolloffMode.Linear;audio.reverbZoneMix=0;
            frame.gameObject.SetActive(false);
        }

        static AudioClip ImportPaperSound(string name)
        {
            string path=Folder+name;
            var importer=AssetImporter.GetAtPath(path) as AudioImporter;
            if(!importer)throw new InvalidOperationException("Missing paper sound: "+name);
            var settings=importer.defaultSampleSettings;
            // These four short sounds total under four seconds. Decode once,
            // preserving sample rate, so a page turn never starts a decoder.
            settings.loadType=AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat=AudioCompressionFormat.PCM;
            settings.sampleRateSetting=AudioSampleRateSetting.PreserveSampleRate;
            settings.preloadAudioData=true;
            importer.defaultSampleSettings=settings;importer.forceToMono=true;importer.SaveAndReimport();
            var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if(!clip || clip.channels!=1)throw new InvalidOperationException("Paper sound import failed: "+name);
            Debug.Log($"XR_PAPER_CLIP {name} seconds={clip.length:F3} channels={clip.channels} rate={clip.frequency} load={clip.loadType}");
            return clip;
        }

        static Mesh PageMesh()
        {
            string path=Folder+"Study sheet grid.asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            // Include the soft trailing edge in stereo frustum culling, also on pre-existing trial assets.
            var bounds=new Bounds(Vector3.zero,new Vector3(3.8f,2.8f,3.8f));
            const int columns=16,rows=48,stride=columns+1;
            if(mesh && mesh.vertexCount==stride*(rows+1)){
                if(mesh.bounds!=bounds){mesh.bounds=bounds;EditorUtility.SetDirty(mesh);}return mesh;
            }
            var v=new Vector3[stride*(rows+1)];var uv=new Vector2[v.Length];
            var indices=new int[columns*rows*6];int n=0;
            for(int y=0;y<=rows;y++)for(int x=0;x<=columns;x++)
            {
                int k=y*stride+x;
                uv[k]=new Vector2((float)x/columns,(float)y/rows);
                v[k]=new Vector3(uv[k].x-.5f,(uv[k].y-.5f)*1.5f);
                if(y==rows || x==columns)continue;
                indices[n++]=k;indices[n++]=k+stride;indices[n++]=k+1;
                indices[n++]=k+1;indices[n++]=k+stride;indices[n++]=k+stride+1;
            }
            bool create=!mesh;
            if(create)mesh=new Mesh{name="Study sheet grid"};else mesh.Clear();
            mesh.vertices=v;mesh.uv=uv;mesh.triangles=indices;
            mesh.bounds=bounds;
            if(create)AssetDatabase.CreateAsset(mesh,path);else EditorUtility.SetDirty(mesh);
            return mesh;
        }
    }
}

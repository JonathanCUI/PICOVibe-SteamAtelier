using System.Collections;
using System.Linq;
using UnityEngine;

public sealed partial class SteamXrTrial
{
    // Explicit launch flag only: same meshes, visibility and installation flow as normal use.
    IEnumerator ReviewGeometry()
    {
        CancelHeld();replay=true;detailReview=true;
        var cards=journey.GetComponent<StudioPartCards>();
        if(cards && cards.visible)cards.Toggle();
        journey.ResetJourney();
        yield return new WaitForSecondsRealtime(2);
        Debug.Log("XR_GEOMETRY_READY");
        yield return new WaitForSecondsRealtime(1);
        var steps=journey.modules.Concat(journey.finishing).ToArray();
        for(int i=0;i<steps.Length;i++)
        {
            var preview=steps[i].preview.transform;
            var rs=preview.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled).ToArray();
            if(rs.Length==0){Debug.LogError("XR_GEOMETRY_FAIL empty preview "+i);yield break;}
            var bounds=new Bounds();bool first=true;
            foreach(var r in rs)
            {
                var box=r.localBounds;
                for(int corner=0;corner<8;corner++)
                {
                    var p=preview.InverseTransformPoint(r.transform.TransformPoint(box.center+Vector3.Scale(box.extents,
                        new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1))));
                    if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);
                }
            }
            Debug.Log($"XR_GEOMETRY_STEP index={i+1} title={steps[i].preview.name}");
            float duration=i==0?12:3.5f;
            float distance=Mathf.Max(bounds.size.magnitude*1.05f,.65f);
            for(float t=0;t<duration;t+=Time.unscaledDeltaTime)
            {
                float angle=(i==0?-75:15)+t/duration*(i==0?245:155);
                var ray=new Vector3(Mathf.Sin(angle*Mathf.Deg2Rad),.22f,Mathf.Cos(angle*Mathf.Deg2Rad)).normalized;
                PlaceView(preview,bounds.center+ray*distance,bounds.center);
                yield return null;
            }
            if(!journey.InstallCurrent()){Debug.LogError("XR_GEOMETRY_FAIL install "+i);yield break;}
            yield return null;
        }
        while(journey.CompletionActive)yield return null;
        for(int mode=0;mode<3;mode++)
        {
            journey.inspectionMode=mode;journey.cutaway=mode!=0;journey.ApplyVisibility();
            Debug.Log("XR_GEOMETRY_INSTALLED mode="+mode);
            for(float t=0;t<8;t+=Time.unscaledDeltaTime)
            {
                float a=(20+t/8*150)*Mathf.Deg2Rad;
                PlaceView(journey.engine.locomotive,new Vector3(Mathf.Sin(a)*10,4.5f,Mathf.Cos(a)*10),new Vector3(0,1.6f,0));
                yield return null;
            }
        }
        journey.ResetJourney();FrameTrain();replay=false;detailReview=false;
        Debug.Log("XR_GEOMETRY_DONE previews=17 installedModes=3");
    }
}

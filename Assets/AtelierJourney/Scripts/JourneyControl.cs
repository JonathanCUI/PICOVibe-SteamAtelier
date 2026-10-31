using UnityEngine;

public enum JourneyAction { Assemble, Verify, Next, Pause, Slow, Cutaway, Drive, Workshop, Throttle, Brake, Reverse, Whistle, Lights, View, PreviousPart, NextPart, Reset, FinishAll, ReturnPart, Introduction, GuidePrevious, GuideNext, ResetGuard, WorkshopGuard }
public sealed class JourneyControl : MonoBehaviour {
    public JourneyAction action;
    public int index;
    public string caption;
}

[System.Serializable]
public sealed class JourneyModule {
    public string title, explanation, observation;
    public GameObject[] objects;
    public GameObject preview;
    public Vector3 socket,previewOffset,previewCentre; public float presentationScale=1,snapRadius=.72f;
    public bool installed, verified;
    [System.NonSerialized] public bool parked;
    public float observed;
}

[System.Serializable]
public sealed class JourneyFlow {
    public Transform marker;
    public Vector3[] points;
    public int stage;
    public float offset, speed=1;
    public int port; // 0 continuous; 1/2 admissions, 3/4 exhausts of the exposed cylinder.
}

[System.Serializable]
public sealed class JourneyDetail {
    public MeshFilter filter;
    public Mesh inspection,miniature;
}

public sealed class JourneyPath {
    const int Samples=1024;
    readonly float[] lengths=new float[Samples+1];
    public float Length {get; private set;}
    public JourneyPath(){var last=Point(0);for(int i=1;i<=Samples;i++){var p=Point(i*2*Mathf.PI/Samples);Length+=Vector3.Distance(p,last);lengths[i]=Length;last=p;}}
    // Surveyed Mesa valley coordinates. Rail mesh, locomotive and coaches share this one curve.
    static readonly Vector2[] MesaRoute={new Vector2(60,-410),new Vector2(130,-389),new Vector2(145,-345),new Vector2(105,-316),new Vector2(65,-297),new Vector2(40,-270),new Vector2(12,-270),new Vector2(-20,-270),new Vector2(-55,-270),new Vector2(-102,-294),new Vector2(-114,-340),new Vector2(-96,-382),new Vector2(-35,-410)};
    public static Vector3 Point(float t){float u=Mathf.Repeat(t/(2*Mathf.PI),1)*MesaRoute.Length;int k=Mathf.FloorToInt(u);float f=u-k;Vector2 a=MesaRoute[(k+MesaRoute.Length-1)%MesaRoute.Length],b=MesaRoute[k],c=MesaRoute[(k+1)%MesaRoute.Length],d=MesaRoute[(k+2)%MesaRoute.Length];Vector2 q=.5f*((2*b)+(-a+c)*f+(2*a-5*b+4*c-d)*f*f+(-a+3*b-3*c+d)*f*f*f);return new Vector3(-(q.x-40)*.08f,1.65f,-(q.y+280)*.08f-4.5f);}
    public Vector3 At(float distance){float s=Mathf.Repeat(distance,Length);int lo=0,hi=Samples;while(hi-lo>1){int m=(lo+hi)/2;if(lengths[m]<s)lo=m;else hi=m;}return Point((lo+Mathf.InverseLerp(lengths[lo],lengths[hi],s))*2*Mathf.PI/Samples);}
    public Quaternion Rotation(float s){var forward=(At(s+.08f)-At(s-.08f)).normalized;return Quaternion.LookRotation(forward,Vector3.up)*Quaternion.Euler(0,-90,0);}
}

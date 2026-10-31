using UnityEngine;
public enum JourneyFluidKind { Smoke, Steam, Bubble, Air }

// Local-space, path-constrained wisps: no arrows or glyphs. Each packet has an age and
// cannot appear downstream before it travels there. Gating stops emission, not existing travel.
public sealed class JourneyFluidField : MonoBehaviour {
    public Vector3[] points;
    public JourneyFluidKind kind;
    public int stage,port;
    public float radius=.04f,velocity=.7f,density=28;
    public Color tint=Color.white;
    public ParticleSystem system;
    public JourneyFluidField upstream;
    public bool Arrived => lengths!=null&&TravelFront>=length-.005f;
    public float TravelFront {get;private set;}
    struct Wisp {public float distance,seed,strength,age,speed;}
    Wisp[] wisps=new Wisp[384];ParticleSystem.Particle[] particles=new ParticleSystem.Particle[384];
    float[] lengths;float length,spawn,time;int count;uint serial;
    void Init(){if(lengths!=null)return;lengths=new float[points.Length];for(int i=1;i<points.Length;i++){length+=Vector3.Distance(points[i-1],points[i]);lengths[i]=length;}system.Pause();}
    public void ResetField(){count=0;spawn=time=TravelFront=0;if(system)system.Clear();}
    public void Advance(float dt,float strength){
        if(!system||points==null||points.Length<2)return;Init();time+=dt;if(upstream&&!upstream.Arrived)strength=0;
        if(strength>.01f){spawn+=dt*density*strength;TravelFront=Mathf.Min(length,TravelFront+velocity*dt);}
        while(spawn>=1){spawn--;serial++;float seed=Mathf.Repeat(serial*.6180339f,1)*31;if(count<wisps.Length)wisps[count++]=new Wisp{distance=0,seed=seed,strength=strength,speed=.78f+.44f*Mathf.PerlinNoise(seed,2.7f)};}
        for(int i=count-1;i>=0;i--){var w=wisps[i];w.age+=dt;w.distance+=velocity*dt*w.speed;if(w.distance>=length){wisps[i]=wisps[--count];continue;}wisps[i]=w;}
        for(int i=0;i<count;i++){
            var w=wisps[i];int segment=1;while(segment<lengths.Length-1&&w.distance>lengths[segment])segment++;
            float u=Mathf.InverseLerp(lengths[segment-1],lengths[segment],w.distance);Vector3 tangent=(points[segment]-points[segment-1]).normalized;
            var right=Vector3.Cross(tangent,Mathf.Abs(tangent.y)>.9f?Vector3.forward:Vector3.up).normalized;var up=Vector3.Cross(right,tangent);
            float swirl=w.seed*6.28f+w.age*(kind==JourneyFluidKind.Smoke?1.5f:1.0f);float r=radius*(.12f+.42f*Mathf.PerlinNoise(w.seed,w.age*.28f));
            var position=Vector3.Lerp(points[segment-1],points[segment],u)+right*Mathf.Sin(swirl)*r+up*Mathf.Cos(swirl*.87f)*r;
            float fade=Mathf.Clamp01(w.distance/(radius*2))*Mathf.Clamp01((length-w.distance)/(radius*3));
            var color=tint;color.a*=fade*w.strength*(.65f+.35f*Mathf.Sin(w.seed*3)*Mathf.Sin(w.seed*3));
            particles[i]=new ParticleSystem.Particle{position=position,velocity=tangent*velocity,startColor=color,startSize=radius*(kind==JourneyFluidKind.Bubble?.22f+.22f*Mathf.PerlinNoise(w.seed,0):1.25f+.65f*Mathf.PerlinNoise(w.seed,1)),rotation=w.seed*179+w.age*8,startLifetime=100,remainingLifetime=100-w.age,randomSeed=(uint)(w.seed*1000+1)};
        }
        system.SetParticles(particles,count);
    }
}

using System;
using UnityEngine;

// Pose only: module visibility belongs to the teaching state machine, not the legacy lesson.
public static class JourneyMotion {
    static void SetPosition(Transform t, Vector3 value) { if (t && !t.localPosition.Equals(value)) t.localPosition=value; }
    static void SetRotation(Transform t, Quaternion value) { if (t && !t.localRotation.Equals(value)) t.localRotation=value; }
    static void Link(Transform t,LocomotiveKinematics.D2 a,LocomotiveKinematics.D2 b,float z) {
        SetPosition(t,a.AtZ(z)); SetRotation(t,Quaternion.Euler(0,0,(float)(Math.Atan2(b.y-a.y,b.x-a.x)*180/Math.PI)));
    }
    public static void Apply(LocomotiveController e){
        foreach(var s in e.sides){int sign=s.sign;double theta=(e.phase+(sign<0?90:0))*Math.PI/180;var p=LocomotiveKinematics.Solve(theta,-e.direction,s.pose.alpha,s.pose.beta);s.pose=p;float deg=(float)(theta*180/Math.PI),wz=sign*.775f,z=sign*1.085f;
            for(int i=0;i<3;i++){SetPosition(s.wheels[i],new Vector3((i-1)*1.45f,.65f,wz));SetRotation(s.wheels[i],Quaternion.Euler(0,0,deg));}
            var q=p.Q;for(int i=0;i<2;i++){var a=q+new LocomotiveKinematics.D2(i==0?-1.45:0,0);var b=a+new LocomotiveKinematics.D2(1.45,0);Link(s.couplings[i],a,b,sign*(.775f+(i==0?.125f:.220f)));int axle=i==0?-1:1;SetPosition(s.outerPins[i],(q+new LocomotiveKinematics.D2(axle*1.45,0)).AtZ(sign*(.775f+(i==0?.125f:.220f))));}
            var slide=new LocomotiveKinematics.D2(p.slider,.65);Link(s.mainRod,q,slide,z);SetPosition(s.crosshead,slide.AtZ(z));SetPosition(s.guide,new Vector3(2.275f,.65f,z));SetPosition(s.crankpin,q.AtZ(z));SetPosition(s.wristpin,slide.AtZ(z));
            SetPosition(s.pistonRod,new Vector3((float)p.slider+.50f,.65f,z));SetPosition(s.piston,new Vector3((float)p.slider+1,.65f,z));
            Link(s.returnCrank,p.Q,p.E,sign*1.165f);Link(s.eccentricRod,p.E,p.D,sign*1.205f);SetPosition(s.expansion,p.A.AtZ(sign*1.27f));SetRotation(s.expansion,Quaternion.Euler(0,0,(float)(p.phi*180/Math.PI)));SetPosition(s.dieBlock,p.B.AtZ(sign*1.27f));SetRotation(s.dieBlock,Quaternion.Euler(0,0,(float)((p.phi-p.alpha)*180/Math.PI)));
            Link(s.radiusRod,p.B,p.R,sign*1.32f);Link(s.combination,p.F,p.V,sign*1.37f);Link(s.union,p.U,p.F,sign*1.42f);Link(s.crossheadArm,slide,p.U,sign*1.465f);Link(s.liftArm,p.W,p.T,sign*1.515f);Link(s.liftLink,p.T,p.H,sign*1.47f);
            SetPosition(s.valve,new Vector3((float)p.V.x+.703f,1.03f,z));SetPosition(s.stem,new Vector3((float)p.V.x+.31f,1.02f,z));Link(s.reachRod,new LocomotiveKinematics.D2(-1.90,1.56),p.T,sign*1.56f);
        }
        foreach(var axle in e.axles)if(axle)SetRotation(axle,Quaternion.Euler(0,0,e.phase));
    }
}

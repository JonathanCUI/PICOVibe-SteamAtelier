using System;
using UnityEngine;

// Constrained Walschaerts geometry, in metres. This is a teaching mechanism, not a force simulation.
public static class LocomotiveKinematics {
 public const double WheelRadius=.65, CrankRadius=.28, MainRod=2.15, CouplingRod=1.45;
 public const double RadiusRod=1.3, Rocker=.30, LeverA=.07, LeverB=.50, Union=.45, Lift=.61;
 public static readonly double EccentricRod=Math.Sqrt(1.15*1.15+.20*.20);
 public struct D2 {
  public double x,y;
  public D2(double x,double y){this.x=x;this.y=y;}
  public static D2 operator +(D2 a,D2 b)=>new D2(a.x+b.x,a.y+b.y);
  public static D2 operator -(D2 a,D2 b)=>new D2(a.x-b.x,a.y-b.y);
  public static D2 operator *(D2 a,double b)=>new D2(a.x*b,a.y*b);
  public double Length=>Math.Sqrt(x*x+y*y);
  public Vector3 AtZ(float z)=>new Vector3((float)x,(float)y,z);
 }
 public struct Pose {
  public D2 Q,E,A,D,B,R,V,F,H,T,W,U;
  public double slider,alpha,beta,phi,error;
  public bool valid;
  public double ValveDisplacement=>V.x-2.447;
  public float LeftAdmission=>(float)Math.Max(0,Math.Min(.04,ValveDisplacement-.04));
  public float RightAdmission=>(float)Math.Max(0,Math.Min(.04,-ValveDisplacement-.04));
  public float LeftExhaust=>(float)Math.Max(0,Math.Min(.04,-ValveDisplacement));
  public float RightExhaust=>(float)Math.Max(0,Math.Min(.04,ValveDisplacement));
 }
 static D2 Rotate(D2 v,double a){double c=Math.Cos(a),s=Math.Sin(a);return new D2(c*v.x-s*v.y,s*v.x+c*v.y);}
 static void Points(ref Pose p,double alpha,double beta){
  p.alpha=alpha;p.beta=beta;
  p.B=p.A+Rotate(new D2(RadiusRod*(1-Math.Cos(alpha)),RadiusRod*Math.Sin(alpha)),p.phi);
  double ry=1.02-LeverA*Math.Cos(beta);
  double rx=p.B.x+Math.Sqrt(Math.Max(1e-12,RadiusRod*RadiusRod-(ry-p.B.y)*(ry-p.B.y)));
  p.R=new D2(rx,ry);p.V=new D2(rx-LeverA*Math.Sin(beta),1.02);
  p.F=p.V+new D2(LeverB*Math.Sin(beta),-LeverB*Math.Cos(beta));p.H=p.B+(p.R-p.B)*.18;
 }
 static D2 Residual(Pose p,double alpha,double beta){Points(ref p,alpha,beta);return new D2((p.F-p.U).Length-Union,(p.H-p.T).Length-Lift);}
 public static Pose Solve(double theta,double gear,double alphaGuess=0,double betaGuess=0){
  var result=SolveCore(theta,gear,alphaGuess,betaGuess);if(result.valid)return result;
  // Reset/scrub/reverse can jump across Newton basins. Continue from a known closed pose.
  double angle=theta%(2*Math.PI);if(angle<0)angle+=2*Math.PI;
  var stable=SolveCore(0,gear,0,0);int steps=Math.Max(1,(int)Math.Ceiling(angle/(Math.PI/18)));
  for(int i=1;i<=steps;i++)stable=SolveCore(angle*i/steps,gear,stable.alpha,stable.beta);
  return stable;
 }
 static Pose SolveCore(double theta,double gear,double alphaGuess,double betaGuess){
  var p=new Pose();double s=Math.Sin(theta),c=Math.Cos(theta);
  p.slider=CrankRadius*c+Math.Sqrt(MainRod*MainRod-CrankRadius*CrankRadius*s*s);
  p.Q=new D2(CrankRadius*c,.65+CrankRadius*s);p.E=new D2(.20*s,.65-.20*c);
  p.A=new D2(1.15,.95);p.W=new D2(1.65,1.56);p.U=new D2(p.slider-.12,.53);
  var ev=p.E-p.A;double d=ev.Length;var u=ev*(1/d);var v=new D2(-u.y,u.x);
  double x=(Rocker*Rocker-EccentricRod*EccentricRod+d*d)/(2*d);
  double h=Math.Sqrt(Math.Max(0,Rocker*Rocker-x*x));var d1=p.A+u*x+v*h;var d2=p.A+u*x-v*h;p.D=d1.y<d2.y?d1:d2;
  p.phi=Math.Atan2(p.D.x-p.A.x,-(p.D.y-p.A.y));
  p.T=p.W+new D2(-.30*Math.Cos(gear*.30),.30*Math.Sin(gear*.30));
  double a=alphaGuess,b=betaGuess,eps=1e-5;
  for(int i=0;i<24;i++){
   var f=Residual(p,a,b);if(Math.Max(Math.Abs(f.x),Math.Abs(f.y))<1e-10)break;
   var da=(Residual(p,a+eps,b)-f)*(1/eps);var db=(Residual(p,a,b+eps)-f)*(1/eps);
   double det=da.x*db.y-da.y*db.x;if(Math.Abs(det)<1e-12)break;
   double sa=(f.x*db.y-f.y*db.x)/det,sb=(da.x*f.y-da.y*f.x)/det;
   double step=1;double old=f.Length;
   for(int ls=0;ls<8;ls++){if(Residual(p,a-sa*step,b-sb*step).Length<old)break;step*=.5;}
   a=Math.Max(-.38,Math.Min(.38,a-sa*step));b=Math.Max(-1.2,Math.Min(1.2,b-sb*step));
  }
  Points(ref p,a,b);var r=Residual(p,a,b);p.error=Math.Max(Math.Abs(r.x),Math.Abs(r.y));p.valid=p.error<1e-6 && !double.IsNaN(p.error);return p;
 }
}

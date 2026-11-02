 float noise3(float3 p){return SAMPLE_TEXTURE3D_LOD(_Noise,sampler_Noise,p,0).r;}
 float ell(float3 p,float3 c,float3 r){return (length((p-c)/r)-1)*min(r.x,min(r.y,r.z));}
 float merge(float a,float b){float h=saturate(.5+.5*(b-a)/.032);return lerp(b,a,h)-.032*h*(1-h);}
 float density(float3 p){
  float t=_Phase,seed=_Seed; float3 q=p;
  // Advect interior detail and roll lobes at different rates. This evolves the
  // silhouette even when the world transform and camera remain fixed.
  q.x+=sin(p.y*7+t*.73+seed)*.028;
  q.y+=sin(p.x*8-t*.91+seed)*.025+sin(p.z*11+t*.57)*.014;
  q.z+=cos(p.x*6+t*.62+seed)*.024;
  float s;
  if(_Steam>.5){
   s=ell(q,float3(-.10,-.06,0),float3(.24,.29,.25));
   s=merge(s,ell(q,float3(.14,.05,.02),float3(.23,.27,.22)));
   s=merge(s,ell(q,float3(-.11,.22+sin(t)*.025,.04),float3(.19,.18,.20)));
   s=merge(s,ell(q,float3(.23,-.13,-.05),float3(.15,.17,.19)));
   s=merge(s,ell(q,float3(-.03,.02,.24),float3(.19,.19,.14)));
  }else{
   // An asymmetric rising head and a thinning wind-stretched tail.
   float variant=sin(seed*1.7);
   s=ell(q,float3(-.24,-.10,.02),float3(.20,.19,.24));
   s=merge(s,ell(q,float3(-.14,.05+variant*.025,-.03),float3(.19,.25,.23)));
   s=merge(s,ell(q,float3(-.04,.20+sin(t*.67+seed)*.022,.02),float3(.15,.17,.20)));
   s=merge(s,ell(q,float3(.09,.04,-.04),float3(.19,.20,.22)));
   s=merge(s,ell(q,float3(-.04,-.15,.05),float3(.27,.13,.23)));
   s=merge(s,ell(q,float3(-.25,.08,.06),float3(.11,.13,.14)));
   s=merge(s,ell(q,float3(-.11,.03,.23),float3(.15,.15,.13)));
   s=merge(s,ell(q,float3(.07,-.04,.20),float3(.14,.13,.11)));
   s=merge(s,ell(q,float3(-.10,.02,-.24),float3(.17,.14,.12)));
   s=merge(s,ell(q,float3(-.22,.09,.22),float3(.11,.14,.13)));
   s=merge(s,ell(q,float3(-.03,.20,.20),float3(.10,.13,.12)));
   s=merge(s,ell(q,float3(.10,.075,.21),float3(.105,.12,.12)));
   s=merge(s,ell(q,float3(-.27,-.09,.22),float3(.12,.105,.11)));
   s=merge(s,ell(q,float3(-.23,.12,-.20),float3(.115,.13,.135)));
   s=merge(s,ell(q,float3(.04,.17,-.20),float3(.115,.12,.13)));
   s=merge(s,ell(q,float3(.23,-.08,.02),float3(.19,.070,.12)));
   s=merge(s,ell(q,float3(.32,.015+sin(t*.8+seed)*.03,-.04),float3(.14,.048,.085)));
  }
  float3 adv=q*.83+float3(-t*.048, t*.021,seed*.17+t*.014);
  float low=noise3(adv),fine=noise3(adv*2.71+float3(t*.018,-t*.023,0));
  float detail=noise3(adv*6.1-float3(t*.04,0,t*.027));
  s+=(low-.5)*.21+(fine-.5)*.065+(detail-.5)*.018;
  float body=1-smoothstep(-.032,.044,s);
  if(_Steam<.5){
   float tail=smoothstep(.08,.25,q.x)*(1-smoothstep(.40,.49,q.x));
   float strandY=q.y+.075-sin(q.x*18-t*.9+seed)*.035;
   float strandZ=q.z-sin(q.x*12-t*.55)*.07;
   float threads=exp(-strandY*strandY*100-strandZ*strandZ*65)*smoothstep(.43,.7,fine+detail*.18)*tail*.65;
   body=max(body*(1-tail*.65),threads);
  }
  float pad=1-smoothstep(.42,.498,max(abs(p.x),max(abs(p.y),abs(p.z))));
  return body*pad;
 }

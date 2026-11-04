using UnityEngine;

namespace SwanJourney
{
    public sealed class SwanSketchSheet : MonoBehaviour
    {
        public const float SupportHeight=.40f;
        public GameObject originalFrame;
        public AudioClip openPaper,turnNext,turnPrevious,closePaper;
        static readonly int BendId=Shader.PropertyToID("_PaperBend");
        static readonly int TailId=Shader.PropertyToID("_PaperTail");
        static readonly int ArtworkId=Shader.PropertyToID("_MainTex");
        static readonly int TurnId=Shader.PropertyToID("_PageTurn");
        static readonly int NextInkId=Shader.PropertyToID("_NextTex");
        static readonly int RevealId=Shader.PropertyToID("_PaperReveal");
        const string PageFolder="SwanSketchSheets/Step";
        MeshRenderer surface;
        Collider pickCollider;
        AudioSource rustle;
        MaterialPropertyBlock properties;
        Vector3 lastPosition,gripVelocity,bend,rate,tail,tailRate;
        bool pendingDrag;
        long? owner;
        public Vector3 Bend => bend;
        public Vector3 EdgeBend=>tail;
        public int DisplayedPage {get;private set;}=0;
        Texture2D loadedArtwork,queuedArtwork,turnArtwork;
        ResourceRequest pending;
        int requestedPage,failedPage=-1,queuedPage=-1,turnPage=-1,turnPhase;
        float turnTime,turnDirection;
        float reveal;
        float revealFrom,revealTarget,revealDuration;
        double revealStarted;
        bool presentationVisible,turnSoundPlayed;
        public bool IsTurning=>turnPhase!=0;
        public bool IsRevealing=>presentationVisible?reveal<1:reveal>0;
        public float Unrolled=>reveal;
        public bool KeepGuideVisible=>presentationVisible || reveal>0 || IsTurning;
        public int TurnsCompleted {get;private set;}
        public int RustlesPlayed {get;private set;}

        public static string ResourcePath(int index)=>PageFolder+(index+1).ToString("D2",System.Globalization.CultureInfo.InvariantCulture);
        public bool SetPresentation(int index,bool visible)
        {
            if(pending!=null && pending.isDone){
                var artwork=pending.asset as Texture2D;
                pending=null;
                if(!artwork){
                    failedPage=requestedPage;
                    Debug.LogError("Missing workshop sketch: "+ResourcePath(requestedPage));
                }else if(index==requestedPage && index!=DisplayedPage && index!=turnPage){
                    ClearQueued();queuedPage=index;queuedArtwork=artwork;
                }else Resources.UnloadAsset(artwork);
            }
            if(queuedPage>=0 && queuedPage!=index)ClearQueued();
            bool available=index!=failedPage;
            bool showFrame=visible && !available;
            if(originalFrame.activeSelf!=showFrame)originalFrame.SetActive(showFrame);
            presentationVisible=available&&visible;
            if(!presentationVisible){owner=null;pendingDrag=false;}
            if(presentationVisible && !gameObject.activeSelf)gameObject.SetActive(true);
            if(pickCollider)pickCollider.enabled=presentationVisible && reveal>.98f;
            if(!visible || !available)return available;
            if(index==DisplayedPage || index==turnPage)return true;
            if(index==0)queuedPage=0;
            else if(pending==null && queuedPage!=index){
                requestedPage=index;
                pending=Resources.LoadAsync<Texture2D>(ResourcePath(index));
            }
            // Keep the existing ink visible while loading. Start the turn only when
            // the destination is ready, and coalesce rapid requests into the latest page.
            if(turnPhase==0 && queuedPage==index && (reveal<.12f || reveal>=.98f)){
                turnPage=queuedPage;turnArtwork=queuedArtwork;queuedPage=-1;queuedArtwork=null;
                // While still rolled away, update the ink before the sheet opens.
                if(reveal<.12f){BindArtwork(turnPage,turnArtwork);turnArtwork=null;turnPage=-1;return true;}
                int delta=(turnPage-DisplayedPage+17)%17;
                turnDirection=delta<=8?1f:-1f;
                turnPhase=1;turnTime=0;turnSoundPlayed=false;
                properties.SetTexture(NextInkId,turnArtwork?turnArtwork:surface.sharedMaterial.GetTexture(ArtworkId));
                surface.SetPropertyBlock(properties);
            }
            return available;
        }
        void ClearQueued(){if(queuedArtwork)Resources.UnloadAsset(queuedArtwork);queuedArtwork=null;queuedPage=-1;}
        void BindArtwork(int index,Texture2D artwork)
        {
            Texture2D previous=loadedArtwork;
            loadedArtwork=artwork;
            DisplayedPage=index;
            SetInk(artwork);
            if(previous && previous!=artwork)Resources.UnloadAsset(previous);
        }
        void SetInk(Texture artwork)
        {
            EnsureSurface();
            properties.SetTexture(ArtworkId,artwork?artwork:surface.sharedMaterial.GetTexture(ArtworkId));
            surface.SetPropertyBlock(properties);
        }
        void OnDestroy()
        {
            ClearQueued();
            if(turnArtwork)Resources.UnloadAsset(turnArtwork);
            if(loadedArtwork)Resources.UnloadAsset(loadedArtwork);
            if(pending!=null){
                var unfinished=pending;
                if(unfinished.isDone){if(unfinished.asset)Resources.UnloadAsset(unfinished.asset);}
                else unfinished.completed+=_=>{if(unfinished.asset)Resources.UnloadAsset(unfinished.asset);};
            }
        }

        public bool Grab(long hand)
        {
            if(!isActiveAndEnabled || !presentationVisible || reveal<.98f || owner.HasValue)return false;
            owner=hand;lastPosition=transform.position;
            return true;
        }
        public bool Drag(long hand,Vector3 position)
        {
            if(owner!=hand || !isActiveAndEnabled)return false;
            transform.position=position;pendingDrag=true;return true;
        }
        public void Release(long hand){if(owner==hand)owner=null;}
        void Awake(){EnsureSurface();properties.SetFloat(RevealId,0);surface.SetPropertyBlock(properties);surface.enabled=false;pickCollider.enabled=false;}
        void EnsureSurface()
        {
            if(surface)return;
            surface=GetComponent<MeshRenderer>();
            properties=new MaterialPropertyBlock();
            pickCollider=GetComponent<Collider>();
            rustle=GetComponent<AudioSource>();
        }
        void OnDisable()
        {
            // A hidden/re-shown sheet must never remain edge-on or retain a stale turn.
            if(turnPhase==1){BindArtwork(turnPage,turnArtwork);turnArtwork=null;}
            turnPhase=0;turnPage=-1;turnTime=0;
            reveal=0;presentationVisible=false;
            revealFrom=revealTarget=0;
            owner=null;pendingDrag=false;gripVelocity=bend=rate=tail=tailRate=Vector3.zero;
            if(surface){
                properties.SetVector(BendId,Vector3.zero);properties.SetVector(TailId,Vector3.zero);
                properties.SetVector(TurnId,Vector4.zero);
                properties.SetFloat(RevealId,0);
                surface.SetPropertyBlock(properties);
                surface.enabled=false;pickCollider.enabled=false;
            }
        }
        void UpdateReveal(float dt)
        {
            float target=presentationVisible||IsTurning?1f:0f;
            if(target!=revealTarget){
                revealFrom=reveal;revealTarget=target;
                revealStarted=Time.realtimeSinceStartupAsDouble;
                // Full gestures take 1.3 s, leaving room for the supplied paper
                // recording. A reversal starts continuously from the current roll.
                revealDuration=Mathf.Max(.40f,1.30f*Mathf.Abs(target-reveal));
                PlayPaperSound(target>0?openPaper:closePaper,target>0?"open":"close",target>0?.55f:.65f);
            }
            if(reveal==target)return;
            float t=Mathf.Clamp01((float)(Time.realtimeSinceStartupAsDouble-revealStarted)/revealDuration);
            float eased=t*t*t*(t*(t*6-15)+10);
            reveal=t>=1?target:Mathf.Lerp(revealFrom,target,eased);
            properties.SetFloat(RevealId,reveal);surface.SetPropertyBlock(properties);
            surface.enabled=reveal>0;
            pickCollider.enabled=presentationVisible && reveal>.98f;
            // The eased value can round to its endpoint a frame before t==1.
            // Retire the object at that same endpoint rather than leaving it active.
            if(target==0 && reveal==0)gameObject.SetActive(false);
        }
        void PlayPaperSound(AudioClip clip,string action,float volume)
        {
            if(!rustle || !clip)return;
            // One voice per sheet. Reversing a gesture replaces its old sound.
            rustle.Stop();rustle.clip=clip;rustle.pitch=1;rustle.volume=volume;rustle.Play();
            Debug.Log($"XR_PAPER_AUDIO action={action} time={Time.realtimeSinceStartupAsDouble:F3} volume={volume:F2} pitch=1.00");
        }
        void UpdateTurn(float dt)
        {
            turnTime+=dt;
            float u=Mathf.Clamp01(turnTime/1.12f);
            if(!turnSoundPlayed && u>=(turnDirection>0?.025f:.24f)){
                turnSoundPlayed=true;
                var clip=turnDirection>0?turnNext:turnPrevious;
                if(rustle && clip){
                    PlayPaperSound(clip,turnDirection>0?"next":"previous",turnDirection>0?.50f:.25f);RustlesPlayed++;
                }
            }
            if(u>=1f){
                BindArtwork(turnPage,turnArtwork);turnArtwork=null;
                turnPhase=0;turnPage=-1;TurnsCompleted++;
                properties.SetVector(TurnId,Vector4.zero);
                properties.SetTexture(NextInkId,surface.sharedMaterial.GetTexture(ArtworkId));
                // A small residual flex settles through the existing soft-paper spring.
                tailRate+=new Vector3(turnDirection*.36f,0,.24f);
            }else{
                properties.SetVector(TurnId,new Vector4(u,turnDirection,1,0));
            }
            surface.SetPropertyBlock(properties);
        }
        void LateUpdate()
        {
            float dt=Time.unscaledDeltaTime;
            if(dt<=0f)return;
            UpdateReveal(dt);
            if(!isActiveAndEnabled)return;
            if(turnPhase!=0)UpdateTurn(dt);
            if(!pendingDrag && gripVelocity==Vector3.zero && bend==Vector3.zero && tail==Vector3.zero && rate==Vector3.zero && tailRate==Vector3.zero)return;
            // Measure support motion in sheet widths. Consume the last drag even if released in the same frame.
            Vector3 measured=pendingDrag?Vector3.ClampMagnitude(
                transform.InverseTransformVector(transform.position-lastPosition)/dt,2.6f):Vector3.zero;
            pendingDrag=false;
            lastPosition=transform.position;
            Vector3 next=Vector3.Lerp(gripVelocity,measured,1f-Mathf.Exp(-23f*dt));
            if(measured==Vector3.zero && next.sqrMagnitude<1e-6f)next=Vector3.zero;
            // The hand can brake abruptly. In its local frame the free paper keeps its velocity.
            Vector3 velocityChange=next-gripVelocity;
            gripVelocity=next;
            rate+=1.65f*velocityChange;
            tailRate+=2.75f*velocityChange;
            Spring(ref bend,ref rate,.16f*next,5.8f,7.6f,dt);
            Spring(ref tail,ref tailRate,.18f*next,2.85f,5.5f,dt);
            properties.SetVector(BendId,bend);properties.SetVector(TailId,tail);
            surface.SetPropertyBlock(properties);
        }
        static void Spring(ref Vector3 value,ref Vector3 speed,Vector3 target,float damping,float frequency,float dt)
        {
            var offset=value-target;
            var impulse=(speed+offset*damping)/frequency;
            float decay=Mathf.Exp(-damping*dt),s=Mathf.Sin(frequency*dt),c=Mathf.Cos(frequency*dt);
            var next=decay*(offset*c+impulse*s);
            speed=decay*frequency*(-offset*s+impulse*c)-damping*next;
            value=target+next;
            if(target==Vector3.zero && value.sqrMagnitude<1e-6f && speed.sqrMagnitude<.0001f)value=speed=Vector3.zero;
        }
    }
}

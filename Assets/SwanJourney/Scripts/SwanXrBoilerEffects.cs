using UnityEngine;

namespace SwanJourney
{
    public sealed class SwanXrBoilerEffects : MonoBehaviour
    {
        public SteamJourney journey;
        public Material coalTemplate;
        Material coal, original;
        int lastFuelState=-1, burned, lastSteam=-1;
        Color freshCoal;
        static readonly int BaseColor=Shader.PropertyToID("_BaseColor");
        static readonly int EmissionColor=Shader.PropertyToID("_EmissionColor");

        void Awake()
        {
            coal=new Material(coalTemplate);
            freshCoal=coalTemplate.GetColor(BaseColor);
            var pieces=journey.emberRenderers;
            foreach (var r in pieces)
            {
                if (!r) continue;
                if (!original) original=r.sharedMaterial;
                if (r.sharedMaterial==original) r.sharedMaterial=coal;
            }
        }

        public void Apply(bool active)
        {
            float heat=active && !journey.inSandbox && journey.modules[0].installed ? journey.FireLevel : 0;
            int step=Mathf.RoundToInt(heat*32);
            burned=journey.modules[0].installed ? Mathf.Max(burned,step) : 0;
            int fuelState=burned*33+step;
            if (fuelState!=lastFuelState)
            {
                lastFuelState=fuelState;
                float t=Mathf.SmoothStep(0,1,step/32f);
                var crust=Color.Lerp(freshCoal,new Color(.73f,.72f,.69f),burned/32f);
                coal.SetColor(BaseColor,Color.Lerp(crust,new Color(.37f,.18f,.10f),t));
                coal.SetColor(EmissionColor,new Color(1.65f,.21f,.016f)*t);
            }

            var vapour=journey.steam;
            bool visible=active && !journey.inSandbox && journey.cutaway && journey.modules[1].installed;
            int level=visible ? Mathf.RoundToInt(Mathf.InverseLerp(.49f,.95f,journey.WaterHeat)*12) : 0;
            if (!visible || level==0)
            {
                if (!vapour.isStopped) vapour.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
                lastSteam=-1;
                return;
            }
            if (journey.paused)
            {
                if (vapour.isPlaying) vapour.Pause(false);
                return;
            }
            if (level!=lastSteam)
            {
                var emission=vapour.emission;
                emission.rateOverTime=level*.58f;
                lastSteam=level;
            }
            var main=vapour.main;
            float speed=journey.slow ? .25f : 1f;
            if (main.simulationSpeed!=speed) main.simulationSpeed=speed;
            if (!vapour.isPlaying) vapour.Play(false);
        }

        void OnDestroy()
        {
            if (!coal) return;
            if (journey && original)
                foreach (var r in journey.emberRenderers)
                    if(r && r.sharedMaterial==coal) r.sharedMaterial=original;
            Destroy(coal);
        }
    }
}

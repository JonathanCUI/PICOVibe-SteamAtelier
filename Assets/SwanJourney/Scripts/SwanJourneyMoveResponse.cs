using UnityEngine;

namespace SwanJourney
{
    // Incremental hand displacement avoids a jump when gain changes as the
    // exhibit approaches. Real hand displacement still drives tabletop release.
    public sealed class SwanJourneyMoveResponse
    {
        public float Gain { get; private set; } = 1;
        public static float DistanceGain(float distance) => Mathf.Lerp(1,3,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.65f,2.8f,distance)));
        public void Begin(float distance) { Gain=DistanceGain(distance); }
        public Vector3 Step(Vector3 movement,float distance,float dt)
        {
            Gain=Mathf.Lerp(Gain,DistanceGain(distance),1-Mathf.Exp(-6*Mathf.Clamp(dt,0,.1f)));
            return movement*Gain;
        }
        public static Quaternion FacingPlayer(Vector3 position,Quaternion rotation,Vector3 head,float dt)
        {
            var away=Vector3.ProjectOnPlane(position-head,Vector3.up);
            if(away.sqrMagnitude<.04f)return rotation;
            // Stage -Z is the front; keep roll/pitch level and ease only yaw.
            return Quaternion.RotateTowards(rotation,Quaternion.LookRotation(away.normalized,Vector3.up),55*Mathf.Clamp(dt,0,.1f));
        }
    }
}

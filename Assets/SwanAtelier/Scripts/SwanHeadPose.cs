using UnityEngine;
using UnityEngine.XR;

namespace SwanAtelier
{
    // Both HUDs use the Unity tracking origin, not compositor/world poses.
    public static class SwanHeadPose
    {
        public static bool TryRead(out Pose pose)
        {
            pose = default;
            var device = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if (!device.isValid || (device.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) && !tracked)) return false;
            bool positionValid = device.TryGetFeatureValue(CommonUsages.centerEyePosition, out Vector3 position)
                || device.TryGetFeatureValue(CommonUsages.devicePosition, out position);
            bool rotationValid = device.TryGetFeatureValue(CommonUsages.centerEyeRotation, out Quaternion rotation)
                || device.TryGetFeatureValue(CommonUsages.deviceRotation, out rotation);
            pose = new Pose(position, rotation);
            return positionValid && rotationValid && IsValid(pose);
        }
        public static bool IsValid(Pose pose)
        {
            Vector3 p = pose.position;
            float norm = Quaternion.Dot(pose.rotation, pose.rotation);
            return !float.IsNaN(p.x) && !float.IsNaN(p.y) && !float.IsNaN(p.z)
                && !float.IsInfinity(p.x) && !float.IsInfinity(p.y) && !float.IsInfinity(p.z)
                && norm > .5f && norm < 1.5f;
        }
    }
}

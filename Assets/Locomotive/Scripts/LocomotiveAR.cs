using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;
using Unity.XR.CoreUtils;
using TMPro;
public class LocomotiveAR : MonoBehaviour {
 public LocomotiveController experiment; public Transform content; public GameObject floor;public TMP_Text status;
 GameObject originGO,sessionGO;ARPlaneManager planes;ARRaycastManager raycasts;Camera arCamera;bool active,starting,placed;
 readonly List<ARRaycastHit> hits=new List<ARRaycastHit>();
 public void Toggle(){if(starting)return;if(active){Leave();return;}StartCoroutine(Enter());}
 IEnumerator Enter(){
 #if !UNITY_ANDROID || UNITY_EDITOR
  status.text="AR 放置需要支持 ARCore 的安卓设备。";yield break;
 #else
  starting=true;status.text="正在检查 AR 支持…";
  var manager=XRGeneralSettings.Instance!=null?XRGeneralSettings.Instance.Manager:null;
  if(manager==null){status.text="AR 配置不可用，可以继续使用三维实验台。";starting=false;yield break;}
  if(manager.activeLoader==null)yield return manager.InitializeLoader();
  if(manager.activeLoader==null){status.text="设备未提供 ARCore 支持，可以继续三维实验。";starting=false;yield break;}
  manager.StartSubsystems();
  yield return ARSession.CheckAvailability();
  if(ARSession.state==ARSessionState.NeedsInstall)yield return ARSession.Install();
  if(ARSession.state!=ARSessionState.Ready&&ARSession.state!=ARSessionState.SessionInitializing&&ARSession.state!=ARSessionState.SessionTracking){status.text="AR 当前不可用，请检查 ARCore 与相机权限。";manager.StopSubsystems();starting=false;yield break;}
  if(originGO==null){
   originGO=new GameObject("AR Origin");originGO.SetActive(false);var origin=originGO.AddComponent<XROrigin>();
   var offset=new GameObject("Camera Offset");offset.transform.SetParent(originGO.transform,false);origin.CameraFloorOffsetObject=offset;
   var cam=new GameObject("AR Camera",typeof(Camera),typeof(ARCameraManager),typeof(ARCameraBackground),typeof(UnityEngine.SpatialTracking.TrackedPoseDriver));cam.transform.SetParent(offset.transform,false);arCamera=cam.GetComponent<Camera>();arCamera.nearClipPlane=.02f;arCamera.farClipPlane=50;
   var pose=cam.GetComponent<UnityEngine.SpatialTracking.TrackedPoseDriver>();pose.SetPoseSource(UnityEngine.SpatialTracking.TrackedPoseDriver.DeviceType.GenericXRDevice,UnityEngine.SpatialTracking.TrackedPoseDriver.TrackedPose.ColorCamera);pose.trackingType=UnityEngine.SpatialTracking.TrackedPoseDriver.TrackingType.RotationAndPosition;
   origin.Camera=arCamera;planes=originGO.AddComponent<ARPlaneManager>();planes.requestedDetectionMode=PlaneDetectionMode.Horizontal;raycasts=originGO.AddComponent<ARRaycastManager>();
   sessionGO=new GameObject("AR Session");sessionGO.SetActive(false);sessionGO.AddComponent<ARSession>();sessionGO.AddComponent<ARInputManager>();
  }
  experiment.running=false;experiment.studioCamera.enabled=false;floor.SetActive(false);content.gameObject.SetActive(false);sessionGO.SetActive(true);originGO.SetActive(true);active=true;placed=false;starting=false;status.text="缓慢移动手机扫描桌面，然后点击放置。";
 #endif
 }
 void Update(){
  if(!active)return;
  if(ARSession.state==ARSessionState.SessionTracking&&!placed)status.text="扫描桌面 · 点击屏幕放置机车";
  if(Input.touchCount==2&&placed){var a=Input.GetTouch(0);var b=Input.GetTouch(1);float before=Vector2.Distance(a.position-a.deltaPosition,b.position-b.deltaPosition);float now=Vector2.Distance(a.position,b.position);if(before>1)content.localScale=Vector3.one*Mathf.Clamp(content.localScale.x*now/before,.04f,.16f);var oldVector=(b.position-b.deltaPosition)-(a.position-a.deltaPosition);var newVector=b.position-a.position;content.Rotate(Vector3.up,Vector2.SignedAngle(oldVector,newVector),Space.World);return;}if(Input.touchCount==0)return;var t=Input.GetTouch(0);if(t.phase!=TouchPhase.Began)return;
  if(EventSystem.current!=null&&EventSystem.current.IsPointerOverGameObject(t.fingerId))return;
  if(placed){RaycastHit objectHit;if(Physics.Raycast(arCamera.ScreenPointToRay(t.position),out objectHit,50)){var part=objectHit.collider.GetComponentInParent<LocomotivePart>();if(part){experiment.Select(part);return;}}}if(raycasts.Raycast(t.position,hits,TrackableType.PlaneWithinPolygon)){
   var pose=hits[0].pose;content.position=pose.position;content.rotation=pose.rotation;content.localScale=Vector3.one*.08f;content.gameObject.SetActive(true);placed=true;status.text="已放置 · 点击零件查看作用；双指缩放与旋转；点击空桌面重新放置";
  }
 }
 void Leave(){
  active=false;originGO.SetActive(false);sessionGO.SetActive(false);var manager=XRGeneralSettings.Instance!=null?XRGeneralSettings.Instance.Manager:null;if(manager!=null)manager.StopSubsystems();content.gameObject.SetActive(true);content.position=Vector3.zero;content.rotation=Quaternion.identity;content.localScale=Vector3.one;floor.SetActive(true);experiment.studioCamera.enabled=true;status.text="";
 }
}




using UnityEngine;
public class LocomotivePart : MonoBehaviour {
 public string partId,title;[TextArea]public string explanation;public int stage;public Vector3 home,explodeOffset;public Quaternion homeRotation=Quaternion.identity;public bool hideInPrinciple;public bool moving;
 public void Restore(){transform.localPosition=home;transform.localRotation=homeRotation;}
}

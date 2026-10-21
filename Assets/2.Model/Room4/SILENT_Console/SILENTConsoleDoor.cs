using UnityEngine;

// Attach to the Manual prefab. The setup tool reads the exact imported
// animation endpoints, so the slider works regardless of FBX axis conversion.
[ExecuteAlways]
public class SILENTConsoleDoor : MonoBehaviour
{
    [SerializeField] Transform door;
    [SerializeField, Range(0f,126f)] float angle;
    [SerializeField, HideInInspector] Quaternion closedRotation=Quaternion.identity;
    [SerializeField, HideInInspector] Quaternion openRotation=Quaternion.identity;
    float lastAngle=float.NaN;

    public float Angle => angle;
    public void Configure(Transform doorTransform,Quaternion closed,Quaternion opened)
    {
        door=doorTransform;closedRotation=closed;openRotation=opened;angle=0;Apply();
    }
    public void SetAngle(float degrees)
    {
        angle=Mathf.Clamp(degrees,0,126);Apply();
    }
    [ContextMenu("Open door")]
    public void Open() { SetAngle(126); }
    [ContextMenu("Close door")]
    public void Close() { SetAngle(0); }
    void OnEnable() { lastAngle=float.NaN; }
    void Update() { if(angle!=lastAngle) Apply(); }
    void Apply()
    {
        if(door==null) return;
        angle=Mathf.Clamp(angle,0,126);
        door.localRotation=Quaternion.Slerp(closedRotation,openRotation,angle/126f);
        lastAngle=angle;
    }
}

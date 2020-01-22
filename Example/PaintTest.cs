using System.Reflection;
using UnityEngine;

public class PaintTest : MonoBehaviour
{
    public float dis = 20;
    [SerializeField] private MeshGenerator MeshGenerator;
    private Camera cam;
    private Vector2? mLastPoint;
    private void Awake()
    {
        cam = Camera.main;
    }

    private void Update()
    {
        if(!Input.GetMouseButton(0)) return;
        if(mLastPoint.HasValue && Vector2.Distance(mLastPoint.Value, Input.mousePosition) < dis) return;
        mLastPoint = Input.mousePosition;
        if (Physics.Raycast(cam.ScreenPointToRay(Input.mousePosition), out var hitInfo))
        {
            MeshGenerator.Add(MeshGenerator.transform.InverseTransformPoint(hitInfo.point + new Vector3(0, 0.5f, 0)));
        }
    }
}

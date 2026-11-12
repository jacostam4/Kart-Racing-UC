using UnityEngine;

[DefaultExecutionOrder(50)]
public sealed class KartChaseCamera : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0, 2.5f, -4.8f);
    [SerializeField] private Vector3 lookOffset = new Vector3(0, 0.8f, 1.5f);
    private Vector3 velocity;
    private Camera view;
    private KartMovement kart;
    private float normalFov;

    private void OnEnable()
    {
        view = GetComponent<Camera>();
        normalFov = view.fieldOfView;
        if (target != null)
        {
            kart = target.GetComponent<KartMovement>();
            UpdateView(true);
        }
    }

    private void LateUpdate()
    {
        if (target != null)
            UpdateView(false);
        float desiredFov = normalFov + (kart != null && kart.IsBoosting ? 8 : 0);
        view.fieldOfView = Mathf.Lerp(view.fieldOfView, desiredFov, 1 - Mathf.Exp(-7 * Time.deltaTime));
    }

    private void UpdateView(bool snap)
    {
        Vector3 desired = target.TransformPoint(offset);
        transform.position = snap ? desired : Vector3.SmoothDamp(transform.position, desired, ref velocity, 0.14f);
        Quaternion facing = Quaternion.LookRotation(target.TransformPoint(lookOffset) - transform.position, Vector3.up);
        transform.rotation = snap ? facing : Quaternion.Slerp(transform.rotation, facing, 1 - Mathf.Exp(-12 * Time.deltaTime));
    }
}


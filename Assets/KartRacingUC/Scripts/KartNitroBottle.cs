using UnityEngine;

public sealed class KartNitroBottle : MonoBehaviour
{
    [SerializeField] private KartMovement kart;
    [SerializeField] private Transform visual;

    private bool available;
    private Vector3 initialPosition;

    private void Awake() => initialPosition = visual.localPosition;

    public void SetAvailable(bool value)
    {
        available = value;
        visual.gameObject.SetActive(value);
    }

    private void Update()
    {
        if (!available)
            return;

        visual.localPosition = initialPosition + Vector3.up * (Mathf.Sin(Time.time * 3 + transform.position.x) * 0.12f);
        visual.Rotate(Vector3.up, 78 * Time.deltaTime, Space.Self);
        Vector3 delta = kart.transform.position - transform.position;
        if (delta.sqrMagnitude <= 1.4f * 1.4f && kart.CollectNitro())
            SetAvailable(false);
    }
}

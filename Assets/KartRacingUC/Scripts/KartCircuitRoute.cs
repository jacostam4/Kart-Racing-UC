using UnityEngine;

public sealed class KartCircuitRoute : MonoBehaviour
{
    [SerializeField] private Vector3[] points;
    [SerializeField] private float halfWidth;
    private float[] distances;
    public float HalfWidth => halfWidth;
    public float Length { get { Measure(); return distances[points.Length]; } }

    public void Configure(Vector3[] route, float width)
    {
        points = route;
        halfWidth = width;
        distances = null;
    }

    private void Measure()
    {
        if (distances != null) return;
        distances = new float[points.Length + 1];
        for (int i = 0; i < points.Length; i++)
            distances[i + 1] = distances[i] + Vector3.Distance(points[i], points[(i + 1) % points.Length]);
    }

    public void Sample(float distance, out Vector3 position, out Vector3 forward, out float curveDistance)
    {
        float along = Mathf.Repeat(distance, Length);
        int index = System.Array.BinarySearch(distances, along);
        if (index < 0) index = ~index - 1;
        int next = (index + 1) % points.Length;
        position = Vector3.Lerp(points[index], points[next],
            (along - distances[index]) / (distances[index + 1] - distances[index]));
        forward = (points[next] - points[index]).normalized;
        curveDistance = 30;
        for (int i = index; distances[i] - along < 22;)
        {
            int a = (i + 1) % points.Length;
            int b = (i + 2) % points.Length;
            if (Vector3.Angle(points[a] - points[i], points[b] - points[a]) > 3)
            {
                curveDistance = Mathf.Max(0, distances[i + 1] - along);
                break;
            }
            if (++i >= points.Length - 1) break;
        }
    }

    public float Project(Vector3 position)
    {
        Measure();
        float best = float.PositiveInfinity;
        float progress = 0;
        for (int i = 0; i < points.Length; i++)
        {
            Vector3 delta = points[(i + 1) % points.Length] - points[i];
            float t = Mathf.Clamp01(Vector3.Dot(position - points[i], delta) / delta.sqrMagnitude);
            float error = (position - points[i] - delta * t).sqrMagnitude;
            if (error >= best) continue;
            best = error;
            progress = Mathf.Lerp(distances[i], distances[i + 1], t);
        }
        return Mathf.Repeat(progress, Length);
    }
}

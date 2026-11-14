using System.Collections.Generic;
using UnityEngine;

public sealed class KartRival : MonoBehaviour
{
    // Lista de todos los rivales que están en la carrera (para que se esquiven entre ellos y para el puesto).
    public static readonly List<KartRival> All = new();
    // Dificultad del nivel: la cambia KartLevels (0.9 = más lentos, 1.07 = más rápidos).
    public static float Difficulty = 1;

    [SerializeField] private KartStartLights lights;
    [SerializeField] private KartMovement player;
    [SerializeField] private Collider road;
    [SerializeField] private Transform[] wheels;
    // Dónde arranca en la parrilla y qué tan rápido va. Los valores por defecto son los de siempre.
    [SerializeField] private float gridDistance = -7;
    [SerializeField] private float startLane = 1.75f;
    [SerializeField] private float speedFactor = 1;

    private const float HalfStraight = 26;
    private const float Radius = 20;
    private const float StraightLength = 2 * HalfStraight;
    private const float ArcLength = Mathf.PI * Radius;
    public const float LoopLength = 2 * (StraightLength + ArcLength);
    private KartCircuitRoute route;
    private float TrackLength => route != null ? route.Length : LoopLength;
    private float FinishDistance => TrackLength * 3;
    private const float TopSpeed = 13;
    private const float CornerSpeed = 10.5f;
    private const float LaneLimit = 2.15f;
    private const float Braking = 5.5f;
    private float distance;
    private float speed;
    private float lane;
    private float spin;
    private float passLane;
    private float passUntil;
    private double startedAt;
    private bool running;

    public float Steering { get; private set; }
    public bool Finished { get; private set; }
    public double FinishTime { get; private set; }
    public int Lap => Mathf.Clamp(Mathf.FloorToInt(distance / TrackLength) + 1, 1, 3);
    public float Speed => speed;
    public float Progress => distance;
    public string DisplayName { get; set; } = "Gabriel";

    // Lo usa el segundo rival: le da otro puesto en la parrilla y otra velocidad.
    public void Configure(float grid, float laneOnGrid, float pace)
    {
        gridDistance = grid;
        startLane = laneOnGrid;
        speedFactor = pace;
        ResetRival();
    }

    private void OnEnable()
    {
        route = road.GetComponentInParent<KartCircuitRoute>();
        All.Add(this);
        lights.CountdownStarted += ResetRival;
        lights.GreenLight += Begin;
        ResetRival();
    }

    private void OnDisable()
    {
        All.Remove(this);
        lights.CountdownStarted -= ResetRival;
        lights.GreenLight -= Begin;
    }

    private void ResetRival()
    {
        distance = gridDistance;
        speed = 0;
        lane = startLane;
        spin = 0;
        passLane = lane;
        passUntil = 0;
        Steering = 0;
        running = false;
        Finished = false;
        FinishTime = 0;
        PlaceKart();
    }

    private void Begin()
    {
        startedAt = Time.timeAsDouble;
        running = true;
    }

    private void FixedUpdate()
    {
        if (!running)
            return;

        SampleRoute(distance, out var center, out var forward, out float curveDistance);
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        Vector3 toPlayer = player.transform.position - center;
        float ahead = Vector3.Dot(toPlayer, forward);
        float across = Vector3.Dot(toPlayer, right);
        float separation = Mathf.Abs(across - lane);
        bool playerOnTrack = Mathf.Abs(across) <= 4.5f &&
            Mathf.Abs(player.transform.position.y - transform.position.y) < 3;
        bool corner = curveDistance == 0;
        float dt = Time.fixedDeltaTime;
        float targetSpeed = Mathf.Min(TopSpeed * speedFactor * Difficulty,
            Mathf.Sqrt(Mathf.Pow(CornerSpeed * speedFactor * Difficulty, 2) + 2 * Braking * Mathf.Max(0, curveDistance - 2.5f)));

        // Si otro rival va justo adelante en el mismo carril: frena un poco e intenta pasarlo por el lado.
        float wanted = targetSpeed;
        foreach (var other in All)
        {
            if (other == this)
                continue;
            float gap = other.distance - distance;
            if (gap <= 0 || gap > 7 || Mathf.Abs(other.lane - lane) > 1.7f)
                continue;
            if (Time.time >= passUntil && wanted > other.speed + 0.5f)
            {
                passLane = other.lane >= 0 ? -LaneLimit : LaneLimit;
                passUntil = Time.time + 2;
            }
            targetSpeed = Mathf.Min(targetSpeed, Mathf.Max(0, other.speed + (gap - 3.2f) * 1.25f));
        }

        if (playerOnTrack && ahead > 0 && ahead < 10 && separation < 1.9f &&
            targetSpeed > player.Speed + 0.8f && Time.time >= passUntil)
        {
            passLane = across >= 0 ? -LaneLimit : LaneLimit;
            passUntil = Time.time + 2;
        }

        float targetLane = RacingLine(curveDistance);
        if (Time.time < passUntil || (playerOnTrack && ahead > -3.3f && ahead < 1))
            targetLane = passLane;
        if (Finished)
        {
            targetLane = LaneLimit;
            targetSpeed = 0;
        }

        float laneStep = Mathf.MoveTowards(lane, targetLane, 2.25f * dt) - lane;
        if (playerOnTrack && Mathf.Abs(ahead) < 2.7f && separation < 2.1f &&
            Mathf.Abs(across - (lane + laneStep)) < separation)
            laneStep = 0;

        if (playerOnTrack && ahead > 0 && ahead < 8 && separation < 1.65f)
        {
            float safeSpeed = Mathf.Max(0, player.Speed + (ahead - 3.2f) * 1.25f);
            targetSpeed = Mathf.Min(targetSpeed, safeSpeed);
        }

        lane += laneStep;
        speed = Mathf.MoveTowards(speed, targetSpeed, (speed > targetSpeed ? Braking : 4.2f) * dt);
        float advance = speed * dt;
        if (playerOnTrack && ahead > 0 && separation < 1.55f)
            advance = Mathf.Min(advance, Mathf.Max(0, ahead - 2.5f));
        foreach (var other in All)
        {
            float gap = other.distance - distance;
            if (other != this && gap > 0 && Mathf.Abs(other.lane - lane) < 1.4f)
                advance = Mathf.Min(advance, Mathf.Max(0, gap - 2.4f));
        }
        distance += advance;
        if (!Finished && distance >= FinishDistance)
        {
            Finished = true;
            FinishTime = Time.timeAsDouble - startedAt;
        }

        float steer = (corner ? -0.45f : 0) + laneStep / dt * 0.22f;
        Steering = Mathf.MoveTowards(Steering, Mathf.Clamp(steer, -1, 1), dt * 5);
        spin = Mathf.Repeat(spin + advance / 0.27f * Mathf.Rad2Deg, 360);
        PlaceKart();
        if (Finished && speed <= 0)
            running = false;
    }

    private static float RacingLine(float curveDistance)
    {
        if (curveDistance > 14)
            return 1.35f;
        return Mathf.Lerp(-LaneLimit, 1.35f, Mathf.SmoothStep(0, 1, curveDistance / 14));
    }

    private void PlaceKart()
    {
        SampleRoute(distance, out var position, out var forward, out _);
        position += Vector3.Cross(Vector3.up, forward) * lane;
        var ray = new Ray(position + Vector3.up * 12, Vector3.down);
        if (road.Raycast(ray, out var hit, 16))
        {
            var direction = Vector3.ProjectOnPlane(forward, hit.normal);
            transform.SetPositionAndRotation(hit.point + hit.normal * 0.015f, Quaternion.LookRotation(direction, hit.normal));
        }

        for (int i = 0; i < wheels.Length; i++)
            wheels[i].localRotation = Quaternion.Euler(0, i < 2 ? Steering * 25 : 0, 0) * Quaternion.Euler(spin, 0, 0);
    }

    // Convierte una posición del mundo en "metros recorridos desde la meta" sobre el óvalo (sirve para el puesto).
    public static float RouteDistance(Vector3 point)
    {
        float route;
        if (point.x >= HalfStraight)
        {
            float angle = Mathf.Atan2(point.z, point.x - HalfStraight);
            route = StraightLength + (angle + Mathf.PI / 2) * Radius;
        }
        else if (point.x <= -HalfStraight)
        {
            float angle = Mathf.Atan2(point.z, point.x + HalfStraight);
            if (angle < 0)
                angle += 2 * Mathf.PI;
            route = 2 * StraightLength + ArcLength + (angle - Mathf.PI / 2) * Radius;
        }
        else if (point.z < 0)
        {
            route = point.x + HalfStraight;
        }
        else
        {
            route = StraightLength + ArcLength + (HalfStraight - point.x);
        }
        return Mathf.Repeat(route - HalfStraight, LoopLength);
    }

    private void SampleRoute(float distance, out Vector3 position, out Vector3 forward, out float curveDistance)
    {
        if (this.route != null)
        {
            this.route.Sample(distance, out position, out forward, out curveDistance);
            return;
        }
        float route = Mathf.Repeat(distance + HalfStraight, LoopLength);
        if (route < StraightLength)
        {
            position = new Vector3(route - HalfStraight, 0, -Radius);
            forward = Vector3.right;
            curveDistance = StraightLength - route;
            return;
        }

        route -= StraightLength;
        if (route < ArcLength)
        {
            Arc(HalfStraight, route / Radius - Mathf.PI / 2, out position, out forward);
            curveDistance = 0;
            return;
        }

        route -= ArcLength;
        if (route < StraightLength)
        {
            position = new Vector3(HalfStraight - route, 0, Radius);
            forward = Vector3.left;
            curveDistance = StraightLength - route;
            return;
        }

        Arc(-HalfStraight, (route - StraightLength) / Radius + Mathf.PI / 2, out position, out forward);
        curveDistance = 0;
    }

    private static void Arc(float center, float angle, out Vector3 position, out Vector3 forward)
    {
        float cosine = Mathf.Cos(angle);
        float sine = Mathf.Sin(angle);
        position = new Vector3(center + Radius * cosine, 0, Radius * sine);
        forward = new Vector3(-sine, 0, cosine);
    }
}







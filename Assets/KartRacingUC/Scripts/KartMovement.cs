using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(BoxCollider))]
public sealed class KartMovement : MonoBehaviour
{
    [SerializeField] private Collider road;
    [SerializeField] private Collider ground;
    [SerializeField] private Transform spawn;
    [SerializeField] private Transform[] wheels;

    private readonly RaycastHit[] hits = new RaycastHit[24];
    private readonly Collider[] overlaps = new Collider[24];
    private BoxCollider chassis;
    private float speed;
    private float wheelAngle;
    private float heading;
    private bool resetRequested;
    private bool boostRequested;
    private const float NitroCapacity = 100;
    public float Nitro { get; private set; }
    public int Bottles { get; private set; }
    public bool IsBoosting { get; private set; }

    public bool CollectNitro()
    {
        if (!CanDrive)
            return false;
        Nitro = Mathf.Min(NitroCapacity, Nitro + 35);
        Bottles++;
        return true;
    }

    public float Speed => speed;
    public bool TouchAccelerate { get; set; }
    public bool TouchBrake { get; set; }
    public bool TouchReverse { get; set; }
    public float TouchSteering { get; set; }
    public void RequestBoost() => boostRequested = true;
    public void RequestReset() => resetRequested = true;
    public bool IsBraking { get; private set; }
    public event System.Action<Vector3, Vector3, float> Impact;
    public event System.Action ReturnedToGrid;
    public Transform RearWheel(int side) => wheels[side + 2];
    public Material RoadMaterial => road.GetComponent<Renderer>().sharedMaterial;

    public bool TryGetRoadContact(Vector3 position, out RaycastHit hit) =>
        road.Raycast(new Ray(position + Vector3.up, Vector3.down), out hit, 3);
    public bool CanDrive { get; set; } = true;

    public float Steering { get; private set; }

    private void OnEnable()
    {
        chassis = GetComponent<BoxCollider>();
        heading = transform.eulerAngles.y;
    }

    private void Update()
    {
        if (Keyboard.current?.rKey.wasPressedThisFrame == true)
            resetRequested = true;
        if (Keyboard.current?.eKey.wasPressedThisFrame == true)
            boostRequested = true;
    }

    private void FixedUpdate()
    {
        IsBraking = false;
        if (resetRequested)
        {
            resetRequested = false;
            speed = 0;
            Nitro = 0;
            Bottles = 0;
            IsBoosting = false;
            boostRequested = false;
            Steering = 0;
            heading = spawn.eulerAngles.y;
            transform.SetPositionAndRotation(Grounded(spawn.position, out _), spawn.rotation);
            ReturnedToGrid?.Invoke();
            return;
        }

        if (!CanDrive)
        {
            boostRequested = false;
            return;
        }

        var keyboard = Keyboard.current;
        bool accelerate = TouchAccelerate || keyboard != null && (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed);
        bool reverse = TouchReverse || keyboard != null && (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed);
        bool brake = TouchBrake || keyboard != null && keyboard.spaceKey.isPressed;
        float turn = TouchSteering;
        if (keyboard != null)
        {
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                turn -= 1;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                turn += 1;
        }

        float dt = Time.fixedDeltaTime;
        if (boostRequested && Nitro > 0 && !brake && !reverse)
            IsBoosting = true;
        boostRequested = false;
        if (brake || reverse || Nitro <= 0)
            IsBoosting = false;
        if (IsBoosting)
            Nitro = Mathf.Max(0, Nitro - 30 * dt);

        Steering = Mathf.MoveTowards(Steering, Mathf.Clamp(turn, -1, 1), 5 * dt);
        float desiredSpeed = IsBoosting ? 23 : brake || accelerate == reverse ? 0 : accelerate ? 15 : -4;
        IsBraking = Mathf.Abs(speed) > 1.5f && (brake || speed * desiredSpeed < 0);
        float response = brake ? 27 : desiredSpeed == 0 ? 4 :
            speed * desiredSpeed < 0 ? 18 : IsBoosting ? 16 : 8;
        speed = Mathf.MoveTowards(speed, desiredSpeed, response * dt);

        float turnRate = Mathf.Lerp(98, 62, Mathf.Abs(speed) / 15);
        float nextHeading = heading + Steering * turnRate * Mathf.Clamp(speed / 3, -1, 1) * dt;
        Vector3 forward = Quaternion.Euler(0, nextHeading, 0) * Vector3.forward;
        Vector3 position = Grounded(transform.position + forward * (speed * dt), out var normal);
        Quaternion rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(forward, normal), normal);
        Vector3 displacement = position - transform.position;

        if (HitsBarrier(displacement, position, rotation, out var contact, out var impactNormal))
        {
            if (Mathf.Abs(speed) >= 1.8f)
                Impact?.Invoke(contact, impactNormal, Mathf.Abs(speed));
            IsBraking = false;
            IsBoosting = false;
            speed = 0;
        }
        else
        {
            heading = nextHeading;
            transform.SetPositionAndRotation(position, rotation);
            wheelAngle = Mathf.Repeat(wheelAngle + speed * dt / 0.27f * Mathf.Rad2Deg, 360);
        }

        for (int index = 0; index < wheels.Length; index++)
        {
            Quaternion yaw = Quaternion.Euler(0, index < 2 ? Steering * 25 : 0, 0);
            wheels[index].localRotation = yaw * Quaternion.Euler(wheelAngle, 0, 0);
        }
    }

    private Vector3 Grounded(Vector3 position, out Vector3 normal)
    {
        var ray = new Ray(new Vector3(position.x, 20, position.z), Vector3.down);
        if (road.Raycast(ray, out var hit, 40) || ground.Raycast(ray, out hit, 40))
        {
            normal = hit.normal;
            return hit.point + normal * 0.015f;
        }

        normal = Vector3.up;
        return transform.position;
    }

    private bool HitsBarrier(Vector3 displacement, Vector3 position, Quaternion rotation,
        out Vector3 contact, out Vector3 normal)
    {
        contact = default;
        normal = default;
        float distance = displacement.magnitude;
        if (distance < 0.0001f)
            return false;

        Vector3 extents = chassis.size * 0.5f - Vector3.one * 0.035f;
        Vector3 center = transform.TransformPoint(chassis.center);
        int count = Physics.BoxCastNonAlloc(center, extents, displacement / distance, hits,
            transform.rotation, distance + 0.035f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float nearest = float.PositiveInfinity;
        for (int index = 0; index < count; index++)
        {
            var hit = hits[index];
            if (!IsObstacle(hit.collider) || hit.distance >= nearest)
                continue;

            nearest = hit.distance;
            contact = hit.distance > 0 ? hit.point : hit.collider.ClosestPoint(center);
            normal = hit.distance > 0 ? hit.normal : -displacement.normalized;
        }

        if (nearest < float.PositiveInfinity)
            return true;

        count = Physics.OverlapBoxNonAlloc(position + rotation * chassis.center, extents, overlaps, rotation,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int index = 0; index < count; index++)
        {
            var obstacle = overlaps[index];
            if (!IsObstacle(obstacle))
                continue;

            contact = obstacle.ClosestPoint(center);
            normal = (center - contact).normalized;
            if (normal.sqrMagnitude < 0.01f)
                normal = -displacement.normalized;
            return true;
        }

        return false;
    }

    private bool IsObstacle(Collider candidate) =>
        candidate != null && candidate != chassis && candidate != road && candidate != ground;
}







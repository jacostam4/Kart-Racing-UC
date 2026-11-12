using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(20)]
public sealed class KartDriverPresentation : MonoBehaviour
{
    [SerializeField] private Transform driver;
    [SerializeField] private Transform seat;
    [SerializeField] private Transform steeringWheel;

    private readonly Dictionary<string, Transform> bones = new();
    private Quaternion steeringRest;
    private readonly Dictionary<Transform, Quaternion> restRotations = new();
    private float steering;
    private KartMovement movement;

    private void OnEnable()
    {
        bones.Clear();
        restRotations.Clear();
        foreach (var bone in driver.GetComponentsInChildren<Transform>(true))
            bones.TryAdd(bone.name, bone);
        movement = GetComponent<KartMovement>();
        steeringRest = steeringWheel.localRotation;
        foreach (var bone in bones.Values)
            restRotations.Add(bone, bone.localRotation);
        ApplyPose();
    }

    private void LateUpdate()
    {
        steering = movement != null ? movement.Steering : 0;
        ApplyPose();
    }

    private void ApplyPose()
    {
        foreach (var rest in restRotations)
            rest.Key.localRotation = rest.Value;

        var hips = bones["Hips"];
        driver.position += seat.position + transform.up * 0.085f - hips.position;
        bones["Spine"].Rotate(transform.right, 10 + Mathf.Sin(Time.time * 2.5f) * 1.25f, Space.World);
        bones["Spine"].Rotate(transform.forward, -steering * 8, Space.World);

        steeringWheel.localRotation = steeringRest * Quaternion.Euler(0, -steering * 38, 0);
        PoseSide("Left", -1);
        PoseSide("Right", 1);
    }

    private void PoseSide(string side, float sign)
    {
        var thigh = bones[side + "UpLeg"];
        var calf = bones[side + "Leg"];
        var foot = bones[side + "Foot"];
        var footTarget = transform.TransformPoint(new Vector3(sign * 0.21f, 0.30f, 0.66f));
        Bend(thigh, calf, foot, footTarget, transform.up + transform.right * (sign * 0.35f));
        if (bones.TryGetValue(side + "ToeBase", out var toe))
            Aim(foot, toe, transform.forward);

        var arm = bones[side + "Arm"];
        var forearm = bones[side + "ForeArm"];
        var hand = bones[side + "Hand"];
        var grip = steeringWheel.TransformPoint(new Vector3(sign * 0.14f, 0, -0.025f));
        Bend(arm, forearm, hand, grip, transform.right * sign - transform.up * 0.5f);
    }

    private static void Bend(Transform upper, Transform lower, Transform end, Vector3 target, Vector3 bend)
    {
        Vector3 delta = target - upper.position;
        float first = Vector3.Distance(upper.position, lower.position);
        float second = Vector3.Distance(lower.position, end.position);
        if (delta.sqrMagnitude < 0.000001f || first < 0.001f || second < 0.001f)
            return;

        Vector3 axis = delta.normalized;
        float distance = Mathf.Clamp(delta.magnitude, Mathf.Abs(first - second) + 0.0001f, first + second - 0.0001f);
        float projection = (first * first - second * second + distance * distance) / (2 * distance);
        float height = Mathf.Sqrt(Mathf.Max(0, first * first - projection * projection));
        Vector3 elbow = axis * projection + Vector3.ProjectOnPlane(bend, axis).normalized * height;
        Aim(upper, lower, elbow);
        Aim(lower, end, upper.position + axis * distance - lower.position);
    }

    private static void Aim(Transform bone, Transform child, Vector3 direction)
    {
        bone.rotation = Quaternion.FromToRotation(child.position - bone.position, direction) * bone.rotation;
    }
}




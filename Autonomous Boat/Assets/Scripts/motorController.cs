using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BoatDifferentialDrive : MonoBehaviour
{
    [Header("References")]
    public Rigidbody boat;        // Boat root Rigidbody
    public Transform leftMotor;   // empty on left pontoon (same Y as right)
    public Transform rightMotor;  // empty on right pontoon

    [Header("Controls")]
    public float maxThrust = 50f;
    public float maxReverseThrust = 35f;
    [Range(0f, 1.5f)] public float turnMix = 0.8f;
    public bool invertSteer = false;

    [Header("Stability / Damping")]
    public float comLowering = 0.3f;   // lower CoM to reduce roll
    public float linearDragWater = 1.2f; // extra horizontal drag
    public float angularDragYaw = 0.8f;  // yaw damping
    public float rollPitchBrake = 10f;   // suppress X/Z rotation
    public bool uprightStabilizer = true;
    public float uprightSpring = 14f;
    public float uprightDamping = 5f;

    [Header("Debug")]
    public bool straightLineDebug = false; // set true to test forward-only motion

    float throttle, steer;

    void Reset() { boat = GetComponent<Rigidbody>(); }

    void Start()
    {
        if (!boat) boat = GetComponent<Rigidbody>();

        // Unity 6 damping names
        boat.linearDamping = 0.3f;
        boat.angularDamping = 0.4f;

        // Lower / center COM (assumes your prefab is centered)
        var com = boat.centerOfMass;
        com.y -= Mathf.Abs(comLowering);
        boat.centerOfMass = com;

        boat.interpolation = RigidbodyInterpolation.Interpolate;
        boat.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        boat.maxAngularVelocity = 20f;
    }

    void Update()
    {
        throttle = Input.GetAxisRaw("Vertical");   // Up/Down, W/S
        steer = Input.GetAxisRaw("Horizontal"); // Left/Right, A/D
        if (invertSteer) steer = -steer;
    }

    void FixedUpdate()
    {
        if (!boat) return;

        // Always push along the water plane (so ↑ never adds lift)
        Vector3 fwd = Vector3.ProjectOnPlane(boat.transform.right, Vector3.up).normalized;

        if (straightLineDebug || !leftMotor || !rightMotor)
        {
            // Simple forward-only force at CoM (for debugging asymmetries)
            float cmd = Mathf.Clamp(throttle, -1f, 1f);
            float thrust = (cmd >= 0f ? cmd * maxThrust : cmd * maxReverseThrust) * 2f; // ~two motors
            boat.AddForce(fwd * thrust, ForceMode.Force);
        }
        else
        {
            // Differential mixing
            float lCmd = Mathf.Clamp(throttle - steer * turnMix, -1f, 1f);
            float rCmd = Mathf.Clamp(throttle + steer * turnMix, -1f, 1f);

            float lN = (lCmd >= 0f ? lCmd * maxThrust : lCmd * maxReverseThrust);
            float rN = (rCmd >= 0f ? rCmd * maxThrust : rCmd * maxReverseThrust);

            Vector3 mid = (leftMotor.position + rightMotor.position) * 0.5f;

            // 1) Net forward force at midpoint (prevents pitch/roll torque)
            boat.AddForceAtPosition(fwd * (lN + rN), mid, ForceMode.Force);

            // 2) Yaw-only torque about world up (robust to slight asymmetries)
            float halfTrack = 0.5f * Vector3.Distance(
                new Vector3(leftMotor.position.x, mid.y, leftMotor.position.z),
                new Vector3(rightMotor.position.x, mid.y, rightMotor.position.z)
            );
            boat.AddTorque(Vector3.up * ((rN - lN) * halfTrack), ForceMode.Force);
        }

        // Extra damping to keep it planted
        Vector3 horizV = Vector3.ProjectOnPlane(boat.linearVelocity, Vector3.up);
        boat.AddForce(-horizV * linearDragWater, ForceMode.Acceleration);

        Vector3 w = boat.angularVelocity;
        Vector3 yaw = Vector3.Project(w, Vector3.up);
        boat.AddTorque(-yaw * angularDragYaw, ForceMode.Acceleration);

        Vector3 rollPitch = w - yaw;
        boat.AddTorque(-rollPitch * rollPitchBrake, ForceMode.Acceleration);

        if (uprightStabilizer) ApplyUprightPD();
    }

    void ApplyUprightPD()
    {
        // Align boat.up to world up (ignore yaw)
        Vector3 axis = Vector3.Cross(boat.transform.up, Vector3.up);
        float mag = axis.magnitude;
        if (mag < 1e-6f) return;

        axis /= mag;
        float angle = Mathf.Asin(Mathf.Clamp(mag, 0f, 1f));

        Vector3 local = boat.transform.InverseTransformDirection(axis);
        local.y = 0f;
        if (local.sqrMagnitude < 1e-6f) return;

        axis = boat.transform.TransformDirection(local.normalized);
        Vector3 torque = axis * (angle * uprightSpring) - boat.angularVelocity * uprightDamping;
        boat.AddTorque(torque, ForceMode.Acceleration);
    }
}

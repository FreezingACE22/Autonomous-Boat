using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BoatMotorSimple : MonoBehaviour
{
    public enum ForwardAxis { ZPlus, ZMinus, XPlus, XMinus }

    [Header("References")]
    public Rigidbody boat;              // Boat root Rigidbody
    [Tooltip("Optional. If set, this transform's axis defines forward.")]
    public Transform thrustRef;

    [Header("Facing")]
    [Tooltip("Which local axis points toward the bow.")]
    public ForwardAxis bowAxis = ForwardAxis.ZPlus;

    [Header("Thrust")]
    [Tooltip("Forward thrust (N) at full W/Up.")]
    public float maxThrust = 100f;
    [Tooltip("Reverse thrust (N) at full S/Down.")]
    public float maxReverseThrust = 70f;

    [Header("Damping (optional)")]
    [Tooltip("Extra planar drag to keep it from sliding.")]
    public float planarDrag = 0.6f;
    [Tooltip("Tiny yaw damping so it doesn't spin from waves.")]
    public float yawDamping = 0.05f;

    float throttle;

    void Reset() { boat = GetComponent<Rigidbody>(); }

    void Start()
    {
        if (!boat) boat = GetComponent<Rigidbody>();
        if (!thrustRef) thrustRef = transform;

        boat.useGravity = true;
        boat.isKinematic = false;
        boat.interpolation = RigidbodyInterpolation.Interpolate;
        boat.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    void Update()
    {
        // W/S or Up/Down
        throttle = Mathf.Clamp(Input.GetAxisRaw("Vertical"), -1f, 1f);
    }

    void FixedUpdate()
    {
        if (!boat) return;

        // Forward direction projected on water plane
        Vector3 fwd = GetPlanarForward();

        // Thrust value
        float thrust = throttle >= 0f ? throttle * maxThrust : throttle * maxReverseThrust;

        // Apply force at CoM (pure translation, no unintended pitch/roll)
        boat.AddForce(fwd * thrust, ForceMode.Force);

        // Optional: light planar drag + tiny yaw damping
        Vector3 horizV = Vector3.ProjectOnPlane(boat.linearVelocity, Vector3.up);
        boat.AddForce(-horizV * planarDrag, ForceMode.Acceleration);

        Vector3 yaw = Vector3.Project(boat.angularVelocity, Vector3.up);
        boat.AddTorque(-yaw * yawDamping, ForceMode.Acceleration);
    }

    Vector3 GetPlanarForward()
    {
        Transform t = thrustRef ? thrustRef : transform;
        Vector3 dir = bowAxis switch
        {
            ForwardAxis.ZPlus => t.forward,
            ForwardAxis.ZMinus => -t.forward,
            ForwardAxis.XPlus => t.right,
            ForwardAxis.XMinus => -t.right,
            _ => t.forward
        };
        return Vector3.ProjectOnPlane(dir, Vector3.up).normalized;
    }
}

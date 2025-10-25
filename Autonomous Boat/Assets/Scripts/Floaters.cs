using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

public class Floater : MonoBehaviour
{
    [Header("References")]
    public Rigidbody boat;                 // Boat Rigidbody
    public WaterSurface water;             // HDRP WaterSurface

    [Header("Buoyancy Settings")]
    [Tooltip("Depth where full buoyant force is reached.")]
    public float depthBefSub = 0.6f;
    [Tooltip("Buoyancy strength multiplier (per floater).")]
    public float displacementAmt = 1f;
    [Tooltip("Total number of floaters on the boat.")]
    public int floaters = 4;

    [Header("Water Drag")]
    public float waterDrag = 0.8f;
    public float waterAngularDrag = 0.8f;

    [Header("Debug")]
    public bool logDepth = false;
    public bool drawForces = false;

    private WaterSearchParameters search;
    private WaterSearchResult searchResult;

    void FixedUpdate()
    {
        if (!boat || !water) return;

        // Apply distributed gravity (so 4 floaters = total gravity)
        boat.AddForceAtPosition(Physics.gravity / Mathf.Max(1, floaters),
                                transform.position,
                                ForceMode.Acceleration);

        // Get water surface height
        search.startPositionWS = transform.position;
        water.ProjectPointOnWaterSurface(search, out searchResult);
        float waterY = searchResult.projectedPositionWS.y;

        float depth = waterY - transform.position.y;

        if (logDepth)
            Debug.Log($"{name}: depth={depth:F3}, waterY={waterY:F3}");

        if (depth > 0f)
        {
            // Buoyant force proportional to submersion
            float disp = Mathf.Clamp01(depth / Mathf.Max(0.001f, depthBefSub)) * displacementAmt;

            Vector3 buoyancy = Vector3.up * Mathf.Abs(Physics.gravity.y) * disp;
            boat.AddForceAtPosition(buoyancy, transform.position, ForceMode.Acceleration);

            // Optional: draw buoyancy debug vector
            if (drawForces)
                Debug.DrawRay(transform.position, buoyancy * 0.02f, Color.green, 0.1f, false);

            // Water drag to reduce drift/oscillation
            Vector3 horizV = Vector3.ProjectOnPlane(boat.linearVelocity, Vector3.up);
            boat.AddForce(-horizV * waterDrag * disp * Time.fixedDeltaTime, ForceMode.VelocityChange);

            Vector3 w = boat.angularVelocity;
            Vector3 yaw = Vector3.Project(w, boat.transform.up);
            Vector3 rollPitch = w - yaw;
            boat.AddTorque(-(yaw + rollPitch) * waterAngularDrag * disp * Time.fixedDeltaTime,
                           ForceMode.VelocityChange);
        }
    }
}

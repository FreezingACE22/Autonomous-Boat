using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

public class Floater : MonoBehaviour
{
    public Rigidbody boat;
    public float depthBefSub = 0.6f;      // depth for full buoyancy at this point
    public float displacementAmt = 1f;    // strength multiplier
    public int floaters = 4;              // total number of Floater components on the boat
    public float waterDrag = 0.8f;        // linear damping when submerged
    public float waterAngularDrag = 0.8f; // angular damping when submerged
    public WaterSurface water;

    WaterSearchParameters search;
    WaterSearchResult searchResult;

    void FixedUpdate()
    {
        if (!boat || !water) return;

        // Gravity split between floaters
        boat.AddForceAtPosition(Physics.gravity / Mathf.Max(1, floaters),
                                transform.position,
                                ForceMode.Acceleration);

        // Query water height here
        search.startPositionWS = transform.position;
        water.ProjectPointOnWaterSurface(search, out searchResult);
        float waterY = searchResult.projectedPositionWS.y;

        float depth = waterY - transform.position.y;
        if (depth > 0f)
        {
            float disp = Mathf.Clamp01(depth / Mathf.Max(0.0001f, depthBefSub)) * displacementAmt;

            // Buoyancy up
            boat.AddForceAtPosition(Vector3.up * Mathf.Abs(Physics.gravity.y) * disp,
                                    transform.position,
                                    ForceMode.Acceleration);

            // Extra damping while submerged (helps stop idle drifting/rotating)
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

using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

/// Stable spring-damper buoyancy at a point (Unity 6 safe).
/// Attach one to each corner floater: LFC, LBC, RFC, RBC.
/// Boat Rigidbody should have "Use Gravity" ON. Do NOT add gravity here.
public class StableFloater : MonoBehaviour
{
    [Header("References")]
    public Rigidbody boat;               // Boat root Rigidbody
    public WaterSurface water;           // HDRP WaterSurface (optional but recommended)

    [Header("Buoyancy (per floater)")]
    [Tooltip("Meters of submersion for full spring force (bigger = softer).")]
    public float fullSubmersionDepth = 0.8f;
    [Tooltip("Spring strength (N) applied when depth reaches 'fullSubmersionDepth'.")]
    public float springStrength = 0f;    // 0 = auto from mass
    [Tooltip("Damps bobbing; uses vertical point velocity.")]
    public float springDamping = 160f;

    [Header("Water Resistance")]
    [Tooltip("Horizontal water resistance (per floater).")]
    public float horizontalDrag = 0.9f;
    [Tooltip("Angular damping per floater when submerged.")]
    public float angularWaterDamping = 0.6f;

    [Header("Setup")]
    [Tooltip("Total number of floaters on the boat (usually 4).")]
    public int totalFloaters = 4;

    [Header("Fallback (if HDRP CPU water is off)")]
    [Tooltip("Use this Y as the water plane if HDRP CPU queries are unavailable.")]
    public bool useFlatFallbackWater = true;
    public float fallbackWaterLevelY = 0f;

    // HDRP query state
    private WaterSearchParameters _query;
    private WaterSearchResult _hit;

    void Start()
    {
        if (!boat) return;

        // If springStrength is 0, auto-calc per floater ≈ weight/floaters
        if (springStrength <= 0f)
        {
            float weight = boat.mass * Mathf.Abs(Physics.gravity.y);
            springStrength = weight / Mathf.Max(1, totalFloaters);
        }
    }

    void FixedUpdate()
    {
        if (!boat) return;

        // Get water height at this point
        float waterY = fallbackWaterLevelY;

        if (water && !useFlatFallbackWater)
        {
            _query.startPositionWS = transform.position;
            // Requires HDRP Water "Simulation → CPU" enabled
            water.ProjectPointOnWaterSurface(_query, out _hit);
            waterY = _hit.projectedPositionWS.y;
        }

        float depth = waterY - transform.position.y; // >0 when submerged
        if (depth > 0f)
        {
            // Normalize depth to [0..1] over the submersion range
            float depth01 = Mathf.Clamp01(depth / Mathf.Max(0.0001f, fullSubmersionDepth));

            // Spring-damper buoyancy (vertical only)
            float spring = depth01 * springStrength;
            float pointVy = boat.GetPointVelocity(transform.position).y;
            float damping = springDamping * pointVy;

            Vector3 buoy = Vector3.up * (spring - damping);
            boat.AddForceAtPosition(buoy, transform.position, ForceMode.Force);

            // Horizontal drag at the point (reduces sideways skate)
            Vector3 vPoint = boat.GetPointVelocity(transform.position);
            Vector3 horiz = Vector3.ProjectOnPlane(vPoint, Vector3.up);
            boat.AddForceAtPosition(-horiz * horizontalDrag, transform.position, ForceMode.Force);

            // Gentle angular damping (spread across floaters)
            boat.AddTorque(-boat.angularVelocity * (angularWaterDamping / Mathf.Max(1, totalFloaters)),
                           ForceMode.Acceleration);
        }
    }
}

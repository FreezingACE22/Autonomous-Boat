using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

[RequireComponent(typeof(Rigidbody))]
public class MeshBuoyancyHDRP : MonoBehaviour
{
    [Header("References")]
    public Rigidbody boat;                     // Boat Rigidbody (root)
    public WaterSurface water;                 // HDRP WaterSurface in your scene
    public MeshFilter meshFilter;              // Mesh to sample; if null, we try MeshFilter on this GO
    public MeshCollider meshCollider;          // Optional: use collider mesh
    [Tooltip("If true and a MeshCollider is assigned, use its sharedMesh instead of MeshFilter.")]
    public bool useColliderMesh = false;

    [Header("Sampling")]
    [Tooltip("Sample every Nth vertex to reduce cost (1 = all vertices).")]
    [Min(1)] public int vertexStride = 3;
    [Tooltip("Transform that defines the mesh space; leave null to use this transform.")]
    public Transform meshTransform;            // Defaults to this.transform

    [Header("Buoyancy")]
    [Tooltip("Water density (kg/m^3). 1000 for fresh water.")]
    public float waterDensity = 1000f;
    [Tooltip("Depth at which a vertex reaches full buoyant force.")]
    public float fullBuoyDepth = 0.6f;
    [Tooltip("Scales total buoyancy. Increase if the boat sinks too much.")]
    public float buoyancyScale = 1.0f;

    [Header("Water Drag")]
    [Tooltip("Planar (horizontal) drag while the vertex is submerged.")]
    public float planarDrag = 0.8f;
    [Tooltip("Normal drag (opposes movement normal to water surface).")]
    public float normalDrag = 0.4f;
    [Tooltip("Extra yaw damping. Keep modest so differential steering still works.")]
    public float yawAngularDrag = 0.1f;
    [Tooltip("Roll/Pitch damping.")]
    public float rollPitchAngularDrag = 0.8f;

    [Header("Debug")]
    public bool drawSampledVertices = false;
    public bool drawForces = false;

    // --- internals ---
    struct Sample
    {
        public int index;     // vertex index
        public float weight;  // area-based weight for vertex
    }

    Mesh _mesh;
    Vector3[] _vertsLocal;
    List<Sample> _samples = new List<Sample>(1024);
    WaterSearchParameters _search;
    WaterSearchResult _result;

    void Reset()
    {
        boat = GetComponent<Rigidbody>();
        meshFilter = GetComponent<MeshFilter>();
        meshTransform = transform;
    }

    void Awake()
    {
        if (!boat) boat = GetComponent<Rigidbody>();
        if (!meshTransform) meshTransform = transform;

        // Pick a mesh
        if (useColliderMesh && meshCollider && meshCollider.sharedMesh)
            _mesh = meshCollider.sharedMesh;
        else if (meshFilter && meshFilter.sharedMesh)
            _mesh = meshFilter.sharedMesh;
        else if (TryGetComponent(out MeshFilter mf))
            _mesh = mf.sharedMesh;

        if (_mesh == null)
        {
            Debug.LogError($"{name}: No mesh found for MeshBuoyancyHDRP.");
            enabled = false;
            return;
        }

        _vertsLocal = _mesh.vertices;

        BuildSamples(vertexStride);
    }

    void BuildSamples(int stride)
    {
        _samples.Clear();

        // Compute per-vertex area weights so bigger triangles contribute more
        var tris = _mesh.triangles;
        int vCount = _vertsLocal.Length;
        var weights = new float[vCount];

        for (int i = 0; i < tris.Length; i += 3)
        {
            int i0 = tris[i];
            int i1 = tris[i + 1];
            int i2 = tris[i + 2];

            Vector3 a = _vertsLocal[i0];
            Vector3 b = _vertsLocal[i1];
            Vector3 c = _vertsLocal[i2];
            float area = Vector3.Cross(b - a, c - a).magnitude * 0.5f;

            // Distribute triangle area to its three vertices
            weights[i0] += area / 3f;
            weights[i1] += area / 3f;
            weights[i2] += area / 3f;
        }

        // Downsample vertices by stride, but keep weights
        for (int i = 0; i < vCount; i += Mathf.Max(1, stride))
        {
            float w = weights[i];
            if (w <= 0f) continue;
            _samples.Add(new Sample { index = i, weight = w });
        }

        // Normalize weights so the sum == 1 (more robust to mesh scale)
        float sum = 0f;
        foreach (var s in _samples) sum += s.weight;
        if (sum > 1e-6f)
        {
            for (int i = 0; i < _samples.Count; i++)
            {
                var s = _samples[i];
                s.weight /= sum;
                _samples[i] = s;
            }
        }

        // Hint: increase/downsample by changing vertexStride in the inspector.
        // 1 = all vertices (most accurate), 3..6 = good compromise, 10+ = fast.
    }

    void FixedUpdate()
    {
        if (!water || _mesh == null) return;

        // Split angular damping: keep yaw low so differential steering works
        Vector3 w = boat.angularVelocity;
        Vector3 yaw = Vector3.Project(w, Vector3.up);
        Vector3 rollPitch = w - yaw;
        boat.AddTorque(-yaw * yawAngularDrag, ForceMode.Acceleration);
        boat.AddTorque(-rollPitch * rollPitchAngularDrag, ForceMode.Acceleration);

        // For each sampled vertex
        for (int si = 0; si < _samples.Count; si++)
        {
            var s = _samples[si];
            Vector3 vLocal = _vertsLocal[s.index];
            Vector3 vWorld = meshTransform.TransformPoint(vLocal);

            // Query HDRP water height at this point
            _search.startPositionWS = vWorld;
            water.ProjectPointOnWaterSurface(_search, out _result);
            float waterY = _result.projectedPositionWS.y;

            float depth = waterY - vWorld.y; // > 0 if submerged
            if (depth <= 0f) continue;

            // Buoyancy scaling with depth (soft clamp at fullBuoyDepth)
            float disp = Mathf.Clamp01(depth / Mathf.Max(0.001f, fullBuoyDepth));

            // Upward buoyant acceleration at this vertex (area-weighted)
            float g = Mathf.Abs(Physics.gravity.y);
            Vector3 buoy = Vector3.up * waterDensity * g * disp * s.weight * buoyancyScale;

            boat.AddForceAtPosition(buoy, vWorld, ForceMode.Force);

            // Water drag (planar + normal) at the point
            Vector3 pointVel = boat.GetPointVelocity(vWorld);
            Vector3 tangential = Vector3.ProjectOnPlane(pointVel, Vector3.up);
            Vector3 normal = Vector3.up * Vector3.Dot(pointVel, Vector3.up);

            boat.AddForceAtPosition(-tangential * planarDrag * disp * s.weight, vWorld, ForceMode.Force);
            boat.AddForceAtPosition(-normal * normalDrag * disp * s.weight, vWorld, ForceMode.Force);

            if (drawForces)
            {
                Debug.DrawRay(vWorld, buoy * 0.001f, Color.green); // scaled for visibility
            }
        }
    }

    void OnValidate()
    {
        if (vertexStride < 1) vertexStride = 1;
        if (Application.isPlaying) return;

        // Rebuild samples in editor when changing stride
        if (_mesh == null)
        {
            var mf = meshFilter ? meshFilter : GetComponent<MeshFilter>();
            if (useColliderMesh && meshCollider && meshCollider.sharedMesh)
                _mesh = meshCollider.sharedMesh;
            else if (mf) _mesh = mf.sharedMesh;

            if (_mesh != null) _vertsLocal = _mesh.vertices;
        }
        if (_mesh != null && _vertsLocal != null) BuildSamples(vertexStride);
    }

    void OnDrawGizmosSelected()
    {
        if (!drawSampledVertices || _mesh == null || _samples == null) return;
        Transform t = meshTransform ? meshTransform : transform;
        Gizmos.color = Color.cyan;
        for (int i = 0; i < _samples.Count; i++)
        {
            Vector3 p = t.TransformPoint(_vertsLocal[_samples[i].index]);
            Gizmos.DrawSphere(p, 0.03f);
        }
    }
}

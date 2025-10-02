using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Rendering.HighDefinition;

public class Floater : MonoBehaviour
{
    // --- Public settings ---
    public Rigidbody boat;
    public float depthBefSub;
    public float displacementAmt;
    public int floaters;
    public float waterDrag;
    public float waterAngularDrag;
    public WaterSurface water;

    // --- Internal water query state ---
    WaterSearchParameters Search;
    WaterSearchResult SearchResult;

    private void FixedUpdate()
    {
        // Apply gravity at floater point
        boat.AddForceAtPosition(
            Physics.gravity / floaters,
            transform.position,
            ForceMode.Acceleration
        );

        // Set up water surface search
        Search.startPositionWS = transform.position;
        water.ProjectPointOnWaterSurface(Search, out SearchResult);

        // If floater is below the water surface
        if (transform.position.y < SearchResult.projectedPositionWS.y)
        {
            // How submerged are we?
            float displacementMulti = Mathf.Clamp01(
                (SearchResult.projectedPositionWS.y - transform.position.y) / depthBefSub
            ) * displacementAmt;

            // Apply buoyancy
            boat.AddForceAtPosition(
                new Vector3(0f, Mathf.Abs(Physics.gravity.y) * displacementMulti, 0f),
                transform.position,
                ForceMode.Acceleration
            );

            // Apply water drag
            boat.AddForce(
                displacementMulti * -boat.linearVelocity * waterDrag * Time.fixedDeltaTime,
                ForceMode.VelocityChange
            );

            // Apply water angular drag
            boat.AddTorque(
                displacementMulti * -boat.angularVelocity * waterAngularDrag * Time.fixedDeltaTime,
                ForceMode.VelocityChange
            );
        }
    }
}
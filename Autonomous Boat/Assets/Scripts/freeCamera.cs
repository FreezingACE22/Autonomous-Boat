using UnityEngine;

public class CameraController : MonoBehaviour
{
    public GameObject boat;
    public float rotationSpeed = 5.0f;
    public float zoomSpeed = 10.0f;
    public float minZoom = 5.0f;
    public float maxZoom = 20.0f;
    public float minY = 1.0f;

    private float currentZoom = 10.0f;
    private float yaw = 0.0f;
    private float pitch = 20.0f;
    private Vector3 offset;

    void Start()
    {
        offset = transform.position - boat.transform.position;
        currentZoom = offset.magnitude;
    }

    void LateUpdate()
    {

        if (Input.GetMouseButton(0))
        {
            yaw += Input.GetAxis("Mouse X") * rotationSpeed;
            pitch -= Input.GetAxis("Mouse Y") * rotationSpeed;


            pitch = Mathf.Clamp(pitch, -40f, 80f);
        }


        float scroll = Input.GetAxis("Mouse ScrollWheel");
        currentZoom -= scroll * zoomSpeed;
        currentZoom = Mathf.Clamp(currentZoom, minZoom, maxZoom);


        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);
        Vector3 newOffset = rotation * Vector3.forward * -currentZoom;


        Vector3 desiredPosition = boat.transform.position + newOffset;


        desiredPosition.y = Mathf.Max(desiredPosition.y, minY);


        transform.position = desiredPosition;


        transform.LookAt(boat.transform.position);
    }
}
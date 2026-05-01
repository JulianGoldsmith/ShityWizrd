using UnityEngine;
using UnityEngine.InputSystem; // Switched to the New Input System!

[RequireComponent(typeof(Camera))]
public class XPBDCamera : MonoBehaviour
{
    [Header("Camera Focus")]
    [Tooltip("The object the camera orbits around. If left empty, it will create an invisible target automatically.")]
    public Transform target;

    [Header("Orbit & Pan Settings")]
    public float distance = 5.0f;
    public float orbitSpeed = 2.0f;
    public float panSpeed = 0.5f;
    public float zoomSpeed = 0.05f;

    private float _yaw = 0.0f;
    private float _pitch = 20.0f;

    [Header("Physics Interaction")]
    [Tooltip("How much force is applied per unit of mouse drag.")]
    public float dragForceMultiplier = 10.0f;
    [Tooltip("If true, dragging acts like a slingshot. If false, it pulls the object towards the mouse.")]
    public bool slingshotMode = false;

    // Interaction State
    private Camera _cam;
    private Rigidbody _draggedRb;
    private float _dragZDepth;
    private Vector3 _startDragWorldPos;

    public static Rigidbody SelectedBone { get; private set; }

    void Start()
    {
        _cam = GetComponent<Camera>();

        // If no target is assigned, create a dummy pivot point at the origin
        if (target == null)
        {
            GameObject dummyTarget = new GameObject("Camera_Pivot_Target");
            target = dummyTarget.transform;
            target.position = Vector3.up * 1.5f;
        }

        Vector3 angles = transform.eulerAngles;
        _yaw = angles.y;
        _pitch = angles.x;
    }

    void LateUpdate()
    {
        if (Mouse.current == null) return; // Safety check

        HandleCameraMovement();
        HandlePhysicsDrag();
    }

    private void HandleCameraMovement()
    {
        if (!target) return;

        // 1. Zooming (Scroll Wheel)
        float scroll = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            distance -= scroll * zoomSpeed;
            distance = Mathf.Clamp(distance, 1.0f, 20.0f);
        }

        // Mouse Delta (Scaled down because the new input system uses raw pixels)
        Vector2 mouseDelta = Mouse.current.delta.ReadValue() * 0.1f;

        // 2. Orbiting (Right Mouse Button)
        if (Mouse.current.rightButton.isPressed)
        {
            _yaw += mouseDelta.x * orbitSpeed;
            _pitch -= mouseDelta.y * orbitSpeed;
            _pitch = Mathf.Clamp(_pitch, -89f, 89f);
        }

        // 3. Panning (Middle Mouse Button)
        if (Mouse.current.middleButton.isPressed)
        {
            float panX = -mouseDelta.x * panSpeed;
            float panY = -mouseDelta.y * panSpeed;

            // Move the target relative to the camera's current orientation
            target.position += transform.right * panX + transform.up * panY;
        }

        // 4. Apply transforms
        Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0);
        Vector3 position = target.position - (rotation * Vector3.forward * distance);

        transform.rotation = rotation;
        transform.position = position;
    }

    private void HandlePhysicsDrag()
    {
        Vector2 mousePos2D = Mouse.current.position.ReadValue();

        // On Click: Try to grab a Rigidbody
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Ray ray = _cam.ScreenPointToRay(mousePos2D);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (hit.rigidbody != null)
                {
                    SelectedBone = hit.rigidbody;
                    _draggedRb = hit.rigidbody;
                    _dragZDepth = hit.distance; // Remember how far away the object is from the camera
                    _startDragWorldPos = hit.point;
                }
            }
        }

        // While Holding: Draw a debug line to show the force vector
        if (Mouse.current.leftButton.isPressed && _draggedRb != null)
        {
            Vector3 currentMouseWorldPos = _cam.ScreenToWorldPoint(new Vector3(mousePos2D.x, mousePos2D.y, _dragZDepth));

            Color dragColor = slingshotMode ? Color.red : Color.green;
            Debug.DrawLine(_startDragWorldPos, currentMouseWorldPos, dragColor);
        }

        // On Release: Apply the Impulse
        if (Mouse.current.leftButton.wasReleasedThisFrame && _draggedRb != null)
        {
            Vector3 currentMouseWorldPos = _cam.ScreenToWorldPoint(new Vector3(mousePos2D.x, mousePos2D.y, _dragZDepth));

            // Calculate the 3D vector of the mouse drag
            Vector3 dragVector = currentMouseWorldPos - _startDragWorldPos;

            if (slingshotMode)
            {
                dragVector = -dragVector; // Reverse it like a slingshot
            }

            // Apply force!
            _draggedRb.AddForceAtPosition(dragVector * dragForceMultiplier, _startDragWorldPos, ForceMode.Impulse);

            // Clear the reference
            _draggedRb = null;
        }
    }
}
using UnityEngine;
using UnityEngine.InputSystem;

public class PhysicsDragDebugger : MonoBehaviour
{
    [SerializeField] private float forceMultiplier = 10f;

    private Camera mainCamera;
    private Vector2 dragStartPosition;
    private Rigidbody selectedRigidbody;

    void Awake()
    {
        mainCamera = Camera.main;
    }

    void Update()
    {
        if (Mouse.current == null)
        {
            return;
        }

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            dragStartPosition = Mouse.current.position.ReadValue();

            Ray ray = mainCamera.ScreenPointToRay(dragStartPosition);
            if (Physics.Raycast(ray, out RaycastHit hitInfo))
            {
                Rigidbody rb = hitInfo.collider.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    selectedRigidbody = rb;
                }
            }
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            if (selectedRigidbody != null)
            {
                Vector2 dragEndPosition = Mouse.current.position.ReadValue();
                Vector2 dragVectorScreen = dragEndPosition - dragStartPosition;

                Vector3 forceDirectionWorld = (mainCamera.transform.right * dragVectorScreen.x + mainCamera.transform.up * dragVectorScreen.y).normalized;
                float forceMagnitude = dragVectorScreen.magnitude * forceMultiplier;
                Vector3 impulse = forceDirectionWorld * forceMagnitude;

                selectedRigidbody.AddForce(impulse, ForceMode.Impulse);

                selectedRigidbody = null;
            }
        }
    }
}

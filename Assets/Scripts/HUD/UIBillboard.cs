using UnityEngine;

public class UIBillboard : MonoBehaviour
{
    private Camera _mainCamera;

    void Start()
    {
        _mainCamera = Camera.main;
    }

    void LateUpdate()
    {
        if (_mainCamera != null)
        {
            // Makes the UI face the camera, maintaining its upright orientation
            transform.LookAt(transform.position + _mainCamera.transform.rotation * Vector3.forward,
                             _mainCamera.transform.rotation * Vector3.up);
        }
    }
}
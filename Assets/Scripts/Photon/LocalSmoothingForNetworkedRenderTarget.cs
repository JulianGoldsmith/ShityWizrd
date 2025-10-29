using UnityEngine;
using Fusion;

/// <summary>
/// This script provides a smoothed transform that follows a jittery network target.
/// This is ideal for smoothing out prediction "snaps" from NetworkRigidbodies.
/// 
/// It should be placed on a new, empty GameObject. All visual components
/// (like the camera anchor and visual animators) should then follow THIS
/// object's transform instead of the raw networked object.
/// </summary>
public class LocalSmoothingForNetworkedRenderTarget : MonoBehaviour
{
    public Transform target;

    public float baseSmoothingFactor = 20f;

    public bool scaleByPing = true;

    public float pingScalar = 5.0f;

    private NetworkRunner _runner;

    public bool unParentOnSpawn = false;

    public bool syncScale = true;

    void Start()
    {
        // Try to get the runner from the singleton you mentioned
        if (GameController.Instance != null)
        {
            _runner = GameController.Instance.networkingController._runner;
        }

        if (target == null)
        {
            Debug.LogError("LocalSmoothingForNetworkedRenderTarget: Target is not set!", this);
            enabled = false;
            return;
        }

        if(unParentOnSpawn)
            this.transform.parent = null;

        transform.position = target.position;
        transform.rotation = target.rotation;

        this.name = target.name + " -- Smoothing target";
    }

    void LateUpdate()
    {
        if (target == null) return;

        float dt = Time.deltaTime;
        if (dt <= 1e-6f) return; 

        float currentSmoothing = baseSmoothingFactor;

        if (scaleByPing && _runner != null && _runner.IsRunning)
        {

            double pingInSeconds = _runner.GetPlayerRtt(_runner.LocalPlayer);


            float pingScale = 1.0f + ((float)pingInSeconds * pingScalar);

            currentSmoothing = baseSmoothingFactor / pingScale;
        }


        transform.position = Vector3.Lerp(transform.position, target.position, dt * currentSmoothing);
        transform.rotation = Quaternion.Slerp(transform.rotation, target.rotation, dt * currentSmoothing);

        if (syncScale)
        {
            transform.localScale = target.localScale;
        }
    }

    void Teleport()
    {
        transform.position = target.position;
        transform.rotation = transform.rotation;

        if (syncScale)
        {
            transform.localScale = target.localScale;
        }
    }
}

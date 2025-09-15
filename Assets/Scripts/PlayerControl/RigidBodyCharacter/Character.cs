using UnityEngine;

//acts as a cental character hub place for other scritpts
[RequireComponent(typeof(Rigidbody))]
public class Character : MonoBehaviour
{
    public Rigidbody Rigidbody { get; private set; }
    public Collider Collider { get; private set; }
    public GameObject CharacterModel { get; private set; }

    public bool IsGrounded { get; set; }


    private void Awake()
    {
        // Find and assign our core components.
        Rigidbody = GetComponent<Rigidbody>();
        Collider = GetComponent<Collider>();

        Transform modelTransform = transform.Find("Model");
        if (modelTransform != null)
        {
            CharacterModel = modelTransform.gameObject;
        }
    }
}

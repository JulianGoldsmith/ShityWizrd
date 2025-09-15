using UnityEngine;

public class SimpleMovementSB : SpellBehaviour
{
    public float speed = 5;
    private Rigidbody rb;

    void Awake()
    {
        rb = SpellSystemHelpers.AddDefaultSpellRigidBodyToGameObject(this.gameObject);
    }

    void Start()
    {
        rb.linearVelocity = transform.forward * speed;
    }

    void FixedUpdate()
    {
        
    }
}

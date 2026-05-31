using System.Collections;
using UnityEngine;
using UnityEngine.VFX;

public class ShatterVFX : MonoBehaviour
{
    [SerializeField] float seconds_to_despawn;

    public void AssignProperties(PhysicsObjectProperties props, float bonk_amount)
    {
        VisualEffect effect = GetComponent<VisualEffect>();
        effect.SetVector4("ParticleColor",props.physicsobjectmaterial.shatter_particle_color);
        effect.SetFloat("Size",props.Size);
        effect.SetFloat("FinalHit",bonk_amount);
    }
    void Start()
    {
        StartCoroutine(WaitToDespawn());
    }
    IEnumerator WaitToDespawn()
    {
        float t = seconds_to_despawn;
        while (t > 0)
        {
            t -= Time.deltaTime;
            yield return null;
        }
        Destroy(gameObject);
    }
}

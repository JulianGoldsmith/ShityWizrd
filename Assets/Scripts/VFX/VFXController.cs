using UnityEngine;

public class VFXController : MonoBehaviour
{
    public float SizeMult = 1f;

    void Awake()
    {
        transform.localScale *= SizeMult;
    }
}
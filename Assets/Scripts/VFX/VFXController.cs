using UnityEngine;

public class VFXController : MonoBehaviour
{
    public float SizeMult = 1f;

    public void Initialize()
    {
        transform.localScale *= SizeMult;
    }
}
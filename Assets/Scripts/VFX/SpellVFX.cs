using UnityEngine;
using UnityEngine.VFX;

[RequireComponent(typeof(VisualEffect))]
public class SpellVFX : MonoBehaviour
{
    private VisualEffect _vfx;

    // These strings MUST match the "Reference" field in the VFX Graph Inspector
    private readonly int _radiusID = Shader.PropertyToID("Radius");
    private readonly int _magnitudeID = Shader.PropertyToID("Magnitude");
    private readonly int _originID = Shader.PropertyToID("Origin");
    private readonly int _targetID = Shader.PropertyToID("Target");
    private readonly int _themeColorID = Shader.PropertyToID("ThemeColor");

    private void Awake()
    {
        _vfx = GetComponent<VisualEffect>();
    }

    public void Initialize(Color themeColor)
    {
        Debug.Log($"[SpellVFX] Setting ThemeColor to {themeColor}");

        // Bypass the HasVector4 check entirely.
        _vfx.SetVector4(_themeColorID, themeColor);
    }

    public void UpdateSpatialData(float radius, float magnitude, Vector3 origin, Vector3 target)
    {
        // Bypass the HasFloat / HasVector3 checks. 
        // If the reference doesn't exist, Unity will just quietly ignore it.
        _vfx.SetFloat(_radiusID, radius);
        _vfx.SetFloat(_magnitudeID, magnitude);
        _vfx.SetVector3(_originID, origin);
        _vfx.SetVector3(_targetID, target);
    }

    public void StopAndCleanup()
    {
        _vfx.Stop();
        Destroy(gameObject, 2f); // Give particles a moment to fade naturally
    }
}
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class XPBDSandboxUI : MonoBehaviour
{
    [Header("UI Elements")]
    public TextMeshProUGUI boneNameText;
    public Slider massSlider;
    public Slider scaleSlider;

    private Rigidbody _currentBone;

    void Update()
    {
        // 1. Check if the camera selected a new bone
        if (XPBDCamera.SelectedBone != _currentBone)
        {
            _currentBone = XPBDCamera.SelectedBone;

            if (_currentBone != null)
            {
                boneNameText.text = "Selected: " + _currentBone.gameObject.name;

                // Update sliders to match the newly selected bone
                massSlider.SetValueWithoutNotify(_currentBone.mass);
                scaleSlider.SetValueWithoutNotify(_currentBone.transform.localScale.x); // Assuming uniform scale
            }
            else
            {
                boneNameText.text = "Selected: None";
            }
        }
    }

    // --- LINK THESE TO THE SLIDER 'OnValueChanged' EVENTS IN THE INSPECTOR ---

    public void OnMassChanged(float newMass)
    {
        if (_currentBone != null)
        {
            _currentBone.mass = newMass;
            // Unity automatically updates the inertia tensor when mass changes!
        }
    }

    public void OnScaleChanged(float newScale)
    {
        if (_currentBone != null)
        {
            // Uniformly scale the visual and the collider
            _currentBone.transform.localScale = new Vector3(newScale, newScale, newScale);
            // Unity automatically updates the inertia tensor when the collider scales!
        }
    }
}
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum MaterialStateDebugValue
{
    Temperature,
    Wetness,
    Charge,

    Stoneify,
    Gooify,

    Frozen,
    Heated,
    Burning,
    Conductive,

    ScaleMultiplier,
    DensityMultiplier,
    GravityMultiplier
}

[Serializable]
public class MaterialStateDebugBar
{
    [Header("State")]
    public MaterialStateDebugValue stateValue;
    public string displayName;

    [Header("Display Range")]
    public float minValue = 0f;
    public float maxValue = 1f;
    public string valueFormat = "0.00";

    [Header("Colours")]
    public Color minColor = Color.clear;
    public Color maxColor = Color.white;

    [Header("UI References")]
    public TMP_Text labelText;
    public Image fillImage;
    public TMP_Text valueText;

    [Header("Optional Baseline")]
    public bool showBaseline;
    public float baselineValue;
    public RectTransform baselineMarker;
}

public class MaterialStateDebugDisplay : MonoBehaviour
{
    [Header("Canvas")]
    [SerializeField]
    private Canvas worldCanvas;

    [Header("Header")]
    [SerializeField]
    private TMP_Text titleText;

    [SerializeField]
    private TMP_Text tickText;

    [Header("Material State Bars")]
    [SerializeField]
    private List<MaterialStateDebugBar> materialStateBars = new List<MaterialStateDebugBar>();

    [Header("Following")]
    [SerializeField]
    private Vector3 panelOffset = new Vector3(0f, 0.15f, 0f);

    [SerializeField]
    private bool positionAboveCollider = true;

    [SerializeField]
    private bool faceCamera = true;

    private PhysicsObjectProperties targetProperties;
    private Collider targetCollider;
    private Camera targetCamera;

    public PhysicsObjectProperties TargetProperties => targetProperties;

    public bool HasValidTarget =>
        targetProperties != null &&
        targetProperties.isActiveAndEnabled &&
        targetProperties.gameObject.activeInHierarchy;

    private void Awake(){
        if (worldCanvas == null)
        {
            worldCanvas = GetComponent<Canvas>();
        }

        ConfigureStaticBarElements();
    }

    public void Bind(PhysicsObjectProperties newTargetProperties,Collider newTargetCollider,Camera newTargetCamera) {
        targetProperties = newTargetProperties;
        targetCollider = newTargetCollider;
        targetCamera = newTargetCamera;

        if (worldCanvas != null)
        {
            worldCanvas.worldCamera = targetCamera;
        }

        ConfigureStaticBarElements();
        UpdateDisplay();
        UpdateFollowTransform();
    }

    private void Update()
    {
        if (!HasValidTarget)
            return;

        UpdateDisplay();
    }

    private void LateUpdate()
    {
        if (!HasValidTarget)
            return;

        UpdateFollowTransform();
    }

    private void ConfigureStaticBarElements()
    {
        foreach (MaterialStateDebugBar materialStateBar in materialStateBars)
        {
            if (materialStateBar == null)
                continue;

            if (materialStateBar.labelText != null)
            {
                materialStateBar.labelText.text = string.IsNullOrWhiteSpace(materialStateBar.displayName) ? materialStateBar.stateValue.ToString()
                        : materialStateBar.displayName;
            }

            UpdateBaselineMarker(materialStateBar);
        }
    }

    private void UpdateDisplay()
    {
        if (!HasValidTarget)
            return;

        NetworkedMaterialState cachedNetworkState = targetProperties.CachedNetworkState;

        MaterialState materialState = cachedNetworkState.State;

        UpdateHeader(cachedNetworkState);

        foreach (MaterialStateDebugBar materialStateBar in materialStateBars)
        {
            if (materialStateBar == null)
                continue;

            float rawValue = GetMaterialStateValue(in materialState, materialStateBar.stateValue);

            float normalizedValue = NormalizeValue(rawValue,materialStateBar.minValue,materialStateBar.maxValue);

            RectTransform fillRect = materialStateBar.fillImage.rectTransform;

            materialStateBar.fillImage.enabled = normalizedValue > 0.001f;

            if (materialStateBar.fillImage.enabled)
            {
                fillRect.anchorMin = new Vector2(0f, 0f);

                fillRect.anchorMax = new Vector2(normalizedValue, 1f);

                fillRect.offsetMin = new Vector2(2f, 2f);

                fillRect.offsetMax =new Vector2(-2f, -2f);

                materialStateBar.fillImage.color = Color.Lerp(
                    materialStateBar.minColor,
                    materialStateBar.maxColor,
                    normalizedValue
                );
            }

            if (materialStateBar.valueText != null)
            {
                string valueFormat =
                    string.IsNullOrWhiteSpace(
                        materialStateBar.valueFormat
                    )
                        ? "0.00"
                        : materialStateBar.valueFormat;

                // The numeric value remains unclamped, even if the
                // visible bar exceeds its configured range.
                materialStateBar.valueText.text =
                    rawValue.ToString(valueFormat);
            }
        }
    }

    private void UpdateHeader(
        NetworkedMaterialState cachedNetworkState)
    {
        if (titleText != null)
        {
            string materialName = "No Material";

            PhysicsObjectMaterial physicsObjectMaterial =
                targetProperties.physicsobjectmaterial;

            if (physicsObjectMaterial != null)
            {
                if (!string.IsNullOrWhiteSpace(
                    physicsObjectMaterial.material_name))
                {
                    materialName =
                        physicsObjectMaterial.material_name;
                }
                else
                {
                    materialName =
                        $"Material {targetProperties.Material_label}";
                }
            }

            titleText.text =
                $"{targetProperties.gameObject.name} — {materialName}";
        }

        if (tickText != null)
        {
            tickText.text =
                $"Cached Tick: {cachedNetworkState.Tick}   " +
                $"Checkpoint: {targetProperties.CheckpointState.Tick}";
        }
    }

    private void UpdateFollowTransform()
    {
        if (!HasValidTarget)
            return;

        Vector3 targetPosition = targetProperties.transform.position;

        Vector3 horizontalOffset = targetCamera != null ? targetCamera.transform.right * panelOffset.x : Vector3.right * panelOffset.x;

        Vector3 verticalOffset = Vector3.up * panelOffset.y;

        Vector3 depthOffset = targetCamera != null ? targetCamera.transform.forward * panelOffset.z : Vector3.forward * panelOffset.z;

        targetPosition += horizontalOffset + verticalOffset + depthOffset;

        if ( positionAboveCollider && targetCollider != null && targetCollider.enabled && targetCollider.gameObject.activeInHierarchy) {
            Bounds colliderBounds = targetCollider.bounds;

            targetPosition = colliderBounds.center + (Vector3.up * colliderBounds.extents.y);
        }

        transform.position = targetPosition;

        if (!faceCamera)
            return;

        if (targetCamera == null)
        {
            targetCamera = Camera.main;

            if (worldCanvas != null)
            {
                worldCanvas.worldCamera = targetCamera;
            }
        }

        if (targetCamera != null)
        {
            transform.LookAt(
                transform.position +
                targetCamera.transform.rotation *
                Vector3.forward,

                targetCamera.transform.rotation *
                Vector3.up
            );
        }
    }

    private void UpdateBaselineMarker(
        MaterialStateDebugBar materialStateBar)
    {
        if (materialStateBar.baselineMarker == null)
            return;

        materialStateBar.baselineMarker.gameObject.SetActive(
            materialStateBar.showBaseline
        );

        if (!materialStateBar.showBaseline)
            return;

        float normalizedBaseline = NormalizeValue(
            materialStateBar.baselineValue,
            materialStateBar.minValue,
            materialStateBar.maxValue
        );

        RectTransform baselineMarker = materialStateBar.baselineMarker;

        baselineMarker.anchorMin =new Vector2(normalizedBaseline, 0f);

        baselineMarker.anchorMax =
            new Vector2(normalizedBaseline, 1f);

        baselineMarker.anchoredPosition =
            Vector2.zero;

        baselineMarker.sizeDelta =
            new Vector2(
                baselineMarker.sizeDelta.x,
                0f
            );
    }

    private float NormalizeValue(
        float rawValue,
        float minValue,
        float maxValue)
    {
        if (Mathf.Approximately(minValue, maxValue))
            return 0f;

        return Mathf.Clamp01(
            Mathf.InverseLerp(
                minValue,
                maxValue,
                rawValue
            )
        );
    }

    private float GetMaterialStateValue(
        in MaterialState materialState,
        MaterialStateDebugValue stateValue)
    {
        switch (stateValue)
        {
            case MaterialStateDebugValue.Temperature:
                return materialState.Temperature;

            case MaterialStateDebugValue.Wetness:
                return materialState.Wetness;

            case MaterialStateDebugValue.Charge:
                return materialState.Charge;

            case MaterialStateDebugValue.Stoneify:
                return materialState.Stoneify;

            case MaterialStateDebugValue.Gooify:
                return materialState.Gooify;

            case MaterialStateDebugValue.Frozen:
                return materialState.Frozen;

            case MaterialStateDebugValue.Heated:
                return materialState.Heated;

            case MaterialStateDebugValue.Burning:
                return materialState.Burning;

            case MaterialStateDebugValue.Conductive:
                return materialState.Conductive;

            case MaterialStateDebugValue.ScaleMultiplier:
                return materialState.ScaleMultiplier;

            case MaterialStateDebugValue.DensityMultiplier:
                return materialState.DensityMultiplier;

            case MaterialStateDebugValue.GravityMultiplier:
                return materialState.GravityMultiplier;

            default:
                return 0f;
        }
    }
}
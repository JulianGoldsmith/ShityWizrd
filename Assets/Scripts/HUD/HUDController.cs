using UnityEngine;
using UnityEngine.UI; // Still needed if you wanted to change color, but not required for this

public class HUDController : MonoBehaviour
{
    public static HUDController Instance { get; private set; }

    [Header("Bonk Bar Elements")]
    [SerializeField] private RectTransform bonkFillRect;

    private Vector2 originalAnchorMax;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }

        if (bonkFillRect != null)
        {
            originalAnchorMax = bonkFillRect.anchorMax;
        }
    }
    public void UpdateBonkBar(float currentBonk, float maxBonk)
    {
        if (bonkFillRect == null)
        {
            Debug.LogWarning("Bonk Fill Rect is not assigned in HUDController!");
            return;
        }

        float fillPercent = currentBonk / maxBonk;
        fillPercent = Mathf.Clamp01(fillPercent);

        bonkFillRect.anchorMax = new Vector2(fillPercent * originalAnchorMax.x, originalAnchorMax.y);
    }
}
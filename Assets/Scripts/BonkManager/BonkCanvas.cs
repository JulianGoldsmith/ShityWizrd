using UnityEngine;
using UnityEngine.UI;

public class BonkCanvas : MonoBehaviour
{
    [SerializeField] private LayoutElement kineticUI;
    [SerializeField] private LayoutElement hotUI;
    [SerializeField] private LayoutElement coldUI;
    [SerializeField] private LayoutElement burnUI;

    public void SetBonkValues(float kinetic, float hot, float cold, float burn, float maxBonk, float maxWidth)
    {
        float widthPerBonk = maxWidth / Mathf.Max(0.0001f, maxBonk);

        SetWidth(kineticUI, kinetic * widthPerBonk);
        SetWidth(hotUI, hot * widthPerBonk);
        SetWidth(coldUI, cold * widthPerBonk);
        SetWidth(burnUI, burn * widthPerBonk);
    }

    private static void SetWidth(LayoutElement element, float width)
    {
        if (element == null) return;

        element.preferredWidth = width;
        element.minWidth = width;
    }
}
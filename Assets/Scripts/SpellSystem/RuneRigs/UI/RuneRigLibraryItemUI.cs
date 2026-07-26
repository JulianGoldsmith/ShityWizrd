using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class RuneRigLibraryItemUI : MonoBehaviour, IPointerDownHandler
{
    public TextMeshProUGUI CapacityText;

    private RuneRigLibraryUI _library;
    private SpellNode _definition;
    private byte _bayCapacity;

    public void Initialize(RuneRigLibraryUI library, SpellNode definition, byte bayCapacity)
    {
        _library = library;
        _definition = definition;
        _bayCapacity = bayCapacity;

        gameObject.name = $"{definition.nodeName}_{bayCapacity}_Bays";

        if (CapacityText != null)
            CapacityText.text = bayCapacity.ToString();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || _library == null || _definition == null)
            return;

        _library.SpawnRune(_definition, _bayCapacity);
    }
}
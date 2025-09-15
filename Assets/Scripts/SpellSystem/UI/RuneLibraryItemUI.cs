using UnityEngine;
using UnityEngine.EventSystems;

//Controls the items in the UI on the right of the sceen when editing spells
public class RuneLibraryItemUI : MonoBehaviour, IPointerDownHandler
{
    public SpellNode spellNodeTemplate;

    private RuneLibraryUI libraryController;

    void Awake()
    {
 
        libraryController = GetComponentInParent<RuneLibraryUI>();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;

        SpellGraphController.Instance.StartDraggingNewRuneFromLibrary(spellNodeTemplate, eventData.position);

        if (libraryController != null)
        {
            libraryController.HidePanel();
        }
    }
}

using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class SavedSpellsUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI References")]
    public Transform contentParent;
    public GameObject savedSpellButtonPrefab;

    [Header("Controls")]
    public TMP_InputField saveNameInputField;
    public Button saveButton;
    public Button clearButton; // NEW: Reference for the clear button

    [Header("Animation Settings")]
    public float panelWidth = 300f;
    public float hiddenVisibleWidth = 20f;
    public float animationSpeed = 10f;

    private RectTransform panelRectTransform;
    private Coroutine panelAnimationCoroutine;

    void Start()
    {
        panelRectTransform = GetComponent<RectTransform>();

        // Hook up the buttons automatically
        if (saveButton != null) saveButton.onClick.AddListener(SaveCurrentSpell);
        if (clearButton != null) clearButton.onClick.AddListener(ClearCurrentSpell);

        Invoke(nameof(PopulateSavedSpellsList), 0.1f);
        HidePanel(true);
    }

    public void SaveCurrentSpell()
    {
        if (SpellGraphController.Instance == null) return;

        string spellName = saveNameInputField.text.Trim();

        if (string.IsNullOrEmpty(spellName))
        {
            Debug.LogWarning("Cannot save: Spell name is empty!");
            return;
        }

        SpellGraphController.Instance.SaveSpellToAssets(spellName);
        PopulateSavedSpellsList();
        saveNameInputField.text = "";
    }

    // NEW: Method to clear the graph
    public void ClearCurrentSpell()
    {
        if (SpellGraphController.Instance == null) return;

        SpellGraphController.Instance.ClearAndCreateNewSpellOnActiveItem();
        HidePanel(); // Slide the menu away after clearing
    }

    public void ShowPanel()
    {
        if (panelAnimationCoroutine != null) StopCoroutine(panelAnimationCoroutine);
        panelAnimationCoroutine = StartCoroutine(AnimatePanel(0f));
    }

    public void HidePanel(bool instant = false)
    {
        if (panelAnimationCoroutine != null) StopCoroutine(panelAnimationCoroutine);

        float targetX = -panelWidth + hiddenVisibleWidth;

        if (instant)
        {
            panelRectTransform.anchoredPosition = new Vector2(targetX, panelRectTransform.anchoredPosition.y);
        }
        else
        {
            panelAnimationCoroutine = StartCoroutine(AnimatePanel(targetX));
        }
    }

    private IEnumerator AnimatePanel(float targetX)
    {
        float startingX = panelRectTransform.anchoredPosition.x;
        float time = 0;

        while (Mathf.Abs(panelRectTransform.anchoredPosition.x - targetX) > 0.1f)
        {
            float newX = Mathf.Lerp(startingX, targetX, time);
            panelRectTransform.anchoredPosition = new Vector2(newX, panelRectTransform.anchoredPosition.y);
            time += Time.deltaTime * animationSpeed;
            yield return null;
        }

        panelRectTransform.anchoredPosition = new Vector2(targetX, panelRectTransform.anchoredPosition.y);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        ShowPanel();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        HidePanel();
    }

    public void PopulateSavedSpellsList()
    {
        // 1. Clear out old buttons, but PROTECT both control panels!
        foreach (Transform child in contentParent)
        {
            if (child.name == "SavePanel" || child.name == "ClearPanel") continue;
            Destroy(child.gameObject);
        }

        TextAsset[] savedSpells = Resources.LoadAll<TextAsset>("BakedSpells");

        if (savedSpells.Length == 0) return;

        VerticalLayoutGroup vlg = contentParent.gameObject.GetComponent<VerticalLayoutGroup>();
        if (vlg == null) vlg = contentParent.gameObject.AddComponent<VerticalLayoutGroup>();

        vlg.padding = new RectOffset(10, 10, 10, 10);
        vlg.spacing = 5;
        vlg.childControlWidth = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        foreach (TextAsset spellAsset in savedSpells)
        {
            GameObject btnObj = Instantiate(savedSpellButtonPrefab, contentParent);

            btnObj.GetComponentInChildren<TextMeshProUGUI>().text = spellAsset.name;

            Button btn = btnObj.GetComponent<Button>();
            string spellNameToLoad = spellAsset.name;

            btn.onClick.AddListener(() =>
            {
                if (SpellGraphController.Instance != null)
                {
                    SpellGraphController.Instance.LoadSpellByNameToCurrentItem(spellNameToLoad);
                    HidePanel();
                }
            });
        }
    }
}
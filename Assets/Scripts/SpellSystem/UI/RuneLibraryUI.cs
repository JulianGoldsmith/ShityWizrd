using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

//Controls the UI on the right of the sceen when editing spells
public class RuneLibraryUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{

    [Header("UI References")]
    public Transform contentParent;
    public GameObject categoryHeaderPrefab; 
    public GameObject runeItemPrefab; 


    public float panelWidth = 300f;
    public float hiddenVisibleWidth = 20f;
    public float animationSpeed = 10f;

    public int runeSize = 16;
    public int runeSpacing = 4;

    private RectTransform panelRectTransform;
    private Coroutine panelAnimationCoroutine;
    private bool isMouseOverPanel = false;

    void Start()
    {
        panelRectTransform = GetComponent<RectTransform>();
        Invoke(nameof(PopulateRuneLibrary), 0.1f);
        HidePanel(true);
    }

    public void ShowPanel()
    {
        if (panelAnimationCoroutine != null)
        {
            StopCoroutine(panelAnimationCoroutine);
        }
        panelAnimationCoroutine = StartCoroutine(AnimatePanel(0));
    }


    public void HidePanel(bool instant = false)
    {
        if (panelAnimationCoroutine != null)
        {
            StopCoroutine(panelAnimationCoroutine);
        }

        float targetX = panelWidth - hiddenVisibleWidth; ;

        if (instant)
        {
            panelRectTransform.offsetMax = new Vector2(targetX, panelRectTransform.offsetMax.y);
            panelRectTransform.offsetMin = new Vector2(targetX - panelWidth, panelRectTransform.offsetMin.y);

        }
        else
        {
            panelAnimationCoroutine = StartCoroutine(AnimatePanel(targetX));
        }
    }

    private IEnumerator AnimatePanel(float targetRightOffsetX)
    {
        float startingRightOffsetX = panelRectTransform.offsetMax.x;
        float time = 0;

        while (Mathf.Abs(panelRectTransform.offsetMax.x - targetRightOffsetX) > 0.1f)
        {

            float newRightOffsetX = Mathf.Lerp(startingRightOffsetX, targetRightOffsetX, time);

            panelRectTransform.offsetMax = new Vector2(newRightOffsetX, panelRectTransform.offsetMax.y);
            panelRectTransform.offsetMin = new Vector2(newRightOffsetX - panelWidth, panelRectTransform.offsetMin.y);

            time += Time.deltaTime * animationSpeed;
            yield return null;
        }

        panelRectTransform.offsetMax = new Vector2(targetRightOffsetX, panelRectTransform.offsetMax.y);
        panelRectTransform.offsetMin = new Vector2(targetRightOffsetX - panelWidth, panelRectTransform.offsetMin.y);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isMouseOverPanel = true;
        ShowPanel();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isMouseOverPanel = false;
        HidePanel();
    }

    public void PopulateRuneLibrary()
    {
        if (SpellGraphController.Instance == null)
        {
            Debug.LogError("SpellGraphController instance not found!");
            return;
        }

        foreach (Transform child in contentParent)
        {
            Destroy(child.gameObject);
        }

        var allNodes = SpellGraphController.Instance.availableNodeTemplates;

        var nodesByCategory = allNodes
        .Where(n => n != null)
        .GroupBy(n => n.category)
        .OrderBy(g => (int)g.Key);

        foreach (var group in nodesByCategory)
        {
            string categoryTitle = group.Key.ToString() + "s"; 
            List<SpellNode> nodesInGroup = group.ToList();

            PopulateRuneCategory(categoryTitle, nodesInGroup);
        }
    }

    void PopulateRuneCategory(string title, List<SpellNode> nodes)
    {
        if (nodes == null || nodes.Count == 0) return;

        GameObject headerObj = Instantiate(categoryHeaderPrefab, contentParent);
        headerObj.GetComponent<TextMeshProUGUI>().text = title;

        GameObject runeContainer = new GameObject(title + " Container");
        runeContainer.transform.SetParent(contentParent, false);

        GridLayoutGroup glg = runeContainer.AddComponent<GridLayoutGroup>();
        glg.padding = new RectOffset(10, 10, 10, 10); 
        glg.spacing = new Vector2(runeSpacing, runeSpacing);            
        glg.cellSize = new Vector2(runeSize, runeSize);          
        glg.constraint = GridLayoutGroup.Constraint.Flexible;

        runeContainer.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        foreach (var node in nodes)
        {
            if (node == null) continue;

            GameObject itemObj = Instantiate(runeItemPrefab, runeContainer.transform);

            Image iconImage = itemObj.GetComponent<Image>();
            if (iconImage != null && node.icon != null)
            {
                iconImage.sprite = Sprite.Create(node.icon, new Rect(0, 0, node.icon.width, node.icon.height), new Vector2(0.5f, 0.5f));
            }
            RuneLibraryItemUI itemUI = itemObj.GetComponent<RuneLibraryItemUI>();
            if (itemUI != null)
            {
                itemUI.spellNodeTemplate = node;
            }
        }
    }
}
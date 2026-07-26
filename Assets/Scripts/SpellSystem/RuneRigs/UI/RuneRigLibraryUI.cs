using System.Collections.Generic;
using System.Linq;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
public class RuneRigLibraryUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Data")]
    public MasterNodeDictionary NodeDictionary;

    [Header("UI")]
    public Transform ContentParent;
    public GameObject CategoryHeaderPrefab;
    public GameObject RuneItemPrefab;

    [Header("Layout")]
    public int RuneSize = 42;
    public int RuneSpacing = 5;

    [Header("Panel Expansion")]
    public bool StartExpanded = false;
    public float CollapsedVisibleWidth = 20f;
    public float ExpandSpeed = 1000f;

    private RectTransform _panelRect;
    private float _expandedX;
    private bool _isExpanded;

    private float CollapsedX => _expandedX + Mathf.Max(0f, _panelRect.rect.width - CollapsedVisibleWidth);

    private void Start()
    {
        Populate();
    }

    private void Awake()
    {
        _panelRect = GetComponent<RectTransform>();
        _expandedX = _panelRect.anchoredPosition.x;
    }

    private void OnEnable()
    {
        _isExpanded = StartExpanded;

        Vector2 position = _panelRect.anchoredPosition;
        position.x = _isExpanded ? _expandedX : CollapsedX;
        _panelRect.anchoredPosition = position;
    }

    private void Update()
    {
        float targetX = _isExpanded ? _expandedX : CollapsedX;
        Vector2 position = _panelRect.anchoredPosition;
        position.x = Mathf.MoveTowards(position.x, targetX, ExpandSpeed * Time.unscaledDeltaTime);
        _panelRect.anchoredPosition = position;
    }

    public void Expand()
    {
        _isExpanded = true;
    }

    public void Collapse()
    {
        _isExpanded = false;
    }

    public void ToggleExpanded()
    {
        _isExpanded = !_isExpanded;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        Expand();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Collapse();
    }

    [ContextMenu("Populate Rune Rig Library")]
    public void Populate()
    {
        if (NodeDictionary == null || ContentParent == null || CategoryHeaderPrefab == null || RuneItemPrefab == null)
            return;

        foreach (Transform child in ContentParent)
            Destroy(child.gameObject);

        var nodesByCategory = NodeDictionary.BakedNodes
            .Where(node => node != null && node.PhysicalRune != null && node.PhysicalRune.PhysicalPrefab != null)
            .GroupBy(node => node.GetRuneType())
            .OrderBy(group => (int)group.Key);

        foreach (var group in nodesByCategory)
            PopulateCategory(group.Key.ToString(), group.ToList());
    }

    private void PopulateCategory(string title, List<SpellNode> definitions)
    {
        if (definitions.Count == 0)
            return;

        GameObject headerObject = Instantiate(CategoryHeaderPrefab, ContentParent);
        TextMeshProUGUI headerText = headerObject.GetComponent<TextMeshProUGUI>();

        if (headerText != null)
            headerText.text = title;

        GameObject runeContainer = new GameObject($"{title} Container");
        runeContainer.transform.SetParent(ContentParent, false);

        GridLayoutGroup grid = runeContainer.AddComponent<GridLayoutGroup>();
        grid.padding = new RectOffset(10, 10, 10, 10);
        grid.spacing = new Vector2(RuneSpacing, RuneSpacing);
        grid.cellSize = new Vector2(RuneSize, RuneSize);
        grid.constraint = GridLayoutGroup.Constraint.Flexible;

        ContentSizeFitter sizeFitter = runeContainer.AddComponent<ContentSizeFitter>();
        sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        foreach (SpellNode definition in definitions)
        {
            PhysicalRuneSettings settings = definition.PhysicalRune;
            List<byte> capacities;

            if (settings.AllowedBayCapacities != null && settings.AllowedBayCapacities.Count > 0)
            {
                capacities = settings.AllowedBayCapacities
                    .Where(capacity => capacity <= settings.MaximumBayCapacity)
                    .Distinct()
                    .OrderBy(capacity => capacity)
                    .ToList();
            }
            else
            {
                capacities = Enumerable.Range(0, settings.MaximumBayCapacity + 1)
                    .Select(capacity => (byte)capacity)
                    .ToList();
            }

            foreach (byte capacity in capacities)
            {
                GameObject itemObject = Instantiate(RuneItemPrefab, runeContainer.transform);
                Image icon = itemObject.GetComponent<Image>();

                if (icon != null && definition.icon != null)
                    icon.sprite = Sprite.Create(definition.icon, new Rect(0, 0, definition.icon.width, definition.icon.height), new Vector2(0.5f, 0.5f));

                RuneRigLibraryItemUI item = itemObject.GetComponent<RuneRigLibraryItemUI>();

                if (item != null)
                    item.Initialize(this, definition, capacity);
            }
        }
    }

    public void SpawnRune(SpellNode definition, byte bayCapacity)
    {
        if (definition == null || GameController.Instance == null || GameController.Instance.networkingController == null)
            return;

        NetworkRunner runner = GameController.Instance.networkingController._runner;

        if (runner == null || !runner.IsRunning)
            return;

        if (!runner.TryGetPlayerObject(runner.LocalPlayer, out NetworkObject playerObject))
            return;

        if (!playerObject.TryGetComponent(out RuneRigSpawnController spawnController))
            return;

        if (!spawnController.QueueRuneDefinitionSpawn(definition.NetworkNodeID, bayCapacity))
            Debug.LogWarning($"Could not queue '{definition.nodeName}' with {bayCapacity} bays.", this);
    }
}
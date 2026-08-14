using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Linq;
using TMPro;
using Unity.Collections;

public class GameController : MonoBehaviour
{
    public static GameController Instance { get; private set; }
    public static bool gamePlayActive;

    public PlayerInput playerInput;

    //public CharacterCameraController mainCameraController;

    [Header("Spell Editing")]
    [SerializeField] private bool _useLegacyEditor = false;
    [SerializeField] private GameObject runeSpawnerUI;

    public GameObject spellEditorWorld;
    public SpellGraphController spellGraphController;

    public bool isEditorActive;

    public BasicSpawner networkingController;

    public LevelGenerator levelGenerator;
    public LevelNetworkController levelNetworkController;

    public TMP_Text textDisplay;

    public SpellStateManager spellStateManager;

    public XPBDGlobalManager xPBDGlobalManager;

    public CustomPhysicsFormulas customPhysicsFormulas;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            Instance = this;
        }
        if (customPhysicsFormulas == null) customPhysicsFormulas = GetComponent<CustomPhysicsFormulas>();

        if (levelGenerator != null)
        {
            levelGenerator.OnLevelReady += TeleportExistingPlayers;
        }
        if (levelNetworkController == null)
        {
            levelNetworkController = GetComponent<LevelNetworkController>(); ;
        }
        if(spellStateManager == null)
        {
            spellStateManager = GetComponent<SpellStateManager>();
        }
        if(xPBDGlobalManager == null)
        {
            xPBDGlobalManager = GetComponent<XPBDGlobalManager>();
        }
    }

    void Start()
    {
        if (spellEditorWorld != null)
            spellEditorWorld.SetActive(false);

        if (runeSpawnerUI != null)
            runeSpawnerUI.SetActive(false);

        isEditorActive = false;

        if (playerInput != null)
            EnableGameplayInput();
    }

    void Update()
    {
        if (playerInput == null) return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame )
        {
            if (gamePlayActive)
            {
                EnableUIInput();
            }
            else
            {
                EnableGameplayInput();
            }
        }

        string blueprintSpells = "";
        if(spellStateManager.active_spellblueprints.Count < 10)
        {
            blueprintSpells = $"BluePrintSpells = {spellStateManager.active_spellblueprints.Count}";
            foreach (var kvp in spellStateManager.active_spellblueprints)
            {
                blueprintSpells += $"\n SpellGraphID: {kvp.Key.ToString()}";
            }
        }
        else
        {
            blueprintSpells = $"BluePrintSpells = {spellStateManager.active_spellblueprints.Count}";
        }

        if (textDisplay != null)
            textDisplay.text = $"ActiveSpells = {spellStateManager.activeSpells.Count} \n " +
                blueprintSpells;
    }

    public void EnableGameplayInput()
    {
        gamePlayActive=true;
        playerInput.SwitchCurrentActionMap("Gameplay");
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void EnableUIInput()
    {
        gamePlayActive = false;
        playerInput.SwitchCurrentActionMap("UI");
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ToggleSpellEditor(bool _overrideUseLegacyEditor = false)
    {
        bool legacy = _overrideUseLegacyEditor? !_useLegacyEditor : _useLegacyEditor; 

        GameObject selectedInterface = legacy ? spellEditorWorld : runeSpawnerUI;

        if (selectedInterface == null)
        {
            Debug.LogError(legacy ? "Legacy spell editor is not assigned." : "Rune spawner UI is not assigned.", this);
            return;
        }

        bool shouldOpen = !selectedInterface.activeSelf;

        if (spellEditorWorld != null)
            spellEditorWorld.SetActive(false);

        if (runeSpawnerUI != null)
            runeSpawnerUI.SetActive(false);

        isEditorActive = shouldOpen;

        if (!shouldOpen)
        {
            EnableGameplayInput();
            return;
        }

        selectedInterface.SetActive(true);
        EnableUIInput();

        if (!legacy)
            return;

        Vector3 position = Vector3.zero;

        if (networkingController != null && networkingController._runner != null && networkingController._runner.TryGetPlayerObject(networkingController._runner.LocalPlayer, out NetworkObject player))
            position = Camera.main.transform.position + Camera.main.transform.forward * 1.5f;

        if (SpellGraphController.Instance != null)
            SpellGraphController.Instance.EditSpellFromActiveItem(position);
    }

    private void TeleportExistingPlayers()
    {
        if (networkingController == null || networkingController._runner == null || !networkingController._runner.IsServer)
            return;

        Debug.Log("Level is ready. Teleporting all existing players...");

        Vector3 spawnPoint = levelGenerator.StartRoomSpawnPoint.position;

        foreach (var playerObject in networkingController._spawnedCharacters.Values)
        {
            if (playerObject != null && playerObject.TryGetComponent<HybridCharacterController>(out var controller))
            {
                controller.TeleportTo(spawnPoint, Quaternion.identity);
            }
        }
    }

}


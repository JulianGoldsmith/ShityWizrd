using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Linq;

public class GameController : MonoBehaviour
{
    public static GameController Instance { get; private set; }
    public static bool gamePlayActive;

    public PlayerInput playerInput;

    //public CharacterCameraController mainCameraController;

    public GameObject spellEditorWorld; 

    public bool isEditorActive = false;

    public VFXDatabase vfxDatabase;

    public BasicSpawner networkingController;

    public LevelGenerator levelGenerator;
    public LevelNetworkController levelNetworkController;

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
        if (levelGenerator != null)
        {
            levelGenerator.OnLevelReady += TeleportExistingPlayers;
        }
        if (levelNetworkController == null)
        {
            levelNetworkController = GetComponent<LevelNetworkController>(); ;
        }
        
    }

    void Start()
    {
        if(spellEditorWorld != null)
            spellEditorWorld.SetActive(false);

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

    public void ToggleSpellEditor()
    {
        isEditorActive = !spellEditorWorld.activeSelf;
        spellEditorWorld.SetActive(isEditorActive);
        Debug.Log("ToggleSpellEditor");
        if (isEditorActive)
        {
            Vector3 pos = Vector3.zero;
            if(networkingController._runner.TryGetPlayerObject(networkingController._runner.LocalPlayer, out NetworkObject player))
            {
                pos = player.GetComponent<HybridCharacterController>().hipsRb.transform.position;
                pos.y -= 0.58f;

            }
            SpellGraphController.Instance.EditSpellFromActiveItem(pos + Vector3.forward * 0.5f);
            EnableUIInput();
            //mainCameraController.SwitchToEditorView();
        }
        else
        {
            EnableGameplayInput();
            //mainCameraController.SwitchToGameplayView();
        }
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


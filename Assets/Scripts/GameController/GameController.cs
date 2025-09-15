using UnityEngine;
using UnityEngine.InputSystem;

public class GameController : MonoBehaviour
{
    public static GameController Instance { get; private set; }
    public static bool gamePlayActive;

    public PlayerInput playerInput;

    public CharacterCameraController mainCameraController;

    public GameObject spellEditorWorld; 

    public bool isEditorActive = false;

    public VFXDatabase vfxDatabase;

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
        playerInput = Object.FindAnyObjectByType<PlayerInput>();
    }

    void Start()
    {
        spellEditorWorld.SetActive(false);
        EnableGameplayInput();
    }

    void Update()
    {
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (gamePlayActive) {
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

        if (isEditorActive)
        {
            SpellGraphController.Instance.EditSpellFromActiveItem();
            EnableUIInput();
            mainCameraController.SwitchToEditorView();
        }
        else
        {
            EnableGameplayInput();
            mainCameraController.SwitchToGameplayView();
        }
    }

    
}


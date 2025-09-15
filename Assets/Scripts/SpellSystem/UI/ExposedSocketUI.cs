using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ExposedSocketUI : MonoBehaviour
{
    public TMP_InputField nameInputField;
    public Image socketColorImage;
    public Button removeButton;

    private ExposedSocketInfo socketData;
    private SpellGraphController controller;

    public void Initialize(ExposedSocketInfo data, SpellGraphController spellGraphController)
    {
        this.socketData = data;
        this.controller = spellGraphController;

        nameInputField.text = data.exposedName;
        socketColorImage.color = GetColorForSocketType(data.type);

        nameInputField.onEndEdit.AddListener(OnNameChanged);
        removeButton.onClick.AddListener(OnRemoveClicked);
    }

    private void OnNameChanged(string newName)
    {
        controller.UpdateExposedSocketName(socketData.internalNodeGuid, socketData.internalSocketName, newName);
    }

    private void OnRemoveClicked()
    {
        controller.UnexposeSocket(socketData.internalNodeGuid, socketData.internalSocketName);
    }

    private Color GetColorForSocketType(SocketType type)
    {
        switch (type)
        {
            case SocketType.ExecutionLink: return Color.white;
            case SocketType.BehaviourLink: return Color.purple;
            case SocketType.FilterLink: return Color.green;
            case SocketType.Data: return new Color(1, 0.5f, 0);
            default: return Color.magenta;
        }
    }
}

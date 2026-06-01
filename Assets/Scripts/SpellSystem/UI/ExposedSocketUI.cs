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

        nameInputField.text = data.ExposedName;
        socketColorImage.color = GetColorForSocketType(data.Type);

        nameInputField.onEndEdit.AddListener(OnNameChanged);
        removeButton.onClick.AddListener(OnRemoveClicked);
    }

    private void OnNameChanged(string newName)
    {
        // Now passing the byte indices instead of string GUIDs!
        controller.UpdateExposedSocketName(socketData.InternalNodeIndex, socketData.InternalSocketIndex, newName);
    }

    private void OnRemoveClicked()
    {
        controller.UnexposeSocket(socketData.InternalNodeIndex, socketData.InternalSocketIndex);
    }

    private Color GetColorForSocketType(SocketType type)
    {
        switch (type)
        {
            case SocketType.ExecutionLink: return Color.white;
            case SocketType.BehaviourLink: return new Color(0.5f, 0, 0.5f); // Purple
            case SocketType.FilterLink: return Color.green;
            case SocketType.Data: return new Color(1, 0.5f, 0);
            default: return Color.magenta;
        }
    }
}
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.VFX;
using static UnityEngine.GridBrushBase;

public class SocketUI : MonoBehaviour
{
    public SocketDefinition SocketData { get; private set; }
    public RuneUI ParentRune { get; private set; }

    private VisualEffect _socketVFX;

    private Vector3 _targetLocalPosition;
    private bool _isAttractedToMouse = false;
    private float _smoothingSpeed = 15f;

    private SpellGraphController controller;

    public void Initialize(RuneUI parentRune, SocketDefinition socketData)
    {
        controller = SpellGraphController.Instance;
        this.ParentRune = parentRune;
        this.SocketData = socketData;
        //this.SocketData.OwningNodeGUID = ParentRune.InstanceData.guid;
        this.name = $"Port_{socketData.Name}";

        UpdateVisuals();
    }

    public void SetTargetLocalPosition(Vector3 newPosition)
    {
        _targetLocalPosition = newPosition;
    }

    public void SetMouseAttraction(bool isAttracted)
    {
        _isAttractedToMouse = isAttracted;
    }

    private void Update()
    {
        Vector3 finalTargetPosition = _targetLocalPosition;
        float currentSmoothingSpeed = _smoothingSpeed;

        if (_isAttractedToMouse)
        {
            float attractionRange = controller.socketAttractionRange;
            Vector3 mouseLocalPos = ParentRune.transform.InverseTransformPoint(GetMouseWorldPosition());
            mouseLocalPos.y = 0;

            float distance = Vector3.Distance(_targetLocalPosition, mouseLocalPos);

            if (distance < attractionRange)
            {
                finalTargetPosition = mouseLocalPos;
                float influence = 1.0f - (distance / attractionRange);
                currentSmoothingSpeed *= (1 + (influence * 3f));
            }
        }

        transform.localPosition = Vector3.Lerp(transform.localPosition, finalTargetPosition, Time.deltaTime * currentSmoothingSpeed);
    }

    private Vector3 GetMouseWorldPosition()
    {
        var controller = SpellGraphController.Instance;
        Ray ray = controller.editorCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        Plane plane = new Plane(controller.editorCamera.transform.forward, ParentRune.transform.position);
        if (plane.Raycast(ray, out float enter))
        {
            return ray.GetPoint(enter);
        }
        return transform.position;
    }

    private void UpdateVisuals()
    {
        _socketVFX = GetComponentInChildren<VisualEffect>();
        if (_socketVFX == null)
        {
            Debug.Log("No socket vfx fiound");
            return;
        }

        bool isInput = (SocketData.Direction == SocketDirection.Input);
        _socketVFX.SetBool("IsInput", isInput);

        Color socketColor = GetColorForSocketType(SocketData.Type);

        _socketVFX.SetVector4("Color", socketColor);
    }
    private Color GetColorForSocketType(SocketType type)
    {
        var controller = SpellGraphController.Instance;
        if (controller == null) return Color.magenta;

        switch (type)
        {
            case SocketType.ExecutionLink: return controller.executionLinkColor;
            case SocketType.BehaviourLink: return controller.behaviourLinkColor;
            case SocketType.FilterLink: return controller.filterLinkColor;
            case SocketType.Data: return controller.dataColor;
            default: return Color.magenta; 
        }
    }

    public void UpdateOwningNodeGUID(string newGuid)
    {
        var updatedSocketData = new SocketDefinition(this.SocketData)
        {
            OwningNodeGUID = newGuid
        };
        this.SocketData = updatedSocketData;
    }
}



public enum SocketType
{
    ExecutionLink,  // the main flow of the spell (Caster -> Core -> Trigger -> Effect)
    BehaviourLink,  //  attaching a Behaviour to a Core
    FilterLink,     // attaching a Filter to a Trigger
    Data            // connecting all values (using DataTypeTags for specifics)
}

public enum SocketDirection { Input, Output };

public enum DataTypeTag { Generic, Speed, Damage, Radius, Force, Size, Lifetime, Material, Duration }


[System.Serializable]
public struct SocketDefinition
{
    public string Name;
    public SocketType Type;
    public SocketDirection Direction;
    public DataTypeTag Tag;
    public System.Type DataType;
    public string OwningNodeGUID; 
    public string TargetFieldName; 


    public SocketDefinition(string name, SocketType type, SocketDirection direction, DataTypeTag tag, System.Type dataType = null, string owningNodeGUID = "", string targetFieldName = "")
    {
        this.Name = name;
        this.Type = type;
        this.Direction = direction;
        this.Tag = tag;
        this.DataType = dataType;
        this.OwningNodeGUID = owningNodeGUID;
        this.TargetFieldName = targetFieldName;
    }

    public SocketDefinition(SocketDefinition socketDef)
    {
        this.Name = socketDef.Name; 
        this.Type = socketDef.Type;
        this.Direction = socketDef.Direction;
        this.Tag = socketDef.Tag;
        this.DataType = socketDef.DataType;
        this.OwningNodeGUID = socketDef.OwningNodeGUID;
        this.TargetFieldName = socketDef.TargetFieldName;
    }
}
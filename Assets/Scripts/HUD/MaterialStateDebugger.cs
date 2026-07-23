using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class MaterialStateDebugger : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("Prevents M from creating displays while the spell editor or UI input is active.")]
    public bool onlyWhenGameplayActive = true;

    [Header("Raycast")]
    public Camera debugCamera;
    public float raycastDistance = 100f;
    public LayerMask debugLayerMask = ~0;
    public QueryTriggerInteraction queryTriggerInteraction =
        QueryTriggerInteraction.Ignore;

    [Header("Display")]
    public MaterialStateDebugDisplay materialStateDebugPrefab;

    [Tooltip("Zero or lower allows unlimited displays.")]
    public int maxActiveDisplays = 16;

    private readonly Dictionary<
        PhysicsObjectProperties,
        MaterialStateDebugDisplay
    > activeDisplays =
        new Dictionary<
            PhysicsObjectProperties,
            MaterialStateDebugDisplay
        >();

    private readonly List<PhysicsObjectProperties>
        invalidTargets =
            new List<PhysicsObjectProperties>();

    private void Start()
    {
        FindDebugCamera();
    }

    private void Update()
    {
        PruneInvalidDisplays();

        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return;

        if (!keyboard.mKey.wasPressedThisFrame)
            return;

        bool clearAll =
            keyboard.leftShiftKey.isPressed ||
            keyboard.rightShiftKey.isPressed;

        if (clearAll)
        {
            ClearAllDisplays();
            return;
        }

        if (
            onlyWhenGameplayActive &&
            GameController.Instance != null &&
            !GameController.gamePlayActive)
        {
            return;
        }

        ToggleLookTarget();
    }

    private void OnDisable()
    {
        ClearAllDisplays();
    }

    private void ToggleLookTarget()
    {
        FindDebugCamera();

        if (debugCamera == null)
        {
            Debug.LogWarning(
                "[MaterialStateDebugger] No debug camera was found.",
                this
            );

            return;
        }

        Ray ray = debugCamera.ViewportPointToRay(
            new Vector3(0.5f, 0.5f, 0f)
        );

        if (!Physics.Raycast(
            ray,
            out RaycastHit raycastHit,
            raycastDistance,
            debugLayerMask,
            queryTriggerInteraction))
        {
            // Looking at empty space deliberately leaves existing
            // displays open.
            return;
        }

        if (!TryGetTargetProperties(
            raycastHit.collider,
            out PhysicsObjectProperties targetProperties))
        {
            return;
        }

        if (activeDisplays.ContainsKey(targetProperties))
        {
            RemoveDisplay(targetProperties);
            return;
        }

        AddDisplay(
            targetProperties,
            raycastHit.collider
        );
    }

    private bool TryGetTargetProperties(
        Collider hitCollider,
        out PhysicsObjectProperties targetProperties)
    {
        targetProperties = null;

        if (hitCollider == null)
            return false;

        // This is normally enough for an ordinary PhysicsObject
        // or an individual ragdoll bone.
        targetProperties =
            hitCollider.GetComponentInParent<
                PhysicsObjectProperties
            >();

        if (targetProperties != null)
            return true;

        PhysicsObject physicsObject =
            hitCollider.GetComponentInParent<PhysicsObject>();

        if (
            physicsObject != null &&
            physicsObject.physicsObjectProperties != null)
        {
            targetProperties =
                physicsObject.physicsObjectProperties;

            return true;
        }

        // Fallback for auxiliary colliders which report through a
        // parent PhysicsObject.
        PhysicsSubObject physicsSubObject =
            hitCollider.GetComponentInParent<PhysicsSubObject>();

        if (
            physicsSubObject != null &&
            physicsSubObject.parent_physics_object != null)
        {
            targetProperties =
                physicsSubObject
                    .parent_physics_object
                    .physicsObjectProperties;

            return targetProperties != null;
        }

        return false;
    }

    private void AddDisplay(
        PhysicsObjectProperties targetProperties,
        Collider selectedCollider)
    {
        if (materialStateDebugPrefab == null)
        {
            Debug.LogWarning(
                "[MaterialStateDebugger] No MaterialStateDebugDisplay prefab has been assigned.",
                this
            );

            return;
        }

        if (
            maxActiveDisplays > 0 &&
            activeDisplays.Count >= maxActiveDisplays)
        {
            Debug.LogWarning(
                $"[MaterialStateDebugger] Maximum active display count of {maxActiveDisplays} reached. Use Shift+M to clear them.",
                this
            );

            return;
        }

        MaterialStateDebugDisplay newDisplay =
            Instantiate(materialStateDebugPrefab);

        newDisplay.name =
            $"{materialStateDebugPrefab.name} " +
            $"({targetProperties.gameObject.name})";

        newDisplay.Bind(
            targetProperties,
            selectedCollider,
            debugCamera
        );

        activeDisplays.Add(
            targetProperties,
            newDisplay
        );
    }

    private void RemoveDisplay(
        PhysicsObjectProperties targetProperties)
    {
        if (!activeDisplays.TryGetValue(
            targetProperties,
            out MaterialStateDebugDisplay display))
        {
            return;
        }

        activeDisplays.Remove(targetProperties);

        if (display != null)
        {
            Destroy(display.gameObject);
        }
    }

    private void ClearAllDisplays()
    {
        foreach (
            MaterialStateDebugDisplay display
            in activeDisplays.Values)
        {
            if (display != null)
            {
                Destroy(display.gameObject);
            }
        }

        activeDisplays.Clear();
        invalidTargets.Clear();
    }

    private void PruneInvalidDisplays()
    {
        invalidTargets.Clear();

        foreach (var activeDisplay in activeDisplays)
        {
            PhysicsObjectProperties targetProperties =
                activeDisplay.Key;

            MaterialStateDebugDisplay display =
                activeDisplay.Value;

            if (
                targetProperties == null ||
                display == null ||
                !display.HasValidTarget)
            {
                invalidTargets.Add(targetProperties);
            }
        }

        for (int i = 0; i < invalidTargets.Count; i++)
        {
            PhysicsObjectProperties targetProperties =
                invalidTargets[i];

            if (activeDisplays.TryGetValue(
                targetProperties,
                out MaterialStateDebugDisplay display))
            {
                if (display != null)
                {
                    Destroy(display.gameObject);
                }
            }

            activeDisplays.Remove(targetProperties);
        }
    }

    private void FindDebugCamera()
    {
        if (debugCamera == null)
        {
            debugCamera = Camera.main;
        }
    }
}
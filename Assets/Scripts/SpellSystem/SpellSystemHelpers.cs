using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public static class SpellSystemHelpers
{

    public static GameObject CreateVFX(VFXContext context, ModifierType type, Transform parent, float sizeMult, bool ignore_parent_scale = false)
    {
        if (GameController.Instance.vfxDatabase == null)
        {
            Debug.LogError("VFX Database is not assigned in GameController!");
            return null;
        }
        VFXDatabase vfxDatabase = GameController.Instance.vfxDatabase;

        GameObject vfxPrefab = vfxDatabase.GetVFX(context, type);

        if (vfxPrefab != null)
        {
            GameObject vfxInstance = GameObject.Instantiate(vfxPrefab, parent);

            if (parent != null)
            {
                vfxInstance.transform.position = parent.position;
                vfxInstance.transform.rotation = parent.rotation;

                if (ignore_parent_scale)
                {
                    // divide through by parent scale, to shrink back to base scale.
                    Vector3 new_scale = vfxPrefab.transform.localScale;
                    new_scale.x /= parent.localScale.x;
                    new_scale.y /= parent.localScale.y;
                    new_scale.z /= parent.localScale.z;
                    vfxInstance.transform.localScale = new_scale;
                }
            }

            VFXController vfxController = vfxInstance.AddComponent<VFXController>();
            if (vfxController != null)
            {
                vfxController.SizeMult = sizeMult;

                vfxController.Initialize();
            }



            return vfxInstance;
        }

        return null;
    }

    public static Vector3 GetSpellPosition(SpellPosition positionType, SpellTriggerInfo triggerInfo, List<Transform> targets = null)
    {
        SpellState state = triggerInfo.State;

        switch (positionType)
        {
            case SpellPosition.TriggerPoint:
                //needs changing
                return triggerInfo.HasOverridePosition ? triggerInfo.TriggerPoint : state.CastPosition;

            case SpellPosition.LastCastPosition:
                return state.CastPosition;

            case SpellPosition.CasterPosition:
                return state.Controller != null ? state.Controller.transform.position : state.CastPosition;

            case SpellPosition.CameraToScreenPoint:
                Ray ray = Camera.main.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2));
                if (Physics.Raycast(ray, out RaycastHit hit, 100f))
                {
                    return hit.point;
                }
                return ray.GetPoint(100f);

            case SpellPosition.ClosestTarget:
                if (targets == null || targets.Count == 0) return state.Controller.transform.position;
                Transform closestTarget = targets.OrderBy(t => Vector3.Distance(state.Controller.transform.position, t.transform.position)).FirstOrDefault();
                return closestTarget != null ? closestTarget.position : state.Controller.transform.position;

            case SpellPosition.ClosestVisibleTarget:
                if (targets == null || targets.Count == 0) return state.Controller.transform.position;
                Transform closestVisibleTarget = targets.OrderBy(t => Vector3.Distance(state.Controller.transform.position, t.transform.position)).FirstOrDefault();
                return closestVisibleTarget != null ? closestVisibleTarget.position : state.Controller.transform.position;
           
            default:
                Debug.LogWarning($"SpellPosition type '{positionType}' not implemented. Defaulting to CasterPosition.");
                return state.Controller.transform.position;
        }
    }

    public static Quaternion GetSpellRotation(SpellRotation rotationType, SpellPosition positionType, SpellTriggerInfo triggerInfo)
    {
        SpellState state = triggerInfo.State;
        switch (rotationType)
        {
            case SpellRotation.TriggerRotation:
                return triggerInfo.TriggerRotation != null ? triggerInfo.TriggerRotation : state.Controller.transform.rotation;

            case SpellRotation.TriggerNormal:
                return triggerInfo.TriggerRotation != null ? triggerInfo.TriggerRotation : state.Controller.transform.rotation;

            case SpellRotation.LastCastRotation:
                return state.CastRotation != null ? state.CastRotation : state.Controller.transform.rotation;

            case SpellRotation.CasterRotation:
                return state.Controller.transform.rotation;

            case SpellRotation.TowardsAimPoint:
                Vector3 targetPoint = triggerInfo.State.CastAimTargetPos;
                Vector3 spawnPoint = GetSpellPosition(positionType, triggerInfo);
                Vector3 directionToTarget = (targetPoint - spawnPoint).normalized;
                return Quaternion.LookRotation(directionToTarget, Vector3.up);

            case SpellRotation.PlayerToClosestEnemy:
                Vector3 closestTargetPos = GetSpellPosition(SpellPosition.ClosestTarget, triggerInfo);
                if (closestTargetPos == state.Controller.transform.position) return state.Controller.transform.rotation; // No target found
                Vector3 directionToClosest = (closestTargetPos - state.Controller.transform.position).normalized;
                return Quaternion.LookRotation(directionToClosest, Vector3.up);

            case SpellRotation.PlayerToClosestVisibleEnemy:
                Vector3 closestVisibleTargetPos = GetSpellPosition(SpellPosition.ClosestVisibleTarget, triggerInfo);
                if (closestVisibleTargetPos == state.Controller.transform.position) return state.Controller.transform.rotation; // No visible target found
                Vector3 directionToVisible = (closestVisibleTargetPos - state.Controller.transform.position).normalized;
                return Quaternion.LookRotation(directionToVisible);

            default:
                Debug.LogWarning($"SpellRotation type '{rotationType}' not implemented. Defaulting to CasterRotation.");
                return state.Controller.transform.rotation;
        }
    }

    //method to unify adding rigidbodys that work as spells to spells 
    public static Rigidbody AddDefaultSpellRigidBodyToGameObject(GameObject go)
    {
        var rb = go.GetComponent<Rigidbody>();
        if (!rb) rb = go.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        return rb;
    }

    public static GameObject AddSpellShapeToGO(GameObject go, SpellShape shape)
    {
        return go;
    }

}


//Enum that defines where a spells position is, used for things like where the core should be created
public enum SpellPosition
{
    TriggerPoint, LastCastPosition, CasterPosition, CameraToScreenPoint, ClosestTarget, ClosestVisibleTarget, 
}
public enum SpellRotation
{
    TriggerRotation, TriggerNormal, LastCastRotation, CasterRotation, TowardsAimPoint, PlayerToClosestEnemy, PlayerToClosestVisibleEnemy
}

public enum ModifierType
{
    Arcane,
    Fire,
    Frost,
    Water,
    Void,
    Earth,
}

public enum SpellShape
{
    Sphere, 
    Box,
}
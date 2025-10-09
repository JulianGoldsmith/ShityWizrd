using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "MeleeHitBoxCore", menuName = "SpellNodes/CoreNodes/MeleeHitBoxCore")]
public class MeleeHitBoxCore : CoreNode
{
    public override void CreateSpellCore(SpellTriggerInfo triggerInfo)
    {
        EquipableItem weapon = triggerInfo.State.CastItem;
        if (weapon == null)
        {
            Debug.LogError("MeleeHitBoxCore: Cannot find the weapon/item that cast this spell.");
            return;
        }

        GameObject spellCore = new GameObject("MeleeHitBoxCore");

        AttatchBehavioursAndTriggers(spellCore, triggerInfo);
        spellCore.SetActive(true);
        triggerInfo.State.Controller.GetComponent<CastActionController>()?.RegisterActiveHitbox(spellCore);
    }
}

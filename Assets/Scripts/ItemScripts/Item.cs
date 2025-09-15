using NUnit.Framework;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class Item : MonoBehaviour
{
    public string itemName;

    public string primarySpellID, secondarySpellID;

    public SpellGraph primaryActionSpell, secondaryActionSpell;

    public HandState heldHandState;

    public Transform primaryHandle, secondaryHandle;

    //public GameObject hitbox;
    [Header("Hitbox ponts for melee sweep")]
    public Transform weaponBase, weaponEnd;

    public Transform projectileSpawnPoint;

    public void LoadSpells()
    {

        if (!string.IsNullOrEmpty(primarySpellID))
        {
            primaryActionSpell = SpellGraphController.Instance.GetSpellFromAssestsByName(primarySpellID);
        }


        if (!string.IsNullOrEmpty(secondarySpellID))
        {
            secondaryActionSpell = SpellGraphController.Instance.GetSpellFromAssestsByName(secondarySpellID);
        }
    }

    public void Start()
    {
        LoadSpells();
    }

}

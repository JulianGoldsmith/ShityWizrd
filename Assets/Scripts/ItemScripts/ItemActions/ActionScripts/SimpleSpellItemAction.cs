using UnityEngine;

[CreateAssetMenu(fileName = "SimpleSpellItemAction", menuName = "Items/Actions/Simple Spell")]
public class SimpleSpellItemAction : ItemAction
{
    [Header("Legacy authoring values")]
    public float cooldown = 0.5f;
    public float comboWindow = 0.8f;
}
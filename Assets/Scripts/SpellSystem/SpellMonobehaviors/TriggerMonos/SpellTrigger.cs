using System.Collections.Generic;
using UnityEngine;
public abstract class SpellTrigger : MonoBehaviour
{
    public SpellState state;

    public List<FilterNode> filterNodes;

    public List<SpellNode> outcomeNodes;

    public virtual void OnTick() { }
}

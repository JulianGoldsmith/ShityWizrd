using UnityEngine;

[CreateAssetMenu(fileName = "HitBoxCastNode", menuName = "Scriptable Objects/HitBoxCastNode")]
public class HitBoxCastNode : CasterNode
{

    public GameObject hitBoxObject;

    public override void OnCastStarted(SpellState state, CastActionController castController)
    {
        throw new System.NotImplementedException();
    }

    public override void OnCastUpdate(SpellState state, CastActionController castController)
    {
        throw new System.NotImplementedException();
    }

    public override void OnCastCanceled(SpellState state, CastActionController castController)
    {
        throw new System.NotImplementedException();
    }
}

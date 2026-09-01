using UnityEngine;

[CreateAssetMenu(fileName = "Tether Law", menuName = "SpellNodes/Links/Tether Law")]
public class TetherLawNode : LinkLawNode
{
    public float MaximumLength = 2f;
    public float BreakForce;
    public float Compliance = 0.0001f;
    public float Damping = 0.5f;

    public override IRuntimeNode CompileNode(SpellCompilationContext context)
    {
        return new RuntimeTetherLaw
        {
            MaximumLength = MaximumLength,
            BreakForce = BreakForce,
            Compliance = Compliance,
            Damping = Damping
        };
    }
}

public class RuntimeTetherLaw : RuntimeLinkLaw
{
    public float MaximumLength;
    public float BreakForce;
    public float Compliance;
    public float Damping;
}

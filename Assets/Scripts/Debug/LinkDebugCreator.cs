using Fusion;
using UnityEngine;

public class LinkDebugCreator : MonoBehaviour
{
    [Header("Rune Hydration Test")]
    public LinkNode LinkKnot;
    public TetherLawNode TetherLaw;

    [Header("Physical Link Test")]
    public CasterLinkController Controller;

    public NetworkObject EndpointA;
    public NetworkObject EndpointB;

    public float MaximumLength = 2f;
    public float Compliance = 0.0001f;
    public float Damping = 1f;
    public float Duration;

    public int CreatedSlot = -1;

    [ContextMenu("Test Runtime Link Hydration")]
    public void TestRuntimeLinkHydration()
    {
        RuneSpellBlueprintData blueprint = new RuneSpellBlueprintData(new[]
        {
            RuneNodeData.CreateBlueprintRoot(LinkKnot.NetworkNodeID, 1),
            RuneNodeData.CreateChild(TetherLaw.NetworkNodeID, 0, 0, true, 0)
        });

        IRuntimeNode[] runtimeNodes = RuneSpellHydrator.Hydrate(blueprint);
        RuntimeLink runtimeLink = (RuntimeLink)runtimeNodes[0];
        RuntimeTetherLaw runtimeTether = (RuntimeTetherLaw)runtimeLink.Law;

        if (!RuneSpellBlueprintBuilder.TryGetEntryPointType(blueprint, out EntryPointType entryType, out string error)) throw new System.Exception(error);
        if (entryType != EntryPointType.Link) throw new System.Exception($"Expected Link entry point but got {entryType}.");
        if (runtimeLink.Duration != LinkKnot.Duration || runtimeLink.SpellLoad != LinkKnot.SpellLoad) throw new System.Exception("RuntimeLink values do not match the Link Knot definition.");
        if (runtimeTether.MaximumLength != TetherLaw.MaximumLength || runtimeTether.BreakForce != TetherLaw.BreakForce || runtimeTether.Compliance != TetherLaw.Compliance || runtimeTether.Damping != TetherLaw.Damping) throw new System.Exception("RuntimeTetherLaw values do not match the Tether Law definition.");

        Debug.Log($"Runtime link hydration passed. Entry={entryType}, Duration={runtimeLink.Duration}, Load={runtimeLink.SpellLoad}, Length={runtimeTether.MaximumLength}, Compliance={runtimeTether.Compliance}, Damping={runtimeTether.Damping}.");
    }

    [ContextMenu("Create Test Link")]
    public void CreateTestLink()
    {
        Rigidbody bodyA = EndpointA.GetComponent<Rigidbody>();
        Rigidbody bodyB = EndpointB.GetComponent<Rigidbody>();

        LinkEndpoint endpointA = new LinkEndpoint
        {
            Kind = LinkEndpointKind.NetworkBody,
            ObjectId = EndpointA.Id,
            Anchor = bodyA.centerOfMass
        };

        LinkEndpoint endpointB = new LinkEndpoint
        {
            Kind = LinkEndpointKind.NetworkBody,
            ObjectId = EndpointB.Id,
            Anchor = bodyB.centerOfMass
        };

        CreatedSlot = Controller.CreateLink(default, endpointA, endpointB, MaximumLength, 0f, Compliance, Damping, Duration, 0f);

        Debug.Log($"Created test link in slot {CreatedSlot}.");
    }

    [ContextMenu("Remove Test Link")]
    public void RemoveTestLink()
    {
        Controller.RemoveLink(CreatedSlot);
        CreatedSlot = -1;
    }
}

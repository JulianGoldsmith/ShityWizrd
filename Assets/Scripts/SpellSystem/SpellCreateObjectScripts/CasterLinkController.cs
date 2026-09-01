using Fusion;
using UnityEngine;

public class CasterLinkController : NetworkBehaviour
{
    public const int MAX_LINKS = 4;

    [Networked, Capacity(MAX_LINKS)]
    public NetworkArray<ActiveLinkState> ActiveLinks { get; }

    [Networked] public NetworkBool HasPendingEndpoint { get; set; }
    [Networked] public LinkEndpoint PendingEndpoint { get; set; }

    public HydratedTetherLink[] HydratedLinks { get; private set; }

    private XPBDGlobalManager _registeredManager;

    public override void Spawned()
    {
        base.Spawned();

        HydratedLinks = new HydratedTetherLink[MAX_LINKS];

        for (int i = 0; i < MAX_LINKS; i++)
            HydratedLinks[i] = new HydratedTetherLink();

        _registeredManager = GameController.Instance.xPBDGlobalManager;
        _registeredManager.RegisterLinkController(this);
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        _registeredManager.UnregisterLinkController(this);
        _registeredManager = null;

        base.Despawned(runner, hasState);
    }

    public int CreateLink(ActiveCastID castId, LinkEndpoint endpointA, LinkEndpoint endpointB, float maximumLength, float breakForce, float compliance, float damping, float duration, float spellLoad)
    {
        for (int i = 0; i < MAX_LINKS; i++)
        {
            if (ActiveLinks[i].Exists)
                continue;

            int endTick = 0;

            if (duration > 0f)
                endTick = Runner.Tick + Mathf.CeilToInt(duration / Runner.DeltaTime);

            ActiveLinkState link = new ActiveLinkState
            {
                Phase = LinkPhase.Active,
                CastId = castId,
                ManifestationCoreId = default,

                A = endpointA,
                B = endpointB,

                StartTick = Runner.Tick,
                EndTick = endTick,

                MaximumLength = maximumLength,
                BreakForce = breakForce,
                Compliance = compliance,
                Damping = damping,
                SpellLoad = spellLoad
            };

            ActiveLinks.Set(i, link);
            return i;
        }

        Debug.LogWarning($"[CasterLinkController] {name} has no free link slots.");
        return -1;
    }

    public int BeginLink(ActiveCastID castId, NetworkId manifestationCoreId, LinkEndpoint endpointA, float breakForce, float compliance, float damping, float duration, float spellLoad)
    {
        for (int i = 0; i < MAX_LINKS; i++)
        {
            if (ActiveLinks[i].Exists)
                continue;

            int endTick = 0;

            if (duration > 0f)
                endTick = Runner.Tick + Mathf.CeilToInt(duration / Runner.DeltaTime);

            ActiveLinkState link = new ActiveLinkState
            {
                Phase = LinkPhase.WaitingForB,
                CastId = castId,
                ManifestationCoreId = manifestationCoreId,

                A = endpointA,
                B = default,

                StartTick = Runner.Tick,
                EndTick = endTick,

                MaximumLength = 0f,
                BreakForce = breakForce,
                Compliance = compliance,
                Damping = damping,
                SpellLoad = spellLoad
            };

            ActiveLinks.Set(i, link);
            return i;
        }

        Debug.LogWarning($"[CasterLinkController] {name} has no free link slots.");
        return -1;
    }

    public bool CompleteLink(NetworkId manifestationCoreId, LinkEndpoint endpointB, float maximumLength)
    {
        for (int i = 0; i < MAX_LINKS; i++)
        {
            ActiveLinkState link = ActiveLinks[i];

            if (link.Phase != LinkPhase.WaitingForB || !link.ManifestationCoreId.Equals(manifestationCoreId))
                continue;

            link.B = endpointB;
            link.MaximumLength = maximumLength;
            link.Phase = LinkPhase.Active;

            ActiveLinks.Set(i, link);
            return true;
        }

        return false;
    }

    public void RemoveLink(int slotIndex)
    {
        ActiveLinks.Set(slotIndex, default);
    }

    public void RemoveLinksForManifestation(NetworkId manifestationCoreId)
    {
        for (int i = 0; i < MAX_LINKS; i++)
        {
            ActiveLinkState link = ActiveLinks[i];

            if (link.Exists && link.ManifestationCoreId.Equals(manifestationCoreId))
                RemoveLink(i);
        }
    }

    public override void FixedUpdateNetwork()
    {
        for (int i = 0; i < MAX_LINKS; i++)
        {
            ActiveLinkState link = ActiveLinks[i];

            if (!link.Exists)
                continue;

            if (link.EndTick > 0 && Runner.Tick >= link.EndTick)
                RemoveLink(i);
        }
    }
}
using Fusion;
using System.Collections.Generic;
using UnityEngine;
using Fusion.Addons.Physics;

[CreateAssetMenu(fileName = "LinkCore", menuName = "SpellNodes/CoreNodes/LinkCore")]
public class LinkCore : ObjectCore
{
    [Promotable("Spell Load", DataTypeTag.Generic)]
    public float spellLoad = 1f;

    public override List<SocketDefinition> GetSockets()
    {
        List<SocketDefinition> sockets = base.GetSockets();
        sockets.Add(new SocketDefinition("Link Law", SocketType.ExecutionLink, SocketDirection.Input, DataTypeTag.Generic, typeof(LinkLawNode), InstanceGuid));
        return sockets;
    }

    public override IRuntimeNode CompileNode(SpellCompilationContext context)
    {
        RuntimeLinkCore runtimeCore = (RuntimeLinkCore)base.CompileNode(context);

        runtimeCore.CastSpawnPosition = SpellPosition.TriggerPoint;
        runtimeCore.TriggerSpawnPosition = SpellPosition.TriggerPoint;
        runtimeCore.CastSpawnRotation = SpellRotation.TriggerRotation;
        runtimeCore.TriggerSpawnRotation = SpellRotation.TriggerRotation;
        runtimeCore.spellLoad = new RuntimeFloatProperty(spellLoad);
        runtimeCore.IsKinematic = true;

        return runtimeCore;
    }

    protected override RuntimeObjectCore CreateRuntimeCore()
    {
        return new RuntimeLinkCore();
    }
}

public class RuntimeLinkCore : RuntimeObjectCore
{
    public RuntimeFloatProperty spellLoad;
    public RuntimeLinkLaw Law;

    public override void ExecuteCore(SpellTriggerInfo triggerInfo)
    {
        Vector3 claspPosition = SpellSystemHelpers.GetSpellPosition(triggerInfo.IsCast ? CastSpawnPosition : TriggerSpawnPosition, triggerInfo);
        Quaternion claspRotation = SpellSystemHelpers.GetSpellRotation(triggerInfo.IsCast ? CastSpawnRotation : TriggerSpawnRotation, triggerInfo.IsCast ? CastSpawnPosition : TriggerSpawnPosition, triggerInfo);

        SpellCreatedCore manifestation = SpawnCore(triggerInfo);

        manifestation.TryGetCoreComponent(out AttatchedSpellComponent attachedLink);
        attachedLink.AttachmentPoints[1].GetComponentInChildren<Collider>().enabled = true;

        LinkEndpoint endpointA;

        if (triggerInfo.HitObject != null && triggerInfo.HitObject.TryGetComponent(out NetworkObject targetObject) && triggerInfo.HitObject.TryGetComponent(out Rigidbody targetBody))
        {
            endpointA = new LinkEndpoint
            {
                Kind = LinkEndpointKind.NetworkBody,
                ObjectId = targetObject.Id,
                Anchor = Quaternion.Inverse(targetBody.rotation) * (claspPosition - targetBody.position),
                AnchorRotation = Quaternion.Inverse(targetBody.rotation) * claspRotation
            };
        }
        else
        {
            endpointA = new LinkEndpoint
            {
                Kind = LinkEndpointKind.WorldPoint,
                ObjectId = default,
                Anchor = claspPosition,
                AnchorRotation = claspRotation
            };
        }

        RuntimeTetherLaw tetherLaw = (RuntimeTetherLaw)Law;
        CasterLinkController linkController = triggerInfo.State.Caster.GetComponent<CasterLinkController>();

        linkController.BeginLink(
            triggerInfo.State.ActiveCastID,
            manifestation.CoreNetworkId,
            endpointA,
            tetherLaw.BreakForce,
            tetherLaw.Compliance,
            tetherLaw.Damping,
            lifetime.GetValue(triggerInfo),
            spellLoad.GetValue(triggerInfo));
    }
    public override void TickBeforePhysics(SpellCreatedCore core)
    {
        if (!core.HasStateAuthority) return;
        if (!core.Runner.TryFindObject(core.ActiveCastID.CasterId, out NetworkObject casterObject)) return;

        CasterLinkController linkController = casterObject.GetComponent<CasterLinkController>();

        for (int i = 0; i < CasterLinkController.MAX_LINKS; i++)
        {
            ActiveLinkState link = linkController.ActiveLinks[i];

            if (link.Phase != LinkPhase.WaitingForB || !link.ManifestationCoreId.Equals(core.CoreNetworkId))
                continue;

            Vector3 position;
            Quaternion rotation;

            if (link.A.Kind == LinkEndpointKind.NetworkBody)
            {
                if (!core.Runner.TryFindObject(link.A.ObjectId, out NetworkObject objectA)) return;

                Rigidbody bodyA = objectA.GetComponent<Rigidbody>();
                position = bodyA.position + bodyA.rotation * link.A.Anchor;
                rotation = bodyA.rotation * link.A.AnchorRotation;
            }
            else
            {
                position = link.A.Anchor;
                rotation = link.A.AnchorRotation;
            }

            Rigidbody coreBody = core.GetComponent<Rigidbody>();
            coreBody.MovePosition(position);
            coreBody.MoveRotation(rotation);
            return;
        }
    }
    public override void TickAfterPhysics(SpellCreatedCore core)
    {
        if (!core.HasStateAuthority) return;
        if (core.TickContacts.Count == 0) return;
        if (!core.Runner.TryFindObject(core.ActiveCastID.CasterId, out NetworkObject casterObject)) return;

        CasterLinkController linkController = casterObject.GetComponent<CasterLinkController>();
        RuntimeTetherLaw tetherLaw = (RuntimeTetherLaw)Law;

        core.TryGetCoreComponent(out AttatchedSpellComponent attachedLink);
        Transform claspB = attachedLink.AttachmentPoints[1];
        Collider claspCollider = claspB.GetComponentInChildren<Collider>();
        Vector3 claspPosition = claspB.position;
        Quaternion claspRotation = claspB.rotation;

        for (int i = 0; i < CasterLinkController.MAX_LINKS; i++)
        {
            ActiveLinkState link = linkController.ActiveLinks[i];

            if (link.Phase != LinkPhase.WaitingForB || !link.ManifestationCoreId.Equals(core.CoreNetworkId))
                continue;

            foreach (PendingContact contact in core.TickContacts)
            {
                GameObject hitObject = contact.Target;

                if (hitObject == core.SourceObject)
                    continue;

                if (hitObject.TryGetComponent(out NetworkObject hitNetworkObject) && link.A.Kind == LinkEndpointKind.NetworkBody && hitNetworkObject.Id.Equals(link.A.ObjectId))
                    continue;

                Physics.ComputePenetration(claspCollider, claspCollider.transform.position, claspCollider.transform.rotation, contact.Collider, contact.Collider.transform.position, contact.Collider.transform.rotation, out Vector3 surfaceNormal, out _);
                Quaternion surfaceRotation = Quaternion.FromToRotation(claspRotation * Vector3.up, -surfaceNormal) * claspRotation;

                LinkEndpoint endpointB;

                if (hitObject.TryGetComponent(out hitNetworkObject) && hitObject.TryGetComponent(out Rigidbody hitBody))
                {
                    endpointB = new LinkEndpoint
                    {
                        Kind = LinkEndpointKind.NetworkBody,
                        ObjectId = hitNetworkObject.Id,
                        Anchor = Quaternion.Inverse(hitBody.rotation) * (claspPosition - hitBody.position),
                        AnchorRotation = Quaternion.Inverse(hitBody.rotation) * surfaceRotation
                    };
                }
                else
                {
                    endpointB = new LinkEndpoint
                    {
                        Kind = LinkEndpointKind.WorldPoint,
                        ObjectId = default,
                        Anchor = claspPosition,
                        AnchorRotation = surfaceRotation
                    };
                }

                Vector3 pointA;

                if (link.A.Kind == LinkEndpointKind.NetworkBody)
                {
                    if (!core.Runner.TryFindObject(link.A.ObjectId, out NetworkObject objectA)) return;

                    Rigidbody bodyA = objectA.GetComponent<Rigidbody>();
                    pointA = bodyA.position + bodyA.rotation * link.A.Anchor;
                }
                else
                {
                    pointA = link.A.Anchor;
                }

                float capturedLength = Vector3.Distance(pointA, claspPosition);
                float maximumLength = tetherLaw.MaximumLength > 0f ? Mathf.Min(capturedLength, tetherLaw.MaximumLength) : capturedLength;

                linkController.CompleteLink(core.CoreNetworkId, endpointB, maximumLength);
                claspCollider.enabled = false;
                return;
            }

            return;
        }
    }

    public override void AfterRender(SpellCreatedCore core)
    {
        if (!core.Runner.TryFindObject(core.ActiveCastID.CasterId, out NetworkObject casterObject)) return;

        CasterLinkController linkController = casterObject.GetComponent<CasterLinkController>();
        core.TryGetCoreComponent(out AttatchedSpellComponent attachedLink);

        for (int i = 0; i < CasterLinkController.MAX_LINKS; i++)
        {
            ActiveLinkState link = linkController.ActiveLinks[i];

            if (!link.Exists || !link.ManifestationCoreId.Equals(core.CoreNetworkId))
                continue;

            Vector3 positionA;
            Quaternion rotationA;

            if (link.A.Kind == LinkEndpointKind.NetworkBody)
            {
                if (!core.Runner.TryFindObject(link.A.ObjectId, out NetworkObject objectA)) return;

                Rigidbody bodyA = objectA.GetComponent<Rigidbody>();
                NetworkRigidbody3D networkBodyA = objectA.GetComponent<NetworkRigidbody3D>();
                Transform renderedBodyA = networkBodyA != null && networkBodyA.InterpolationTarget != null ? networkBodyA.InterpolationTarget : bodyA.transform;

                positionA = renderedBodyA.position + renderedBodyA.rotation * link.A.Anchor;
                rotationA = renderedBodyA.rotation * link.A.AnchorRotation;
            }
            else
            {
                positionA = link.A.Anchor;
                rotationA = link.A.AnchorRotation;
            }

            attachedLink.VisualAttachmentPoints[0].SetPositionAndRotation(positionA, rotationA);

            if (!link.IsActive)
            {
                Transform physicalA = attachedLink.AttachmentPoints[0];
                Transform physicalB = attachedLink.AttachmentPoints[1];

                Vector3 openPositionOffset = Quaternion.Inverse(physicalA.rotation) * (physicalB.position - physicalA.position);
                Quaternion openRotationOffset = Quaternion.Inverse(physicalA.rotation) * physicalB.rotation;

                attachedLink.VisualAttachmentPoints[1].SetPositionAndRotation(positionA + rotationA * openPositionOffset, rotationA * openRotationOffset);
                return;
            }

            Vector3 positionB;
            Quaternion rotationB;

            if (link.B.Kind == LinkEndpointKind.NetworkBody)
            {
                if (!core.Runner.TryFindObject(link.B.ObjectId, out NetworkObject objectB)) return;

                Rigidbody bodyB = objectB.GetComponent<Rigidbody>();
                NetworkRigidbody3D networkBodyB = objectB.GetComponent<NetworkRigidbody3D>();
                Transform renderedBodyB = networkBodyB != null && networkBodyB.InterpolationTarget != null ? networkBodyB.InterpolationTarget : bodyB.transform;

                positionB = renderedBodyB.position + renderedBodyB.rotation * link.B.Anchor;
                rotationB = renderedBodyB.rotation * link.B.AnchorRotation;
            }
            else
            {
                positionB = link.B.Anchor;
                rotationB = link.B.AnchorRotation;
            }

            attachedLink.VisualAttachmentPoints[1].SetPositionAndRotation(positionB, rotationB);
            return;
        }
    }
}
using UnityEngine;
using Fusion;
using System.Collections.Generic;
public class XPBDNetworkTest : NetworkBehaviour
{
    public XPBDPosAndRotSolver posAndRotSolver;

    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();
    }

    public override void Spawned()
    {
        base.Spawned();
        GameController.Instance.xPBDGlobalManager.RegisterRagdoll(posAndRotSolver);
       // GameController.Instance.xPBDGlobalManager.registeredRagdolls.Add(posAndRotSolver);
        Runner.SetIsSimulated(this.Object, true);
        foreach(XPBDTestJoint j in posAndRotSolver.joints)
        {
            Runner.SetIsSimulated(j.child.GetComponent<NetworkObject>(), true);
            Runner.SetIsSimulated(j.parent.GetComponent<NetworkObject>(), true);
        }
    }
}

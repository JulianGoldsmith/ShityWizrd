using UnityEngine;
using Fusion;
public class XPBDNetworkTest : NetworkBehaviour
{
    public XPBDPosAndRotSolver posAndRotSolver;

    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();
        posAndRotSolver.SolveJointTick(Runner.DeltaTime);
    }

    public override void Spawned()
    {
        base.Spawned();
        Runner.SetIsSimulated(this.Object, true);
        foreach(XPBDTestJoint j in posAndRotSolver.joints)
        {
            Runner.SetIsSimulated(j.child.GetComponent<NetworkObject>(), true);
            Runner.SetIsSimulated(j.parent.GetComponent<NetworkObject>(), true);
        }
    }
}

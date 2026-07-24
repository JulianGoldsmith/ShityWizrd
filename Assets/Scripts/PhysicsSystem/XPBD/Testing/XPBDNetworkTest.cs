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

        // XPBDPosAndRotSolver now owns registration, unregistration, and
        // enabling simulation for its joint bodies.
    }
}

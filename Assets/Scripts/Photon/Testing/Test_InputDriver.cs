using Fusion;
using UnityEngine;

public class Test_InputDriver : NetworkBehaviour
{
    public Test_ProxyCounter targetProxy;
    [HideInInspector][Networked] public NetworkButtons _lastButtonsInput { get; set; }

    public override void FixedUpdateNetwork()
    {
   
        if (GetInput(out NetworkInputData data))
        {
            if (data.buttons.WasPressed(_lastButtonsInput, EInputButton.TEST_COUNT))
            {
                if (targetProxy != null)
                {
                    // Apply logic to the Proxy Object
                    targetProxy.ModifyNetworkState();
                }
            }
        }
    }

    public override void Spawned()
    {
        targetProxy = GameObject.Find("TestCounter").GetComponent<Test_ProxyCounter>();
    }
}

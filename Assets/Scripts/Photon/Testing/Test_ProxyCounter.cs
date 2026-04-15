using Fusion;
using UnityEngine;
using TMPro;

public class Test_ProxyCounter : NetworkBehaviour
{
    [Networked] public int NetCount { get; set; }
    public TMP_Text textDisplay;

    // We use a local value to compare what "Logic" thinks vs "Network" thinks
    private int _localVisualCount;

    public override void Render()
    {
        // Visualise the Networked Variable directly
        if (textDisplay != null)
            textDisplay.text = $"Tick: {Runner.Tick}\nNetCount: {NetCount} \n ";
    }

    public void ModifyNetworkState()
    {
        // This is the critical line. 
        // If State persists: 0 -> 1 -> 2 -> 3
        // If State resets:   0 -> 1, 0 -> 1, 0 -> 1
        NetCount++;
        Debug.Log($"[Tick {Runner.Tick}] Incremented. Value is now: {NetCount}");
    }

    public override void Spawned()
    {
        base.Spawned();
        Runner.SetIsSimulated(Object, true);
 
    }
}
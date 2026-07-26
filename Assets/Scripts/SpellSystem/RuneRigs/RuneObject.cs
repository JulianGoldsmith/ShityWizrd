using System;
using System.Collections.Generic;
using UnityEngine;

public class RuneObject : MonoBehaviour
{
    public Transform VisualRoot;

    public List<RuneBay> Bays = new List<RuneBay>();
    public RuneRigObject OwningRig { get; internal set; }
    public int NodeIndex { get; internal set; } = -1;


    public RuneBay GetBay(byte bayIndex)
    {
        foreach (RuneBay bay in Bays)
        {
            if (bay != null && bay.BayIndex == bayIndex)
                return bay;
        }

        return null;
    }

    private void OnDrawGizmosSelected()
    {
        if (Bays == null)
            return;

        Gizmos.color = Color.cyan;

        foreach (RuneBay bay in Bays)
        {
            if (bay == null || bay.BayTransform == null)
                continue;

            Gizmos.DrawWireSphere(bay.BayTransform.position, 0.025f);
            Gizmos.DrawLine(bay.BayTransform.position, bay.BayTransform.position + bay.BayTransform.forward * 0.1f);
        }
    }
}


[Serializable]
public class RuneBay
{
    public byte BayIndex;
    public Transform BayTransform;
}
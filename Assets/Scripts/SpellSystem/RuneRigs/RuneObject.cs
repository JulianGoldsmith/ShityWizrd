using System;
using System.Collections.Generic;
using UnityEngine;

public class RuneObject : MonoBehaviour
{
    [Header("Rune")]
    public Transform VisualRoot;
    public Transform RootConnectionTransform;

    [Min(0.01f)] public float Size = 0.25f;

    [Header("Bays")]
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
        if (RootConnectionTransform != null)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f);
            Gizmos.DrawWireSphere(RootConnectionTransform.position, 0.035f);
            Gizmos.DrawLine(RootConnectionTransform.position, RootConnectionTransform.position + RootConnectionTransform.forward * 0.12f);

            Gizmos.color = Color.green;
            Gizmos.DrawLine(RootConnectionTransform.position, RootConnectionTransform.position + RootConnectionTransform.up * 0.08f);
        }

        if (Bays == null)
            return;

        foreach (RuneBay bay in Bays)
        {
            if (bay == null || bay.BayTransform == null)
                continue;

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(bay.BayTransform.position, 0.025f);
            Gizmos.DrawLine(bay.BayTransform.position, bay.BayTransform.position + bay.BayTransform.forward * 0.1f);

            Gizmos.color = Color.green;
            Gizmos.DrawLine(bay.BayTransform.position, bay.BayTransform.position + bay.BayTransform.up * 0.07f);
        }
    }
}

[Serializable]
public class RuneBay
{
    public byte BayIndex;
    public Transform BayTransform;
}
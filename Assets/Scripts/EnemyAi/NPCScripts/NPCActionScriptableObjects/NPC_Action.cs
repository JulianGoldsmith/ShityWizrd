using System;
using System.IO;
using UnityEngine;


public abstract class NPC_Action: ScriptableObject
{
    public AnimationClip windUpClip;
    public AnimationClip holdClip;
    public AnimationClip releaseClip;

    [HideInInspector] public int comboPoint = 0;

    public float holdDuration = 1f;

}


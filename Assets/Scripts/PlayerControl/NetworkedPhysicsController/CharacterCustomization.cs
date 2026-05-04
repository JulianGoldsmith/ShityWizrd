using Fusion;
using System.Collections.Generic;
using UnityEngine;

public class CharacterCustomization : NetworkBehaviour
{
    [Header("Component References")]
    [SerializeField] private SkinnedMeshRenderer modelRenderer;
    [SerializeField] private SkinnedMeshRenderer ragDollRenderer;
    //[SerializeField] private SkinnedMeshRenderer robeRenderer;

    public List<SkinnedMeshRenderer> apparel;

    [Header("Material Options")]
    [SerializeField] private Material hostMat;
    [SerializeField] private Material clientMat;

    public bool boolShowApparelOnLocalPlayer = false;

    [Networked, OnChangedRender(nameof(OnAppearanceStateChanged))] public bool IsHost { get; set; }

    // --- Add future [Networked] properties here ---
    // [Networked(OnChanged = nameof(OnAppearanceStateChanged))]
    // public int ArmorIndex { get; set; }

    // [Networked(OnChanged = nameof(OnAppearanceStateChanged))]
    // public int SkinColorIndex { get; set; }

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            IsHost = Object.HasInputAuthority;
        }
        if (HasInputAuthority && !boolShowApparelOnLocalPlayer)
        {
            modelRenderer.enabled = false;
            //robeRenderer.enabled = false;
            foreach(var appa in apparel)
            {
                appa.enabled = false;
            }
        }
        OnAppearanceStateChanged();
    }

    private void OnAppearanceStateChanged()
    {
        Material mat = IsHost ? hostMat : clientMat;
        modelRenderer.material = mat;
        //ragDollRenderer.material = mat;
        this.GetComponent<NetworkedHandsController>().SetHandModelMaterial(mat);
    }

}
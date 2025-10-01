using UnityEngine;

[CreateAssetMenu(fileName = "physicsobjectmaterial", menuName = "PhysicsSystem/PhysicsObjectMaterial", order = 1)]

public class PhysicsObjectMaterial : ScriptableObject
{
    /* Defines some base properties of a PhysicsObject.
     Modifier runes can change a PhysicsObject's Material (POM). */

    #region Labelling
    public string material_name;

    #endregion


    #region Base Properties 
    // (to be used by physics system)
    public float density; // affects mass, so inertia, moment of inertia, weight.
    public float hardness; // brittleness, affects sound
    public float elasticity; // bounciness during collisions
    public float viscosity; // deformity of shape? does it droop and goop? (don't know how much that can be done).
    [Range(0,1)]public float stickiness; // defines collision friction. (dynamic friction)

    #endregion


    #region Visuals 
    // (VFX / shaders / actual material)
    public Material vfx_material;
    #endregion
}
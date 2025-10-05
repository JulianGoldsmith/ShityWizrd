using UnityEngine;

[CreateAssetMenu(fileName = "physicsobjectmaterial", menuName = "PhysicsSystem/PhysicsObjectMaterial", order = 1)]

public class PhysicsObjectMaterial : ScriptableObject
{
    /* Defines some base properties of a PhysicsObject.
     Modifier runes can change a PhysicsObject's Material (POM). */

    #region Labelling
    public string material_name;
    public PHYSICS_OBJECT_MATERIAL label;

    #endregion


    #region Base Properties 
    // (to be used by physics system)
    [Header("Physics Properties")]
    public float density; // affects mass, so inertia, moment of inertia, weight.
    public float hardness; // brittleness, affects sound
    public float elasticity; // bounciness during collisions
    public float brittleness; // how easily it is destroyed on collision.
    [Range(0,1)]public float stickiness; // defines collision friction. (dynamic friction)

    #endregion


    #region Visuals 
    // (VFX / shaders / actual material)
    [Header("Visuals")]
    public Material vfx_material;
    public bool casts_shadows = true;
    public Color shatter_particle_color;
    #endregion
}

public enum PHYSICS_OBJECT_MATERIAL
{
    // To allow easier communication across network,
    // we store materials as enum-labels and store
    // the objects in a data-dictionary which is looked
    // up at runtime.
    NULL = 0,

    STONE = 1,
    GLASS = 2,
    GOO = 3,

    MAX_N
}
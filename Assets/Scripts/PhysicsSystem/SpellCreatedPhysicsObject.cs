
public class SpellCreatedPhysicsObject : PhysicsObject
{
    public void AssignProperties(ObjectCore createdby)
    {
        // Carryover any spell-modifiers into the properties.
        physicsObjectProperties.size = createdby.size;
    }
}

using Fusion;
public interface IHasPrefabRefToBuffer
{
    // Just tells you that the given
    // spellnode spawns a network object.
    // And gives you the prefabid of that object.

    public NetworkPrefabRef prefabRefToBuffer { get; }
}

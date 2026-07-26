public enum RuneRigSpawnSource : byte
{
    None = 0,
    RuneDefinition = 1,
    BlueprintCatalog = 2,
    InventorySlot = 3
}

public static class RuneRigSpawnCommand
{
    private const int VariantShift = 16;
    private const int SourceShift = 23;
    private const int SequenceShift = 25;

    private const uint ReferenceMask = 0xFFFFu;
    private const uint VariantMask = 0x7Fu;
    private const uint SourceMask = 0x3u;
    private const uint SequenceMask = 0x7Fu;

    public static uint Pack(RuneRigSpawnSource source, ushort referenceId, byte variant, byte sequence)
    {
        return (uint)referenceId | (((uint)variant & VariantMask) << VariantShift) | (((uint)source & SourceMask) << SourceShift) | (((uint)sequence & SequenceMask) << SequenceShift);
    }

    public static ushort GetReferenceId(uint command)
    {
        return (ushort)(command & ReferenceMask);
    }

    public static byte GetVariant(uint command)
    {
        return (byte)((command >> VariantShift) & VariantMask);
    }

    public static RuneRigSpawnSource GetSource(uint command)
    {
        return (RuneRigSpawnSource)((command >> SourceShift) & SourceMask);
    }

    public static byte GetSequence(uint command)
    {
        return (byte)((command >> SequenceShift) & SequenceMask);
    }

    public static bool IsValid(uint command)
    {
        return command != 0 && GetSource(command) != RuneRigSpawnSource.None && GetSequence(command) != 0;
    }
}
using Fusion;
using UnityEngine;

public class NetworkedMemoryAllocator : NetworkBehaviour
{
    [Networked, Capacity(16)] public NetworkArray<float> FloatMemory { get; }
    [Networked, Capacity(8)] public NetworkArray<int> IntMemory { get; }

    // Allocation Trackers (1 = Used, 0 = Free)
    [Networked] public int UsedFloatBitmask { get; set; }
    [Networked] public int UsedIntBitmask { get; set; }

    // Hardcoded to match the Capacities above
    private const int MAX_FLOAT_CAPACITY = 16;
    private const int MAX_INT_CAPACITY = 32;

    public bool TryClaimFloats(int count, out byte startIndex)
    {
        startIndex = 0;

        if (count == 0) return true; // Edge case: spell needs 0 floats
        if (count > MAX_FLOAT_CAPACITY) return false;

        // Search for a block of empty slots
        int foundIndex = FindContiguousFreeSlots(UsedFloatBitmask, MAX_FLOAT_CAPACITY, count);

        if (foundIndex != -1)
        {
            // Create a mask of 1s (e.g., if count is 3, mask is binary 111)
            // Then shift it to the correct starting position.
            int claimMask = ((1 << count) - 1) << foundIndex;

            // Flip those specific bits to 1 (Claimed)
            UsedFloatBitmask |= claimMask;

            startIndex = (byte)foundIndex;
            return true;
        }

        return false; // Out of memory! Effect is rejected.
    }

    public void FreeFloats(byte startIndex, int count)
    {
        if (count == 0) return;

        // Create the exact same mask we used to claim it
        int freeMask = ((1 << count) - 1) << startIndex;

        // Invert the mask (111 becomes 000, rest are 1s) and use AND to flip the bits to 0
        UsedFloatBitmask &= ~freeMask;
    }


    // --- INT MEMORY API ---
    // Identical logic, just pointing to the Int bitmask.

    public bool TryClaimInts(int count, out byte startIndex)
    {
        startIndex = 0;
        if (count == 0) return true;
        if (count > MAX_INT_CAPACITY) return false;

        int foundIndex = FindContiguousFreeSlots(UsedIntBitmask, MAX_INT_CAPACITY, count);
        if (foundIndex != -1)
        {
            int claimMask = ((1 << count) - 1) << foundIndex;
            UsedIntBitmask |= claimMask;

            startIndex = (byte)foundIndex;
            return true;
        }

        return false;
    }

    public void FreeInts(byte startIndex, int count)
    {
        if (count == 0) return;
        int freeMask = ((1 << count) - 1) << startIndex;
        UsedIntBitmask &= ~freeMask;
    }


    // --- CORE DETERMINISTIC LOGIC ---

    /// <summary>
    /// Searches a bitmask from index 0 upwards for a sequence of contiguous 0s.
    /// Returns the starting index, or -1 if no space is found.
    /// </summary>
    private int FindContiguousFreeSlots(int bitmask, int capacity, int requiredCount)
    {
        // We need a sequence of 'requiredCount' number of 1s to test against.
        // If requiredCount is 2, requiredMask is binary 11 (decimal 3).
        int requiredMask = (1 << requiredCount) - 1;

        // Slide a window across the bitmask from index 0 upwards
        for (int i = 0; i <= capacity - requiredCount; i++)
        {
            // Shift our test mask to the current index 'i'
            int testMask = requiredMask << i;

            // If bitmask & testMask == 0, it means ALL those specific bits in the bitmask are currently 0!
            if ((bitmask & testMask) == 0)
            {
                return i; // We found a perfect contiguous block!
            }
        }

        return -1; // Failed to find a big enough block
    }
}
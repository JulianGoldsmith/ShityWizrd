using UnityEngine;

public class TickDurationAttribute : PropertyAttribute
{
    public int MinValue { get; }

    public TickDurationAttribute(int minValue = 0)
    {
        MinValue = minValue;
    }
}
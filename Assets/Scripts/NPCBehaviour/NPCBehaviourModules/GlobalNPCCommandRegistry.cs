using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "AI Commands/Global Registry")]
public class GlobalNPCCommandRegistry : ScriptableObject
{
    [System.Serializable]
    public struct Mapping
    {
        public CommandType Type;
        public NPCCommand Command;
    }

    public List<Mapping> UniversalCommands;

    private Dictionary<CommandType, NPCCommand> _dict;

    public void Initialize()
    {
        if (_dict != null) return;
        _dict = new Dictionary<CommandType, NPCCommand>();
        foreach (var map in UniversalCommands)
        {
            _dict[map.Type] = map.Command;
        }
    }

    public NPCCommand GetUniversalCommand(CommandType type)
    {
        if (_dict.TryGetValue(type, out var proc)) return proc;
        Debug.LogError($"[GlobalCommandRegistry] Missing universal fallback for {type}!");
        return null;
    }
}
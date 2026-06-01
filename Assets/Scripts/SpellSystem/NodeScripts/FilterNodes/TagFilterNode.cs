using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "TagFilterNode", menuName = "SpellNodes/TagFilterNode")]
public class TagFilterNode : FilterNode
{
    public List<string> tags;

    public override IRuntimeNode CompileNode(SpellCompilationContext context)
    {
        throw new System.NotImplementedException();
    }

    public override bool Evaluate(GameObject go) {
        bool isValid = false;

        foreach (string tag in tags) {
            if (go.CompareTag(tag)) {
                isValid = true;
                break;
            }
        }
        return isValid;
    }
}

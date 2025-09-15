using System;
using System.Collections.Generic;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Composite = Unity.Behavior.Composite;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "Reactive Selector", story: "Reacitve Selector", category: "Flow", id: "4a526593c5c76b5538bb22e4bddc759e")]
public partial class ReactiveSelectorSequence : Composite
{
    [CreateProperty] private int m_Current = -1;

    [CreateProperty] private int m_ScanIndex = 0;

    protected override Status OnStart()
    {
        m_Current = -1;
        m_ScanIndex = 0;
        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if (m_Current > 0)
        {
            EndNode(Children[m_Current]);
            m_Current = -1;
            m_ScanIndex = 0;    
            return Status.Running; 
        }

 
        if (m_Current == 0)
        {
            var s0 = Children[0].CurrentStatus;
            if (s0 == Status.Running) return Status.Waiting;
            if (s0 == Status.Success) { m_Current = -1; m_ScanIndex = 0; return Status.Success; }
            
            m_Current = -1;
            m_ScanIndex = 1;
            return Status.Running;
        }

        for (int i = m_ScanIndex; i < Children.Count; i++)
        {
            var s = StartNode(Children[i]); 

            if (s == Status.Failure)
            {

                m_ScanIndex = i + 1;
                if (m_ScanIndex >= Children.Count) { m_ScanIndex = 0; return Status.Failure; }
                return Status.Running; 
            }

            if (s == Status.Running)
            {
                m_Current = i;
                return Status.Waiting; 
            }

            m_Current = -1;
            m_ScanIndex = 0;
            return Status.Success;
        }

        m_ScanIndex = 0;
        return Status.Failure;
    }

    protected override void OnEnd()
    {
        m_Current = -1;
        m_ScanIndex = 0;
    }
}



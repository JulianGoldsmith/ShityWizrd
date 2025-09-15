using System;
using System.Collections.Generic;
using Unity.Behavior;
using Unity.Properties;

namespace Unity.Behavior
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Priority Selector (Reactive)",
        description: "Rechecks higher-priority children each tick and preempts lower-priority ones.",
        icon: "Icons/selector",
        category: "Flow",
        id: "1f3d3f4b1f8d4b7a9a3c2d9e7c0a1234")]
    internal partial class PrioritySelectorComposite : Composite
    {
        [CreateProperty] private int m_CurrentChild = -1;

        [CreateProperty] public List<BlackboardVariable<bool>> CanRun = new();

        protected override Status OnStart()
        {
            m_CurrentChild = -1;
            return Status.Running;
        }

        bool Eligible(int i) => i < CanRun.Count ? CanRun[i].Value : true;

        protected override Status OnUpdate()
        {

            int desired = -1;
            for (int i = 0; i < Children.Count; i++)
                if (Eligible(i)) { desired = i; break; }

            if (desired == -1)
            {
                if (m_CurrentChild >= 0)
                {
                    EndNode(Children[m_CurrentChild]); 
                    m_CurrentChild = -1;
                }
                return Status.Failure;
            }

            if (desired != m_CurrentChild)
            {
                if (m_CurrentChild >= 0)
                    EndNode(Children[m_CurrentChild]); 

                m_CurrentChild = desired;
                var s = StartNode(Children[m_CurrentChild]);
                return s == Status.Running ? Status.Waiting : s;
            }

            var cs = Children[m_CurrentChild].CurrentStatus;

            if (cs == Status.Failure)
            {
                for (int i = m_CurrentChild + 1; i < Children.Count; i++)
                    if (Eligible(i))
                    {
                        m_CurrentChild = i;
                        var s2 = StartNode(Children[m_CurrentChild]);
                        return s2 == Status.Running ? Status.Waiting : s2;
                    }
                m_CurrentChild = -1;
                return Status.Failure;
            }

            return cs == Status.Running ? Status.Waiting : cs;
        }

        protected override void OnEnd()
        {
            m_CurrentChild = -1;
        }
    }
}
using System;
using System.Collections.Generic;

namespace Yuspec.Unity
{
    [Serializable]
    public sealed class YuspecScenarioResult
    {
        public string Name;
        public bool Passed;
        public string Message;
        public List<string> Failures = new List<string>();
    }

    [Serializable]
    public sealed class YuspecStateMachineStatus
    {
        public string BehaviorName;
        public string EntityId;
        public string EntityType;
        public string CurrentState;
        public float StateElapsed;
        public float LastTransitionTime;
    }
}

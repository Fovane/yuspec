using System;
using UnityEngine;

namespace Yuspec.Unity
{
    [Serializable]
    public sealed class YuspecEvent
    {
        public string eventName;
        public YuspecEntity actor;
        public YuspecEntity target;
        public string payload;
        public float time;

        public YuspecEvent(string eventName, YuspecEntity actor = null, YuspecEntity target = null, string payload = "")
        {
            this.eventName = eventName;
            this.actor = actor;
            this.target = target;
            this.payload = payload;
            time = Application.isPlaying ? Time.time : 0f;
        }

        public override string ToString()
        {
            var actorText = actor != null ? actor.EntityId : "-";
            var targetText = target != null ? target.EntityId : "-";
            var payloadText = string.IsNullOrEmpty(payload) ? string.Empty : $" {payload}";
            return $"{time:0.000} {eventName} actor={actorText} target={targetText}{payloadText}";
        }
    }
}

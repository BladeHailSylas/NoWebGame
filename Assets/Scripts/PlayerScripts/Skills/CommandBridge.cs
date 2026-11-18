using Moves;
using UnityEngine;

namespace PlayerScripts.Skills
{
    public interface ICommandPort
    {
        void EnqueueCommand(SkillCommand cmd);
    }

    public static class CommandBridge
    {
        public static void Enqueue(SkillCommand cmd) => TryEnqueue(cmd);
        public static bool TryEnqueue(SkillCommand cmd)
        {
            var collector = CommandCollector.Instance;
            if (!collector || !collector.isActiveAndEnabled)
            {
                AttackLog.Write(cmd.Trace, "CommandDropped", "reason=MissingGlobalCollector");
                Debug.LogWarning($"CommandBridge dropped {cmd.Mech?.GetType().Name ?? "Unknown"} because CommandCollector.Instance is not available.");
                return false;
            }

            return collector.TryEnqueueCommand(cmd);
        }
    }
}

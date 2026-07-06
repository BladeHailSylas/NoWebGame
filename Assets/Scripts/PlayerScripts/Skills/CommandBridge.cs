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
        public static void Enqueue(SkillCommand cmd)
        {
            var collector = CommandCollector.Instance;
            if (collector is null)
            {
                Debug.LogWarning($"CommandBridge dropped {cmd.Mech?.GetType().Name ?? "Unknown"} because CommandCollector.Instance is not available.");
                return;
            }

            collector.EnqueueCommand(cmd);
        }
    }
}

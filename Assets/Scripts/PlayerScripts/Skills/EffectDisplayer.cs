using System.Numerics;
using Moves;
using Moves.Effects.Definitions;
using Moves.Mechanisms;

namespace PlayerScripts.Skills
{
    public sealed class EffectDisplayer
    {
        public void Display(SkillCommand cmd)
        {
            switch (cmd.Mech)
            {
                case MeleeMechanism:
                    SpawnMeleeEffect(cmd);
                    break;
            }
        }

        private void SpawnMeleeEffect(SkillCommand cmd)
        {
            if (cmd.Mech is not MeleeMechanism mech || cmd.Params is not MeleeParams param) return;
        }
    }
}
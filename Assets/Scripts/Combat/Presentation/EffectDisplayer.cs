using global::NoWebGame.Combat.Mechanisms;
using global::NoWebGame.Combat.Casting;
using System.Numerics;




namespace NoWebGame.Combat.Presentation
{
    public interface IEffectDisplayer
    {
        void Display(SkillCommand cmd);
    }
    public sealed class EffectDisplayer : IEffectDisplayer
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
            if (cmd.Mech is not MeleeMechanism mech) return;
        }
    }
}

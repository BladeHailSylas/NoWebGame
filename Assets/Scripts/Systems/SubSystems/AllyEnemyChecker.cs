using UnityEngine;

namespace Systems.SubSystems
{
    public static class AllyEnemyChecker
    {
        public static bool IsAlly(int targetLayer, int sourceLayer)
        {
            if (sourceLayer == LayerMask.NameToLayer("You") || sourceLayer == LayerMask.NameToLayer("Ally"))
                return targetLayer == LayerMask.NameToLayer("You")
                       || targetLayer == LayerMask.NameToLayer("Ally");

            if (sourceLayer == LayerMask.NameToLayer("Foe"))
                return targetLayer == LayerMask.NameToLayer("Foe");

            return false;
        }
        public static bool IsEnemy(int targetLayer, int sourceLayer)
        {
            if (sourceLayer == LayerMask.NameToLayer("Foe"))
                return targetLayer == LayerMask.NameToLayer("You")
                       || targetLayer == LayerMask.NameToLayer("Ally");

            if (sourceLayer == LayerMask.NameToLayer("You")
                || sourceLayer == LayerMask.NameToLayer("Ally"))
                return targetLayer == LayerMask.NameToLayer("Foe");

            return false;
        }
    }

}
using System.Collections.Generic;
using System.Linq;

#region ===== Intent Validate =====
namespace Olds.Systems.Intents
{
    /// <summary>
    /// This filters the available intents
    /// </summary>
    public sealed class IntentValidator
    {
        private List<IIntent> _validIntents;
        private ushort[] _immovableIDs;
        private ushort[] _unattackableIDs;
        public IIntent[] ValidatedIntents => _validIntents.ToArray();
        public void GetFlush(IIntent[] intents)
        {
            _validIntents.Clear();
            foreach (var intent in intents.Where(intent => !(_immovableIDs.Contains(intent.OwnerID) || _unattackableIDs.Contains(intent.OwnerID))))
            {
                if(intent.Type == IntentType.Move || intent.Type == IntentType.Cast) _validIntents.Add(intent);
            }
        }
    }
}
#endregion
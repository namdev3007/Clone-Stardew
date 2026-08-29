using Plugins.Lowscope.ComponentSaveSystem;
using UnityEngine;

namespace Action.Actions
{
    [CreateAssetMenu(menuName = "Actions/Set To Temporary Save Slot")]
    public class ActionSetToTemporarySaveSlot : ScriptableObject
    {
        public void SetToTemporarySlot()
        {
            SaveMaster.SetSlotToTemporarySlot(false);
        }
    }
}

using UnityEngine;

namespace BlueprintSystem
{
    public sealed class UIBlueprintBinder : BlueprintRunner
    {
        [SerializeField] private bool triggerOnEnable = true;
        [SerializeField] private string enableEventName = "OnOpen";
        [SerializeField] private bool triggerOnDisable = true;
        [SerializeField] private string disableEventName = "OnClose";

        private void OnEnable()
        {
            if (triggerOnEnable)
            {
                TriggerEvent(enableEventName);
            }
        }

        private void OnDisable()
        {
            if (triggerOnDisable)
            {
                TriggerEvent(disableEventName);
            }

            ClearReactiveBindings();
        }
    }
}

namespace BlueprintSystem
{
    public static class BlueprintEventUtility
    {
        public static bool IsEventNode(BlueprintNodeManifest manifest)
        {
            if (manifest == null)
            {
                return false;
            }

            if (manifest.TypeId != null && manifest.TypeId.Contains(".Event."))
            {
                return true;
            }

            return manifest.Executor == "Flow.Event";
        }

        public static string GetEventName(BlueprintNodeSource node)
        {
            if (node == null)
            {
                return null;
            }

            object explicitName;
            if (node.Properties.TryGetValue("eventName", out explicitName) && explicitName != null)
            {
                return explicitName.ToString();
            }

            switch (node.TypeId)
            {
                case "UI.Event.OnOpen":
                    return "OnOpen";
                case "UI.Event.OnClose":
                    return "OnClose";
                case "Game.Event.OnStart":
                    return "OnStart";
                case "Game.Event.OnTick":
                    return GetTickEventName(node);
                default:
                    int lastDot = node.TypeId == null ? -1 : node.TypeId.LastIndexOf('.');
                    return lastDot >= 0 ? node.TypeId.Substring(lastDot + 1) : node.TypeId;
            }
        }

        private static string GetTickEventName(BlueprintNodeSource node)
        {
            object phaseValue;
            string phase = node.Properties.TryGetValue("phase", out phaseValue) && phaseValue != null
                ? phaseValue.ToString()
                : "Update";

            switch (phase)
            {
                case "FixedUpdate":
                    return "OnFixedTick";
                case "LateUpdate":
                    return "OnLateTick";
                default:
                    return "OnTick";
            }
        }
    }
}

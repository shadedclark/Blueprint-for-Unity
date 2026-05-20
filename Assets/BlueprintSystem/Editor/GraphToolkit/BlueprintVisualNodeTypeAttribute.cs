using System;

namespace BlueprintSystem.Editor
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class BlueprintVisualNodeTypeAttribute : Attribute
    {
        public string BlueprintTypeId { get; private set; }

        public BlueprintVisualNodeTypeAttribute(string typeId)
        {
            BlueprintTypeId = typeId;
        }
    }
}

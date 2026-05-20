namespace BlueprintSystem
{
    public sealed class BlueprintRef
    {
        public BlueprintRef(BlueprintRunner runner)
            : this(runner as IBlueprintInstance)
        {
        }

        public BlueprintRef(IBlueprintInstance instance)
        {
            Instance = instance;
        }

        public IBlueprintInstance Instance { get; private set; }

        public BlueprintRunner Runner
        {
            get { return Instance as BlueprintRunner; }
        }

        public bool IsValid
        {
            get { return Instance != null; }
        }
    }
}

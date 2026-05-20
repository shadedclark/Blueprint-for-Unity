using UnityEngine;

namespace BlueprintSystem
{
    public sealed class GameSetRendererMaterialExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Game.SetRendererMaterial"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            string target = context.GetInputValue(node, "target", string.Empty);
            Renderer renderer = GameExecutorBindingUtility.ResolveBinding<Renderer>(context, target);
            if (renderer == null)
            {
                return BlueprintExecResult.Error("Game.SetRendererMaterial could not resolve Renderer binding '" + target + "'.");
            }

            string value = context.GetInputValue(node, "value", string.Empty);
            Material material = GameExecutorBindingUtility.ResolveBinding<Material>(context, value);
            if (material == null)
            {
                return BlueprintExecResult.Error("Game.SetRendererMaterial could not resolve Material binding '" + value + "'.");
            }

            Material[] materials = renderer.materials;
            if (materials == null || materials.Length == 0)
            {
                return BlueprintExecResult.Error("Game.SetRendererMaterial target Renderer has no material slots.");
            }

            int materialIndex = context.GetInputValue(node, "materialIndex", 0);
            if (materialIndex < 0 || materialIndex >= materials.Length)
            {
                return BlueprintExecResult.Error("Game.SetRendererMaterial materialIndex '" + materialIndex + "' is out of range.");
            }

            materials[materialIndex] = material;
            renderer.materials = materials;
            return BlueprintExecResult.Continue("execOut");
        }
    }

    public sealed class GameSetRendererMaterialColorExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Game.SetRendererMaterialColor"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            string target = context.GetInputValue(node, "target", string.Empty);
            Renderer renderer = GameExecutorBindingUtility.ResolveBinding<Renderer>(context, target);
            if (renderer == null)
            {
                return BlueprintExecResult.Error("Game.SetRendererMaterialColor could not resolve Renderer binding '" + target + "'.");
            }

            Material material;
            BlueprintExecResult error;
            if (!TryGetRendererMaterial(renderer, "Game.SetRendererMaterialColor", out material, out error))
            {
                return error;
            }

            string propertyName = context.GetInputValue(node, "propertyName", "_Color");
            if (string.IsNullOrEmpty(propertyName) || !material.HasProperty(propertyName))
            {
                return BlueprintExecResult.Error("Game.SetRendererMaterialColor material does not have property '" + propertyName + "'.");
            }

            Color value = GameExecutorValueUtility.GetColorInput(context, node, "value", Color.white);
            material.SetColor(propertyName, value);
            return BlueprintExecResult.Continue("execOut");
        }

        private static bool TryGetRendererMaterial(Renderer renderer, string executorId, out Material material, out BlueprintExecResult error)
        {
            material = null;
            error = new BlueprintExecResult();
            Material[] materials = renderer.materials;
            if (materials == null || materials.Length == 0 || materials[0] == null)
            {
                error = BlueprintExecResult.Error(executorId + " target Renderer has no material.");
                return false;
            }

            material = materials[0];
            return true;
        }
    }

    public sealed class GameSetRendererTextureExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Game.SetRendererTexture"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            string target = context.GetInputValue(node, "target", string.Empty);
            Renderer renderer = GameExecutorBindingUtility.ResolveBinding<Renderer>(context, target);
            if (renderer == null)
            {
                return BlueprintExecResult.Error("Game.SetRendererTexture could not resolve Renderer binding '" + target + "'.");
            }

            string value = context.GetInputValue(node, "value", string.Empty);
            Texture texture = GameExecutorBindingUtility.ResolveBinding<Texture>(context, value);
            if (texture == null)
            {
                return BlueprintExecResult.Error("Game.SetRendererTexture could not resolve Texture binding '" + value + "'.");
            }

            Material material;
            BlueprintExecResult error;
            if (!TryGetRendererMaterial(renderer, out material, out error))
            {
                return error;
            }

            string propertyName = context.GetInputValue(node, "propertyName", "_MainTex");
            if (string.IsNullOrEmpty(propertyName) || !material.HasProperty(propertyName))
            {
                return BlueprintExecResult.Error("Game.SetRendererTexture material does not have property '" + propertyName + "'.");
            }

            material.SetTexture(propertyName, texture);
            return BlueprintExecResult.Continue("execOut");
        }

        private static bool TryGetRendererMaterial(Renderer renderer, out Material material, out BlueprintExecResult error)
        {
            material = null;
            error = new BlueprintExecResult();
            Material[] materials = renderer.materials;
            if (materials == null || materials.Length == 0 || materials[0] == null)
            {
                error = BlueprintExecResult.Error("Game.SetRendererTexture target Renderer has no material.");
                return false;
            }

            material = materials[0];
            return true;
        }
    }
}

using System;
using UnityEngine;

namespace BlueprintSystem
{
    public sealed class GameSetRigidbodyLinearVelocityExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Game.SetRigidbodyLinearVelocity"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            string target = context.GetInputValue(node, "target", string.Empty);
            Rigidbody rigidbody = GameExecutorBindingUtility.ResolveBinding<Rigidbody>(context, target);
            if (rigidbody == null)
            {
                return BlueprintExecResult.Error("Game.SetRigidbodyLinearVelocity could not resolve Rigidbody binding '" + target + "'.");
            }

            rigidbody.linearVelocity = GameExecutorValueUtility.GetVector3Input(context, node, "value", Vector3.zero);
            return BlueprintExecResult.Continue("execOut");
        }
    }

    public sealed class GameAddRigidbodyForceExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Game.AddRigidbodyForce"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            string target = context.GetInputValue(node, "target", string.Empty);
            Rigidbody rigidbody = GameExecutorBindingUtility.ResolveBinding<Rigidbody>(context, target);
            if (rigidbody == null)
            {
                return BlueprintExecResult.Error("Game.AddRigidbodyForce could not resolve Rigidbody binding '" + target + "'.");
            }

            string modeName = context.GetInputValue(node, "mode", "Force");
            ForceMode mode;
            if (!TryParseForceMode(modeName, out mode))
            {
                return BlueprintExecResult.Error("Game.AddRigidbodyForce has invalid mode '" + modeName + "'.");
            }

            Vector3 force = GameExecutorValueUtility.GetVector3Input(context, node, "force", Vector3.zero);
            rigidbody.AddForce(force, mode);
            return BlueprintExecResult.Continue("execOut");
        }

        private static bool TryParseForceMode(string modeName, out ForceMode mode)
        {
            string normalized = string.IsNullOrEmpty(modeName) ? "Force" : modeName.Trim();
            if (string.Equals(normalized, "Force", StringComparison.OrdinalIgnoreCase))
            {
                mode = ForceMode.Force;
                return true;
            }

            if (string.Equals(normalized, "Acceleration", StringComparison.OrdinalIgnoreCase))
            {
                mode = ForceMode.Acceleration;
                return true;
            }

            if (string.Equals(normalized, "Impulse", StringComparison.OrdinalIgnoreCase))
            {
                mode = ForceMode.Impulse;
                return true;
            }

            if (string.Equals(normalized, "VelocityChange", StringComparison.OrdinalIgnoreCase))
            {
                mode = ForceMode.VelocityChange;
                return true;
            }

            mode = ForceMode.Force;
            return false;
        }
    }

    public sealed class GameSetColliderEnabledExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Game.SetColliderEnabled"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            string target = context.GetInputValue(node, "target", string.Empty);
            Collider collider = GameExecutorBindingUtility.ResolveBinding<Collider>(context, target);
            if (collider == null)
            {
                return BlueprintExecResult.Error("Game.SetColliderEnabled could not resolve Collider binding '" + target + "'.");
            }

            collider.enabled = context.GetInputValue(node, "value", true);
            return BlueprintExecResult.Continue("execOut");
        }
    }

    public sealed class GameSetColliderIsTriggerExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Game.SetColliderIsTrigger"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            string target = context.GetInputValue(node, "target", string.Empty);
            Collider collider = GameExecutorBindingUtility.ResolveBinding<Collider>(context, target);
            if (collider == null)
            {
                return BlueprintExecResult.Error("Game.SetColliderIsTrigger could not resolve Collider binding '" + target + "'.");
            }

            collider.isTrigger = context.GetInputValue(node, "value", true);
            return BlueprintExecResult.Continue("execOut");
        }
    }

    public sealed class GameSetRigidbody2DLinearVelocityExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Game.SetRigidbody2DLinearVelocity"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            string target = context.GetInputValue(node, "target", string.Empty);
            Rigidbody2D rigidbody = GameExecutorBindingUtility.ResolveBinding<Rigidbody2D>(context, target);
            if (rigidbody == null)
            {
                return BlueprintExecResult.Error("Game.SetRigidbody2DLinearVelocity could not resolve Rigidbody2D binding '" + target + "'.");
            }

            rigidbody.linearVelocity = GameExecutorValueUtility.GetVector2Input(context, node, "value", Vector2.zero);
            return BlueprintExecResult.Continue("execOut");
        }
    }

    public sealed class GameAddRigidbody2DForceExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Game.AddRigidbody2DForce"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            string target = context.GetInputValue(node, "target", string.Empty);
            Rigidbody2D rigidbody = GameExecutorBindingUtility.ResolveBinding<Rigidbody2D>(context, target);
            if (rigidbody == null)
            {
                return BlueprintExecResult.Error("Game.AddRigidbody2DForce could not resolve Rigidbody2D binding '" + target + "'.");
            }

            string modeName = context.GetInputValue(node, "mode", "Force");
            ForceMode2D mode;
            if (!TryParseForceMode2D(modeName, out mode))
            {
                return BlueprintExecResult.Error("Game.AddRigidbody2DForce has invalid mode '" + modeName + "'.");
            }

            Vector2 force = GameExecutorValueUtility.GetVector2Input(context, node, "force", Vector2.zero);
            rigidbody.AddForce(force, mode);
            return BlueprintExecResult.Continue("execOut");
        }

        private static bool TryParseForceMode2D(string modeName, out ForceMode2D mode)
        {
            string normalized = string.IsNullOrEmpty(modeName) ? "Force" : modeName.Trim();
            if (string.Equals(normalized, "Force", StringComparison.OrdinalIgnoreCase))
            {
                mode = ForceMode2D.Force;
                return true;
            }

            if (string.Equals(normalized, "Impulse", StringComparison.OrdinalIgnoreCase))
            {
                mode = ForceMode2D.Impulse;
                return true;
            }

            mode = ForceMode2D.Force;
            return false;
        }
    }

    public sealed class GameSetCollider2DEnabledExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Game.SetCollider2DEnabled"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            string target = context.GetInputValue(node, "target", string.Empty);
            Collider2D collider = GameExecutorBindingUtility.ResolveBinding<Collider2D>(context, target);
            if (collider == null)
            {
                return BlueprintExecResult.Error("Game.SetCollider2DEnabled could not resolve Collider2D binding '" + target + "'.");
            }

            collider.enabled = context.GetInputValue(node, "value", true);
            return BlueprintExecResult.Continue("execOut");
        }
    }

    public sealed class GameSetCollider2DIsTriggerExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Game.SetCollider2DIsTrigger"; }
        }

        public override BlueprintExecResult Execute(BlueprintExecutionContext context, RuntimeNode node)
        {
            string target = context.GetInputValue(node, "target", string.Empty);
            Collider2D collider = GameExecutorBindingUtility.ResolveBinding<Collider2D>(context, target);
            if (collider == null)
            {
                return BlueprintExecResult.Error("Game.SetCollider2DIsTrigger could not resolve Collider2D binding '" + target + "'.");
            }

            collider.isTrigger = context.GetInputValue(node, "value", true);
            return BlueprintExecResult.Continue("execOut");
        }
    }
}

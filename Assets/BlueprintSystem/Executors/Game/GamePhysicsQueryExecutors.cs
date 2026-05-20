using System.Collections.Generic;
using UnityEngine;

namespace BlueprintSystem
{
    public sealed class GameRaycastExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Game.Raycast"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            PhysicsRaycastResult result = GamePhysicsQueryUtility.Raycast(context, node);
            return GamePhysicsQueryUtility.ReadRaycastResult(result, outputPortId);
        }
    }

    public sealed class GameSphereCastExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Game.SphereCast"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            PhysicsRaycastResult result = GamePhysicsQueryUtility.SphereCast(context, node);
            return GamePhysicsQueryUtility.ReadRaycastResult(result, outputPortId);
        }
    }

    public sealed class GameBoxCastExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Game.BoxCast"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            PhysicsRaycastResult result = GamePhysicsQueryUtility.BoxCast(context, node);
            return GamePhysicsQueryUtility.ReadRaycastResult(result, outputPortId);
        }
    }

    public sealed class GameOverlapSphereExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Game.OverlapSphere"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            PhysicsOverlapResult result = GamePhysicsQueryUtility.OverlapSphere(context, node);
            return GamePhysicsQueryUtility.ReadOverlapResult(result, outputPortId);
        }
    }

    public sealed class GameOverlapBoxExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Game.OverlapBox"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            PhysicsOverlapResult result = GamePhysicsQueryUtility.OverlapBox(context, node);
            return GamePhysicsQueryUtility.ReadOverlapResult(result, outputPortId);
        }
    }

    public sealed class GameRaycast2DExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Game.Raycast2D"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            PhysicsRaycast2DResult result = GamePhysicsQueryUtility.Raycast2D(context, node);
            return GamePhysicsQueryUtility.ReadRaycast2DResult(result, outputPortId);
        }
    }

    public sealed class GameOverlapCircle2DExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Game.OverlapCircle2D"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            PhysicsOverlapResult result = GamePhysicsQueryUtility.OverlapCircle2D(context, node);
            return GamePhysicsQueryUtility.ReadOverlapResult(result, outputPortId);
        }
    }

    public sealed class GameOverlapBox2DExecutor : BlueprintNodeExecutor
    {
        public override string ExecutorId
        {
            get { return "Game.OverlapBox2D"; }
        }

        public override object Evaluate(BlueprintExecutionContext context, RuntimeNode node, string outputPortId)
        {
            PhysicsOverlapResult result = GamePhysicsQueryUtility.OverlapBox2D(context, node);
            return GamePhysicsQueryUtility.ReadOverlapResult(result, outputPortId);
        }
    }

    internal struct PhysicsRaycastResult
    {
        public bool Hit;
        public Vector3 Point;
        public Vector3 Normal;
        public float Distance;
        public string ColliderName;
        public string GameObjectName;
    }

    internal struct PhysicsRaycast2DResult
    {
        public bool Hit;
        public Vector2 Point;
        public Vector2 Normal;
        public float Distance;
        public string ColliderName;
        public string GameObjectName;
    }

    internal struct PhysicsOverlapResult
    {
        public bool HasAny;
        public int Count;
        public string FirstName;
        public List<object> Names;
    }

    internal static class GamePhysicsQueryUtility
    {
        public static PhysicsRaycastResult Raycast(BlueprintExecutionContext context, RuntimeNode node)
        {
            Vector3 origin = GameExecutorValueUtility.GetVector3Input(context, node, "origin", Vector3.zero);
            Vector3 direction = GameExecutorValueUtility.GetVector3Input(context, node, "direction", Vector3.forward);
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return default(PhysicsRaycastResult);
            }

            RaycastHit hit;
            bool hasHit = Physics.Raycast(
                origin,
                direction.normalized,
                out hit,
                GetDistance(context, node, "maxDistance"),
                GetLayerMask(context, node),
                GetQueryTriggerInteraction(context, node));
            return FromHit(hasHit, hit);
        }

        public static PhysicsRaycastResult SphereCast(BlueprintExecutionContext context, RuntimeNode node)
        {
            Vector3 origin = GameExecutorValueUtility.GetVector3Input(context, node, "origin", Vector3.zero);
            Vector3 direction = GameExecutorValueUtility.GetVector3Input(context, node, "direction", Vector3.forward);
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return default(PhysicsRaycastResult);
            }

            RaycastHit hit;
            bool hasHit = Physics.SphereCast(
                origin,
                Mathf.Max(0f, context.GetInputValue(node, "radius", 0.5f)),
                direction.normalized,
                out hit,
                GetDistance(context, node, "maxDistance"),
                GetLayerMask(context, node),
                GetQueryTriggerInteraction(context, node));
            return FromHit(hasHit, hit);
        }

        public static PhysicsRaycastResult BoxCast(BlueprintExecutionContext context, RuntimeNode node)
        {
            Vector3 center = GameExecutorValueUtility.GetVector3Input(context, node, "center", Vector3.zero);
            Vector3 halfExtents = GameExecutorValueUtility.GetVector3Input(context, node, "halfExtents", Vector3.one * 0.5f);
            Vector3 direction = GameExecutorValueUtility.GetVector3Input(context, node, "direction", Vector3.forward);
            Vector3 orientationEuler = GameExecutorValueUtility.GetVector3Input(context, node, "orientationEuler", Vector3.zero);
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return default(PhysicsRaycastResult);
            }

            RaycastHit hit;
            bool hasHit = Physics.BoxCast(
                center,
                Abs(halfExtents),
                direction.normalized,
                out hit,
                Quaternion.Euler(orientationEuler),
                GetDistance(context, node, "maxDistance"),
                GetLayerMask(context, node),
                GetQueryTriggerInteraction(context, node));
            return FromHit(hasHit, hit);
        }

        public static PhysicsOverlapResult OverlapSphere(BlueprintExecutionContext context, RuntimeNode node)
        {
            Vector3 center = GameExecutorValueUtility.GetVector3Input(context, node, "center", Vector3.zero);
            float radius = Mathf.Max(0f, context.GetInputValue(node, "radius", 0.5f));
            Collider[] colliders = Physics.OverlapSphere(
                center,
                radius,
                GetLayerMask(context, node),
                GetQueryTriggerInteraction(context, node));
            return FromColliders(colliders);
        }

        public static PhysicsOverlapResult OverlapBox(BlueprintExecutionContext context, RuntimeNode node)
        {
            Vector3 center = GameExecutorValueUtility.GetVector3Input(context, node, "center", Vector3.zero);
            Vector3 halfExtents = GameExecutorValueUtility.GetVector3Input(context, node, "halfExtents", Vector3.one * 0.5f);
            Vector3 orientationEuler = GameExecutorValueUtility.GetVector3Input(context, node, "orientationEuler", Vector3.zero);
            Collider[] colliders = Physics.OverlapBox(
                center,
                Abs(halfExtents),
                Quaternion.Euler(orientationEuler),
                GetLayerMask(context, node),
                GetQueryTriggerInteraction(context, node));
            return FromColliders(colliders);
        }

        public static PhysicsRaycast2DResult Raycast2D(BlueprintExecutionContext context, RuntimeNode node)
        {
            Vector2 origin = GameExecutorValueUtility.GetVector2Input(context, node, "origin", Vector2.zero);
            Vector2 direction = GameExecutorValueUtility.GetVector2Input(context, node, "direction", Vector2.right);
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return default(PhysicsRaycast2DResult);
            }

            RaycastHit2D hit = Physics2D.Raycast(
                origin,
                direction.normalized,
                GetDistance(context, node, "distance"),
                GetLayerMask(context, node));
            return FromHit2D(hit);
        }

        public static PhysicsOverlapResult OverlapCircle2D(BlueprintExecutionContext context, RuntimeNode node)
        {
            Vector2 point = GameExecutorValueUtility.GetVector2Input(context, node, "point", Vector2.zero);
            float radius = Mathf.Max(0f, context.GetInputValue(node, "radius", 0.5f));
            Collider2D[] colliders = Physics2D.OverlapCircleAll(point, radius, GetLayerMask(context, node));
            return FromColliders2D(colliders);
        }

        public static PhysicsOverlapResult OverlapBox2D(BlueprintExecutionContext context, RuntimeNode node)
        {
            Vector2 point = GameExecutorValueUtility.GetVector2Input(context, node, "point", Vector2.zero);
            Vector2 size = GameExecutorValueUtility.GetVector2Input(context, node, "size", Vector2.one);
            float angle = context.GetInputValue(node, "angle", 0f);
            Collider2D[] colliders = Physics2D.OverlapBoxAll(point, Abs(size), angle, GetLayerMask(context, node));
            return FromColliders2D(colliders);
        }

        public static object ReadRaycastResult(PhysicsRaycastResult result, string outputPortId)
        {
            switch (outputPortId)
            {
                case "hit":
                    return result.Hit;
                case "point":
                    return result.Point;
                case "normal":
                    return result.Normal;
                case "distance":
                    return result.Distance;
                case "colliderName":
                    return result.ColliderName ?? string.Empty;
                case "gameObjectName":
                    return result.GameObjectName ?? string.Empty;
                default:
                    return null;
            }
        }

        public static object ReadRaycast2DResult(PhysicsRaycast2DResult result, string outputPortId)
        {
            switch (outputPortId)
            {
                case "hit":
                    return result.Hit;
                case "point":
                    return result.Point;
                case "normal":
                    return result.Normal;
                case "distance":
                    return result.Distance;
                case "colliderName":
                    return result.ColliderName ?? string.Empty;
                case "gameObjectName":
                    return result.GameObjectName ?? string.Empty;
                default:
                    return null;
            }
        }

        public static object ReadOverlapResult(PhysicsOverlapResult result, string outputPortId)
        {
            switch (outputPortId)
            {
                case "hasAny":
                    return result.HasAny;
                case "count":
                    return result.Count;
                case "firstName":
                    return result.FirstName ?? string.Empty;
                case "names":
                    return result.Names ?? new List<object>();
                default:
                    return null;
            }
        }

        private static PhysicsRaycastResult FromHit(bool hasHit, RaycastHit hit)
        {
            if (!hasHit || hit.collider == null)
            {
                return default(PhysicsRaycastResult);
            }

            return new PhysicsRaycastResult
            {
                Hit = true,
                Point = hit.point,
                Normal = hit.normal,
                Distance = hit.distance,
                ColliderName = hit.collider.name,
                GameObjectName = hit.collider.gameObject.name
            };
        }

        private static PhysicsRaycast2DResult FromHit2D(RaycastHit2D hit)
        {
            if (hit.collider == null)
            {
                return default(PhysicsRaycast2DResult);
            }

            return new PhysicsRaycast2DResult
            {
                Hit = true,
                Point = hit.point,
                Normal = hit.normal,
                Distance = hit.distance,
                ColliderName = hit.collider.name,
                GameObjectName = hit.collider.gameObject.name
            };
        }

        private static PhysicsOverlapResult FromColliders(Collider[] colliders)
        {
            List<object> names = new List<object>();
            if (colliders != null)
            {
                for (int i = 0; i < colliders.Length; i++)
                {
                    if (colliders[i] != null)
                    {
                        names.Add(colliders[i].gameObject.name);
                    }
                }
            }

            return FromNames(names);
        }

        private static PhysicsOverlapResult FromColliders2D(Collider2D[] colliders)
        {
            List<object> names = new List<object>();
            if (colliders != null)
            {
                for (int i = 0; i < colliders.Length; i++)
                {
                    if (colliders[i] != null)
                    {
                        names.Add(colliders[i].gameObject.name);
                    }
                }
            }

            return FromNames(names);
        }

        private static PhysicsOverlapResult FromNames(List<object> names)
        {
            return new PhysicsOverlapResult
            {
                HasAny = names != null && names.Count > 0,
                Count = names == null ? 0 : names.Count,
                FirstName = names != null && names.Count > 0 ? names[0] as string : string.Empty,
                Names = names ?? new List<object>()
            };
        }

        private static float GetDistance(BlueprintExecutionContext context, RuntimeNode node, string portId)
        {
            float value = context.GetInputValue(node, portId, 1000f);
            return value <= 0f ? Mathf.Infinity : value;
        }

        private static int GetLayerMask(BlueprintExecutionContext context, RuntimeNode node)
        {
            return context.GetInputValue(node, "layerMask", -1);
        }

        private static QueryTriggerInteraction GetQueryTriggerInteraction(BlueprintExecutionContext context, RuntimeNode node)
        {
            bool includeTriggers = context.GetInputValue(node, "includeTriggers", true);
            return includeTriggers ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore;
        }

        private static Vector2 Abs(Vector2 value)
        {
            return new Vector2(Mathf.Abs(value.x), Mathf.Abs(value.y));
        }

        private static Vector3 Abs(Vector3 value)
        {
            return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }
    }
}

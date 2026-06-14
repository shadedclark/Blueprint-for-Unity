using System;
using System.Collections.Generic;
using Unity.GraphToolkit.Editor;

namespace BlueprintSystem.Editor
{
    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Game.Event.OnTick")]
    public sealed class GameOnTickEventVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Game.Event.OnTick", "On Tick", "Events", "Entry point fired from BlueprintRunner Update, FixedUpdate, or LateUpdate.");
            AddValueInput("phase", "TickPhase", false, "property");
            AddExecOutput("execOut", true);
            AddProperty("phase", "TickPhase", false, "Update");
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            EnsurePhaseSurface();
            base.OnDefineOptions(context);
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            EnsurePhaseSurface();
            base.OnDefinePorts(context);
        }

        private void EnsurePhaseSurface()
        {
            if (string.IsNullOrEmpty(TypeId))
            {
                return;
            }

            if (Inputs == null)
            {
                Inputs = new List<BlueprintVisualPortData>();
            }

            if (!HasInput("phase"))
            {
                Inputs.Insert(0, new BlueprintVisualPortData
                {
                    Id = "phase",
                    Kind = "value",
                    Type = "TickPhase",
                    Required = false,
                    Source = "property",
                    AllowMultiple = false
                });
            }

            if (Properties == null)
            {
                Properties = new List<BlueprintVisualPropertyData>();
            }

            if (!HasProperty("phase"))
            {
                Properties.Add(new BlueprintVisualPropertyData
                {
                    Id = "phase",
                    Type = "TickPhase",
                    Required = false,
                    HasValue = true,
                    JsonValue = "\"Update\"",
                    ShowInInspectorOnly = false
                });
            }
        }

        private bool HasInput(string id)
        {
            for (int i = 0; i < Inputs.Count; i++)
            {
                BlueprintVisualPortData input = Inputs[i];
                if (input != null && input.Id == id)
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasProperty(string id)
        {
            for (int i = 0; i < Properties.Count; i++)
            {
                BlueprintVisualPropertyData property = Properties[i];
                if (property != null && property.Id == id)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public abstract class GameTimeValueVisualNode : BlueprintVisualNode
    {
        protected void ConfigureTimeValue(string typeId, string title, string description)
        {
            SetIdentity(typeId, title, "Game/Time", description);
            AddValueOutput("value", "float");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Game.GetDeltaTime")]
    public sealed class GameGetDeltaTimeVisualNode : GameTimeValueVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            ConfigureTimeValue("Game.GetDeltaTime", "Get Delta Time", "Returns Unity Time.deltaTime.");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Game.GetFixedDeltaTime")]
    public sealed class GameGetFixedDeltaTimeVisualNode : GameTimeValueVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            ConfigureTimeValue("Game.GetFixedDeltaTime", "Get Fixed Delta Time", "Returns Unity Time.fixedDeltaTime.");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Game.GetTimeSeconds")]
    public sealed class GameGetTimeSecondsVisualNode : GameTimeValueVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            ConfigureTimeValue("Game.GetTimeSeconds", "Get Time Seconds", "Returns Unity Time.time.");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Game.GetUnscaledTime")]
    public sealed class GameGetUnscaledTimeVisualNode : GameTimeValueVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            ConfigureTimeValue("Game.GetUnscaledTime", "Get Unscaled Time", "Returns Unity Time.unscaledTime.");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Game.GetTimeScale")]
    public sealed class GameGetTimeScaleVisualNode : GameTimeValueVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            ConfigureTimeValue("Game.GetTimeScale", "Get Time Scale", "Returns Unity Time.timeScale.");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Game.SetTimeScale")]
    public sealed class GameSetTimeScaleVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Game.SetTimeScale", "Set Time Scale", "Game/Time", "Sets Unity Time.timeScale clamped to zero or above.");
            AddExecInput("execIn");
            AddValueInput("value", "float", true, "propertyOrConnection");
            AddExecOutput("execOut");
            AddProperty("value", "float", false, 1f);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Game.InstantiateObject")]
    public sealed class GameInstantiateObjectVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Game.InstantiateObject", "Instantiate Object", "Game/Object", "Instantiates a GameObject prefab from a binding or connected runtime asset.");
            AddExecInput("execIn");
            AddValueInput("prefab", "Binding<GameObject>", true, "propertyOrConnection");
            AddValueInput("parent", "Binding<Transform>", false, "propertyOrConnection");
            AddExecOutput("execOut");
            AddValueOutput("instance", "GameObject");
            AddValueOutput("transform", "Transform");
            AddProperty("prefab", "Binding<GameObject>", false);
            AddProperty("parent", "Binding<Transform>", false);
        }
    }

    public abstract class GameTransformGetterVisualNode : BlueprintVisualNode
    {
        protected void ConfigureGetter(string typeId, string title, string description)
        {
            SetIdentity(typeId, title, "Game/Transform", description);
            AddValueInput("target", "Binding<Transform>", true, "property");
            AddValueOutput("value", "Vector3");
            AddProperty("target", "Binding<Transform>", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Game.GetTransformPosition")]
    public sealed class GameGetTransformPositionVisualNode : GameTransformGetterVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            ConfigureGetter("Game.GetTransformPosition", "Get Transform Position", "Returns world Transform.position.");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Game.GetTransformEulerAngles")]
    public sealed class GameGetTransformEulerAnglesVisualNode : GameTransformGetterVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            ConfigureGetter("Game.GetTransformEulerAngles", "Get Transform Euler Angles", "Returns world Transform.eulerAngles.");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Game.GetTransformLocalPosition")]
    public sealed class GameGetTransformLocalPositionVisualNode : GameTransformGetterVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            ConfigureGetter("Game.GetTransformLocalPosition", "Get Transform Local Position", "Returns Transform.localPosition.");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Game.GetTransformLocalEulerAngles")]
    public sealed class GameGetTransformLocalEulerAnglesVisualNode : GameTransformGetterVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            ConfigureGetter("Game.GetTransformLocalEulerAngles", "Get Transform Local Euler Angles", "Returns Transform.localEulerAngles.");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Game.GetTransformLocalScale")]
    public sealed class GameGetTransformLocalScaleVisualNode : GameTransformGetterVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            ConfigureGetter("Game.GetTransformLocalScale", "Get Transform Local Scale", "Returns Transform.localScale.");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Game.GetTransformForward")]
    public sealed class GameGetTransformForwardVisualNode : GameTransformGetterVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            ConfigureGetter("Game.GetTransformForward", "Get Transform Forward", "Returns Transform.forward.");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Game.GetTransformRight")]
    public sealed class GameGetTransformRightVisualNode : GameTransformGetterVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            ConfigureGetter("Game.GetTransformRight", "Get Transform Right", "Returns Transform.right.");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Game.GetTransformUp")]
    public sealed class GameGetTransformUpVisualNode : GameTransformGetterVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            ConfigureGetter("Game.GetTransformUp", "Get Transform Up", "Returns Transform.up.");
        }
    }

    public abstract class GameTransformSetterVisualNode : BlueprintVisualNode
    {
        protected void ConfigureSetter(string typeId, string title, string description, string valueId, List<object> defaultValue)
        {
            SetIdentity(typeId, title, "Game/Transform", description);
            AddExecInput("execIn");
            AddValueInput("target", "Binding<Transform>", true, "property");
            AddValueInput(valueId, "Vector3", true, "propertyOrConnection");
            AddExecOutput("execOut");
            AddProperty("target", "Binding<Transform>", true);
            AddProperty(valueId, "Vector3", false, defaultValue);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Game.SetTransformLocalPosition")]
    public sealed class GameSetTransformLocalPositionVisualNode : GameTransformSetterVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            ConfigureSetter("Game.SetTransformLocalPosition", "Set Transform Local Position", "Sets Transform.localPosition.", "value", new List<object> { 0f, 0f, 0f });
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Game.SetTransformLocalEulerAngles")]
    public sealed class GameSetTransformLocalEulerAnglesVisualNode : GameTransformSetterVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            ConfigureSetter("Game.SetTransformLocalEulerAngles", "Set Transform Local Euler Angles", "Sets Transform.localEulerAngles.", "value", new List<object> { 0f, 0f, 0f });
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Game.TranslateTransform")]
    public sealed class GameTranslateTransformVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Game.TranslateTransform", "Translate Transform", "Game/Transform", "Moves a Transform by a translation in self or world space.");
            AddExecInput("execIn");
            AddValueInput("target", "Binding<Transform>", true, "property");
            AddValueInput("translation", "Vector3", true, "propertyOrConnection");
            AddValueInput("relativeToSelf", "bool", false, "propertyOrConnection");
            AddExecOutput("execOut");
            AddProperty("target", "Binding<Transform>", true);
            AddProperty("translation", "Vector3", false, new List<object> { 0f, 0f, 0f });
            AddProperty("relativeToSelf", "bool", false, true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Game.RotateTransform")]
    public sealed class GameRotateTransformVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Game.RotateTransform", "Rotate Transform", "Game/Transform", "Rotates a Transform by Euler angles in self or world space.");
            AddExecInput("execIn");
            AddValueInput("target", "Binding<Transform>", true, "property");
            AddValueInput("eulerAngles", "Vector3", true, "propertyOrConnection");
            AddValueInput("relativeToSelf", "bool", false, "propertyOrConnection");
            AddExecOutput("execOut");
            AddProperty("target", "Binding<Transform>", true);
            AddProperty("eulerAngles", "Vector3", false, new List<object> { 0f, 0f, 0f });
            AddProperty("relativeToSelf", "bool", false, true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Game.LookAtTransform")]
    public sealed class GameLookAtTransformVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Game.LookAtTransform", "Look At Transform", "Game/Transform", "Rotates a Transform to look at another Transform binding or a world position.");
            AddExecInput("execIn");
            AddValueInput("target", "Binding<Transform>", true, "property");
            AddValueInput("lookTarget", "Binding<Transform>", false, "propertyOrConnection");
            AddValueInput("targetPosition", "Vector3", false, "propertyOrConnection");
            AddValueInput("worldUp", "Vector3", false, "propertyOrConnection");
            AddExecOutput("execOut");
            AddProperty("target", "Binding<Transform>", true);
            AddProperty("lookTarget", "Binding<Transform>", false);
            AddProperty("targetPosition", "Vector3", false, new List<object> { 0f, 0f, 0f });
            AddProperty("worldUp", "Vector3", false, new List<object> { 0f, 1f, 0f });
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Game.SetTransformParent")]
    public sealed class GameSetTransformParentVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Game.SetTransformParent", "Set Transform Parent", "Game/Transform", "Parents a Transform to another bound Transform.");
            AddExecInput("execIn");
            AddValueInput("target", "Binding<Transform>", true, "property");
            AddValueInput("parent", "Binding<Transform>", true, "property");
            AddValueInput("worldPositionStays", "bool", false, "propertyOrConnection");
            AddExecOutput("execOut");
            AddProperty("target", "Binding<Transform>", true);
            AddProperty("parent", "Binding<Transform>", true);
            AddProperty("worldPositionStays", "bool", false, true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Game.DetachTransform")]
    public sealed class GameDetachTransformVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Game.DetachTransform", "Detach Transform", "Game/Transform", "Clears a Transform parent.");
            AddExecInput("execIn");
            AddValueInput("target", "Binding<Transform>", true, "property");
            AddValueInput("worldPositionStays", "bool", false, "propertyOrConnection");
            AddExecOutput("execOut");
            AddProperty("target", "Binding<Transform>", true);
            AddProperty("worldPositionStays", "bool", false, true);
        }
    }

    public abstract class GamePhysicsRaycastVisualNode : BlueprintVisualNode
    {
        protected void AddRaycastOutputs(bool twoDimensional)
        {
            AddValueOutput("hit", "bool");
            AddValueOutput("point", twoDimensional ? "Vector2" : "Vector3");
            AddValueOutput("normal", twoDimensional ? "Vector2" : "Vector3");
            AddValueOutput("distance", "float");
            AddValueOutput("colliderName", "string");
            AddValueOutput("gameObjectName", "string");
        }

        protected void AddLayerMaskProperty()
        {
            AddValueInput("layerMask", "int", false, "propertyOrConnection");
            AddProperty("layerMask", "int", false, -1);
        }

        protected void AddTriggerProperty()
        {
            AddValueInput("includeTriggers", "bool", false, "propertyOrConnection");
            AddProperty("includeTriggers", "bool", false, true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Game.Raycast")]
    public sealed class GameRaycastVisualNode : GamePhysicsRaycastVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Game.Raycast", "Raycast", "Game/Physics", "Casts a 3D ray and returns hit data.");
            AddValueInput("origin", "Vector3", true, "propertyOrConnection");
            AddValueInput("direction", "Vector3", true, "propertyOrConnection");
            AddValueInput("maxDistance", "float", false, "propertyOrConnection");
            AddLayerMaskProperty();
            AddTriggerProperty();
            AddRaycastOutputs(false);
            AddProperty("origin", "Vector3", false, new List<object> { 0f, 0f, 0f });
            AddProperty("direction", "Vector3", false, new List<object> { 0f, 0f, 1f });
            AddProperty("maxDistance", "float", false, 1000f);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Game.SphereCast")]
    public sealed class GameSphereCastVisualNode : GamePhysicsRaycastVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Game.SphereCast", "Sphere Cast", "Game/Physics", "Casts a 3D sphere and returns hit data.");
            AddValueInput("origin", "Vector3", true, "propertyOrConnection");
            AddValueInput("radius", "float", true, "propertyOrConnection");
            AddValueInput("direction", "Vector3", true, "propertyOrConnection");
            AddValueInput("maxDistance", "float", false, "propertyOrConnection");
            AddLayerMaskProperty();
            AddTriggerProperty();
            AddRaycastOutputs(false);
            AddProperty("origin", "Vector3", false, new List<object> { 0f, 0f, 0f });
            AddProperty("radius", "float", false, 0.5f);
            AddProperty("direction", "Vector3", false, new List<object> { 0f, 0f, 1f });
            AddProperty("maxDistance", "float", false, 1000f);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Game.BoxCast")]
    public sealed class GameBoxCastVisualNode : GamePhysicsRaycastVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Game.BoxCast", "Box Cast", "Game/Physics", "Casts a 3D box and returns hit data.");
            AddValueInput("center", "Vector3", true, "propertyOrConnection");
            AddValueInput("halfExtents", "Vector3", true, "propertyOrConnection");
            AddValueInput("direction", "Vector3", true, "propertyOrConnection");
            AddValueInput("orientationEuler", "Vector3", false, "propertyOrConnection");
            AddValueInput("maxDistance", "float", false, "propertyOrConnection");
            AddLayerMaskProperty();
            AddTriggerProperty();
            AddRaycastOutputs(false);
            AddProperty("center", "Vector3", false, new List<object> { 0f, 0f, 0f });
            AddProperty("halfExtents", "Vector3", false, new List<object> { 0.5f, 0.5f, 0.5f });
            AddProperty("direction", "Vector3", false, new List<object> { 0f, 0f, 1f });
            AddProperty("orientationEuler", "Vector3", false, new List<object> { 0f, 0f, 0f });
            AddProperty("maxDistance", "float", false, 1000f);
        }
    }

    public abstract class GamePhysicsOverlapVisualNode : BlueprintVisualNode
    {
        protected void AddOverlapOutputs()
        {
            AddValueOutput("hasAny", "bool");
            AddValueOutput("count", "int");
            AddValueOutput("firstName", "string");
            AddValueOutput("names", "Array<string>");
        }

        protected void AddLayerMaskProperty()
        {
            AddValueInput("layerMask", "int", false, "propertyOrConnection");
            AddProperty("layerMask", "int", false, -1);
        }

        protected void AddTriggerProperty()
        {
            AddValueInput("includeTriggers", "bool", false, "propertyOrConnection");
            AddProperty("includeTriggers", "bool", false, true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Game.OverlapSphere")]
    public sealed class GameOverlapSphereVisualNode : GamePhysicsOverlapVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Game.OverlapSphere", "Overlap Sphere", "Game/Physics", "Finds 3D colliders inside a sphere.");
            AddValueInput("center", "Vector3", true, "propertyOrConnection");
            AddValueInput("radius", "float", true, "propertyOrConnection");
            AddLayerMaskProperty();
            AddTriggerProperty();
            AddOverlapOutputs();
            AddProperty("center", "Vector3", false, new List<object> { 0f, 0f, 0f });
            AddProperty("radius", "float", false, 0.5f);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Game.OverlapBox")]
    public sealed class GameOverlapBoxVisualNode : GamePhysicsOverlapVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Game.OverlapBox", "Overlap Box", "Game/Physics", "Finds 3D colliders inside an oriented box.");
            AddValueInput("center", "Vector3", true, "propertyOrConnection");
            AddValueInput("halfExtents", "Vector3", true, "propertyOrConnection");
            AddValueInput("orientationEuler", "Vector3", false, "propertyOrConnection");
            AddLayerMaskProperty();
            AddTriggerProperty();
            AddOverlapOutputs();
            AddProperty("center", "Vector3", false, new List<object> { 0f, 0f, 0f });
            AddProperty("halfExtents", "Vector3", false, new List<object> { 0.5f, 0.5f, 0.5f });
            AddProperty("orientationEuler", "Vector3", false, new List<object> { 0f, 0f, 0f });
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Game.Raycast2D")]
    public sealed class GameRaycast2DVisualNode : GamePhysicsRaycastVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Game.Raycast2D", "Raycast 2D", "Game/Physics2D", "Casts a 2D ray and returns hit data.");
            AddValueInput("origin", "Vector2", true, "propertyOrConnection");
            AddValueInput("direction", "Vector2", true, "propertyOrConnection");
            AddValueInput("distance", "float", false, "propertyOrConnection");
            AddLayerMaskProperty();
            AddRaycastOutputs(true);
            AddProperty("origin", "Vector2", false, new List<object> { 0f, 0f });
            AddProperty("direction", "Vector2", false, new List<object> { 1f, 0f });
            AddProperty("distance", "float", false, 1000f);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Game.OverlapCircle2D")]
    public sealed class GameOverlapCircle2DVisualNode : GamePhysicsOverlapVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Game.OverlapCircle2D", "Overlap Circle 2D", "Game/Physics2D", "Finds 2D colliders inside a circle.");
            AddValueInput("point", "Vector2", true, "propertyOrConnection");
            AddValueInput("radius", "float", true, "propertyOrConnection");
            AddLayerMaskProperty();
            AddOverlapOutputs();
            AddProperty("point", "Vector2", false, new List<object> { 0f, 0f });
            AddProperty("radius", "float", false, 0.5f);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Game.OverlapBox2D")]
    public sealed class GameOverlapBox2DVisualNode : GamePhysicsOverlapVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Game.OverlapBox2D", "Overlap Box 2D", "Game/Physics2D", "Finds 2D colliders inside a rotated box.");
            AddValueInput("point", "Vector2", true, "propertyOrConnection");
            AddValueInput("size", "Vector2", true, "propertyOrConnection");
            AddValueInput("angle", "float", false, "propertyOrConnection");
            AddLayerMaskProperty();
            AddOverlapOutputs();
            AddProperty("point", "Vector2", false, new List<object> { 0f, 0f });
            AddProperty("size", "Vector2", false, new List<object> { 1f, 1f });
            AddProperty("angle", "float", false, 0f);
        }
    }
}

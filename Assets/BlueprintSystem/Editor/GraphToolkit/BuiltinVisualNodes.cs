using System;
using System.Collections.Generic;
using Unity.GraphToolkit.Editor;
using UnityEditor;

namespace BlueprintSystem.Editor
{
    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Flow.Branch")]
    public sealed class FlowBranchVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Flow.Branch", "Branch", "Flow", "Routes execution based on a boolean condition.");
            AddExecInput("execIn");
            AddValueInput("condition", "bool", true, "propertyOrConnection");
            AddExecOutput("true");
            AddExecOutput("false");
            AddProperty("condition", "bool", false, false);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Flow.Delay")]
    public sealed class FlowDelayVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Flow.Delay", "Delay", "Flow", "Suspends execution for a number of seconds before continuing.");
            AddExecInput("execIn");
            AddValueInput("seconds", "float", true, "propertyOrConnection");
            AddExecOutput("execOut");
            AddProperty("seconds", "float", false, 0f);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Flow.Sequence")]
    public sealed class FlowSequenceVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Flow.Sequence", "Sequence", "Flow", "Runs up to four exec outputs in order.");
            AddExecInput("execIn");
            AddExecOutput("then0");
            AddExecOutput("then1");
            AddExecOutput("then2");
            AddExecOutput("then3");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Logic.And")]
    public sealed class LogicAndVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Logic.And", "And", "Logic", "Returns true when both boolean inputs are true.");
            AddValueInput("left", "bool", true, "propertyOrConnection");
            AddValueInput("right", "bool", true, "propertyOrConnection");
            AddValueOutput("result", "bool");
            AddProperty("left", "bool", false, false);
            AddProperty("right", "bool", false, false);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Logic.Or")]
    public sealed class LogicOrVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Logic.Or", "Or", "Logic", "Returns true when either boolean input is true.");
            AddValueInput("left", "bool", true, "propertyOrConnection");
            AddValueInput("right", "bool", true, "propertyOrConnection");
            AddValueOutput("result", "bool");
            AddProperty("left", "bool", false, false);
            AddProperty("right", "bool", false, false);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Logic.Not")]
    public sealed class LogicNotVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Logic.Not", "Not", "Logic", "Inverts a boolean input.");
            AddValueInput("value", "bool", true, "propertyOrConnection");
            AddValueOutput("result", "bool");
            AddProperty("value", "bool", false, false);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Game.Event.Custom")]
    public sealed class GameCustomEventVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Game.Event.Custom", "Custom Event", "Events", "Entry point for a named custom event.");
            AddExecOutput("execOut", true);
            AddProperty("eventName", "string", true, null, "Event");
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            ApplyEventMetadata();
            Title = CreateEventTitle(ReadStoredEventName());
            base.OnDefineOptions(context);
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            ApplyEventMetadata();
            string eventName = ReadEventName();
            Title = CreateEventTitle(eventName);
            SetExecOutDisplayName(eventName);
            base.OnDefinePorts(context);
        }

        private void ApplyEventMetadata()
        {
            if (Properties == null)
            {
                return;
            }

            for (int i = 0; i < Properties.Count; i++)
            {
                BlueprintVisualPropertyData property = Properties[i];
                if (property != null && property.Id == "eventName")
                {
                    property.DisplayName = "Event";
                    return;
                }
            }
        }

        private void SetExecOutDisplayName(string eventName)
        {
            if (Outputs == null)
            {
                return;
            }

            string displayName = string.IsNullOrEmpty(eventName) ? "execOut" : eventName;
            for (int i = 0; i < Outputs.Count; i++)
            {
                BlueprintVisualPortData output = Outputs[i];
                if (output != null && output.Id == "execOut")
                {
                    output.DisplayName = displayName;
                    return;
                }
            }
        }

        private string ReadEventName()
        {
            INodeOption option = GetNodeOptionByName("eventName");
            object optionValue;
            if (option != null &&
                BlueprintVisualValueUtility.TryReadOptionValue(option, "string", out optionValue) &&
                optionValue != null &&
                !string.IsNullOrEmpty(optionValue.ToString()))
            {
                return optionValue.ToString();
            }

            return ReadStoredEventName();
        }

        private string ReadStoredEventName()
        {
            if (Properties == null)
            {
                return null;
            }

            for (int i = 0; i < Properties.Count; i++)
            {
                BlueprintVisualPropertyData property = Properties[i];
                if (property == null || property.Id != "eventName" || !property.HasValue)
                {
                    continue;
                }

                object value = BlueprintVisualValueUtility.FromJson(property.JsonValue);
                if (value != null && !string.IsNullOrEmpty(value.ToString()))
                {
                    return value.ToString();
                }
            }

            return null;
        }

        private static string CreateEventTitle(string eventName)
        {
            return string.IsNullOrEmpty(eventName) ? "Custom Event" : "Custom Event: " + eventName;
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Game.Event.OnStart")]
    public sealed class GameOnStartEventVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Game.Event.OnStart", "On Start", "Events", "Entry point fired from BlueprintRunner.Start.");
            AddExecOutput("execOut", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Game.Log")]
    public sealed class GameLogVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Game.Log", "Log", "Game", "Writes a message to the blueprint logger.");
            AddExecInput("execIn");
            AddValueInput("message", "string", true, "propertyOrConnection");
            AddExecOutput("execOut");
            AddProperty("message", "string", false);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Game.SendEvent")]
    public sealed class GameSendEventVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Game.SendEvent", "Send Event", "Game", "Publishes a named event on the current blueprint event bus.");
            AddExecInput("execIn");
            AddValueInput("eventName", "string", true, "propertyOrConnection");
            AddExecOutput("execOut");
            AddProperty("eventName", "string", false);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Game.LoadScene")]
    public sealed class GameLoadSceneVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Game.LoadScene", "Load Scene", "Game", "Loads a Unity scene by name.");
            AddExecInput("execIn");
            AddValueInput("sceneName", "string", true, "propertyOrConnection");
            AddValueInput("mode", "LoadSceneMode", false, "propertyOrConnection");
            AddExecOutput("execOut");
            AddProperty("sceneName", "string", true);
            AddProperty("mode", "LoadSceneMode", false, "Single");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Game.LoadSceneAsync")]
    public sealed class GameLoadSceneAsyncVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Game.LoadSceneAsync", "Load Scene Async", "Game", "Loads a Unity scene asynchronously by name.");
            AddExecInput("execIn");
            AddValueInput("sceneName", "string", true, "propertyOrConnection");
            AddValueInput("mode", "LoadSceneMode", false, "propertyOrConnection");
            AddExecOutput("complete");
            AddProperty("sceneName", "string", true);
            AddProperty("mode", "LoadSceneMode", false, "Single");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Game.IsColliding")]
    public sealed class GameIsCollidingVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Game.IsColliding", "Is Colliding", "Game/Physics", "Returns true when two bound GameObjects have overlapping colliders.");
            AddValueInput("target", "Binding<GameObject>", true, "propertyOrConnection");
            AddValueInput("other", "Binding<GameObject>", true, "propertyOrConnection");
            AddValueOutput("result", "bool");
            AddProperty("target", "Binding<GameObject>", false);
            AddProperty("other", "Binding<GameObject>", false);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Game.SetTransformPosition")]
    public sealed class GameSetTransformPositionVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Game.SetTransformPosition", "Set Transform Position", "Game/Transform", "Sets world position on a bound Transform.");
            AddExecInput("execIn");
            AddValueInput("target", "Binding<Transform>", true, "property");
            AddValueInput("value", "Vector3", true, "propertyOrConnection");
            AddExecOutput("execOut");
            AddProperty("target", "Binding<Transform>", true);
            AddProperty("value", "Vector3", false, new System.Collections.Generic.List<object> { 0f, 0f, 0f });
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Game.SetTransformEulerAngles")]
    public sealed class GameSetTransformEulerAnglesVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Game.SetTransformEulerAngles", "Set Transform Euler Angles", "Game/Transform", "Sets world eulerAngles on a bound Transform.");
            AddExecInput("execIn");
            AddValueInput("target", "Binding<Transform>", true, "property");
            AddValueInput("value", "Vector3", true, "propertyOrConnection");
            AddExecOutput("execOut");
            AddProperty("target", "Binding<Transform>", true);
            AddProperty("value", "Vector3", false, new System.Collections.Generic.List<object> { 0f, 0f, 0f });
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Game.SetTransformLocalScale")]
    public sealed class GameSetTransformLocalScaleVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Game.SetTransformLocalScale", "Set Transform Local Scale", "Game/Transform", "Sets localScale on a bound Transform.");
            AddExecInput("execIn");
            AddValueInput("target", "Binding<Transform>", true, "property");
            AddValueInput("value", "Vector3", true, "propertyOrConnection");
            AddExecOutput("execOut");
            AddProperty("target", "Binding<Transform>", true);
            AddProperty("value", "Vector3", false, new System.Collections.Generic.List<object> { 1f, 1f, 1f });
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Game.SetRigidbodyLinearVelocity")]
    public sealed class GameSetRigidbodyLinearVelocityVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Game.SetRigidbodyLinearVelocity", "Set Rigidbody Linear Velocity", "Game/Physics", "Sets linearVelocity on a bound 3D Rigidbody.");
            AddExecInput("execIn");
            AddValueInput("target", "Binding<Rigidbody>", true, "property");
            AddValueInput("value", "Vector3", true, "propertyOrConnection");
            AddExecOutput("execOut");
            AddProperty("target", "Binding<Rigidbody>", true);
            AddProperty("value", "Vector3", false, new System.Collections.Generic.List<object> { 0f, 0f, 0f });
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Game.SafeTeleportRigidbody")]
    public sealed class GameSafeTeleportRigidbodyVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Game.SafeTeleportRigidbody", "Safe Teleport Rigidbody", "Game/Physics", "Safely teleports a 3D Rigidbody and optionally preserves its velocities.");
            AddExecInput("execIn");
            AddValueInput("target", "Binding<Rigidbody>", true, "propertyOrConnection");
            AddValueInput("position", "Vector3", true, "propertyOrConnection");
            AddValueInput("rotationEulerAngles", "Vector3", false, "propertyOrConnection");
            AddValueInput("setRotation", "bool", false, "propertyOrConnection");
            AddValueInput("preserveLinearVelocity", "bool", false, "propertyOrConnection");
            AddValueInput("preserveAngularVelocity", "bool", false, "propertyOrConnection");
            AddExecOutput("execOut");
            AddProperty("target", "Binding<Rigidbody>", false);
            AddProperty("position", "Vector3", false, new System.Collections.Generic.List<object> { 0f, 0f, 0f });
            AddProperty("rotationEulerAngles", "Vector3", false, new System.Collections.Generic.List<object> { 0f, 0f, 0f });
            AddProperty("setRotation", "bool", false, false);
            AddProperty("preserveLinearVelocity", "bool", false, false);
            AddProperty("preserveAngularVelocity", "bool", false, false);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Game.AddRigidbodyForce")]
    public sealed class GameAddRigidbodyForceVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Game.AddRigidbodyForce", "Add Rigidbody Force", "Game/Physics", "Adds force to a bound 3D Rigidbody.");
            AddExecInput("execIn");
            AddValueInput("target", "Binding<Rigidbody>", true, "property");
            AddValueInput("force", "Vector3", true, "propertyOrConnection");
            AddValueInput("mode", "ForceMode", false, "propertyOrConnection");
            AddExecOutput("execOut");
            AddProperty("target", "Binding<Rigidbody>", true);
            AddProperty("force", "Vector3", false, new System.Collections.Generic.List<object> { 0f, 0f, 0f });
            AddProperty("mode", "ForceMode", false, "Force");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Game.SetColliderEnabled")]
    public sealed class GameSetColliderEnabledVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Game.SetColliderEnabled", "Set Collider Enabled", "Game/Physics", "Sets enabled on a bound 3D Collider.");
            AddExecInput("execIn");
            AddValueInput("target", "Binding<Collider>", true, "property");
            AddValueInput("value", "bool", true, "propertyOrConnection");
            AddExecOutput("execOut");
            AddProperty("target", "Binding<Collider>", true);
            AddProperty("value", "bool", false, true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Game.SetColliderIsTrigger")]
    public sealed class GameSetColliderIsTriggerVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Game.SetColliderIsTrigger", "Set Collider Is Trigger", "Game/Physics", "Sets isTrigger on a bound 3D Collider.");
            AddExecInput("execIn");
            AddValueInput("target", "Binding<Collider>", true, "property");
            AddValueInput("value", "bool", true, "propertyOrConnection");
            AddExecOutput("execOut");
            AddProperty("target", "Binding<Collider>", true);
            AddProperty("value", "bool", false, true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Game.SetRigidbody2DLinearVelocity")]
    public sealed class GameSetRigidbody2DLinearVelocityVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Game.SetRigidbody2DLinearVelocity", "Set Rigidbody2D Linear Velocity", "Game/Physics2D", "Sets linearVelocity on a bound Rigidbody2D.");
            AddExecInput("execIn");
            AddValueInput("target", "Binding<Rigidbody2D>", true, "property");
            AddValueInput("value", "Vector2", true, "propertyOrConnection");
            AddExecOutput("execOut");
            AddProperty("target", "Binding<Rigidbody2D>", true);
            AddProperty("value", "Vector2", false, new System.Collections.Generic.List<object> { 0f, 0f });
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Game.SafeTeleportRigidbody2D")]
    public sealed class GameSafeTeleportRigidbody2DVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Game.SafeTeleportRigidbody2D", "Safe Teleport Rigidbody2D", "Game/Physics2D", "Safely teleports a Rigidbody2D and optionally preserves its velocities.");
            AddExecInput("execIn");
            AddValueInput("target", "Binding<Rigidbody2D>", true, "propertyOrConnection");
            AddValueInput("position", "Vector2", true, "propertyOrConnection");
            AddValueInput("rotationDegrees", "float", false, "propertyOrConnection");
            AddValueInput("setRotation", "bool", false, "propertyOrConnection");
            AddValueInput("preserveLinearVelocity", "bool", false, "propertyOrConnection");
            AddValueInput("preserveAngularVelocity", "bool", false, "propertyOrConnection");
            AddExecOutput("execOut");
            AddProperty("target", "Binding<Rigidbody2D>", false);
            AddProperty("position", "Vector2", false, new System.Collections.Generic.List<object> { 0f, 0f });
            AddProperty("rotationDegrees", "float", false, 0f);
            AddProperty("setRotation", "bool", false, false);
            AddProperty("preserveLinearVelocity", "bool", false, false);
            AddProperty("preserveAngularVelocity", "bool", false, false);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Game.AddRigidbody2DForce")]
    public sealed class GameAddRigidbody2DForceVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Game.AddRigidbody2DForce", "Add Rigidbody2D Force", "Game/Physics2D", "Adds force to a bound Rigidbody2D.");
            AddExecInput("execIn");
            AddValueInput("target", "Binding<Rigidbody2D>", true, "property");
            AddValueInput("force", "Vector2", true, "propertyOrConnection");
            AddValueInput("mode", "ForceMode2D", false, "propertyOrConnection");
            AddExecOutput("execOut");
            AddProperty("target", "Binding<Rigidbody2D>", true);
            AddProperty("force", "Vector2", false, new System.Collections.Generic.List<object> { 0f, 0f });
            AddProperty("mode", "ForceMode2D", false, "Force");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Game.SetCollider2DEnabled")]
    public sealed class GameSetCollider2DEnabledVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Game.SetCollider2DEnabled", "Set Collider2D Enabled", "Game/Physics2D", "Sets enabled on a bound Collider2D.");
            AddExecInput("execIn");
            AddValueInput("target", "Binding<Collider2D>", true, "property");
            AddValueInput("value", "bool", true, "propertyOrConnection");
            AddExecOutput("execOut");
            AddProperty("target", "Binding<Collider2D>", true);
            AddProperty("value", "bool", false, true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Game.SetCollider2DIsTrigger")]
    public sealed class GameSetCollider2DIsTriggerVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Game.SetCollider2DIsTrigger", "Set Collider2D Is Trigger", "Game/Physics2D", "Sets isTrigger on a bound Collider2D.");
            AddExecInput("execIn");
            AddValueInput("target", "Binding<Collider2D>", true, "property");
            AddValueInput("value", "bool", true, "propertyOrConnection");
            AddExecOutput("execOut");
            AddProperty("target", "Binding<Collider2D>", true);
            AddProperty("value", "bool", false, true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Game.SetRendererMaterial")]
    public sealed class GameSetRendererMaterialVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Game.SetRendererMaterial", "Set Renderer Material", "Game/Rendering", "Sets an instance material slot on a bound Renderer.");
            AddExecInput("execIn");
            AddValueInput("target", "Binding<Renderer>", true, "property");
            AddValueInput("value", "Binding<Material>", true, "propertyOrConnection");
            AddValueInput("materialIndex", "int", false, "propertyOrConnection");
            AddExecOutput("execOut");
            AddProperty("target", "Binding<Renderer>", true);
            AddProperty("value", "Binding<Material>", false);
            AddProperty("materialIndex", "int", false, 0);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Game.SetRendererMaterialColor")]
    public sealed class GameSetRendererMaterialColorVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Game.SetRendererMaterialColor", "Set Renderer Material Color", "Game/Rendering", "Sets a color property on a bound Renderer's instance material.");
            AddExecInput("execIn");
            AddValueInput("target", "Binding<Renderer>", true, "property");
            AddValueInput("value", "Color", true, "propertyOrConnection");
            AddValueInput("propertyName", "string", false, "propertyOrConnection");
            AddExecOutput("execOut");
            AddProperty("target", "Binding<Renderer>", true);
            AddProperty("value", "Color", false, new System.Collections.Generic.List<object> { 1f, 1f, 1f, 1f });
            AddProperty("propertyName", "string", false, "_Color");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Game.SetRendererTexture")]
    public sealed class GameSetRendererTextureVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Game.SetRendererTexture", "Set Renderer Texture", "Game/Rendering", "Sets a texture property on a bound Renderer's instance material.");
            AddExecInput("execIn");
            AddValueInput("target", "Binding<Renderer>", true, "property");
            AddValueInput("value", "Binding<Texture>", true, "propertyOrConnection");
            AddValueInput("propertyName", "string", false, "propertyOrConnection");
            AddExecOutput("execOut");
            AddProperty("target", "Binding<Renderer>", true);
            AddProperty("value", "Binding<Texture>", false);
            AddProperty("propertyName", "string", false, "_MainTex");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Game.SetLightEnabled")]
    public sealed class GameSetLightEnabledVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Game.SetLightEnabled", "Set Light Enabled", "Game/Lighting", "Sets enabled on a bound Light.");
            AddExecInput("execIn");
            AddValueInput("target", "Binding<Light>", true, "property");
            AddValueInput("value", "bool", true, "propertyOrConnection");
            AddExecOutput("execOut");
            AddProperty("target", "Binding<Light>", true);
            AddProperty("value", "bool", false, true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Game.SetLightIntensity")]
    public sealed class GameSetLightIntensityVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Game.SetLightIntensity", "Set Light Intensity", "Game/Lighting", "Sets intensity on a bound Light.");
            AddExecInput("execIn");
            AddValueInput("target", "Binding<Light>", true, "property");
            AddValueInput("value", "float", true, "propertyOrConnection");
            AddExecOutput("execOut");
            AddProperty("target", "Binding<Light>", true);
            AddProperty("value", "float", false, 1f);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Game.SetLightColor")]
    public sealed class GameSetLightColorVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Game.SetLightColor", "Set Light Color", "Game/Lighting", "Sets color on a bound Light.");
            AddExecInput("execIn");
            AddValueInput("target", "Binding<Light>", true, "property");
            AddValueInput("value", "Color", true, "propertyOrConnection");
            AddExecOutput("execOut");
            AddProperty("target", "Binding<Light>", true);
            AddProperty("value", "Color", false, new System.Collections.Generic.List<object> { 1f, 1f, 1f, 1f });
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Game.SetLightColorTemperature")]
    public sealed class GameSetLightColorTemperatureVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Game.SetLightColorTemperature", "Set Light Color Temperature", "Game/Lighting", "Enables and sets color temperature in Kelvin on a bound Light.");
            AddExecInput("execIn");
            AddValueInput("target", "Binding<Light>", true, "property");
            AddValueInput("value", "float", true, "propertyOrConnection");
            AddExecOutput("execOut");
            AddProperty("target", "Binding<Light>", true);
            AddProperty("value", "float", false, 6500f);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Game.SetLightRange")]
    public sealed class GameSetLightRangeVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Game.SetLightRange", "Set Light Range", "Game/Lighting", "Sets range on a bound Light.");
            AddExecInput("execIn");
            AddValueInput("target", "Binding<Light>", true, "property");
            AddValueInput("value", "float", true, "propertyOrConnection");
            AddExecOutput("execOut");
            AddProperty("target", "Binding<Light>", true);
            AddProperty("value", "float", false, 10f);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Game.SetLightSpotAngle")]
    public sealed class GameSetLightSpotAngleVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Game.SetLightSpotAngle", "Set Light Spot Angle", "Game/Lighting", "Sets spotAngle on a bound Light.");
            AddExecInput("execIn");
            AddValueInput("target", "Binding<Light>", true, "property");
            AddValueInput("value", "float", true, "propertyOrConnection");
            AddExecOutput("execOut");
            AddProperty("target", "Binding<Light>", true);
            AddProperty("value", "float", false, 30f);
        }
    }

    public abstract class InputAxisVisualNode : BlueprintVisualNode
    {
        protected void ConfigureAxisNode(string typeId, string title, string description)
        {
            SetIdentity(typeId, title, "Input", description);
            AddValueInput("axisName", "string", true, "propertyOrConnection");
            AddValueOutput("value", "float");
            AddProperty("axisName", "string", false, "Horizontal");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Input.GetAxis")]
    public sealed class InputGetAxisVisualNode : InputAxisVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            ConfigureAxisNode("Input.GetAxis", "Get Axis", "Returns Unity legacy Input Manager Input.GetAxis value for an axis name.");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Input.GetAxisRaw")]
    public sealed class InputGetAxisRawVisualNode : InputAxisVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            ConfigureAxisNode("Input.GetAxisRaw", "Get Axis Raw", "Returns Unity legacy Input Manager Input.GetAxisRaw value for an axis name without smoothing.");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Input.GetActionVector2")]
    public sealed class InputGetActionVector2VisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Input.GetActionVector2", "Get Action Vector2", "Input", "Reads a Vector2 value from a project-wide Input System action.");
            AddValueInput("action", "string", true, "property");
            AddValueOutput("value", "Vector2");
            AddProperty("action", "string", true, "Player/Move");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Input.ListenAction")]
    public sealed class InputListenActionVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Input.ListenAction", "Listen Action", "Input", "Polls a project-wide Input System action when executed.");
            AddExecInput("execIn");
            AddValueInput("action", "string", true, "property");
            AddExecOutput("bound");
            AddExecOutput("pressed");
            AddExecOutput("held");
            AddExecOutput("released");
            AddProperty("action", "string", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Input.ListenKey")]
    public sealed class InputListenKeyVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Input.ListenKey", "Listen Key", "Input", "Polls a keyboard key when executed.");
            AddExecInput("execIn");
            AddValueInput("key", "Key", true, "property");
            AddExecOutput("bound");
            AddExecOutput("pressed");
            AddExecOutput("held");
            AddExecOutput("released");
            AddProperty("key", "Key", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("UI.BindButtonClick")]
    public sealed class UIBindButtonClickVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("UI.BindButtonClick", "Bind Button Click", "UI", "Binds a Unity Button click to the clicked execution output.");
            AddExecInput("execIn");
            AddValueInput("target", "Binding<Button>", true, "property");
            AddExecOutput("bound");
            AddExecOutput("clicked");
            AddProperty("target", "Binding<Button>", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("UI.BindText")]
    public sealed class UIBindTextVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("UI.BindText", "Bind Text", "UI", "Binds TMP_Text.text to a blueprint value and refreshes it reactively.");
            AddExecInput("execIn");
            AddValueInput("target", "Binding<TMP_Text>", true, "property");
            AddValueInput("variableName", "string", false, "propertyOrConnection", "Variable");
            AddValueInput("variableTarget", BlueprintVariableTypeRegistry.BlueprintAssetTypeId, false, "propertyOrConnection", "Variable Target");
            AddValueInput("value", "string", false, "propertyOrConnection", "Fallback Value");
            AddExecOutput("bound");
            AddProperty("target", "Binding<TMP_Text>", true);
            AddProperty("variableName", "string", false, null, "Variable");
            AddProperty("variableTarget", BlueprintVariableTypeRegistry.BlueprintAssetTypeId, false, null, null, true);
            AddProperty("value", "string", false, null, "Fallback Value");
        }

        protected override void ApplyDefaultMetadata()
        {
            SetPropertyInspectorOnly("variableTarget", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("UI.BindButtonEvents")]
    public sealed class UIBindButtonEventsVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("UI.BindButtonEvents", "Bind Button Events", "UI", "Binds click, double-click, and long-press events from a Unity Button.");
            AddExecInput("execIn");
            AddValueInput("target", "Binding<Button>", true, "property");
            AddValueInput("longPressSeconds", "float", false, "propertyOrConnection");
            AddValueInput("doubleClickSeconds", "float", false, "propertyOrConnection");
            AddExecOutput("bound");
            AddExecOutput("clicked");
            AddExecOutput("doubleClicked");
            AddExecOutput("longPressed");
            AddProperty("target", "Binding<Button>", true);
            AddProperty("longPressSeconds", "float", false, 0.5f);
            AddProperty("doubleClickSeconds", "float", false, 0.3f);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("UI.BindToggleChanged")]
    public sealed class UIBindToggleChangedVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("UI.BindToggleChanged", "Bind Toggle Changed", "UI", "Binds value-changed events from a Unity Toggle.");
            AddExecInput("execIn");
            AddValueInput("target", "Binding<Toggle>", true, "property");
            AddExecOutput("bound");
            AddExecOutput("changed");
            AddExecOutput("turnedOn");
            AddExecOutput("turnedOff");
            AddValueOutput("value", "bool");
            AddProperty("target", "Binding<Toggle>", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("UI.RefreshLoopScrollView")]
    public sealed class UIRefreshLoopScrollViewVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("UI.RefreshLoopScrollView", "Refresh Loop Scroll View", "UI", "Refreshes a BlueprintLoopScrollView from an array value or variable.");
            AddExecInput("execIn");
            AddValueInput("target", "Binding<BlueprintLoopScrollView>", true, "property");
            AddValueInput("items", null, false, "connection");
            AddValueInput("itemsVariable", "string", false, "propertyOrConnection");
            AddExecOutput("execOut");
            AddProperty("target", "Binding<BlueprintLoopScrollView>", true);
            AddProperty("itemsVariable", "string", false);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("UI.Event.OnClose")]
    public sealed class UIOnCloseEventVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("UI.Event.OnClose", "On Close", "Events", "Entry point fired when a UI panel closes.");
            AddExecOutput("execOut", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("UI.Event.OnOpen")]
    public sealed class UIOnOpenEventVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("UI.Event.OnOpen", "On Open", "Events", "Entry point fired when a UI panel opens.");
            AddExecOutput("execOut", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("UI.SetText")]
    public sealed class UISetTextVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity(
                "UI.SetText",
                "Set Text",
                "UI",
                "Sets text on a bound TMP_Text element.");

            AddExecInput("execIn");
            AddValueInput("target", "Binding<TMP_Text>", true, "property");
            AddValueInput("value", "string", true, "propertyOrConnection");
            AddExecOutput("execOut");
            AddProperty("target", "Binding<TMP_Text>", true);
            AddProperty("value", "string", false);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("UI.SpriteBinding")]
    public sealed class UISpriteBindingVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("UI.SpriteBinding", "Sprite Binding", "UI", "Outputs a bound Sprite name for UI image nodes.");
            AddValueOutput("value", "Binding<Sprite>");
            AddProperty("sprite", "Binding<Sprite>", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("UI.SetImageSprite")]
    public sealed class UISetImageSpriteVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("UI.SetImageSprite", "Set Image Sprite", "UI", "Sets sprite on a bound Unity UI Image.");
            AddExecInput("execIn");
            AddValueInput("target", "Binding<Image>", true, "property");
            AddValueInput("value", "Binding<Sprite>", true, "propertyOrConnection");
            AddExecOutput("execOut");
            AddProperty("target", "Binding<Image>", true);
            AddProperty("value", "Binding<Sprite>", false);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("UI.SetInteractable")]
    public sealed class UISetInteractableVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("UI.SetInteractable", "Set Interactable", "UI", "Sets interactable state on a bound Selectable.");
            AddExecInput("execIn");
            AddValueInput("target", "Binding<Selectable>", true, "property");
            AddValueInput("value", "bool", true, "propertyOrConnection");
            AddExecOutput("execOut");
            AddProperty("target", "Binding<Selectable>", true);
            AddProperty("value", "bool", false, true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("UI.SetVisible")]
    public sealed class UISetVisibleVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("UI.SetVisible", "Set Visible", "UI", "Sets active state on a bound GameObject or Component.");
            AddExecInput("execIn");
            AddValueInput("target", "Binding<GameObject>", true, "property");
            AddValueInput("value", "bool", true, "propertyOrConnection");
            AddExecOutput("execOut");
            AddProperty("target", "Binding<GameObject>", true);
            AddProperty("value", "bool", false, true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("UI.SetGraphicColor")]
    public sealed class UISetGraphicColorVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("UI.SetGraphicColor", "Set Graphic Color", "UI", "Sets color on a bound Unity UI Graphic.");
            AddExecInput("execIn");
            AddValueInput("target", "Binding<Graphic>", true, "property");
            AddValueInput("value", "Color", true, "propertyOrConnection");
            AddExecOutput("execOut");
            AddProperty("target", "Binding<Graphic>", true);
            AddProperty("value", "Color", false, new System.Collections.Generic.List<object> { 1f, 1f, 1f, 1f });
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("UI.SetGraphicEnabled")]
    public sealed class UISetGraphicEnabledVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("UI.SetGraphicEnabled", "Set Graphic Enabled", "UI", "Sets enabled state on a bound Unity UI Graphic.");
            AddExecInput("execIn");
            AddValueInput("target", "Binding<Graphic>", true, "property");
            AddValueInput("value", "bool", true, "propertyOrConnection");
            AddExecOutput("execOut");
            AddProperty("target", "Binding<Graphic>", true);
            AddProperty("value", "bool", false, true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("UI.SetGraphicRaycastTarget")]
    public sealed class UISetGraphicRaycastTargetVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("UI.SetGraphicRaycastTarget", "Set Graphic Raycast Target", "UI", "Sets raycast target state on a bound Unity UI Graphic.");
            AddExecInput("execIn");
            AddValueInput("target", "Binding<Graphic>", true, "property");
            AddValueInput("value", "bool", true, "propertyOrConnection");
            AddExecOutput("execOut");
            AddProperty("target", "Binding<Graphic>", true);
            AddProperty("value", "bool", false, true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("UI.SetImageFillAmount")]
    public sealed class UISetImageFillAmountVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("UI.SetImageFillAmount", "Set Image Fill Amount", "UI", "Sets fillAmount on a bound Unity UI Image.");
            AddExecInput("execIn");
            AddValueInput("target", "Binding<Image>", true, "property");
            AddValueInput("value", "float", true, "propertyOrConnection");
            AddExecOutput("execOut");
            AddProperty("target", "Binding<Image>", true);
            AddProperty("value", "float", false, 1f);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("UI.SetCanvasGroupAlpha")]
    public sealed class UISetCanvasGroupAlphaVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("UI.SetCanvasGroupAlpha", "Set Canvas Group Alpha", "UI", "Sets alpha on a bound CanvasGroup.");
            AddExecInput("execIn");
            AddValueInput("target", "Binding<CanvasGroup>", true, "property");
            AddValueInput("value", "float", true, "propertyOrConnection");
            AddExecOutput("execOut");
            AddProperty("target", "Binding<CanvasGroup>", true);
            AddProperty("value", "float", false, 1f);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("UI.SetCanvasGroupInteractable")]
    public sealed class UISetCanvasGroupInteractableVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("UI.SetCanvasGroupInteractable", "Set Canvas Group Interactable", "UI", "Sets interactable state on a bound CanvasGroup.");
            AddExecInput("execIn");
            AddValueInput("target", "Binding<CanvasGroup>", true, "property");
            AddValueInput("value", "bool", true, "propertyOrConnection");
            AddExecOutput("execOut");
            AddProperty("target", "Binding<CanvasGroup>", true);
            AddProperty("value", "bool", false, true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("UI.SetCanvasGroupBlocksRaycasts")]
    public sealed class UISetCanvasGroupBlocksRaycastsVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("UI.SetCanvasGroupBlocksRaycasts", "Set Canvas Group Blocks Raycasts", "UI", "Sets blocksRaycasts state on a bound CanvasGroup.");
            AddExecInput("execIn");
            AddValueInput("target", "Binding<CanvasGroup>", true, "property");
            AddValueInput("value", "bool", true, "propertyOrConnection");
            AddExecOutput("execOut");
            AddProperty("target", "Binding<CanvasGroup>", true);
            AddProperty("value", "bool", false, true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("UI.SetRectAnchoredPosition")]
    public sealed class UISetRectAnchoredPositionVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("UI.SetRectAnchoredPosition", "Set Rect Anchored Position", "UI", "Sets anchoredPosition on a bound RectTransform.");
            AddExecInput("execIn");
            AddValueInput("target", "Binding<RectTransform>", true, "property");
            AddValueInput("value", "Vector2", true, "propertyOrConnection");
            AddExecOutput("execOut");
            AddProperty("target", "Binding<RectTransform>", true);
            AddProperty("value", "Vector2", false, new System.Collections.Generic.List<object> { 0f, 0f });
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("UI.SetRectSizeDelta")]
    public sealed class UISetRectSizeDeltaVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("UI.SetRectSizeDelta", "Set Rect Size Delta", "UI", "Sets sizeDelta on a bound RectTransform.");
            AddExecInput("execIn");
            AddValueInput("target", "Binding<RectTransform>", true, "property");
            AddValueInput("value", "Vector2", true, "propertyOrConnection");
            AddExecOutput("execOut");
            AddProperty("target", "Binding<RectTransform>", true);
            AddProperty("value", "Vector2", false, new System.Collections.Generic.List<object> { 0f, 0f });
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("UI.SetRectLocalScale")]
    public sealed class UISetRectLocalScaleVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("UI.SetRectLocalScale", "Set Rect Local Scale", "UI", "Sets localScale on a bound RectTransform.");
            AddExecInput("execIn");
            AddValueInput("target", "Binding<RectTransform>", true, "property");
            AddValueInput("value", "Vector3", true, "propertyOrConnection");
            AddExecOutput("execOut");
            AddProperty("target", "Binding<RectTransform>", true);
            AddProperty("value", "Vector3", false, new System.Collections.Generic.List<object> { 1f, 1f, 1f });
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Array.Count")]
    public sealed class ArrayCountVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Array.Count", "Array Count", "Array", "Returns the number of items in an array value.");
            AddValueInput("array", null, true, "connection");
            AddValueOutput("count", "int");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Array.Get")]
    public sealed class ArrayGetVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Array.Get", "Array Get", "Array", "Returns an item from an array by index.");
            AddValueInput("array", null, true, "connection");
            AddValueInput("index", "int", true, "propertyOrConnection");
            AddValueOutput("item", null);
            AddProperty("index", "int", false, 0);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Array.ForEachLoop")]
    public sealed class ArrayForEachLoopVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Array.ForEachLoop", "For Each Loop", "Array", "Executes the loop body once for each item in an array.");
            AddExecInput("execIn");
            AddValueInput("array", null, true, "connection");
            AddExecOutput("loopBody");
            AddExecOutput("completed");
            AddValueOutput("arrayElement", null);
            AddValueOutput("arrayIndex", "int");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Array.ForEachLoopWithBreak")]
    public sealed class ArrayForEachLoopWithBreakVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Array.ForEachLoopWithBreak", "For Each Loop with Break", "Array", "Executes the loop body once for each item in an array and allows early stop.");
            AddExecInput("execIn");
            AddExecInput("break");
            AddValueInput("array", null, true, "connection");
            AddExecOutput("loopBody");
            AddExecOutput("completed");
            AddValueOutput("arrayElement", null);
            AddValueOutput("arrayIndex", "int");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Array.IsValidIndex")]
    public sealed class ArrayIsValidIndexVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Array.IsValidIndex", "Array Is Valid Index", "Array", "Returns true when an index is inside the bounds of an array.");
            AddValueInput("array", null, true, "connection");
            AddValueInput("index", "int", true, "propertyOrConnection");
            AddValueOutput("result", "bool");
            AddProperty("index", "int", false, 0);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Array.Contains")]
    public sealed class ArrayContainsVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Array.Contains", "Array Contains", "Array", "Returns true when an array contains a matching item.");
            AddValueInput("array", null, true, "connection");
            AddValueInput("item", null, true, "propertyOrConnection");
            AddValueOutput("result", "bool");
            AddProperty("item", null, false);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Array.IndexOf")]
    public sealed class ArrayIndexOfVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Array.IndexOf", "Array Index Of", "Array", "Returns the first index of a matching item in an array.");
            AddValueInput("array", null, true, "connection");
            AddValueInput("item", null, true, "propertyOrConnection");
            AddValueOutput("index", "int");
            AddValueOutput("found", "bool");
            AddProperty("item", null, false);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Array.First")]
    public sealed class ArrayFirstVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Array.First", "Array First", "Array", "Returns the first item from an array.");
            AddValueInput("array", null, true, "connection");
            AddValueOutput("item", null);
            AddValueOutput("isValid", "bool");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Array.Last")]
    public sealed class ArrayLastVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Array.Last", "Array Last", "Array", "Returns the last item from an array.");
            AddValueInput("array", null, true, "connection");
            AddValueOutput("item", null);
            AddValueOutput("isValid", "bool");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Variable.BreakStruct")]
    public sealed class VariableBreakStructVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Variable.BreakStruct", "Break Struct", "Variables", "Breaks a Blueprint user struct into field outputs.");
            AddValueInput("target", null, true, "connection");
            AddProperty("structTypeId", "string", true, null, null, false, true);
            AddProperty("structAssetGuid", "string", false, null, null, false, true);
        }

        protected override void ApplyDefaultMetadata()
        {
            BlueprintBreakStructVisualMetadata.Apply(this);
        }

        protected override bool ShouldSuppressEmbeddedInputValue(BlueprintVisualPortData port)
        {
            return port != null && port.Id == BlueprintBreakStructNodeUtility.TargetPortId;
        }
    }

    internal static class BlueprintBreakStructVisualMetadata
    {
        public static bool Apply(BlueprintVisualNode node, BlueprintNodeSource nodeSource = null)
        {
            if (node == null || node.TypeId != BlueprintBreakStructNodeUtility.NodeTypeId)
            {
                return false;
            }

            EnsureLists(node);
            bool changed = false;
            changed |= EnsureHiddenProperty(node.Properties, BlueprintBreakStructNodeUtility.StructTypePropertyId, "string", true);
            changed |= EnsureHiddenProperty(node.Properties, BlueprintBreakStructNodeUtility.StructAssetGuidPropertyId, "string", false);
            changed |= SetInputPortType(node.Inputs, BlueprintBreakStructNodeUtility.TargetPortId, null);

            string structTypeId;
            BlueprintUserStructDefinition definition;
            if (!TryResolveDefinition(nodeSource, node.Properties, out structTypeId, out definition))
            {
                return changed;
            }

            string title = "Break " + structTypeId;
            if (node.Title != title)
            {
                node.Title = title;
                changed = true;
            }

            changed |= SetPropertyValue(node.Properties, BlueprintBreakStructNodeUtility.StructTypePropertyId, "string", true, structTypeId);
            changed |= RebuildOutputs(node, definition);
            return changed;
        }

        private static bool TryResolveDefinition(
            BlueprintNodeSource nodeSource,
            List<BlueprintVisualPropertyData> properties,
            out string structTypeId,
            out BlueprintUserStructDefinition definition)
        {
            structTypeId = null;
            definition = null;

            string assetGuid = GetStringProperty(nodeSource, BlueprintBreakStructNodeUtility.StructAssetGuidPropertyId);
            if (string.IsNullOrEmpty(assetGuid))
            {
                assetGuid = GetStringProperty(properties, BlueprintBreakStructNodeUtility.StructAssetGuidPropertyId);
            }

            if (!string.IsNullOrEmpty(assetGuid))
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
                BlueprintUserStructAsset asset = string.IsNullOrEmpty(assetPath)
                    ? null
                    : AssetDatabase.LoadAssetAtPath<BlueprintUserStructAsset>(assetPath);
                if (asset != null)
                {
                    structTypeId = asset.TypeId;
                    definition = asset.ToDefinition();
                    return !string.IsNullOrEmpty(structTypeId) && definition != null;
                }
            }

            structTypeId = GetStringProperty(nodeSource, BlueprintBreakStructNodeUtility.StructTypePropertyId);
            if (string.IsNullOrEmpty(structTypeId))
            {
                structTypeId = GetStringProperty(properties, BlueprintBreakStructNodeUtility.StructTypePropertyId);
            }

            return !string.IsNullOrEmpty(structTypeId) &&
                BlueprintUserStructRegistry.TryGet(structTypeId, out definition);
        }

        private static bool RebuildOutputs(BlueprintVisualNode node, BlueprintUserStructDefinition definition)
        {
            List<BlueprintVisualPortData> outputs = node.Outputs;
            List<BlueprintVisualPortData> rebuilt = new List<BlueprintVisualPortData>();
            if (definition == null)
            {
                return ReplaceOutputsIfChanged(outputs, rebuilt);
            }

            for (int i = 0; i < definition.Fields.Count; i++)
            {
                BlueprintUserStructField field = definition.Fields[i];
                if (field == null || field.Deprecated || string.IsNullOrEmpty(field.Id))
                {
                    continue;
                }

                rebuilt.Add(new BlueprintVisualPortData
                {
                    Id = field.Id,
                    DisplayName = string.IsNullOrEmpty(field.Name) ? field.Id : field.Name,
                    Kind = "value",
                    Type = field.Type,
                    Required = false,
                    Source = null,
                    AllowMultiple = false
                });
            }

            return ReplaceOutputsIfChanged(outputs, rebuilt);
        }

        private static bool ReplaceOutputsIfChanged(List<BlueprintVisualPortData> outputs, List<BlueprintVisualPortData> rebuilt)
        {
            if (PortListsEqual(outputs, rebuilt))
            {
                return false;
            }

            outputs.Clear();
            outputs.AddRange(rebuilt);
            return true;
        }

        private static bool PortListsEqual(List<BlueprintVisualPortData> left, List<BlueprintVisualPortData> right)
        {
            if (left == null || right == null || left.Count != right.Count)
            {
                return false;
            }

            for (int i = 0; i < left.Count; i++)
            {
                if (!PortsEqual(left[i], right[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool PortsEqual(BlueprintVisualPortData left, BlueprintVisualPortData right)
        {
            if (left == null || right == null)
            {
                return left == right;
            }

            return left.Id == right.Id &&
                left.DisplayName == right.DisplayName &&
                left.Kind == right.Kind &&
                left.Type == right.Type &&
                left.Required == right.Required &&
                left.Source == right.Source &&
                left.AllowMultiple == right.AllowMultiple;
        }

        private static void EnsureLists(BlueprintVisualNode node)
        {
            if (node.Inputs == null)
            {
                node.Inputs = new List<BlueprintVisualPortData>();
            }

            if (node.Outputs == null)
            {
                node.Outputs = new List<BlueprintVisualPortData>();
            }

            if (node.Properties == null)
            {
                node.Properties = new List<BlueprintVisualPropertyData>();
            }
        }

        private static bool SetInputPortType(List<BlueprintVisualPortData> ports, string portId, string type)
        {
            for (int i = 0; i < ports.Count; i++)
            {
                BlueprintVisualPortData port = ports[i];
                if (port != null && port.Id == portId)
                {
                    if (port.Type == type)
                    {
                        return false;
                    }

                    port.Type = type;
                    return true;
                }
            }

            ports.Add(new BlueprintVisualPortData
            {
                Id = portId,
                DisplayName = null,
                Kind = "value",
                Type = type,
                Required = true,
                Source = "connection",
                AllowMultiple = false
            });
            return true;
        }

        private static bool EnsureHiddenProperty(List<BlueprintVisualPropertyData> properties, string propertyId, string type, bool required)
        {
            BlueprintVisualPropertyData property = FindProperty(properties, propertyId);
            if (property == null)
            {
                properties.Add(new BlueprintVisualPropertyData
                {
                    Id = propertyId,
                    Type = type,
                    Required = required,
                    HasValue = false,
                    JsonValue = string.Empty,
                    Hidden = true
                });
                return true;
            }

            bool changed = property.Type != type || property.Required != required || !property.Hidden;
            property.Type = type;
            property.Required = required;
            property.Hidden = true;
            return changed;
        }

        private static bool SetPropertyValue(List<BlueprintVisualPropertyData> properties, string propertyId, string type, bool required, object value)
        {
            BlueprintVisualPropertyData property = FindProperty(properties, propertyId);
            bool changed = false;
            if (property == null)
            {
                property = new BlueprintVisualPropertyData { Id = propertyId };
                properties.Add(property);
                changed = true;
            }

            string jsonValue = BlueprintVisualValueUtility.ToJson(value);
            changed |= property.Type != type ||
                property.Required != required ||
                !property.Hidden ||
                !property.HasValue ||
                property.JsonValue != jsonValue;

            property.Type = type;
            property.Required = required;
            property.Hidden = true;
            property.HasValue = true;
            property.JsonValue = jsonValue;
            return changed;
        }

        private static BlueprintVisualPropertyData FindProperty(List<BlueprintVisualPropertyData> properties, string propertyId)
        {
            if (properties == null)
            {
                return null;
            }

            for (int i = 0; i < properties.Count; i++)
            {
                BlueprintVisualPropertyData property = properties[i];
                if (property != null && property.Id == propertyId)
                {
                    return property;
                }
            }

            return null;
        }

        private static string GetStringProperty(BlueprintNodeSource nodeSource, string propertyId)
        {
            object value;
            if (nodeSource != null && nodeSource.Properties.TryGetValue(propertyId, out value) && value != null)
            {
                return value.ToString();
            }

            return null;
        }

        private static string GetStringProperty(List<BlueprintVisualPropertyData> properties, string propertyId)
        {
            BlueprintVisualPropertyData property = FindProperty(properties, propertyId);
            if (property == null || !property.HasValue)
            {
                return null;
            }

            object value = BlueprintVisualValueUtility.FromJson(property.JsonValue);
            return value == null ? null : value.ToString();
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("DataTable.GetRow")]
    public sealed class DataTableGetRowVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("DataTable.GetRow", "Data Table Get Row", "DataTable", "Returns a row from a Blueprint data table by row name.");
            AddValueInput("dataTable", BlueprintGraphToolkitDataTableTypes.TypeId, false, "propertyOrConnection", "Data Table");
            AddValueInput("rowName", "string", true, "propertyOrConnection");
            AddValueOutput("row", null);
            AddValueOutput("found", "bool");
            AddProperty("dataTable", BlueprintGraphToolkitDataTableTypes.TypeId, false, null, "Data Table");
            AddProperty("rowName", "string", false, string.Empty);
            AddProperty("tablePath", "string", false, null, null, false, true);
            AddProperty("tableAssetGuid", "string", false, null, null, false, true);
            AddProperty("rowStructTypeId", "string", true, null, null, false, true);
        }

        protected override void ApplyDefaultMetadata()
        {
            BlueprintDataTableVisualMetadata.Apply(this);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("DataTable.GetRowNames")]
    public sealed class DataTableGetRowNamesVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("DataTable.GetRowNames", "Data Table Get Row Names", "DataTable", "Returns all row names from a Blueprint data table.");
            AddValueInput("dataTable", BlueprintGraphToolkitDataTableTypes.TypeId, false, "propertyOrConnection", "Data Table");
            AddValueOutput("rowNames", "Array<string>");
            AddProperty("dataTable", BlueprintGraphToolkitDataTableTypes.TypeId, false, null, "Data Table");
            AddProperty("tablePath", "string", false, null, null, false, true);
            AddProperty("tableAssetGuid", "string", false, null, null, false, true);
            AddProperty("rowStructTypeId", "string", true, null, null, false, true);
        }

        protected override void ApplyDefaultMetadata()
        {
            BlueprintDataTableVisualMetadata.Apply(this);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("DataTable.GetAllRows")]
    public sealed class DataTableGetAllRowsVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("DataTable.GetAllRows", "Data Table Get All Rows", "DataTable", "Returns all rows from a Blueprint data table.");
            AddValueInput("dataTable", BlueprintGraphToolkitDataTableTypes.TypeId, false, "propertyOrConnection", "Data Table");
            AddValueOutput("rows", null);
            AddProperty("dataTable", BlueprintGraphToolkitDataTableTypes.TypeId, false, null, "Data Table");
            AddProperty("tablePath", "string", false, null, null, false, true);
            AddProperty("tableAssetGuid", "string", false, null, null, false, true);
            AddProperty("rowStructTypeId", "string", true, null, null, false, true);
        }

        protected override void ApplyDefaultMetadata()
        {
            BlueprintDataTableVisualMetadata.Apply(this);
        }
    }

    internal static class BlueprintDataTableVisualMetadata
    {
        public static bool Apply(BlueprintVisualNode node, BlueprintNodeSource nodeSource = null)
        {
            if (node == null || !BlueprintDataTableNodeUtility.IsDataTableNode(node.TypeId))
            {
                return false;
            }

            EnsureLists(node);
            bool changed = false;
            changed |= EnsureDataTableInputAndProperty(node, null);
            changed |= EnsureHiddenProperty(node.Properties, BlueprintDataTableNodeUtility.TablePathPropertyId, "string", false);
            changed |= EnsureHiddenProperty(node.Properties, BlueprintDataTableNodeUtility.TableAssetGuidPropertyId, "string", false);
            changed |= EnsureHiddenProperty(node.Properties, BlueprintDataTableNodeUtility.RowStructTypePropertyId, "string", true);

            string tablePath;
            BlueprintDataTableDefinition definition;
            if (!TryResolveDefinition(nodeSource, node.Properties, out tablePath, out definition))
            {
                string fallbackStructTypeId = GetStringProperty(nodeSource, BlueprintDataTableNodeUtility.RowStructTypePropertyId);
                if (string.IsNullOrEmpty(fallbackStructTypeId))
                {
                    fallbackStructTypeId = GetStringProperty(node.Properties, BlueprintDataTableNodeUtility.RowStructTypePropertyId);
                }

                changed |= RebuildOutputs(node, fallbackStructTypeId);
                changed |= EnsureDataTableInputAndProperty(node, fallbackStructTypeId);
                return changed;
            }

            string title = GetNodeTitle(node.TypeId, definition);
            if (node.Title != title)
            {
                node.Title = title;
                changed = true;
            }

            bool hasDataTableValue =
                !string.IsNullOrEmpty(GetStringProperty(nodeSource, BlueprintDataTableNodeUtility.DataTableInputId)) ||
                !string.IsNullOrEmpty(GetStringProperty(node.Properties, BlueprintDataTableNodeUtility.DataTableInputId));
            changed |= SetPropertyValue(node.Properties, BlueprintDataTableNodeUtility.TablePathPropertyId, "string", false, tablePath);
            changed |= SetPropertyValue(node.Properties, BlueprintDataTableNodeUtility.RowStructTypePropertyId, "string", true, definition.RowStructTypeId);
            changed |= EnsureDataTableInputAndProperty(node, definition.RowStructTypeId);
            if (hasDataTableValue)
            {
                changed |= SetDataTablePropertyValue(
                    node.Properties,
                    BlueprintDataTableVariableTypeUtility.MakeType(definition.RowStructTypeId),
                    tablePath);
            }

            changed |= RebuildOutputs(node, definition.RowStructTypeId);
            return changed;
        }

        private static string GetNodeTitle(string typeId, BlueprintDataTableDefinition definition)
        {
            string tableLabel = definition == null || string.IsNullOrEmpty(definition.TableId) ? "Data Table" : definition.TableId;
            if (typeId == BlueprintDataTableNodeUtility.GetRowNodeTypeId)
            {
                return "Get Row " + tableLabel;
            }

            if (typeId == BlueprintDataTableNodeUtility.GetRowNamesNodeTypeId)
            {
                return "Get Row Names " + tableLabel;
            }

            return "Get All Rows " + tableLabel;
        }

        private static bool TryResolveDefinition(
            BlueprintNodeSource nodeSource,
            List<BlueprintVisualPropertyData> properties,
            out string tablePath,
            out BlueprintDataTableDefinition definition)
        {
            tablePath = null;
            definition = null;

            tablePath = GetStringProperty(nodeSource, BlueprintDataTableNodeUtility.DataTableInputId);
            if (string.IsNullOrEmpty(tablePath))
            {
                tablePath = GetStringProperty(properties, BlueprintDataTableNodeUtility.DataTableInputId);
            }

            if (!string.IsNullOrEmpty(tablePath) &&
                BlueprintDataTableRegistry.TryGetByPath(tablePath, out definition))
            {
                return true;
            }

            string assetGuid = GetStringProperty(nodeSource, BlueprintDataTableNodeUtility.TableAssetGuidPropertyId);
            if (string.IsNullOrEmpty(assetGuid))
            {
                assetGuid = GetStringProperty(properties, BlueprintDataTableNodeUtility.TableAssetGuidPropertyId);
            }

            if (!string.IsNullOrEmpty(assetGuid))
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
                BlueprintDataTableAsset asset = string.IsNullOrEmpty(assetPath)
                    ? null
                    : AssetDatabase.LoadAssetAtPath<BlueprintDataTableAsset>(assetPath);
                if (asset != null)
                {
                    tablePath = BlueprintDataTableRegistry.GetJsonPathForAssetPath(assetPath);
                    definition = asset.ToDefinition();
                    definition.SourcePath = tablePath;
                    return true;
                }
            }

            tablePath = GetStringProperty(nodeSource, BlueprintDataTableNodeUtility.TablePathPropertyId);
            if (string.IsNullOrEmpty(tablePath))
            {
                tablePath = GetStringProperty(properties, BlueprintDataTableNodeUtility.TablePathPropertyId);
            }

            return !string.IsNullOrEmpty(tablePath) &&
                BlueprintDataTableRegistry.TryGetByPath(tablePath, out definition);
        }

        private static bool EnsureDataTableInputAndProperty(BlueprintVisualNode node, string rowStructTypeId)
        {
            string dataTableType = string.IsNullOrEmpty(rowStructTypeId)
                ? BlueprintGraphToolkitDataTableTypes.TypeId
                : BlueprintDataTableVariableTypeUtility.MakeType(rowStructTypeId);
            bool changed = false;

            BlueprintVisualPortData input = null;
            for (int i = 0; i < node.Inputs.Count; i++)
            {
                if (node.Inputs[i] != null && node.Inputs[i].Id == BlueprintDataTableNodeUtility.DataTableInputId)
                {
                    input = node.Inputs[i];
                    break;
                }
            }

            if (input == null)
            {
                input = new BlueprintVisualPortData
                {
                    Id = BlueprintDataTableNodeUtility.DataTableInputId,
                    DisplayName = "Data Table",
                    Kind = "value",
                    Required = false,
                    Source = "propertyOrConnection"
                };
                node.Inputs.Insert(0, input);
                changed = true;
            }

            if (input.Type != dataTableType)
            {
                input.Type = dataTableType;
                changed = true;
            }

            BlueprintVisualPropertyData property = FindProperty(
                node.Properties,
                BlueprintDataTableNodeUtility.DataTableInputId);
            if (property == null)
            {
                property = new BlueprintVisualPropertyData
                {
                    Id = BlueprintDataTableNodeUtility.DataTableInputId,
                    DisplayName = "Data Table",
                    Required = false,
                    HasValue = false,
                    JsonValue = string.Empty,
                    Hidden = false
                };
                node.Properties.Insert(0, property);
                changed = true;
            }

            if (property.Type != dataTableType ||
                property.Required ||
                property.Hidden ||
                property.DisplayName != "Data Table")
            {
                property.Type = dataTableType;
                property.Required = false;
                property.Hidden = false;
                property.DisplayName = "Data Table";
                changed = true;
            }

            return changed;
        }

        private static bool SetDataTablePropertyValue(
            List<BlueprintVisualPropertyData> properties,
            string dataTableType,
            string tablePath)
        {
            BlueprintVisualPropertyData property = FindProperty(
                properties,
                BlueprintDataTableNodeUtility.DataTableInputId);
            if (property == null)
            {
                return false;
            }

            string jsonValue = BlueprintVisualValueUtility.ToJson(tablePath);
            bool changed = property.Type != dataTableType ||
                property.Required ||
                property.Hidden ||
                !property.HasValue ||
                property.JsonValue != jsonValue ||
                property.DisplayName != "Data Table";
            property.Type = dataTableType;
            property.Required = false;
            property.Hidden = false;
            property.HasValue = true;
            property.JsonValue = jsonValue;
            property.DisplayName = "Data Table";
            return changed;
        }

        private static bool RebuildOutputs(BlueprintVisualNode node, string rowStructTypeId)
        {
            List<BlueprintVisualPortData> rebuilt = new List<BlueprintVisualPortData>();
            if (node.TypeId == BlueprintDataTableNodeUtility.GetRowNodeTypeId)
            {
                rebuilt.Add(new BlueprintVisualPortData
                {
                    Id = "row",
                    Kind = "value",
                    Type = rowStructTypeId,
                    Required = false,
                    Source = null,
                    AllowMultiple = false
                });
                rebuilt.Add(new BlueprintVisualPortData
                {
                    Id = "found",
                    Kind = "value",
                    Type = "bool",
                    Required = false,
                    Source = null,
                    AllowMultiple = false
                });
            }
            else if (node.TypeId == BlueprintDataTableNodeUtility.GetRowNamesNodeTypeId)
            {
                rebuilt.Add(new BlueprintVisualPortData
                {
                    Id = "rowNames",
                    Kind = "value",
                    Type = "Array<string>",
                    Required = false,
                    Source = null,
                    AllowMultiple = false
                });
            }
            else if (node.TypeId == BlueprintDataTableNodeUtility.GetAllRowsNodeTypeId)
            {
                rebuilt.Add(new BlueprintVisualPortData
                {
                    Id = "rows",
                    Kind = "value",
                    Type = string.IsNullOrEmpty(rowStructTypeId) ? null : "Array<" + rowStructTypeId + ">",
                    Required = false,
                    Source = null,
                    AllowMultiple = false
                });
            }

            return ReplaceOutputsIfChanged(node.Outputs, rebuilt);
        }

        private static bool ReplaceOutputsIfChanged(List<BlueprintVisualPortData> outputs, List<BlueprintVisualPortData> rebuilt)
        {
            if (PortListsEqual(outputs, rebuilt))
            {
                return false;
            }

            outputs.Clear();
            outputs.AddRange(rebuilt);
            return true;
        }

        private static bool PortListsEqual(List<BlueprintVisualPortData> left, List<BlueprintVisualPortData> right)
        {
            if (left == null || right == null || left.Count != right.Count)
            {
                return false;
            }

            for (int i = 0; i < left.Count; i++)
            {
                if (!PortsEqual(left[i], right[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool PortsEqual(BlueprintVisualPortData left, BlueprintVisualPortData right)
        {
            if (left == null || right == null)
            {
                return left == right;
            }

            return left.Id == right.Id &&
                left.DisplayName == right.DisplayName &&
                left.Kind == right.Kind &&
                left.Type == right.Type &&
                left.Required == right.Required &&
                left.Source == right.Source &&
                left.AllowMultiple == right.AllowMultiple;
        }

        private static void EnsureLists(BlueprintVisualNode node)
        {
            if (node.Inputs == null)
            {
                node.Inputs = new List<BlueprintVisualPortData>();
            }

            if (node.Outputs == null)
            {
                node.Outputs = new List<BlueprintVisualPortData>();
            }

            if (node.Properties == null)
            {
                node.Properties = new List<BlueprintVisualPropertyData>();
            }
        }

        private static bool EnsureHiddenProperty(List<BlueprintVisualPropertyData> properties, string propertyId, string type, bool required)
        {
            BlueprintVisualPropertyData property = FindProperty(properties, propertyId);
            if (property == null)
            {
                properties.Add(new BlueprintVisualPropertyData
                {
                    Id = propertyId,
                    Type = type,
                    Required = required,
                    HasValue = false,
                    JsonValue = string.Empty,
                    Hidden = true
                });
                return true;
            }

            bool changed = property.Type != type || property.Required != required || !property.Hidden;
            property.Type = type;
            property.Required = required;
            property.Hidden = true;
            return changed;
        }

        private static bool SetPropertyValue(List<BlueprintVisualPropertyData> properties, string propertyId, string type, bool required, object value)
        {
            BlueprintVisualPropertyData property = FindProperty(properties, propertyId);
            bool changed = false;
            if (property == null)
            {
                property = new BlueprintVisualPropertyData { Id = propertyId };
                properties.Add(property);
                changed = true;
            }

            string jsonValue = BlueprintVisualValueUtility.ToJson(value);
            changed |= property.Type != type ||
                property.Required != required ||
                !property.Hidden ||
                !property.HasValue ||
                property.JsonValue != jsonValue;

            property.Type = type;
            property.Required = required;
            property.Hidden = true;
            property.HasValue = true;
            property.JsonValue = jsonValue;
            return changed;
        }

        private static BlueprintVisualPropertyData FindProperty(List<BlueprintVisualPropertyData> properties, string propertyId)
        {
            if (properties == null)
            {
                return null;
            }

            for (int i = 0; i < properties.Count; i++)
            {
                BlueprintVisualPropertyData property = properties[i];
                if (property != null && property.Id == propertyId)
                {
                    return property;
                }
            }

            return null;
        }

        private static string GetStringProperty(BlueprintNodeSource nodeSource, string propertyId)
        {
            object value;
            if (nodeSource != null && nodeSource.Properties.TryGetValue(propertyId, out value) && value != null)
            {
                return value.ToString();
            }

            return null;
        }

        private static string GetStringProperty(List<BlueprintVisualPropertyData> properties, string propertyId)
        {
            BlueprintVisualPropertyData property = FindProperty(properties, propertyId);
            if (property == null || !property.HasValue)
            {
                return null;
            }

            object value = BlueprintVisualValueUtility.FromJson(property.JsonValue);
            return value == null ? null : value.ToString();
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Variable.GetField")]
    public sealed class VariableGetFieldVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Variable.GetField", "Get Field", "Variables", "Reads a field or nested field path from a structured value.");
            AddValueInput("target", null, true, "connection");
            AddValueInput("path", "string", true, "propertyOrConnection");
            AddValueOutput("value", null);
            AddProperty("path", "string", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Variable.SetField")]
    public sealed class VariableSetFieldVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Variable.SetField", "Set Field", "Variables", "Returns a copy of a structured value with one field or nested field path changed.");
            AddValueInput("target", null, true, "connection");
            AddValueInput("path", "string", true, "propertyOrConnection");
            AddValueInput("value", null, true, "propertyOrConnection");
            AddValueOutput("result", null);
            AddProperty("path", "string", true);
            AddProperty("value", null, false);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Variable.Compare")]
    public sealed class VariableCompareVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Variable.Compare", "Compare", "Variables", "Compares two values and returns a boolean.");
            AddValueInput("left", null, true, "propertyOrConnection");
            AddValueInput("right", null, true, "propertyOrConnection");
            AddValueInput("comparison", "ComparisonMode", false, "propertyOrConnection");
            AddValueOutput("result", "bool");
            AddProperty("left", null, false);
            AddProperty("right", null, false);
            AddProperty("comparison", "ComparisonMode", false, "Equals");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Variable.Get")]
    public sealed class VariableGetVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Variable.Get", "Get Variable", "Variables", "Reads a blueprint variable by name.");
            AddValueOutput("value", null);
            AddProperty("name", "string", true);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("Variable.Set")]
    public sealed class VariableSetVisualNode : BlueprintVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("Variable.Set", "Set Variable", "Variables", "Writes a blueprint variable by name.");
            AddExecInput("execIn");
            AddValueInput("value", null, true, "propertyOrConnection", "New Value");
            AddExecOutput("execOut");
            AddProperty("name", "string", true, null, "Variable", true);
            AddProperty("value", null, false);
        }
    }
}

using System;
using System.Collections.Generic;
using Unity.GraphToolkit.Editor;

namespace BlueprintSystem.Editor
{
    public abstract class SmartObjectVisualNode : BlueprintVisualNode
    {
        protected static List<object> Vector3Default(float x, float y, float z)
        {
            return new List<object> { x, y, z };
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("SmartObject.FindBest")]
    public sealed class SmartObjectFindBestVisualNode : SmartObjectVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("SmartObject.FindBest", "SmartObject Find Best", "SmartObject", "Finds the highest-scoring available SmartObject slot.");
            AddValueInput("requesterId", "string", true, "propertyOrConnection");
            AddValueInput("activity", "string", true, "propertyOrConnection");
            AddValueInput("center", "Vector3", true, "propertyOrConnection");
            AddValueInput("radius", "float", true, "propertyOrConnection");
            AddValueInput("requiredTags", "string", false, "propertyOrConnection");
            AddValueInput("forbiddenTags", "string", false, "propertyOrConnection");
            AddValueInput("accessGroup", "string", false, "propertyOrConnection");
            AddValueInput("needScore", "float", false, "propertyOrConnection");
            AddValueInput("maxDistancePenalty", "float", false, "propertyOrConnection");
            AddValueOutput("found", "bool");
            AddValueOutput("objectId", "string");
            AddValueOutput("slotId", "int");
            AddValueOutput("targetPosition", "Vector3");
            AddValueOutput("facingPosition", "Vector3");
            AddValueOutput("useDuration", "float");
            AddValueOutput("score", "float");
            AddValueOutput("failReason", "string");
            AddProperty("requesterId", "string", false, "");
            AddProperty("activity", "string", false, "");
            AddProperty("center", "Vector3", false, Vector3Default(0f, 0f, 0f));
            AddProperty("radius", "float", false, 10f);
            AddProperty("requiredTags", "string", false, "");
            AddProperty("forbiddenTags", "string", false, "");
            AddProperty("accessGroup", "string", false, "");
            AddProperty("needScore", "float", false, 0f);
            AddProperty("maxDistancePenalty", "float", false, 0f);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("SmartObject.FindBestActor")]
    public sealed class SmartObjectFindBestActorVisualNode : SmartObjectVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("SmartObject.FindBestActor", "SmartObject Find Best Actor", "SmartObject", "Finds the highest-scoring available SmartObject slot while optionally excluding a bound GameObject.");
            AddValueInput("requesterId", "string", true, "propertyOrConnection");
            AddValueInput("activity", "string", true, "propertyOrConnection");
            AddValueInput("center", "Vector3", true, "propertyOrConnection");
            AddValueInput("radius", "float", true, "propertyOrConnection");
            AddValueInput("requiredTags", "string", false, "propertyOrConnection");
            AddValueInput("forbiddenTags", "string", false, "propertyOrConnection");
            AddValueInput("accessGroup", "string", false, "propertyOrConnection");
            AddValueInput("needScore", "float", false, "propertyOrConnection");
            AddValueInput("maxDistancePenalty", "float", false, "propertyOrConnection");
            AddValueInput("excludeGameObject", "Binding<GameObject>", false, "propertyOrConnection");
            AddValueOutput("found", "bool");
            AddValueOutput("objectId", "string");
            AddValueOutput("slotId", "int");
            AddValueOutput("targetPosition", "Vector3");
            AddValueOutput("facingPosition", "Vector3");
            AddValueOutput("targetGameObject", "GameObject");
            AddValueOutput("useDuration", "float");
            AddValueOutput("score", "float");
            AddValueOutput("failReason", "string");
            AddProperty("requesterId", "string", false, "");
            AddProperty("activity", "string", false, "");
            AddProperty("center", "Vector3", false, Vector3Default(0f, 0f, 0f));
            AddProperty("radius", "float", false, 10f);
            AddProperty("requiredTags", "string", false, "");
            AddProperty("forbiddenTags", "string", false, "");
            AddProperty("accessGroup", "string", false, "");
            AddProperty("needScore", "float", false, 0f);
            AddProperty("maxDistancePenalty", "float", false, 0f);
            AddProperty("excludeGameObject", "Binding<GameObject>", false);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("SmartObject.Reserve")]
    public sealed class SmartObjectReserveVisualNode : SmartObjectVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("SmartObject.Reserve", "SmartObject Reserve", "SmartObject", "Reserves a specific SmartObject slot.");
            AddExecInput("execIn");
            AddValueInput("requesterId", "string", true, "propertyOrConnection");
            AddValueInput("objectId", "string", true, "propertyOrConnection");
            AddValueInput("slotId", "int", true, "propertyOrConnection");
            AddValueInput("activity", "string", true, "propertyOrConnection");
            AddValueInput("holdSeconds", "float", false, "propertyOrConnection");
            AddValueInput("accessGroup", "string", false, "propertyOrConnection");
            AddExecOutput("execOut");
            AddValueOutput("success", "bool");
            AddValueOutput("reservationToken", "string");
            AddValueOutput("targetPosition", "Vector3");
            AddValueOutput("facingPosition", "Vector3");
            AddValueOutput("useDuration", "float");
            AddValueOutput("failReason", "string");
            AddProperty("requesterId", "string", false, "");
            AddProperty("objectId", "string", false, "");
            AddProperty("slotId", "int", false, -1);
            AddProperty("activity", "string", false, "");
            AddProperty("holdSeconds", "float", false, 30f);
            AddProperty("accessGroup", "string", false, "");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("SmartObject.BeginUse")]
    public sealed class SmartObjectBeginUseVisualNode : SmartObjectVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("SmartObject.BeginUse", "SmartObject Begin Use", "SmartObject", "Converts a reservation token into occupied slot state.");
            AddExecInput("execIn");
            AddValueInput("requesterId", "string", true, "propertyOrConnection");
            AddValueInput("reservationToken", "string", true, "propertyOrConnection");
            AddExecOutput("execOut");
            AddValueOutput("success", "bool");
            AddValueOutput("objectId", "string");
            AddValueOutput("slotId", "int");
            AddValueOutput("useDuration", "float");
            AddValueOutput("failReason", "string");
            AddProperty("requesterId", "string", false, "");
            AddProperty("reservationToken", "string", false, "");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("SmartObject.Release")]
    public sealed class SmartObjectReleaseVisualNode : SmartObjectVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("SmartObject.Release", "SmartObject Release", "SmartObject", "Releases a reservation or occupied SmartObject slot.");
            AddExecInput("execIn");
            AddValueInput("requesterId", "string", true, "propertyOrConnection");
            AddValueInput("reservationToken", "string", true, "propertyOrConnection");
            AddValueInput("reason", "string", false, "propertyOrConnection");
            AddExecOutput("execOut");
            AddValueOutput("success", "bool");
            AddValueOutput("objectId", "string");
            AddValueOutput("slotId", "int");
            AddValueOutput("previousState", "string");
            AddValueOutput("failReason", "string");
            AddProperty("requesterId", "string", false, "");
            AddProperty("reservationToken", "string", false, "");
            AddProperty("reason", "string", false, SmartObjectReleaseReason.Completed);
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("SmartObject.GetReservationInfo")]
    public sealed class SmartObjectGetReservationInfoVisualNode : SmartObjectVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("SmartObject.GetReservationInfo", "SmartObject Get Reservation Info", "SmartObject", "Reads current information for a reservation token.");
            AddValueInput("reservationToken", "string", true, "propertyOrConnection");
            AddValueOutput("valid", "bool");
            AddValueOutput("objectId", "string");
            AddValueOutput("slotId", "int");
            AddValueOutput("requesterId", "string");
            AddValueOutput("state", "string");
            AddValueOutput("targetPosition", "Vector3");
            AddValueOutput("facingPosition", "Vector3");
            AddValueOutput("remainingSeconds", "float");
            AddValueOutput("failReason", "string");
            AddProperty("reservationToken", "string", false, "");
        }
    }

    [Serializable]
    [UseWithGraph(typeof(BlueprintVisualGraph))]
    [BlueprintVisualNodeType("SmartObject.ReleaseByRequester")]
    public sealed class SmartObjectReleaseByRequesterVisualNode : SmartObjectVisualNode
    {
        protected override void ConfigureDefaultNode()
        {
            SetIdentity("SmartObject.ReleaseByRequester", "SmartObject Release By Requester", "SmartObject", "Force-releases all slots held by a requester.");
            AddExecInput("execIn");
            AddValueInput("requesterId", "string", true, "propertyOrConnection");
            AddValueInput("reason", "string", false, "propertyOrConnection");
            AddExecOutput("execOut");
            AddValueOutput("releasedCount", "int");
            AddValueOutput("failReason", "string");
            AddProperty("requesterId", "string", false, "");
            AddProperty("reason", "string", false, SmartObjectReleaseReason.ForceRelease);
        }
    }
}

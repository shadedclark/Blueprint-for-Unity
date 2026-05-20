using System;
using System.Collections.Generic;

namespace BlueprintSystem
{
    public enum BlueprintDiagnosticSeverity
    {
        Info,
        Warning,
        Error
    }

    [Serializable]
    public sealed class BlueprintDiagnostic
    {
        public string Code;
        public BlueprintDiagnosticSeverity Severity;
        public string Message;
        public string File;
        public string NodeId;
        public string PortId;
        public string Edge;

        public static BlueprintDiagnostic Error(string code, string message, string nodeId = null, string portId = null, string edge = null)
        {
            return Create(code, BlueprintDiagnosticSeverity.Error, message, nodeId, portId, edge);
        }

        public static BlueprintDiagnostic Warning(string code, string message, string nodeId = null, string portId = null, string edge = null)
        {
            return Create(code, BlueprintDiagnosticSeverity.Warning, message, nodeId, portId, edge);
        }

        public override string ToString()
        {
            string location = string.IsNullOrEmpty(NodeId) ? string.Empty : " node=" + NodeId;
            if (!string.IsNullOrEmpty(PortId))
            {
                location += " port=" + PortId;
            }

            if (!string.IsNullOrEmpty(Edge))
            {
                location += " edge=" + Edge;
            }

            return Code + " " + Severity + ": " + Message + location;
        }

        private static BlueprintDiagnostic Create(string code, BlueprintDiagnosticSeverity severity, string message, string nodeId, string portId, string edge)
        {
            BlueprintDiagnostic diagnostic = new BlueprintDiagnostic();
            diagnostic.Code = code;
            diagnostic.Severity = severity;
            diagnostic.Message = message;
            diagnostic.NodeId = nodeId;
            diagnostic.PortId = portId;
            diagnostic.Edge = edge;
            return diagnostic;
        }
    }

    public sealed class BlueprintDiagnosticList : List<BlueprintDiagnostic>
    {
        public bool HasErrors
        {
            get
            {
                for (int i = 0; i < Count; i++)
                {
                    if (this[i].Severity == BlueprintDiagnosticSeverity.Error)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public string ToDisplayString()
        {
            if (Count == 0)
            {
                return "No diagnostics.";
            }

            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            for (int i = 0; i < Count; i++)
            {
                builder.AppendLine(this[i].ToString());
            }

            return builder.ToString();
        }
    }

    public sealed class BlueprintCompileResult
    {
        public RuntimeBlueprint Blueprint;
        public readonly BlueprintDiagnosticList Diagnostics = new BlueprintDiagnosticList();

        public bool Success
        {
            get { return Blueprint != null && !Diagnostics.HasErrors; }
        }
    }
}

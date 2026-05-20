using System;

namespace BlueprintSystem
{
    [Serializable]
    public struct BlueprintPortKey : IEquatable<BlueprintPortKey>
    {
        public string NodeId;
        public string PortId;

        public BlueprintPortKey(string nodeId, string portId)
        {
            NodeId = nodeId;
            PortId = portId;
        }

        public bool IsValid
        {
            get { return !string.IsNullOrEmpty(NodeId) && !string.IsNullOrEmpty(PortId); }
        }

        public static bool TryParse(string text, out BlueprintPortKey key)
        {
            key = new BlueprintPortKey();
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            int dot = text.IndexOf('.');
            if (dot <= 0 || dot >= text.Length - 1 || text.IndexOf('.', dot + 1) >= 0)
            {
                return false;
            }

            key = new BlueprintPortKey(text.Substring(0, dot), text.Substring(dot + 1));
            return true;
        }

        public bool Equals(BlueprintPortKey other)
        {
            return NodeId == other.NodeId && PortId == other.PortId;
        }

        public override bool Equals(object obj)
        {
            return obj is BlueprintPortKey && Equals((BlueprintPortKey)obj);
        }

        public override int GetHashCode()
        {
            int nodeHash = NodeId == null ? 0 : NodeId.GetHashCode();
            int portHash = PortId == null ? 0 : PortId.GetHashCode();
            return (nodeHash * 397) ^ portHash;
        }

        public override string ToString()
        {
            return NodeId + "." + PortId;
        }
    }

    [Serializable]
    public sealed class RuntimeEdge
    {
        public BlueprintPortKey From;
        public BlueprintPortKey To;

        public RuntimeEdge(BlueprintPortKey from, BlueprintPortKey to)
        {
            From = from;
            To = to;
        }

        public override string ToString()
        {
            return From + " -> " + To;
        }
    }
}

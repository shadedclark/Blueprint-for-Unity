using UnityEngine;

namespace VehicleRoads
{
    [CreateAssetMenu(menuName = "Vehicle Road/Road Network/Runtime Diagnostics Settings")]
    public sealed class RoadNetworkRuntimeSettings : ScriptableObject
    {
        [SerializeField] private bool enableRuntimeProfilerMarkers;
        [SerializeField] private bool enableDetailedDiagnosticHistory;
        [SerializeField, Range(16, 2048)] private int diagnosticHistoryCapacity = 128;
        [SerializeField] private bool captureSuccessfulQueries;
        [SerializeField] private bool captureFailedQueries = true;
        [SerializeField] private bool captureAgentStateTransitions = true;
        [SerializeField] private bool developmentBuildDiagnostics = true;

        public bool EnableRuntimeProfilerMarkers =>
            IsDiagnosticsBuild && enableRuntimeProfilerMarkers;
        public bool EnableDetailedDiagnosticHistory =>
            IsDiagnosticsBuild && enableDetailedDiagnosticHistory;
        public int DiagnosticHistoryCapacity => Mathf.Clamp(diagnosticHistoryCapacity, 16, 2048);
        public bool CaptureSuccessfulQueries => captureSuccessfulQueries;
        public bool CaptureFailedQueries => captureFailedQueries;
        public bool CaptureAgentStateTransitions => captureAgentStateTransitions;
        public bool DevelopmentBuildDiagnostics => developmentBuildDiagnostics;

        private bool IsDiagnosticsBuild =>
            Application.isEditor || developmentBuildDiagnostics && Debug.isDebugBuild;

        private void OnValidate()
        {
            diagnosticHistoryCapacity = Mathf.Clamp(diagnosticHistoryCapacity, 16, 2048);
        }
    }
}

using System;

namespace Game.Core
{
    /// <summary>Gameplay registers a NET snapshot builder; Core stays free of Gameplay refs.</summary>
    public static class DebugReportContext
    {
        public static Func<string> BuildNetSection { get; set; }

        public static string TryBuildNetSection()
        {
            try
            {
                return BuildNetSection?.Invoke() ?? "role=Offline phase=Offline";
            }
            catch
            {
                return "role=Offline phase=Offline error=context";
            }
        }
    }
}

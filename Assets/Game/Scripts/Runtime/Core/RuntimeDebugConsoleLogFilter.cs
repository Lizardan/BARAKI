using System;
using UnityEngine;

namespace Game.Core
{
    public static class RuntimeDebugConsoleLogFilter
    {
        public static bool ShouldAccept(string message, LogType type)
        {
            if (type is LogType.Error or LogType.Exception or LogType.Assert)
            {
                return true;
            }

            return !string.IsNullOrEmpty(message)
                   && message.StartsWith(PlaytestLog.Marker, StringComparison.Ordinal);
        }
    }
}

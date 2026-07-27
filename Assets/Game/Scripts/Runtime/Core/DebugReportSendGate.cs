using System;

namespace Game.Core
{
    public sealed class DebugReportSendGate
    {
        public static readonly TimeSpan SuccessCooldown = TimeSpan.FromSeconds(60);
        public static readonly TimeSpan FailureCooldown = TimeSpan.FromSeconds(10);

        private DateTime _nextAllowedUtc = DateTime.MinValue;

        public bool TryBeginSend(DateTime utcNow, out string blockReason)
        {
            if (utcNow < _nextAllowedUtc)
            {
                var seconds = Math.Max(1, (int)Math.Ceiling((_nextAllowedUtc - utcNow).TotalSeconds));
                blockReason = $"Подождите {seconds}с";
                return false;
            }

            blockReason = string.Empty;
            return true;
        }

        public void MarkSuccess(DateTime utcNow) =>
            _nextAllowedUtc = utcNow + SuccessCooldown;

        public void MarkFailure(DateTime utcNow) =>
            _nextAllowedUtc = utcNow + FailureCooldown;
    }
}

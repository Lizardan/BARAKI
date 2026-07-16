using System.Reflection;
using Cysharp.Threading.Tasks;
using Game.Gameplay.Networking;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class UnityServicesBootstrapTests
    {
        static readonly FieldInfo InitTaskField = typeof(UnityServicesBootstrap).GetField(
            "s_initTask",
            BindingFlags.NonPublic | BindingFlags.Static);

        [Test]
        public void EnsureInitializedAsync_Restarts_WhenStaleCompletedTaskAndNotReady()
        {
            Assume.That(UnityServicesBootstrap.IsReady, Is.False);
            Assume.That(InitTaskField, Is.Not.Null);

            // Simulate Enter Play Mode Options without domain reload:
            // previous session left a completed task, UGS already torn down.
            InitTaskField.SetValue(null, UniTask.CompletedTask);

            var task = UnityServicesBootstrap.EnsureInitializedAsync();

            if (task.Status == UniTaskStatus.Succeeded)
            {
                Assert.IsTrue(
                    UnityServicesBootstrap.IsReady,
                    "Stale CompletedTask must not short-circuit while services are not ready.");
            }
            else
            {
                Assert.That(
                    task.Status,
                    Is.EqualTo(UniTaskStatus.Pending).Or.EqualTo(UniTaskStatus.Faulted),
                    "Expected a fresh init attempt, not a silent completed no-op.");
            }
        }
    }
}

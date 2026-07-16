using Cysharp.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

namespace Game.Gameplay.Networking
{
    /// <summary>Initializes UGS + anonymous auth once per process.</summary>
    public static class UnityServicesBootstrap
    {
        static UniTask s_initTask;

        public static bool IsReady =>
            UnityServices.State == ServicesInitializationState.Initialized
            && AuthenticationService.Instance.IsSignedIn;

        public static string PlayerId =>
            IsReady ? AuthenticationService.Instance.PlayerId : string.Empty;

        public static UniTask EnsureInitializedAsync()
        {
            if (IsReady)
            {
                return UniTask.CompletedTask;
            }

            // Join in-flight init; otherwise start fresh. Needed when Enter Play Mode
            // Options disable domain reload: statics survive while UGS tears down.
            if (s_initTask.Status == UniTaskStatus.Pending)
            {
                return s_initTask;
            }

            // Preserve: multiple callers may await the same init without token errors.
            s_initTask = InitializeAsync().Preserve();
            return s_initTask;
        }

        static async UniTask InitializeAsync()
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                await UnityServices.InitializeAsync();
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            Debug.Log($"UnityServicesBootstrap: signed in as {AuthenticationService.Instance.PlayerId}");
        }
    }
}

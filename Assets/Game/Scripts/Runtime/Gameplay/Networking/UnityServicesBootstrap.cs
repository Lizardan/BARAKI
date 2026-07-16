using System;
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
        static bool s_started;

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

            if (!s_started)
            {
                s_started = true;
                s_initTask = InitializeAsync();
            }

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

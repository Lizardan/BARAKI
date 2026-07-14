using System;
using Game.Core;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Game.Gameplay.Networking
{
    /// <summary>Persistent owner of the NGO manager and WebSocket transport.</summary>
    [DisallowMultipleComponent]
    public sealed class MatchNetworkBootstrap : MonoBehaviour
    {
        private const string BootstrapName = "MatchNetworkBootstrap";
        private const string LobbyPrefabResource = "Networking/NetworkLobbyState";
        private const string AuthorityPrefabResource = "Networking/MatchNetworkAuthority";

        private static MatchNetworkBootstrap s_instance;

        private NetworkManager _networkManager;
        private UnityTransport _transport;
        private GameObject _lobbyPrefab;
        private GameObject _authorityPrefab;
        private bool _prefabsRegistered;
        private bool _isInitialized;

        public NetworkManager NetworkManager => _networkManager;

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureNetworkComponents();
        }

        private void OnDestroy()
        {
            if (s_instance == this)
            {
                s_instance = null;
            }
        }

        public static MatchNetworkBootstrap Ensure()
        {
            if (s_instance != null)
            {
                s_instance.EnsureNetworkComponents();
                return s_instance;
            }

            var existing = FindAnyObjectByType<MatchNetworkBootstrap>();
            if (existing != null)
            {
                s_instance = existing;
                existing.EnsureNetworkComponents();
                return existing;
            }

            var bootstrapObject = new GameObject(BootstrapName);
            return bootstrapObject.AddComponent<MatchNetworkBootstrap>();
        }

        public static bool TryParseEndpoint(string value, out MatchNetworkEndpoint endpoint) =>
            MatchNetworkEndpoint.TryParse(value, out endpoint);

        public static MatchNetworkEndpoint ParseEndpoint(string value) =>
            MatchNetworkEndpoint.Parse(value);

        public void ConfigureEndpoint(string host, ushort port, bool listenAll)
        {
            if (!EnsureNetworkComponents())
            {
                throw new InvalidOperationException(
                    "MatchNetworkBootstrap: cannot configure endpoint — NGO components missing.");
            }

            var address = string.IsNullOrWhiteSpace(host) ? "127.0.0.1" : host.Trim();
            var resolvedPort = port == 0 ? MatchNetworkEndpoint.DefaultPort : port;
            _transport.SetConnectionData(
                address,
                resolvedPort,
                listenAll ? "0.0.0.0" : null);
        }

        public bool StartAsServer()
        {
            if (!EnsureNetworkComponents())
            {
                return false;
            }

            if (_networkManager.IsListening)
            {
                return _networkManager.IsServer;
            }

            _networkManager.OnServerStarted += OnServerStarted;
            var started = _networkManager.StartServer();
            if (!started)
            {
                _networkManager.OnServerStarted -= OnServerStarted;
            }

            return started;
        }

        public bool StartAsClient()
        {
            if (!EnsureNetworkComponents())
            {
                return false;
            }

            return _networkManager.IsListening
                ? _networkManager.IsClient
                : _networkManager.StartClient();
        }

        public bool StartAsHost()
        {
            if (!EnsureNetworkComponents())
            {
                return false;
            }

            if (_networkManager.IsListening)
            {
                return _networkManager.IsHost;
            }

            _networkManager.OnServerStarted += OnServerStarted;
            var started = _networkManager.StartHost();
            if (!started)
            {
                _networkManager.OnServerStarted -= OnServerStarted;
            }

            return started;
        }

        public void Shutdown()
        {
            if (_networkManager != null && _networkManager.IsListening)
            {
                _networkManager.Shutdown();
            }
        }

        public void EnsureServerLobby()
        {
            if (_networkManager == null || !_networkManager.IsServer || NetworkLobbyState.Instance != null)
            {
                return;
            }

            SpawnNetworkPrefab(_lobbyPrefab, "NetworkLobbyState", typeof(NetworkLobbyState));
        }

        public void EnsureMatchAuthority()
        {
            if (_networkManager == null || !_networkManager.IsServer
                || FindAnyObjectByType<MatchNetworkAuthority>() != null)
            {
                return;
            }

            SpawnNetworkPrefab(_authorityPrefab, "MatchNetworkAuthority", typeof(MatchNetworkAuthority));
        }

        private bool EnsureNetworkComponents()
        {
            if (_networkManager != null && _transport != null && _networkManager.NetworkConfig != null)
            {
                RegisterRuntimePrefabs();
                return true;
            }

            // Own NGO on this bootstrap object — never reuse a foreign Singleton that may be half-dead.
            _networkManager = GetComponent<NetworkManager>();
            if (_networkManager == null)
            {
                _networkManager = gameObject.AddComponent<NetworkManager>();
            }

            if (_networkManager == null)
            {
                Debug.LogError("MatchNetworkBootstrap: failed to create NetworkManager.");
                return false;
            }

            if (_networkManager.NetworkConfig == null)
            {
                _networkManager.NetworkConfig = new NetworkConfig();
            }

            _transport = GetComponent<UnityTransport>();
            if (_transport == null)
            {
                _transport = gameObject.AddComponent<UnityTransport>();
            }

            if (_transport == null)
            {
                Debug.LogError("MatchNetworkBootstrap: failed to create UnityTransport.");
                return false;
            }

            _transport.UseWebSockets = true;
            _networkManager.NetworkConfig.NetworkTransport = _transport;
            _networkManager.NetworkConfig.PlayerPrefab = null;
            _networkManager.NetworkConfig.ForceSamePrefabs = false;
            if (!_isInitialized)
            {
                _transport.SetConnectionData("127.0.0.1", MatchNetworkEndpoint.DefaultPort);
                _isInitialized = true;
            }

            RegisterRuntimePrefabs();
            return true;
        }

        private void RegisterRuntimePrefabs()
        {
            if (_prefabsRegistered || _networkManager == null)
            {
                return;
            }

            _lobbyPrefab ??= Resources.Load<GameObject>(LobbyPrefabResource);
            _authorityPrefab ??= Resources.Load<GameObject>(AuthorityPrefabResource);
            RegisterNetworkPrefab(_lobbyPrefab);
            RegisterNetworkPrefab(_authorityPrefab);
            _prefabsRegistered = _lobbyPrefab != null && _authorityPrefab != null;
        }

        private void RegisterNetworkPrefab(GameObject prefab)
        {
            if (prefab == null || _networkManager == null || _networkManager.IsListening)
            {
                return;
            }

            _networkManager.AddNetworkPrefab(prefab);
        }

        private void OnServerStarted()
        {
            _networkManager.OnServerStarted -= OnServerStarted;
            EnsureServerLobby();
        }

        private void SpawnNetworkPrefab(GameObject prefab, string fallbackName, System.Type behaviourType)
        {
            GameObject instance;
            if (prefab != null)
            {
                instance = Instantiate(prefab);
            }
            else
            {
                Debug.LogWarning(
                    $"{fallbackName} prefab is missing from Resources/Networking; using server-only fallback.");
                instance = new GameObject(fallbackName);
                instance.AddComponent<NetworkObject>();
                instance.AddComponent(behaviourType);
            }

            var networkObject = instance.GetComponent<NetworkObject>();
            networkObject.DestroyWithScene = false;
            DontDestroyOnLoad(instance);
            networkObject.Spawn();
        }
    }
}

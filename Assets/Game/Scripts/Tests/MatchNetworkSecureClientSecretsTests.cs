using Game.Gameplay.Networking;
using NUnit.Framework;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Game.Tests
{
    public sealed class MatchNetworkSecureClientSecretsTests
    {
        [Test]
        public void ConfigureEndpoint_SecureClient_EnablesEncryptionAndHostnameAddress()
        {
            var bootstrap = MatchNetworkBootstrap.Ensure();
            try
            {
                bootstrap.ConfigureEndpoint(
                    "baraki-matchmaker.example.workers.dev",
                    443,
                    listenAll: false,
                    useSecureWebSocket: true);

                var transport = bootstrap.GetComponent<UnityTransport>();
                Assert.IsNotNull(transport);
                Assert.IsTrue(transport.UseEncryption);
                Assert.AreEqual("baraki-matchmaker.example.workers.dev", transport.ConnectionData.Address);
                Assert.AreEqual((ushort)443, transport.ConnectionData.Port);
            }
            finally
            {
                if (bootstrap != null)
                {
                    Object.DestroyImmediate(bootstrap.gameObject);
                }
            }
        }
    }
}

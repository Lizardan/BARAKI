using System;

namespace Game.Core
{
    public enum MatchNetworkEndpointKind
    {
        Invalid = 0,
        Local = 1,
        WebSocket = 2,
        SecureWebSocket = 3,
        RelayHost = 4,
        RelayClient = 5,
    }

    /// <summary>Parsed transport endpoint without any NGO dependency.</summary>
    public readonly struct MatchNetworkEndpoint
    {
        public const ushort DefaultPort = 7777;
        public const string RelayHostPrefix = "relay-host://";
        public const string RelayClientPrefix = "relay://";

        private MatchNetworkEndpoint(
            MatchNetworkEndpointKind kind,
            string host,
            ushort port,
            string localCode)
        {
            Kind = kind;
            Host = host ?? string.Empty;
            Port = port;
            LocalCode = localCode ?? string.Empty;
        }

        public MatchNetworkEndpointKind Kind { get; }
        public string Host { get; }
        public ushort Port { get; }
        public string LocalCode { get; }
        public bool IsLocal => Kind == MatchNetworkEndpointKind.Local;
        public bool IsRelay =>
            Kind is MatchNetworkEndpointKind.RelayHost or MatchNetworkEndpointKind.RelayClient;
        public bool IsRelayHost => Kind == MatchNetworkEndpointKind.RelayHost;
        public bool IsNetworked =>
            Kind is MatchNetworkEndpointKind.WebSocket
                or MatchNetworkEndpointKind.SecureWebSocket
                or MatchNetworkEndpointKind.RelayHost
                or MatchNetworkEndpointKind.RelayClient;
        public bool IsSecure => Kind == MatchNetworkEndpointKind.SecureWebSocket;

        public static string FormatRelayHost(string relayJoinCode) =>
            RelayHostPrefix + (relayJoinCode ?? string.Empty).Trim();

        public static string FormatRelayClient(string relayJoinCode) =>
            RelayClientPrefix + (relayJoinCode ?? string.Empty).Trim();

        public static bool TryParse(string value, out MatchNetworkEndpoint endpoint)
        {
            endpoint = default;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var trimmed = value.Trim();
            const string localPrefix = "local://";
            if (trimmed.StartsWith(localPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var code = trimmed.Substring(localPrefix.Length).Trim().Trim('/');
                if (string.IsNullOrWhiteSpace(code))
                {
                    return false;
                }

                endpoint = new MatchNetworkEndpoint(
                    MatchNetworkEndpointKind.Local,
                    string.Empty,
                    0,
                    code);
                return true;
            }

            if (trimmed.StartsWith(RelayHostPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var code = trimmed.Substring(RelayHostPrefix.Length).Trim().Trim('/');
                if (string.IsNullOrWhiteSpace(code))
                {
                    return false;
                }

                endpoint = new MatchNetworkEndpoint(
                    MatchNetworkEndpointKind.RelayHost,
                    string.Empty,
                    0,
                    code);
                return true;
            }

            if (trimmed.StartsWith(RelayClientPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var code = trimmed.Substring(RelayClientPrefix.Length).Trim().Trim('/');
                if (string.IsNullOrWhiteSpace(code))
                {
                    return false;
                }

                endpoint = new MatchNetworkEndpoint(
                    MatchNetworkEndpointKind.RelayClient,
                    string.Empty,
                    0,
                    code);
                return true;
            }

            if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
                || string.IsNullOrWhiteSpace(uri.Host)
                || (uri.Scheme != "ws" && uri.Scheme != "wss"))
            {
                return false;
            }

            var port = uri.IsDefaultPort
                ? (uri.Scheme == "wss" ? (ushort)443 : DefaultPort)
                : uri.Port;
            if (port is <= 0 or > ushort.MaxValue)
            {
                return false;
            }

            endpoint = new MatchNetworkEndpoint(
                uri.Scheme == "wss"
                    ? MatchNetworkEndpointKind.SecureWebSocket
                    : MatchNetworkEndpointKind.WebSocket,
                uri.Host,
                (ushort)port,
                string.Empty);
            return true;
        }

        public static MatchNetworkEndpoint Parse(string value)
        {
            if (!TryParse(value, out var endpoint))
            {
                throw new FormatException($"Invalid match endpoint: '{value}'.");
            }

            return endpoint;
        }
    }
}

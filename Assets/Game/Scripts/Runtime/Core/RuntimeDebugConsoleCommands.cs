using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Registry for runtime debug-console cheat/commands.
    /// Gameplay registers handlers without Core referencing Gameplay.
    /// </summary>
    public static class RuntimeDebugConsoleCommands
    {
        public delegate string Handler(ReadOnlySpan<string> args);

        private static readonly Dictionary<string, Entry> s_commands =
            new(StringComparer.OrdinalIgnoreCase);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_commands.Clear();
        }

        public static void Register(string name, string help, Handler handler)
        {
            EnsureBuiltIns();
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Command name is required.", nameof(name));
            }

            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            var key = name.Trim();
            s_commands[key] = new Entry(key, help ?? string.Empty, handler);
        }

        public static bool TryExecute(string line, out string status)
        {
            EnsureBuiltIns();
            status = string.Empty;
            if (string.IsNullOrWhiteSpace(line))
            {
                return false;
            }

            if (!TryParse(line, out var name, out var args))
            {
                status = "Пустая команда";
                return false;
            }

            if (!s_commands.TryGetValue(name, out var entry))
            {
                status = $"Неизвестная команда: {name}. help — список.";
                return false;
            }

            try
            {
                status = entry.Handler(args) ?? string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                status = $"Ошибка: {ex.Message}";
                return false;
            }
        }

        public static IReadOnlyList<(string Name, string Help)> List()
        {
            EnsureBuiltIns();
            if (s_commands.Count == 0)
            {
                return Array.Empty<(string, string)>();
            }

            var list = new List<(string, string)>(s_commands.Count);
            foreach (var pair in s_commands)
            {
                list.Add((pair.Value.Name, pair.Value.Help));
            }

            list.Sort((a, b) => string.Compare(a.Item1, b.Item1, StringComparison.OrdinalIgnoreCase));
            return list;
        }

        public static bool TryParse(string line, out string name, out string[] args)
        {
            name = string.Empty;
            args = Array.Empty<string>();
            if (string.IsNullOrWhiteSpace(line))
            {
                return false;
            }

            var parts = line.Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return false;
            }

            name = parts[0];
            if (parts.Length == 1)
            {
                return true;
            }

            args = new string[parts.Length - 1];
            Array.Copy(parts, 1, args, 0, args.Length);
            return true;
        }

        private static void EnsureBuiltIns()
        {
            if (s_commands.ContainsKey("help"))
            {
                return;
            }

            s_commands["help"] = new Entry("help", "Список команд", Help);
            s_commands["chat.setbase"] = new Entry(
                "chat.setbase",
                "chat.setbase <url> — PlayerPrefs baraki.chat.apiBase",
                ChatSetBase);
            s_commands["chat.setkey"] = new Entry(
                "chat.setkey",
                "chat.setkey <key> — PlayerPrefs baraki.chat.apiKey (нужен после деплоя Worker)",
                ChatSetKey);
            s_commands["chat.clear"] = new Entry(
                "chat.clear",
                "Сбросить baraki.chat.apiBase / apiKey",
                ChatClear);
        }

        private static string ChatSetBase(ReadOnlySpan<string> args)
        {
            if (args.Length < 1 || string.IsNullOrWhiteSpace(args[0]))
            {
                return "Использование: chat.setbase <url>";
            }

            var url = args[0].Trim().TrimEnd('/');
            PlayerPrefs.SetString("baraki.chat.apiBase", url);
            PlayerPrefs.Save();
            return $"chat apiBase = {url} (перезапустите warm-up / EnsureInitialized)";
        }

        private static string ChatSetKey(ReadOnlySpan<string> args)
        {
            if (args.Length < 1 || string.IsNullOrWhiteSpace(args[0]))
            {
                return "Использование: chat.setkey <key>";
            }

            var key = args[0].Trim();
            PlayerPrefs.SetString("baraki.chat.apiKey", key);
            PlayerPrefs.Save();
            return "chat apiKey сохранён (перезапустите warm-up / EnsureInitialized)";
        }

        private static string ChatClear(ReadOnlySpan<string> args)
        {
            PlayerPrefs.DeleteKey("baraki.chat.apiBase");
            PlayerPrefs.DeleteKey("baraki.chat.apiKey");
            PlayerPrefs.Save();
            return "chat prefs очищены";
        }

        private static string Help(ReadOnlySpan<string> args)
        {
            var list = List();
            if (list.Count == 0)
            {
                return "Команд нет";
            }

            var lines = new List<string>(list.Count);
            for (var i = 0; i < list.Count; i++)
            {
                var (cmd, help) = list[i];
                lines.Add(string.IsNullOrEmpty(help) ? cmd : $"{cmd} — {help}");
            }

            return string.Join("; ", lines);
        }

        private readonly struct Entry
        {
            public Entry(string name, string help, Handler handler)
            {
                Name = name;
                Help = help;
                Handler = handler;
            }

            public string Name { get; }
            public string Help { get; }
            public Handler Handler { get; }
        }
    }
}

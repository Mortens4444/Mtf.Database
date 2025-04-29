using System.Collections.Concurrent;

namespace Mtf.Database.Services
{
    public static class ScriptCache
    {
        private static readonly ConcurrentDictionary<string, string> cache = new ConcurrentDictionary<string, string>();

        public static string GetScript(string scriptName)
        {
            return cache.GetOrAdd(scriptName, ResourceHelper.GetDbScript);
        }

        public static void Clear() => cache.Clear();

        public static bool Remove(string scriptName) => cache.TryRemove(scriptName, out _);
    }
}

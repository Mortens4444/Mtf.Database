using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace Mtf.Database.Services
{
    public static class ResourceHelper
    {
        public static string GetDbScript(string scriptName)
        {
            return ReadEmbeddedResource(String.Concat(BaseRepository.DatabaseScriptsLocation ?? "Database.Scripts", ".", scriptName, ".sql"), Encoding.UTF8);
        }

        public static string ReadEmbeddedResource(string resourceName, Encoding encoding)
        {
            var assembly = BaseRepository.DatabaseScriptsAssembly ?? Assembly.GetEntryAssembly();
            return ReadEmbeddedResource(resourceName, assembly, encoding);
        }

        public static Stream GetEmbeddedResourceStream(string resourceName, Assembly assembly)
        {
            return assembly == null ? throw new ArgumentNullException(nameof(assembly))
               : assembly.GetManifestResourceStream(resourceName) ?? throw new ArgumentException($"Resource '{resourceName}' not found in assembly '{assembly.FullName}'.", nameof(resourceName));
        }

        public static string ReadEmbeddedResource(string resourceName, Assembly assembly, Encoding encoding)
        {
            using (var stream = GetEmbeddedResourceStream(resourceName, assembly) ?? throw new ArgumentException($"Resource '{resourceName}' not found in assembly '{assembly.FullName}'.", nameof(resourceName)))
            {
                using (var reader = new StreamReader(stream, encoding))
                {
                    return reader.ReadToEnd();
                }
            }
        }
    }
}

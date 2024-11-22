using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace Mtf.Database.Services
{
    internal static class ResourceHelper
    {
        internal static string GetDbScript(string scriptName)
        {
            return ReadEmbeddedResource(String.Concat(BaseRepository.DatabaseScriptsLocation, ".", scriptName, ".sql"), Encoding.UTF8);
        }

        internal static string ReadEmbeddedResource(string resourceName, Encoding encoding)
        {
            return ReadEmbeddedResource(resourceName, BaseRepository.DatabaseScriptsAssembly, encoding);
        }

        internal static Stream GetEmbeddedResourceStream(string resourceName, Assembly assembly)
        {
            return assembly == null ? throw new ArgumentNullException(nameof(assembly))
               : assembly.GetManifestResourceStream(resourceName) ?? throw new ArgumentException($"Resource '{resourceName}' not found.", nameof(resourceName));
        }

        internal static string ReadEmbeddedResource(string resourceName, Assembly assembly, Encoding encoding)
        {
            using (var stream = GetEmbeddedResourceStream(resourceName, assembly) ?? throw new ArgumentException($"Resource '{resourceName}' not found.", nameof(resourceName)))
            {
                using (var reader = new StreamReader(stream, encoding))
                {
                    return reader.ReadToEnd();
                }
            }
        }
    }
}

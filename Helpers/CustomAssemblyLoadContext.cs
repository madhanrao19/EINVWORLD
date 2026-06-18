using System;
using System.Reflection;
using System.Runtime.Loader;

namespace EINVWORLD.Helpers
{
    public class CustomAssemblyLoadContext : AssemblyLoadContext
    {
        public IntPtr LoadUnmanagedLibrary(string absolutePath)
        {
            return LoadUnmanagedDllFromPath(absolutePath);
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            // No need to load any managed assemblies here
            return null;
        }
    }
}

// File: TypeDump.cs
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace Megabonk.Multiplayer
{
    internal static class TypeDump
    {
        public static void DumpAll()
        {
            try
            {
                var sb = new StringBuilder();
                var asms = AppDomain.CurrentDomain.GetAssemblies();
                foreach (var a in asms)
                {
                    try
                    {
                        var types = a.GetTypes();
                        foreach (var t in types) sb.AppendLine(t.FullName);
                    }
                    catch { }
                }
                File.WriteAllText(Path.Combine(Environment.CurrentDirectory, "typelist.txt"), sb.ToString());
            }
            catch (Exception e)
            {
                MultiplayerPlugin.LogS.LogWarning($"[TypeDump] {e.Message}");
            }
        }
    }
}
using System;
using Mono.Cecil;

internal static class Program
{
    static void Main(string[] args)
    {
        using (var module = ModuleDefinition.ReadModule(args[0]))
        {
            foreach (var type in module.GetTypes())
            {
                bool matches = false;
                foreach (string pattern in args[1].Split('|'))
                {
                    if (pattern.StartsWith("=") ? type.FullName == pattern.Substring(1) : type.FullName.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0) matches = true;
                }
                if (!matches) continue;
                Console.WriteLine("TYPE " + type.FullName);
                if (args.Length < 3) continue;
                foreach (var f in type.Fields) Console.WriteLine("  FIELD " + f.FieldType.FullName + " " + f.Name + (f.HasConstant ? " = " + f.Constant : ""));
                foreach (var p in type.Properties) Console.WriteLine("  PROP " + p.PropertyType.FullName + " " + p.Name);
                foreach (var m in type.Methods)
                {
                    Console.WriteLine("  METHOD " + m.FullName);
                    if (args[2] != "il" || !m.HasBody || (args.Length > 3 && m.Name.IndexOf(args[3], StringComparison.OrdinalIgnoreCase) < 0)) continue;
                    foreach (var op in m.Body.Instructions) Console.WriteLine("    " + op);
                }
            }
        }
    }
}

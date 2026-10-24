using System;
using System.IO;

internal static class Program
{
    static int Main(string[] args)
    {
        var domain = new ILRuntime.Runtime.Enviorment.AppDomain();
        try
        {
            using (var sdk = File.OpenRead(args[0]))
            using (var probe = File.OpenRead(args[1]))
            {
                domain.LoadAssembly(sdk); domain.LoadAssembly(probe);
                Console.WriteLine("Interpreter: running prediction flow");
                string result = (string)domain.Invoke("YxPrediction.InterpreterProbe", "Run", null, null);
                Console.WriteLine(result);
                return result == "INTERPRETER PASS" ? 0 : 1;
            }
        }
        catch (Exception ex) { Console.WriteLine(ex); return 1; }
        finally { domain.Dispose(); }
    }
}

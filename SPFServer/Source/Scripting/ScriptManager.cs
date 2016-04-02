using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.CodeDom.Compiler;
using System.Reflection;
using Microsoft.CSharp;
using Microsoft.VisualBasic;
using System.Threading;
using SPFServer.Main;
using SPFServer.Types;

namespace SPFServer
{
    internal sealed class ScriptManager
    {
        private AutoResetEvent arEvent = new AutoResetEvent(false);

        private string scriptDir;

        private List<Tuple<ScriptBase, Thread>> runningScripts =
            new List<Tuple<ScriptBase, Thread>>();

        public ScriptManager(string scriptDirectory)
        {
            scriptDir = scriptDirectory;
            runningScripts = EnumerateScripts();
        }

        public void StartThreads()
        {
            if (runningScripts == null) return;

            foreach (var script in runningScripts)
            {
                if (!script.Item2.IsAlive)
                {
                    script.Item1.running = true;
                    script.Item2.Start();
                }
            }
        }

        public void DoTick()
        {
            arEvent.Set();
        }

        public void DoClientConnect(GameClient sender, DateTime time)
        {
            if (runningScripts == null) return;

            foreach (var script in runningScripts)
            {
                script.Item1.OnClientConnect(sender, time);
            }
        }

        public void DoClientDisconnect(GameClient sender, DateTime time)
        {
            if (runningScripts == null) return;

            foreach (var script in runningScripts)
            {
                script.Item1.OnClientDisconnect(sender, time);
            }
        }

        public void DoMessageReceived(GameClient sender, string message)
        {
            foreach (var script in runningScripts)
            {
                script.Item1.OnMessageReceived(sender, message);
            }
        }

        public void Reload()
        {
            runningScripts.ForEach(x => { x.Item1.running = false; x.Item2.Abort(); });
            runningScripts.Clear();
            runningScripts = EnumerateScripts();
        }

        private List<Tuple<ScriptBase, Thread>> EnumerateScripts()
        {
            if (!Directory.Exists(scriptDir))
            {
                Server.WriteErrorToConsole("scripts directory not found. ScriptManager was not loaded.");
                return null;
            }

            List<Tuple<ScriptBase, Thread>> scripts = new List<Tuple<ScriptBase, Thread>>();

            CodeDomProvider compiler = null;
            CompilerParameters compilerOptions = new CompilerParameters();
            compilerOptions.CompilerOptions = "/optimize";
            compilerOptions.GenerateInMemory = true;
            compilerOptions.IncludeDebugInformation = true;

            if (!File.Exists(AppDomain.CurrentDomain.BaseDirectory + "SPFLib.dll"))
            {
                Server.WriteErrorToConsole("SPFLib.dll not found. ScriptManager was not loaded.");
                return null;
            }

            compilerOptions.ReferencedAssemblies.Add(AppDomain.CurrentDomain.BaseDirectory + "SPFLib.dll");
            compilerOptions.ReferencedAssemblies.Add(Assembly.GetExecutingAssembly().Location);
            compilerOptions.ReferencedAssemblies.Add("System.dll");
            compilerOptions.ReferencedAssemblies.Add("System.Core.dll");
            compilerOptions.ReferencedAssemblies.Add("System.Drawing.dll");

            var files = Directory.GetFiles(scriptDir, "*", SearchOption.AllDirectories);

            foreach (string filepath in files)
            {
                var ext = Path.GetExtension(filepath);

                if (ext.Equals(".cs", StringComparison.InvariantCultureIgnoreCase))
                {
                    compiler = new CSharpCodeProvider();
                    compilerOptions.CompilerOptions += " /unsafe";
                }

                else if (ext.Equals(".vb", StringComparison.InvariantCultureIgnoreCase))
                {
                    compiler = new VBCodeProvider();
                }

                else continue;
              
                CompilerResults compilerResult = compiler.CompileAssemblyFromFile(compilerOptions, filepath);

                if (!compilerResult.Errors.HasErrors)
                {
                    Logger.Log("[DEBUG]" + "Successfully compiled " + Path.GetFileName(filepath) + "'.");
                }

                else
                {
                    StringBuilder errors = new StringBuilder();

                    for (int i = 0; i < compilerResult.Errors.Count; ++i)
                    {
                        errors.Append("   at line ");
                        errors.Append(compilerResult.Errors[0].Line);
                        errors.Append(": ");
                        errors.Append(compilerResult.Errors[0].ErrorText);

                        if (i < compilerResult.Errors.Count - 1)
                        {
                            errors.AppendLine();
                        }
                    }

                    Logger.Log("[ERROR]" + "Failed to compile '" + Path.GetFileName(filepath) + "' with " + compilerResult.Errors.Count.ToString() + " error(s):" + Environment.NewLine + errors.ToString());

                    return null;
                }

                var asm = compilerResult.CompiledAssembly;

                ScriptBase script;

                foreach (Type t in asm.GetTypes())
                {
                    if (typeof(ScriptBase).IsAssignableFrom(t))
                    {
                        try
                        {
                            script = (ScriptBase)Activator.CreateInstance(t);
                            Console.WriteLine("Loaded script '" + t.FullName + "' successfully.");
                        }
                        catch (MissingMethodException)
                        {
                            Logger.Log("[ERROR]" + "Failed to instantiate script '" + t.FullName + "' because no public default constructor was found.");
                        }
                        catch (TargetInvocationException ex)
                        {

                            Logger.Log("[ERROR]" + "Failed to instantiate script '" + t.FullName + "' because constructor threw an exception:" + Environment.NewLine + ex.InnerException.ToString());
                        }
                        catch (Exception ex)
                        {
                            Logger.Log("[ERROR]" + "Failed to instantiate script '" + t.FullName + "':" + Environment.NewLine + ex.ToString());
                        }

                        var scrInstance = (ScriptBase)Activator.CreateInstance(t);

                        if (scrInstance != null)
                        {
                            scripts.Add(new Tuple<ScriptBase, Thread>(scrInstance, 
                                new Thread(() => { while (true) { arEvent.WaitOne();
                                        scrInstance.OnTick(); }})));
                        }
                    }

                    else Console.WriteLine("No scripts found in " + t.FullName);
                }
            }

            return scripts;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.CodeDom.Compiler;
using System.Security;
using Microsoft.CSharp;
using Microsoft.VisualBasic;
using System.Reflection;
using System.IO;

namespace SPFServer.Scripting
{
    internal class RequireScript : System.Attribute
    {
        public RequireScript(System.Type dependency)
        {
            this._dependency = dependency;
        }

        internal System.Type _dependency;
    }

    public sealed class ScriptDomain : MarshalByRefObject
    {
        ScriptBase executingScript;

        List<ScriptBase> executingScripts = new List<ScriptBase>();
        List<Tuple<string, Type>> scriptTypes = new List<Tuple<string, Type>>();

        AppDomain appDomain;

        public ScriptDomain()
        {
            appDomain = AppDomain.CurrentDomain;
        }

        public void DoTick()
        {
            foreach (var script in executingScripts)
            {
                if (!script.running)
                    continue;

                script.OnTick();   
            }
        }

        public void DoClientConnect(string username, DateTime connectTime)
        {
            foreach (var script in executingScripts)
            {
                if (!script.running)
                    continue;

                script.OnClientConnect(username, connectTime);
            }
        }

        public void DoClientDisconnect(string username, DateTime disconnectTime)
        {
            foreach (var script in executingScripts)
            {
                if (!script.running)
                    continue;

                script.OnClientDisconnect(username, disconnectTime);
            }
        }

        public void DoMessageReceived(string username, string message)
        {
            foreach (var script in executingScripts)
            {
                if (!script.running)
                    continue;

                script.OnMessageReceived(username, message);
            }
        }

        public static ScriptDomain Load(string path)
        {
            path = Path.GetFullPath(path);

            AppDomainSetup setup = new AppDomainSetup();
            setup.ApplicationBase = path;
            setup.ShadowCopyFiles = "true";
            setup.ShadowCopyDirectories = path;
            PermissionSet permissions = new PermissionSet(System.Security.Permissions.PermissionState.Unrestricted);

            System.AppDomain appdomain = System.AppDomain.CreateDomain("ScriptDomain_" + (path.GetHashCode() * Environment.TickCount).ToString("X"), null, setup, permissions);
            appdomain.InitializeLifetimeService();

            ScriptDomain scriptdomain = null;

            try
            {
                scriptdomain = (ScriptDomain)appdomain.CreateInstanceFromAndUnwrap(typeof(ScriptDomain).Assembly.Location, typeof(ScriptDomain).FullName);
            }
            catch (Exception ex)
            {
                Logger.Log("[ERROR]" + "Failed to create script domain '" + appdomain.FriendlyName + "':" + Environment.NewLine + ex.ToString());

                System.AppDomain.Unload(appdomain);

                return null;
            }

            Logger.Log("[DEBUG]" + "Loading scripts from '" + path + "' into script domain '" + appdomain.FriendlyName + "' ...");

            if (Directory.Exists(path))
            {
                List<string> filenameScripts = new List<string>();
                List<string> filenameAssemblies = new List<string>();

                try
                {
                    filenameScripts.AddRange(Directory.GetFiles(path, "*.vb", SearchOption.AllDirectories));
                    filenameScripts.AddRange(Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories));
                    filenameAssemblies.AddRange(Directory.GetFiles(path, "*.dll", SearchOption.AllDirectories));
                }
                catch (Exception ex)
                {
                    Logger.Log("[ERROR]" + "Failed to reload scripts:" + Environment.NewLine + ex.ToString());

                    System.AppDomain.Unload(appdomain);

                    return null;
                }

                foreach (string filename in filenameScripts)
                {
                    scriptdomain.LoadScript(filename);
                }
                foreach (string filename in filenameAssemblies)
                {
                    scriptdomain.LoadAssembly(filename);
                }
            }
            else
            {
                Logger.Log("[ERROR]" + "Failed to reload scripts because directory is missing.");
            }

            return scriptdomain;
        }

        public void Start()
        {
            if (executingScripts.Count != 0 || scriptTypes.Count == 0)
            {
                return;
            }

            string assemblyPath = Assembly.GetExecutingAssembly().Location;
            string assemblyFilename = Path.GetFileNameWithoutExtension(assemblyPath);

            foreach (string path in Directory.GetFiles(Path.GetDirectoryName(assemblyPath), "*.log"))
            {
                if (!path.StartsWith(assemblyFilename))
                {
                    continue;
                }

                try
                {
                    TimeSpan logAge = DateTime.Now - DateTime.Parse(Path.GetFileNameWithoutExtension(path).Substring(path.IndexOf('-') + 1));

                    // Delete logs older than 5 days
                    if (logAge.Days >= 5)
                    {
                        File.Delete(path);
                    }
                }
                catch
                {
                    continue;
                }
            }

            Logger.Log("[DEBUG]" + "Starting " + scriptTypes.Count.ToString() + " script(s) ...");

            if (!SortScripts(ref scriptTypes))
            {
                return;
            }

            foreach (Tuple<string, Type> scripttype in scriptTypes)
            {
                ScriptBase script = InstantiateScript(scripttype.Item2);

                if (script == null)
                {

                    continue;
                }

                script.running = true;

                //     script._filename = scripttype.Item1;
                //  script._scriptdomain = this;
                //   script._thread = new Thread(new ThreadStart(script, Script.MainLoop));

                //  script._thread.Start();
                Logger.Log("[DEBUG]" + "Started script '" + script.Name + "'.");

                executingScripts.Add(script);
            }
        }

        bool LoadScript(string filename)
        {
            CodeDomProvider compiler = null;
            CompilerParameters compilerOptions = new CompilerParameters();
            compilerOptions.CompilerOptions = "/optimize";
            compilerOptions.GenerateInMemory = false;
            compilerOptions.IncludeDebugInformation = true;
            compilerOptions.ReferencedAssemblies.Add("System.dll");
            compilerOptions.ReferencedAssemblies.Add("System.Drawing.dll");
            compilerOptions.ReferencedAssemblies.Add("System.Windows.Forms.dll");
            compilerOptions.ReferencedAssemblies.Add(Assembly.GetExecutingAssembly().Location);

            string extension = System.IO.Path.GetExtension(filename);

            if (extension.Equals(".cs", StringComparison.InvariantCultureIgnoreCase))
            {
                compiler = new CSharpCodeProvider();
                compilerOptions.CompilerOptions += " /unsafe";
            }
            else if (extension.Equals(".vb", StringComparison.InvariantCultureIgnoreCase))
            {
                compiler = new VBCodeProvider();
            }
            else
            {
                return false;
            }

            CompilerResults compilerResult = compiler.CompileAssemblyFromFile(compilerOptions, filename);

            if (!compilerResult.Errors.HasErrors)
            {
                Logger.Log("[DEBUG]" + "Successfully compiled " + System.IO.Path.GetFileName(filename) + "'.");

                return LoadAssembly(filename, compilerResult.CompiledAssembly);
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

                Logger.Log("[ERROR]" + "Failed to compile '" + Path.GetFileName(filename) + "' with " + compilerResult.Errors.Count.ToString() + " error(s):" + Environment.NewLine + errors.ToString());

                return false;
            }
        }

        public bool LoadAssembly(string filename)
        {
            System.Reflection.Assembly assembly = null;

            try
            {
                assembly = System.Reflection.Assembly.LoadFrom(filename);
            }
            catch (Exception ex)
            {
                Logger.Log("[ERROR]" + "Failed to load assembly '" + Path.GetFileName(filename) + "':" + Environment.NewLine + ex.ToString());

                return false;
            }

            return LoadAssembly(filename, assembly);
        }

        public bool LoadAssembly(string filename, System.Reflection.Assembly assembly)

        {
            uint count = 0;

            try
            {
                foreach (Type type in assembly.GetTypes())

                {
                    Logger.Log(type.FullName.ToString());

                    if (!type.IsSubclassOf(typeof(ScriptBase)))
                    {
                        continue;
                    }

                    count++;
                    scriptTypes.Add(new Tuple<string, Type>(filename, type));
                }

            }

            catch (System.Reflection.ReflectionTypeLoadException ex)
            {
                Logger.Log("[ERROR]" + "Failed to list assembly types:" + Environment.NewLine + ex.LoaderExceptions[0].ToString());

                return false;
            }

            Logger.Log("[DEBUG]" + "Found " + count.ToString() + " script(s) in '" + Path.GetFileName(filename) + "'.");

            return count != 0;
        }

        private bool SortScripts(ref List<Tuple<string, Type>> scripttypes)
        {
            Dictionary<Tuple<string, Type>, List<Type>> graph = new Dictionary<Tuple<string, Type>, List<Type>>();

            foreach (var scripttype in scripttypes)
            {
                List<Type> dependencies = new List<Type>();

                foreach (RequireScript attribute in ((MemberInfo)scripttype.Item2).GetCustomAttributes(typeof(RequireScript), true))
                {
                    dependencies.Add(attribute._dependency);
                }

                graph.Add(scripttype, dependencies);
            }

            List<Tuple<string, Type>> result = new List<Tuple<string, Type>>(graph.Count);

            while (graph.Count > 0)
            {
                Tuple<string, Type> scriptype = null;

                foreach (var item in graph)
                {
                    if (item.Value.Count == 0)
                    {
                        scriptype = item.Key;
                        break;
                    }
                }

                if (scriptype == null)
                {
                    Logger.Log("[ERROR]" + "Detected a circular script dependency. Aborting ...");
                    return false;
                }

                result.Add(scriptype);
                graph.Remove(scriptype);

                foreach (var item in graph)
                {
                    item.Value.Remove(scriptype.Item2);
                }
            }

            scripttypes = result;

            return true;
        }


        public ScriptBase InstantiateScript(Type scripttype)
        {
            if (!scripttype.IsSubclassOf(typeof(ScriptBase)))
            {
                return null;
            }

            Logger.Log("[DEBUG]" + "Instantiating script '" + scripttype.FullName + "' in script domain '" + appDomain.FriendlyName + "' ...");

            try
            {
                return (ScriptBase)Activator.CreateInstance(scripttype);
            }
            catch (MissingMethodException)
            {
                Logger.Log("[ERROR]" + "Failed to instantiate script '" + scripttype.FullName + "' because no public default constructor was found.");
            }
            catch (TargetInvocationException ex)
            {

                Logger.Log("[ERROR]" + "Failed to instantiate script '" + scripttype.FullName + "' because constructor threw an exception:" + Environment.NewLine + ex.InnerException.ToString());
            }
            catch (Exception ex)
            {
                Logger.Log("[ERROR]" + "Failed to instantiate script '" + scripttype.FullName + "':" + Environment.NewLine + ex.ToString());
            }
            return null;
        }

    }
}

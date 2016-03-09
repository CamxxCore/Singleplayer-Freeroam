/**
 * Copyright (C) 2015 crosire
 *
 * This software is  provided 'as-is', without any express  or implied  warranty. In no event will the
 * authors be held liable for any damages arising from the use of this software.
 * Permission  is granted  to anyone  to use  this software  for  any  purpose,  including  commercial
 * applications, and to alter it and redistribute it freely, subject to the following restrictions:
 *
 *   1. The origin of this software must not be misrepresented; you must not claim that you  wrote the
 *      original  software. If you use this  software  in a product, an  acknowledgment in the product
 *      documentation would be appreciated but is not required.
 *   2. Altered source versions must  be plainly  marked as such, and  must not be  misrepresented  as
 *      being the original software.
 *   3. This notice may not be removed or altered from any source distribution.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.CodeDom.Compiler;
using Microsoft.CSharp;
using Microsoft.VisualBasic;
using System.Threading;
using System.Windows.Forms;

namespace SPFServer.Scripting
{
    interface IScriptTask
    {
        void Run();
    };

    [System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = true)]
    public class RequireScript : System.Attribute
    {
        RequireScript(System.Type dependency)
        {
            _dependency = dependency;
        }

        internal Type _dependency;
    };

     sealed class ScriptDomain : MarshalByRefObject
    {
        public ScriptBase ExecutingScript { get { return sCurrentDomain.ExecutingScript; } }

        public AppDomain AppDomain { get { return _appdomain; } }

        public ScriptDomain CurrentDomain { get { return sCurrentDomain; } }

        ScriptBase _executingScript;

        static ScriptDomain sCurrentDomain;

        private System.AppDomain _appdomain;
        private List<ScriptBase> _runningScripts;
        private List<Tuple<string, Type>> _scriptTypes;
        private Queue<IScriptTask> _taskQueue;
		private List<IntPtr> _pinnedStrings;
        private int _executingThreadId;
        private bool _recordKeyboardEvents;
        bool[] _keyboardState;

        public ScriptDomain()
        {
            _appdomain = AppDomain.CurrentDomain;
            _executingThreadId = Thread.CurrentThread.ManagedThreadId;
            _runningScripts = new List<ScriptBase>();
            _taskQueue = new Queue<IScriptTask>();
            _pinnedStrings = new List<IntPtr>();
            _scriptTypes = new List<Tuple<string, Type>>();
            _recordKeyboardEvents = true;
            _keyboardState = new bool[256];
            _appdomain.AssemblyResolve += new ResolveEventHandler(HandleResolve);
            _appdomain.UnhandledException += new UnhandledExceptionEventHandler(HandleUnhandledException);
        }

    /*ScriptDomain::~ScriptDomain()
    {
        CleanupStrings();

        Log("[DEBUG]", "Deleted script domain '", _appdomain->FriendlyName, "'.");
    }
    */

    public void DoTick()
        {
            // Execute scripts
            foreach (ScriptBase script in _runningScripts)
            {
                if (!script._running)
                {
                    continue;
                }

                _executingScript = script;

                while ((script._running = SignalAndWait(script._continueEvent, script._waitEvent, 5000)) && _taskQueue.Count > 0)
                {
                    _taskQueue.Dequeue().Run();
                }

                _executingScript = null;

                if (!script._running)
                {
                   // Log("[ERROR]", "Script '", script.Name, "' is not responding! Aborting ...");

                    AbortScript(script);
                    continue;
                }
            }

            // Clean up pinned strings
        //    CleanupStrings();
        }


        bool LoadScript(string filename)
        {
            CodeDomProvider compiler = null;
            CompilerParameters compilerOptions = new CompilerParameters();
            compilerOptions.CompilerOptions = "/optimize";
            compilerOptions.GenerateInMemory = true;
            compilerOptions.IncludeDebugInformation = true;
            compilerOptions.ReferencedAssemblies.Add("System.dll");
            compilerOptions.ReferencedAssemblies.Add("System.Drawing.dll");
            compilerOptions.ReferencedAssemblies.Add("System.Windows.Forms.dll");
            //  compilerOptions.ReferencedAssemblies.Add(GTA.Script.typeid.Assembly.Location);

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
                Console.WriteLine("[DEBUG]", "Successfully compiled '", System.IO.Path.GetFileName(filename), "'.");

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

                //  Console.WriteLine("[ERROR]", "Failed to compile '", IO::Path::GetFileName(filename), "' with ", compilerResult.Errors.Count.ToString(), " error(s):", Environment::NewLine, errors.ToString());

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
                //  Log("[ERROR]", "Failed to load assembly '", IO.Path.GetFileName(filename), "':", Environment.NewLine, ex.ToString());

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
                    if (!type.IsSubclassOf(typeof(ScriptBase)))
                    {
                        continue;
                    }

                    count++;
                    _scriptTypes.Add(new Tuple<string, Type>(filename, type));
                }

            }

            catch (System.Reflection.ReflectionTypeLoadException ex)
            {
                //  Log("[ERROR]", "Failed to list assembly types:", Environment.NewLine, ex.ToString());

                return false;
            }

            //  Log("[DEBUG]", "Found ", count.ToString(), " script(s) in '", IO.Path.GetFileName(filename), "'.");

            return count != 0;
        }

        private void HandleUnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            if (!args.IsTerminating)
            {
                //Log("[ERROR]", "Caught unhandled exception:", Environment.NewLine, args.ExceptionObject.ToString());
            }
            else
            {
               // Log("[ERROR]", "Caught fatal unhandled exception:", Environment.NewLine, args.ExceptionObject.ToString());
            }
        }

        private System.Reflection.Assembly HandleResolve(object sender, ResolveEventArgs args)
        {
            if (args.Name.ToLower().Contains("scripthookvdotnet"))
            {
                return System.Reflection.Assembly.GetAssembly(typeof(ScriptBase));
            }

            return null;
        }


        private void SignalAndWait(AutoResetEvent toSignal, AutoResetEvent toWaitOn)
        {
            toSignal.Set();
            toWaitOn.WaitOne();
        }
        private bool SignalAndWait(AutoResetEvent toSignal, AutoResetEvent toWaitOn, int timeout)
        {
            toSignal.Set();
            return toWaitOn.WaitOne(timeout);
        }

        public void DoKeyboardMessage(Keys key, bool status, bool statusCtrl, bool statusShift, bool statusAlt)
        {
            int keycode = (int)key;

            if (keycode < 0 || keycode >= 256)
            {
                return;
            }

            _keyboardState[keycode] = status;

            if (_recordKeyboardEvents)
            {
                KeyEventArgs args = new KeyEventArgs(key | (statusCtrl ? Keys.Control : Keys.None) | (statusShift ? Keys.Shift : Keys.None) | (statusAlt ? Keys.Alt : Keys.None));
                Tuple<bool, KeyEventArgs> eventinfo = new Tuple<bool, KeyEventArgs>(status, args);

                foreach (ScriptBase script in _runningScripts)
                {
                    script._keyboardEvents.Enqueue(eventinfo);
                }
            }
        }
        public void PauseKeyboardEvents(bool pause)
        {
            _recordKeyboardEvents = !pause;
        }
        public void ExecuteTask(IScriptTask task)
        {
            if (Thread.CurrentThread.ManagedThreadId == _executingThreadId)
            {
                task.Run();
            }
            else
            {
                _taskQueue.Enqueue(task);

                SignalAndWait(ExecutingScript._waitEvent, ExecutingScript._continueEvent);
            }
        }

        public void Unload(ref ScriptDomain domain)

        {
            //   Log("[DEBUG]", "Unloading script domain '", domain.Name, "' ...");

            domain.Abort();

            System.AppDomain appdomain = domain.AppDomain;

            domain = null;

            try
            {
                System.AppDomain.Unload(appdomain);
            }
            catch (Exception ex)
            {
                //Log("[ERROR]", "Failed to unload deleted script domain:", Environment.NewLine, ex.ToString());
            }

            domain = null;

            GC.Collect();
        }



        public ScriptBase InstantiateScript(Type scripttype)

        {
            if (!scripttype.IsSubclassOf(typeof(ScriptBase)))
            {
                return null;
            }

            //   Log("[DEBUG]", "Instantiating script '", scripttype.FullName, "' in script domain '", Name, "' ...");

            try
            {
                return (ScriptBase)Activator.CreateInstance(scripttype);
            }
            catch (MissingMethodException)
            {
                // Log("[ERROR]", "Failed to instantiate script '", scripttype.FullName, "' because no public default constructor was found.");
            }

            catch (System.Reflection.TargetInvocationException ex)
            {
                //   Log("[ERROR]", "Failed to instantiate script '", scripttype.FullName, "' because constructor threw an exception:", Environment.NewLine, ex.InnerException.ToString());
            }

            catch (Exception ex)
            {
                //   Log("[ERROR]", "Failed to instantiate script '", scripttype.FullName, "':", Environment.NewLine, ex.ToString());
            }

            return null;
        }

        public void Abort()
        {
            //     Log("[DEBUG]", "Stopping ", _runningScripts.Count.ToString(), " script(s) ...");

            foreach (ScriptBase script in _runningScripts)
            {
                AbortScript(script);
            }

            _scriptTypes.Clear();
            _runningScripts.Clear();

            GC.Collect();
        }

        public void Start()
        {
            if (_runningScripts.Count != 0 || _scriptTypes.Count == 0)
            {
                return;
            }

            string assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string assemblyFilename = System.IO.Path.GetFileNameWithoutExtension(assemblyPath);

            foreach (string path in System.IO.Directory.GetFiles(System.IO.Path.GetDirectoryName(assemblyPath), "*.log"))
            {
                if (!path.StartsWith(assemblyFilename))
                {
                    continue;
                }

                try
                {
                    TimeSpan logAge = DateTime.Now - DateTime.Parse(System.IO.Path.GetFileNameWithoutExtension(path).Substring(path.IndexOf('-') + 1));

                    // Delete logs older than 5 days
                    if (logAge.Days >= 5)
                    {
                        System.IO.File.Delete(path);
                    }
                }
                catch
                {
                    continue;
                }
            }

            // Log("[DEBUG]", "Starting ", _scriptTypes.Count.ToString(), " script(s) ...");

            if (!SortScripts(ref _scriptTypes))
            {
                return;
            }

            foreach (Tuple<string, Type> scripttype in _scriptTypes)
            {
                ScriptBase script = InstantiateScript(scripttype.Item2);

                if (object.ReferenceEquals(script, null))
                {
                    continue;
                }

                script._running = true;
                script._filename = scripttype.Item1;
                script._scriptdomain = this;
                script._thread = new System.Threading.Thread(script.MainLoop);
                script._thread.Start();

                //  Log("[DEBUG]", "Started script '", script.Name, "'.");

                _runningScripts.Add(script);
            }
        }

        private bool SortScripts(ref List<Tuple<string, Type>> scripttypes)
        {
            Dictionary<Tuple<string, Type>, List<Type>> graph = new Dictionary<Tuple<string, Type>, List<Type>>();

            foreach (var scripttype in scripttypes)
            {
                List<Type> dependencies = new List<Type>();

                foreach (RequireScript attribute in ((System.Reflection.MemberInfo)scripttype.Item2).GetCustomAttributes(typeof(RequireScript), true))
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
                    //Log("[ERROR]", "Detected a circular script dependency. Aborting ...");
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


        public void AbortScript(ScriptBase script)
        {
            if (object.ReferenceEquals(script._thread, null))
            {
                return;
            }

            script._running = false;

            script._thread.Abort();
            script._thread = null;

            //Log("[DEBUG]", "Aborted script '", script.Name, "'.");
        }
    }
}

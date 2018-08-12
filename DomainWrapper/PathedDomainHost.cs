using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace DomainWrapper
{
    public interface IAssemblyLoader
    {
        void LoadAndRun(string file, string args);
    }

    public class Startup
    {
        private const Keys ReloadKey = Keys.F11;
        private const Keys StopKey = Keys.F12;

        [STAThread]
        public static int EntryPoint(string filePath, string args = "")
        {
            bool firstLoaded = false;
            bool running = true;
            //todo this while loop breaks my launcher
            while (running) // keep the domain alive to enable reloading of sub processes
            {
                if (!firstLoaded)
                {
                    firstLoaded = true;
                    new SharpDomain(filePath, args);
                }
                if ((GetAsyncKeyState((int) ReloadKey) & 1) == 1)
                {
                    // this seems to generate a memory leak if spammed ?
                    new SharpDomain(filePath, args);
                }
                if ((GetAsyncKeyState((int) StopKey) & 1) == 1)
                {
                    running = false;
                }

                Thread.Sleep(10);
            }
            return 0;
        }

        [DllImport("User32.dll")]
        private static extern short GetAsyncKeyState(int vKey);
    }

    public static class DomainManager
    {
        public static AppDomain CurrentDomain { get; set; }
        public static WSharpAssemblyLoader CurrentAssemblyLoader { get; set; }
    }

    public class WSharpAssemblyLoader : MarshalByRefObject, IAssemblyLoader
    {
        public WSharpAssemblyLoader()
        {
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
        }

        #region IAssemblyLoader Members

        public void LoadAndRun(string file, string args)
        {
            Assembly asm = Assembly.Load(file);

            var staMethods =
                asm.GetTypes()
                    .SelectMany(t => t.GetMethods())
                    .Where(m => m.GetCustomAttributes(typeof (STAThreadAttribute), false).Length > 0)
                    .ToArray();

            if (staMethods.Count() != 1)
            {
                if (!staMethods.Any())
                {
                    PatchedDomainLoader.ShowError(new Exception("Unable to find entry function with [STAThread] attribute"));
                    return;
                }
                PatchedDomainLoader.ShowError(
                    new Exception(
                        "More then one function with [STAThread] attribute was found; injected dll's should only have one"));
                return;
            }

            var entry2 = staMethods[0];
            if (entry2 == null)
            {
                PatchedDomainLoader.ShowError(
                    new Exception($"[STAThread] attribute for {file} not found"));
            }
            if (string.IsNullOrEmpty(args))
                entry2?.Invoke(null, null);
            else
                entry2?.Invoke(null, new object[] {args});
        }
        #endregion

        private Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            if (args.Name == Assembly.GetExecutingAssembly().FullName)
                return Assembly.GetExecutingAssembly();

            string appDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string shortAsmName = Path.GetFileName(args.Name);
            if (appDir == null || shortAsmName == null)
            {
                return null;
            }
            string fileName = Path.Combine(appDir, shortAsmName);

            if (File.Exists(fileName))
            {
                return Assembly.LoadFrom(fileName);
            }
            return Assembly.GetExecutingAssembly().FullName == args.Name ? Assembly.GetExecutingAssembly() : null;
        }
    }

    public class SharpDomain
    {
        private readonly Random _rand = new Random();

        public SharpDomain(string assemblyName, string args)
        {
            try
            {
                string appBase = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                var ads = new AppDomainSetup
                {
                    ApplicationBase = appBase,
                    PrivateBinPath = appBase
                };
                DomainManager.CurrentDomain = AppDomain.CreateDomain("SharpDomain-Internal-" + _rand.Next(0, 10000),
                    AppDomain.CurrentDomain.Evidence, ads);

                AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

                DomainManager.CurrentAssemblyLoader =
                    (WSharpAssemblyLoader)
                        DomainManager.CurrentDomain.CreateInstanceAndUnwrap(Assembly.GetExecutingAssembly().FullName,
                            typeof (WSharpAssemblyLoader).FullName);

                string fileToLoadAndRun = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                    assemblyName);
                DomainManager.CurrentAssemblyLoader.LoadAndRun(fileToLoadAndRun, args);
            }
            catch (Exception ex)
            {
                PatchedDomainLoader.ShowError(ex);
            }
            finally
            {
                DomainManager.CurrentAssemblyLoader = null;
                AppDomain.Unload(DomainManager.CurrentDomain);
            }
        }

        Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            try
            {
                if (args.Name == Assembly.GetExecutingAssembly().FullName)
                    return Assembly.GetExecutingAssembly();

                Assembly assembly = Assembly.Load(args.Name);
                if (assembly != null)
                    return assembly;
            }
            catch
            {
                // apparently we ignore this?
            }

            // *** note: this wont work for special search paths
            string[] parts = args.Name.Split(',');
            string file = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\" + parts[0].Trim() +
                          ".dll";
            return Assembly.LoadFrom(file);
        }
    }
}
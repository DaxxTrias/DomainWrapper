//#define LAUNCH_MDA
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace DomainWrapper
{
    // The exported methods to call our methods to host a proxy domain in order to run injected .net code inside of a process smoothly.
    public static class PatchedDomainLoader
    {
        #region Fields, Private Properties

        internal static string ApplicationArguments = "-blah -bleh";
        internal static string ApplicationToHostName = "Amalgama_Hook.dll";

        [MarshalAs(UnmanagedType.LPWStr)] internal static string ApplicationToHostDirectory =
            @"E:\newdev-2018\testcase";
        #endregion

        //todo do we need to dllexport this?
        [DllExport("HostDomain", CallingConvention.Cdecl)]
        [STAThread]
        public static void HostDomain()
        {
#if LAUNCH_MDA
            System.Diagnostics.Debugger.Launch();
#endif

            if (string.IsNullOrEmpty(ApplicationToHostName) || string.IsNullOrEmpty(ApplicationToHostDirectory))
            {
                throw new InvalidDataException("You must set LoadDomainHostSettings before calling HostDomain()");
            }
            try
            {
                if (ApplicationToHostName.EndsWith("exe") || ApplicationToHostName.EndsWith("dll"))
                {
                    new SharpDomain(Path.Combine(ApplicationToHostDirectory, ApplicationToHostName), ApplicationArguments);
                }
            }
            catch (Exception e)
            {
                throw new FileLoadException(e.ToString());
            }
            finally
            {
                if (DomainManager.CurrentDomain != null)
                {
                    AppDomain.Unload(DomainManager.CurrentDomain);
                    DomainManager.CurrentDomain = null;
                }
            }
        }

        internal static void ShowError(Exception ex)
        {
            throw new Exception(ex.ToString());
        }

        [DllExport("LoadDomainHostSettings", CallingConvention.Cdecl)]
        public static void LoadDomainHostSettings(string loadDirectory, string applicationName, string applicationArguments)
        {
            ApplicationToHostDirectory = loadDirectory;
            ApplicationToHostName = applicationName;
            ApplicationArguments = applicationArguments;
        }
    }
}
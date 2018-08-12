//#define LAUNCH_MDA
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DomainWrapper
{
    // The exported methods to call our methods to host a proxy domain in order to run injected .net code inside of a process smoothly.
    public static class PatchedDomainLoader
    {
        #region Fields, Private Properties

        internal static string ApplicationArguments = "-blah -bleh";
        internal static string ApplicationToHostName = "Amalgama_Hook.dll";

        [MarshalAs(UnmanagedType.LPWStr)] internal static string ApplicationToHostDirectory =
            @"E:\NewDev-2016\MyForks\Amalgama\Debug";
        #endregion

        //todo do we need to dllexport this?
        [DllExport]
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
                    Startup.EntryPoint(Path.Combine(ApplicationToHostDirectory, ApplicationToHostName), ApplicationArguments);
                }
                else
                {
                    MessageBox.Show("Invalid file type, SharpDomain can only load exe/dll files");
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.ToString());
            }
        }

        internal static void ShowError(Exception ex)
        {
            MessageBox.Show(ex.ToString(), "SharpDomain Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        [DllExport("LoadDomainSettings", CallingConvention.Cdecl)]
        public static void LoadDomainHostSettings(string loadDirectory, string applicationName, string applicationArguments)
        {
            ApplicationToHostDirectory = loadDirectory;
            ApplicationToHostName = applicationName;
            ApplicationArguments = applicationArguments;
        }
    }
}
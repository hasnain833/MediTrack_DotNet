using System;
using System.Threading;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace DChemist
{
    public static class Program
    {
        [DllImport("Microsoft.ui.xaml.dll")]
        private static extern void XamlCheckProcessRequirements();

        [STAThread]
        static void Main(string[] args)
        {
            using var mutex = new Mutex(true, "DChemist_Global_App_Mutex", out bool isNew);

            if (!isNew)
            {
                var currentProcess = Process.GetCurrentProcess();
                var otherProcesses = Process.GetProcessesByName(currentProcess.ProcessName);

                foreach (var other in otherProcesses)
                {
                    if (other.Id != currentProcess.Id)
                    {
                        try 
                        { 
                            other.Kill(); 
                            other.WaitForExit(3000); 
                        } 
                        catch { /* Already exiting or access denied */ }
                    }
                }
            }

            XamlCheckProcessRequirements();
            Application.Start((p) =>
            {
                var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                new App();
            });
        }
    }
}

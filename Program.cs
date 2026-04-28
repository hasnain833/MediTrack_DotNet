using System;
using System.Threading;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System.Runtime.InteropServices;

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
                // Safe single-instance behavior:
                // do not kill running process; just exit this new instance.
                return;
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

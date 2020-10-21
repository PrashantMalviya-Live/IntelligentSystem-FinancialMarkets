using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.ServiceProcess;
using GlobalCore;
using GlobalLayer;
using System.Security.Principal;
using System.Security.Permissions;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using System.Runtime.ConstrainedExecution;
using System.Security;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace MarketView.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class WSController : ControllerBase
    {
        private const string mdservice = "MarketDataService";
        SafeAccessTokenHandle safeAccessTokenHandle;

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool LogonUser(String lpszUsername, String lpszDomain, String lpszPassword,
        int dwLogonType, int dwLogonProvider, out SafeAccessTokenHandle safeAccessTokenHandle);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public extern static bool CloseHandle(IntPtr handle);

        // POST api/<WSController>
        [HttpPost]
        public IActionResult MarketDataService([FromBody] ServiceActionParams start)
        {
            try
            {
                const int LOGON32_PROVIDER_DEFAULT = 0;
                //This parameter causes LogonUser to create a primary token.
                const int LOGON32_LOGON_INTERACTIVE = 2;
                const int LOGON32_LOGON_BATCH = 4;
                

                // Call LogonUser to obtain a handle to an access token.
                bool returnValue = LogonUser("zm", "desktop-eij2i36", "zm",
                    LOGON32_LOGON_INTERACTIVE, LOGON32_PROVIDER_DEFAULT,
                    out safeAccessTokenHandle);

                WindowsIdentity.RunImpersonated(safeAccessTokenHandle, () =>
                {
                    var impersonatedUser = WindowsIdentity.GetCurrent().Name;

                    OkObjectResult result;
                    if (start.start)
                    {
                        StartService(mdservice, 2000);
                    }
                    else
                    {
                        StopService(mdservice, 2000);
                    }
                });
            }
            catch (Exception ex)
            {

            }
            return new OkObjectResult(new { message = "200 OK" });
        }
        //public void ControlService(string host, string username, string password, string name, string action)
        //{
        //    var credentials = new UserCredential(username);
        //    Impersonation.RunAsUser(credentials, SimpleImpersonation.LogonType.Interactive, () =>
        //    {
        //        ServiceControllerPermission scp = new ServiceControllerPermission(ServiceControllerPermissionAccess.Control, host, name);
        //        scp.Assert();

        //        ServiceController sc = new ServiceController(name, host);
        //        TimeSpan timeout = new TimeSpan(0, 0, 30);
        //        switch (action)
        //        {
        //            case "start":
        //                sc.Start();
        //                sc.WaitForStatus(ServiceControllerStatus.Running, timeout);
        //                break;
        //            case "stop":
        //                sc.Stop();
        //                sc.WaitForStatus(ServiceControllerStatus.Stopped, timeout);
        //                break;
        //            default:
        //                string msg = String.Format("Unknown action: '{0}'", action);
        //                throw new Exception(msg);
        //        }
        //    });
        //}


        public static void StartService(string serviceName, int timeoutMilliseconds)
        {
            ServiceControllerPermission scp = new ServiceControllerPermission(ServiceControllerPermissionAccess.Control, "desktop-eij2i36", mdservice);
            scp.Assert();

            ServiceController service = new ServiceController(serviceName, "desktop-eij2i36");

            try
            {
                TimeSpan timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds);

                service.Start();
                service.WaitForStatus(ServiceControllerStatus.Running, timeout);
            }
            catch (Exception ex)
            {
                Logger.LogWrite("Issue with starting market data service");
                Logger.LogWrite(ex.StackTrace);
            }
        }
        public static void StopService(string serviceName, int timeoutMilliseconds)
        {
            ServiceController service = new ServiceController(serviceName);
            try
            {
                TimeSpan timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds);

                service.Stop();
                service.WaitForStatus(ServiceControllerStatus.Stopped, timeout);
            }
            catch (Exception ex)
            {
                Logger.LogWrite("Issue with stopping market data service");
                Logger.LogWrite(ex.StackTrace);
            }
        }
        public class ServiceActionParams
        {
            public bool start { get; set; }
        }
    }
public sealed class SafeTokenHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    private SafeTokenHandle()
        : base(true)
    {
    }

    [DllImport("kernel32.dll")]
    [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
    [SuppressUnmanagedCodeSecurity]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    protected override bool ReleaseHandle()
    {
        return CloseHandle(handle);
    }
}
   
}

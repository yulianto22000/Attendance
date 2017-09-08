using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Topshelf;

namespace KeicoService
{
    class Program
    {
        static void Main(string[] args)
        {
            Program.Devices = new System.Collections.Concurrent.ConcurrentDictionary<int, Device>();
            // change from service account's dir to more logical one
            //System.Net.ServicePointManager.Expect100Continue = false; //add date 23 aug 2017

            var host = HostFactory.New(x =>
            {
                x.RunAsLocalSystem();
                x.SetDescription("Attendance machine data collector service");
                x.SetDisplayName("Attendance Machine Service");
                x.SetServiceName("AttendanceService");
                //x.SetDescription(Configuration.ServiceDescription);
                //x.SetDisplayName(Configuration.ServiceDisplayName);
                //x.SetServiceName(Configuration.ServiceName);

                x.Service<ReaderService>();
                x.StartAutomatically();
                
                
            });
            host.Run();
            
        }

        

        public static System.Collections.Concurrent.ConcurrentDictionary<int, Device> Devices
        {
            get;
            set;
        }
    }
}

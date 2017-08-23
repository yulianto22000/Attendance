using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KeicoService
{
    public class Device
    {
        private string status;
        public string IPAddress { get; set; }
        public int Port { get; set; }
        public string PortName { get; set; }
        public int BaudRate { get; set; }
        public int NID { get; set; }
        public string Place { get; set; }
        public int Password { get; set; }
        public string Type { get; set; }
        public byte TerminalGroup { get; set; }
        public bool IsOffline { get; set; }
        public bool IsInprogress { get; set; }
        public string Status {
            get
            {
                return status;
            }
            set
            {
                status = value;
                StatusDate = DateTime.Now;
            }
        }
        public DateTime StatusDate { get; private set; }
        public string ToConnectionString()
        {
            if (!string.IsNullOrEmpty(IPAddress))
            {
                return string.Format("IPAddress={0};Port={1};NID={2};Password={3}", IPAddress, Port, NID, Password);
            }
            else if (!string.IsNullOrEmpty(PortName))
            {
                return string.Format("PortName={0};BaudRate={1};NID={2};Password={3}", PortName, BaudRate, NID, Password);
            }
            else
            {
                return "";
            }
        }
    }
}

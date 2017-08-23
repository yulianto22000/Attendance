using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KeicoService
{
    interface IDevice : IDisposable
    {
        bool Connect(string connectionString);
        void Disconnect();
        IEnumerable<AttendanceData> GetAttendanceData();
        IEnumerable<AttendanceData> GetAttendanceData(bool allData);
        IEnumerable<UserData> GetUsers();
    }
}

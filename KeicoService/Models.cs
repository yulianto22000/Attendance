using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KeicoService
{
    public enum UserType
    {
        Unregistered = 0,
        Registered = 1
    }
    [Flags]
    public enum SensorType
    {
        Other = 0,
        Keypad = 1,
        Card = 2,
        Fingerprint = 4
    }

    public enum Mode
    {
        AnyMode = 0,
        FingerPrint = 1,
        CardOrFingerPrint = 2,
        IdAndFingerPrintOrCard = 3,
        IdAndFingerPrintOrIdAndCard = 4,
        IdAndFingerPrintOrCardAndFingerPrint = 5,
        Open = 6,
        Close = 7,
        Card = 8,
        IdOrFingerPrint = 9,
        IdOrCard = 10,
        IdAndCard = 11,
        CardAndFingerPrint = 12,
        IdAndFingerPrint = 13,
        IdAndCardAndFingerPrint = 14
    }

    public enum FunctionKey
    {
        F1 = 0,
        F2 = 1,
        F3 = 2,
        F4 = 3,
        None = 4
    }

    public class AttendanceData
    {
        public int UserID { get; set; }
        public DateTime DateTime { get; set; }
        public UserType UserType { get; set; }
        public SensorType SensorType { get; set; }
        public FunctionKey FunctionKey { get; set; }
        public int FunctionNumber { get; set; }
        public Mode Mode { get; set; }
    }
    public class UserData
    {
        public int UserID { get; set; }
        public UserLevel UserLevel { get; set; }
        public UserSensor UserSensor { get; set; }
        public int CardID { get; set; }
        public byte[] EnrollData { get; set; }
    }
    public enum UserLevel
    {
        User = 0,
        Master = 1
    }
    [Flags]
    public enum UserSensor
    {
        FingerPrint1 = 1,
        FingerPrint2 = 2,
        Card = 8
    }
}

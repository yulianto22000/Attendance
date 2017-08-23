using System;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using Topshelf;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.Configuration;
using Common.Logging;




namespace KeicoService
{
    public class ReaderService: ServiceControl
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(ReaderService));
        public bool Start(HostControl hostControl)
        {
            string strNameMsn;
            double dblGLOG = 0;
            var devices = new List<Device>();
            var connectionStringhrportal = System.Configuration.ConfigurationManager.ConnectionStrings["HR-Portal"].ConnectionString;            
            var connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["Attendance"].ConnectionString;
           
            using (var connection = new SqlConnection(connectionString))
            {
                logger.Info("Open Coonection with Database For Get Hardware");
                connection.Open();
                using (var command = new SqlCommand("SELECT [IPAddress], [IPPort], [NID],[place] FROM [dbo].[HardwareKeicoSF3] WITH (NOLOCK)", connection))
                {
                    var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var config = new Device()
                        {
                            IPAddress = reader.GetString(0),
                            Port = int.Parse(reader.GetString(1)),
                            NID = int.Parse(reader.GetString(2)),
                            Password = 0
                        };
                        devices.Add(config);
                        logger.Info("Device ADD Config");
                    }
                }
                connection.Close();
                logger.Info("Connection Close");
            }

            
            //var devices = Program.Devices;
            //devices[1] = new Device() { IPAddress = "192.168.1.12", Port = 5005, NID = 1, Password = 0 };
            //devices[2] = new Device() { IPAddress = "192.168.1.13", Port = 5005, NID = 1, Password = 0 };
            
            System.Collections.Concurrent.ConcurrentQueue<NetworkDevice> queue = new System.Collections.Concurrent.ConcurrentQueue<NetworkDevice>();
            foreach (var dev in devices)
            {
                queue.Enqueue(new NetworkDevice(dev.NID, dev.NID) { IPAddress = dev.IPAddress, Port = dev.Port });
            }

           
            Task.Factory.StartNew(() =>
            {
                while (true)
                {
                    Task.Factory.StartNew(() =>
                    {
                        NetworkDevice device;
                        if (queue.TryDequeue(out device))
                        {
                            if (!device.IsConnected)
                            {
                                Console.WriteLine("Connecting " + device.IPAddress);
                                //logger.Info("Connecting " + device.IPAddress);
                                device.Connect();
                            }
                            if (device.IsConnected)
                            {
                                Console.WriteLine("Connected " + device.IPAddress);
                                var root = AppDomain.CurrentDomain.BaseDirectory;
                                var glogDir = System.IO.Path.Combine(root, "GLOG");
                                var temporaryFilePath = System.IO.Path.Combine(glogDir, string.Format("~GLOG_{0:000}.txt", device.NID));
                                var today = DateTime.Today;
                                var path = System.IO.Path.Combine(glogDir, today.ToString("yyyyMMdd"));
                                if (!System.IO.Directory.Exists(path))
                                {
                                    System.IO.Directory.CreateDirectory(path);
                                }
                                // device connected
                                // get unread attendance data
                                var data = device.GetAttendanceData();
                               /* if (data != null)
                                {
                                    

                                    foreach (var dat in data)
                                    {
                                        Console.WriteLine(string.Format("{0} {1:yyyy-MM-dd H:mm:ss}", dat.UserID, dat.DateTime));
                                        //lakukan simpan ke database

                                    }
                                }*/
                                
                                if ((data != null && data.Count() > 0) || System.IO.File.Exists(temporaryFilePath))
                                {
                                    if (data == null) // true when temporary glog is exists
                                    {
                                        data = new List<AttendanceData>(); // create object instance to avoid object reference not set exception
                                    }
                                    var filePath = System.IO.Path.Combine(path, string.Format("GLOG_{0:000}_{1:yyyy-MM-dd}.txt", device.NID, DateTime.Today));
                                    // read and write from and to temporary ~GLOG file
                                    int firstIndex;
                                    IEnumerable<AttendanceData> existingData = null;
                                    // open ~GLOG file without lock it or create new one if does not exist
                                    using (System.IO.FileStream fs = System.IO.File.Open(temporaryFilePath, System.IO.FileMode.OpenOrCreate, System.IO.FileAccess.ReadWrite, System.IO.FileShare.ReadWrite))
                                    {
                                        // read unsaved data from ~GLOG file if any
                                        using (System.IO.StreamReader sr = new System.IO.StreamReader(fs))
                                        {
                                            // parse GLOG file to List of AttendanceData object
                                            var allText = sr.ReadToEnd();
                                            if (!string.IsNullOrEmpty(allText))
                                            {
                                                existingData = (
                                                    from d in
                                                        from c in allText.Split(new char[] { '\n' }) where !string.IsNullOrEmpty(c) select c.Split(new char[] { '\t' })
                                                    select new AttendanceData() { UserID = int.Parse(d[1]), UserType = (UserType)int.Parse(d[3]), SensorType = (SensorType)int.Parse(d[4]), Mode = (Mode)int.Parse(d[5]), DateTime = DateTime.Parse(d[8], System.Globalization.CultureInfo.InvariantCulture) }
                                                ).ToList();
                                            }
                                        }
                                        // read line count
                                        firstIndex = existingData != null ? existingData.Count() : 0;
                                    }
                                    // append data to existing ~GLOG file
                                    using (System.IO.FileStream fs = System.IO.File.Open(temporaryFilePath, System.IO.FileMode.Append, System.IO.FileAccess.Write, System.IO.FileShare.ReadWrite))
                                    {
                                        foreach (var line in data.Select((c, index) => string.Format("{0}\t{1}\t\t{2}\t{3}\t{4}\t{5}\t{6}\t{7:yyyy-MM-dd HH:mm:ss}\r\n", firstIndex + index + 1, c.UserID, (int)c.UserType, (int)c.SensorType, (int)c.Mode, ((int)c.FunctionKey * 10) + c.FunctionNumber, 0, c.DateTime)))
                                        {
                                            fs.Write(System.Text.UTF8Encoding.ASCII.GetBytes(line), 0, line.Count());
                                        }
                                        fs.Flush();
                                        fs.Close();
                                    }
                                    // write to GLOG
                                    int lastSequence = 0;
                                    using (System.IO.FileStream fs = System.IO.File.Open(filePath, System.IO.FileMode.OpenOrCreate, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite))
                                    {
                                        using (System.IO.StreamReader sr = new System.IO.StreamReader(fs))
                                        {
                                            string line;
                                            while (!string.IsNullOrEmpty(line = sr.ReadLine()))
                                            {
                                                int.TryParse(line.Split(new char[] { '\t' }).FirstOrDefault(), out lastSequence);
                                            }
                                        }
                                    }
                                    using (System.IO.FileStream fs = System.IO.File.Open(filePath, System.IO.FileMode.Append, System.IO.FileAccess.Write, System.IO.FileShare.ReadWrite))
                                    {
                                        foreach (var line in data.Select((c, index) => string.Format("{0}\t{1}\t\t{2}\t{3}\t{4}\t{5}\t{6}\t{7:yyyy-MM-dd HH:mm:ss}\r\n", lastSequence + index + 1, c.UserID, (int)c.UserType, (int)c.SensorType, (int)c.Mode, ((int)c.FunctionKey * 10) + c.FunctionNumber, 0, c.DateTime)))
                                        {
                                            fs.Write(System.Text.UTF8Encoding.ASCII.GetBytes(line), 0, line.Count());
                                        }
                                        fs.Flush();
                                        fs.Close();
                                    }

                                    
                                    // if there is unsaved data from ~GLOG file, combine unsaved data with new data from machine                                                                      
                                    var dataTobeSaved = existingData != null && existingData.Count() > 0 ? existingData.Union(data) : data;
                                    foreach (var row in dataTobeSaved)
                                    {
                                        //get mesin name                                         
                                        using (var connection = new SqlConnection(connectionString))
                                        {
                                           
                                            connection.Open();
                                            strNameMsn = "0001";
                                            using (var command = new SqlCommand("SELECT [NID],[place] FROM [HardwareKeicoSF3] where nid='" + device.NID + "'", connection))
                                            {
                                                var reader = command.ExecuteReader();
                                                while (reader.Read())
                                                {
                                                    strNameMsn = string.Format("{0:0000}", reader.GetString(1));
                                                }
                                            }
                                            connection.Close();
                                           
                                        }
                                        //end get mesin name

                                        //save to database
                                        if (row.UserID > 0) //nilai -1 (Access denied tidak diambil)
                                        {
                                            dblGLOG++;
                                            using (var connectionhr = new SqlConnection(connectionStringhrportal))
                                            {
                                                logger.Info("Connection Open DB Portal and save to Table");
                                                connectionhr.Open();
                                                var xuserid = row.UserID > 0 ? row.UserID.ToString("0000000") : row.UserID.ToString();
                                                using (var command = new SqlCommand("INSERT INTO trans_R(seq_no,EL5K_No,Dev_Type,Dev_id,tr_date,tr_time,tr_data,tr_code,extra,tr_user,staff_number) VALUES('0','" + strNameMsn + "','R','" + device.NID.ToString("00") + "','" + String.Format("{0:yyyyMMdd}", row.DateTime.Date) + "','" + string.Format("{0:00}",row.DateTime.Hour) + string.Format("{0:00}",row.DateTime.Minute) + string.Format("{0:00}",row.DateTime.Second) + "','','0','0000','','" + xuserid + "')", connectionhr))
                                                command.ExecuteNonQuery();
                                                connectionhr.Close();
                                            }
                                        }
                                        //end save db
                                    }
                                    // delete temporary GLOG
                                    if (System.IO.File.Exists(temporaryFilePath))
                                    {
                                        System.IO.File.Delete(temporaryFilePath);
                                    }
                                  
                                    
                                }
                            }
                            
                            queue.Enqueue(device);
                        }
                    });
                    //// set parallel option based on number of CPU and configuration setting.
                    //ParallelOptions options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 4 };
                    //Parallel.ForEach(devices, options, dev =>
                    //{
                    //    // check if device is still in process
                    //    if (!dev.Value.IsInprogress)
                    //    {
                    //        try
                    //        {
                    //            Program.Devices[dev.Value.NID].IsInprogress = true;
                    //            // initialize new device instance
                    //            using (var device = new NetworkDevice())
                    //            {
                    //                if (!device.IsConnected)
                    //                {
                    //                    device.Connect();
                    //                }
                    //                // connecting to device
                    //                if (device.IsConnected)
                    //                {
                    //                    // device connected
                    //                    dev.Value.IsOffline = false;
                    //                    // get unread attendance data
                    //                    var data = device.GetAttendanceData();
                    //                    if (data != null)
                    //                    {
                    //                        foreach (var dat in data)
                    //                        {
                    //                            Console.WriteLine(string.Format("{0} {1:yyyy-MM-dd H:mm:ss}", dat.UserID, dat.DateTime));
                    //                        }
                    //                    }
                    //                }
                    //                else
                    //                {
                    //                    dev.Value.IsOffline = true;
                    //                }
                    //            }
                    //        }
                    //        catch (System.IO.IOException ioException)
                    //        {
                    //        }
                    //        catch (Exception ex)
                    //        {
                    //        }
                    //        Program.Devices[dev.Value.NID].IsInprogress = false;
                    //    }
                    //});

                    Thread.Sleep(100);
                }
            }, TaskCreationOptions.LongRunning);
            return true;
        }

        public bool Stop(HostControl hostControl)
        {
            throw new NotImplementedException();
        }

        public SqlConnection connection { get; set; }
    }
}

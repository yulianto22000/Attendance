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
using System.IO;


namespace KeicoService
{
    public class ReaderService: ServiceControl
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(ReaderService));
        string strNameMsn = "0000";
        double dblGLOG = 0;
       
        public bool Start(HostControl hostControl)
        {
           
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
                            PlaceName=reader.GetString(3),
                            Password = 0
                        };
                        devices.Add(config);
                        logger.Info("Device ADD Config");
                    }
                }
                
                connection.Close();
                logger.Info("Connection Close");
            }

         
            
            System.Collections.Concurrent.ConcurrentQueue<NetworkDevice> queue = new System.Collections.Concurrent.ConcurrentQueue<NetworkDevice>();
            foreach (var dev in devices)
            {
                queue.Enqueue(new NetworkDevice(dev.NID, dev.NID,dev.PlaceName) { IPAddress = dev.IPAddress, Port = dev.Port });
            }


            Task.Factory.StartNew(async () =>
            {
                while (true)
                {
                    Task.Factory.StartNew( async () =>
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
                            if (device.IsConnected || !device.IsConnected) //untuk load test
                            //if (device.IsConnected)
                            {
                                Console.WriteLine("Connected " + device.IPAddress);
                                Console.WriteLine("ID : " + device.NID);
                                
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
                                        
                                        strNameMsn = string.Format("{0:0000}", device.PlaceName);
                                        //save to database
                                        if (row.UserID > 0) //nilai -1 (Access denied tidak diambil)
                                        {
                                            dblGLOG++;
                                            using (var connectionhr = new SqlConnection(connectionStringhrportal))
                                            {
                                                logger.Info("Connection Open DB Portal and save to Table");
                                                connectionhr.Open();
                                                var xuserid = row.UserID > 0 ? row.UserID.ToString("0000000") : row.UserID.ToString();
                                                using (var command = new SqlCommand("INSERT INTO trans_R(seq_no,EL5K_No,Dev_Type,Dev_id,tr_date,tr_time,tr_data,tr_code,extra,tr_user,staff_number) VALUES('0','" + strNameMsn + "','R','" + device.NID.ToString("00") + "','" + String.Format("{0:yyyyMMdd}", row.DateTime.Date) + "','" + string.Format("{0:00}", row.DateTime.Hour) + string.Format("{0:00}", row.DateTime.Minute) + string.Format("{0:00}", row.DateTime.Second) + "','','0','0000','','" + xuserid + "')", connectionhr))
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

                            //lakukan simpan status koneksi mesin ke dalam database                           
                            string strStatusLast = "";
                            string stsKon = "";
                            //status koneksi
                            if (!device.IsConnected)
                            {
                                stsKon = "OFFLINE";
                            }
                            else
                            {
                                stsKon = "ONLINE";
                            }

                            using (var connection = new SqlConnection(connectionString))
                            {
                                connection.Open();
                                using (var command = new SqlCommand("select top(1) * from status_machine where nid='" + device.NID + "' and CAST(FLOOR(CAST(updated AS float)) AS datetime)='" + string.Format("{0:yyyy-MM-dd}", DateTime.Now) + "' order by updated desc", connection))
                                {
                                    var reader = command.ExecuteReader();
                                    while (reader.Read())
                                    {
                                        strStatusLast = reader.GetString(3);                                    
                                    }
                                    reader.Close();
                                }
                                
                                if (strStatusLast != stsKon)
                                {
                                    SqlCommand myCommand = new SqlCommand("INSERT INTO status_machine(nid,ipaddr,namemch,statusmch,updated) VALUES('" + device.NID + "','" + device.IPAddress + "','" + device.PlaceName + "','" + stsKon + "','" + string.Format("{0:yyyy-MM-dd HH:mm:ss.fff}", DateTime.Now) + "')", connection);                                    
                                    myCommand.ExecuteNonQuery();                                    
                                }
                                
                                connection.Close();
                            }
                            queue.Enqueue(device);
                           // await Task.Delay(50);
                        }

                        string logdir = System.Configuration.ConfigurationManager.AppSettings["logDir"];
                        var root1 = AppDomain.CurrentDomain.BaseDirectory;
                        var glogDir1 = System.IO.Path.Combine(root1, "Logger");
                        var temporaryFilePath1 = System.IO.Path.Combine(glogDir1, string.Format("Logger.txt"));
                        var today1 = DateTime.Now;
                        var path1 = System.IO.Path.Combine(glogDir1, today1.ToString("yyyyMMdd"));
                       /* using (System.IO.FileStream fs = new FileStream(temporaryFilePath1, System.IO.FileMode.OpenOrCreate, System.IO.FileAccess.ReadWrite, System.IO.FileShare.ReadWrite))
                        {
                            var sw = new StreamWriter(fs);
                            sw.WriteLine("Start Read break_cd in log dir " + logdir + "break_cd.txt "  + today1);
                            if (File.Exists(logdir + "break_cd.txt"))
                            {
                                sw.WriteLine("Filename exist break_cd log dir " + logdir + "break_cd.txt " + today1);
                            }
                            else
                            {
                                sw.WriteLine("Filename not exist break_cd log dir " + logdir + "break_cd.txt " + today1);
                            }
                            sw.Close();
                            fs.Close();
                        }
                        */
                        //pengecekkan jika ada yang meminta untuk berhenti dulu
                        //ini dilakukan jika clent akan melakukan managment user
                        if (File.Exists(logdir + "break_cd.txt"))
                        {
                            using (System.IO.FileStream fs = new FileStream(temporaryFilePath1, System.IO.FileMode.OpenOrCreate, System.IO.FileAccess.ReadWrite, System.IO.FileShare.ReadWrite))
                            {
                                var sw = new StreamWriter(fs);
                                sw.WriteLine("break_cd.txt exist and create break_cd_do.txt" + today1);
                                sw.Close();
                                fs.Close();
                            }

                            using (System.IO.FileStream fs = new FileStream(logdir + "break_cd_do.txt", System.IO.FileMode.OpenOrCreate, System.IO.FileAccess.ReadWrite, System.IO.FileShare.ReadWrite))
                            {
                                var sw = new StreamWriter(fs);
                                sw.WriteLine("do!");
                                sw.Close();
                                fs.Close();
                            }


                            using (System.IO.FileStream fs = new FileStream(temporaryFilePath1, System.IO.FileMode.OpenOrCreate, System.IO.FileAccess.ReadWrite, System.IO.FileShare.ReadWrite))
                            {
                                var sw = new StreamWriter(fs);
                                sw.WriteLine("break_cd.txt exist");
                                sw.Close();
                                fs.Close();
                            }

                            while (File.Exists(logdir + "break_cd_do.txt"))
                            {
                                if (File.Exists(logdir + "break_cd.txt"))//karena terkadang file ini sudah tidak ada tetapi break_cd_do masih ada
                                {
                                    Console.WriteLine("Stop Data Collection");
                                    using (System.IO.FileStream fs = new FileStream(temporaryFilePath1, System.IO.FileMode.OpenOrCreate, System.IO.FileAccess.ReadWrite, System.IO.FileShare.ReadWrite))
                                    {
                                        var sw = new StreamWriter(fs);
                                        sw.WriteLine("Stop data collection " + today1);
                                        sw.Close();
                                        fs.Close();
                                    }
                                }
                            }
                        }                       
                        //end stop break
                    });
                    Thread.Sleep(20);                    
                    await Task.Delay(20);
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

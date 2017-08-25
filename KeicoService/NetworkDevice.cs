using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Collections.Generic;
using System;

namespace KeicoService
{
    public class NetworkDevice : IDevice
    {
        private System.Net.Sockets.TcpClient tcpClient;
        // private bool connected;

        private string ipAddress;
        private int port;

        private const int START_DWORD = 0xaa55;
        private const int END_DWORD = 0x1979;

        //private static readonly log4net.ILog log = log4net.LogManager.GetLogger
        //        (System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public bool IsConnected
        {
            get
            {
                return (this.tcpClient != null && this.tcpClient.Connected);
            }
        }
        public NetworkDevice()
        {
        }
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="deviceID">Device ID as know as NID</param>
        public NetworkDevice(int NID, int password)
        {
            //log.Debug("Object " + this.GetHashCode() + " created");
            this.tcpClient = new System.Net.Sockets.TcpClient();
            this.NID = NID;
            this.Password = password;
        }
        public int NID { get; set; }
        public int Password { get; set; }
        public string IPAddress
        {
            get
            {
                return this.ipAddress;
            }
            set
            {
                this.ipAddress = value;
            }
        }
        public int Port
        {
            get { return this.port; }
            set { this.port = value; }
        }
        public bool Connect()
        {
            return this.Connect(System.Net.IPAddress.Parse(this.ipAddress), this.port);
        }
        public bool Connect(IPAddress address, int port)
        {
            //log.Debug("Ping " + address.ToString());
            if (PingHost(address))
            {
				this.tcpClient = new System.Net.Sockets.TcpClient();
                //log.Debug("Replied from " + address.ToString());
                // Don't allow another socket to bind to this port.
                //tcpClient.ExclusiveAddressUse = true;
                
                // Allow another socket to bind to this port.
                tcpClient.ExclusiveAddressUse = false;

                // The socket will linger for 1 seconds after  
                // Socket.Close is called.
                tcpClient.LingerState = new LingerOption(true, 1);

                // Disable the Nagle Algorithm for this tcp socket.
                tcpClient.NoDelay = true;

                // Set the receive buffer size to 8k
                tcpClient.ReceiveBufferSize = 8192;

                // Set the timeout for synchronous receive methods to  
                // 1 second (5000 milliseconds.)
                tcpClient.ReceiveTimeout = 5000;

                // Set the send buffer size to 8k.
                tcpClient.SendBufferSize = 16000; // 8192;

                // Set the timeout for synchronous send methods 
                // to 1 second (5000 milliseconds.)			
                tcpClient.SendTimeout = 5000;

                // Set the Time To Live (TTL) to 42 router hops.
                //tcpClient.Ttl = 42;
                
                //log.Debug("Connecting socket to " + address.ToString() + ":" + port.ToString());
                try
                {
                    tcpClient.Connect(address, port);
                }
                catch (System.Net.Sockets.SocketException socketException)
                {
                    if (socketException.ErrorCode == 10061) // No connection could be made because the target computer actively refused it
                    {
                        //Program.Devices[this.NID].Status = "Invalid IP address or port number";
                        //log.Info(socketException.Message);
                        return false;
                    }
                    else
                    {
                        //log.Error(socketException);
                    }
                    return false;
                }
                catch (Exception ex)
                {
                    //log.Error(ex);
                    return false;
                }
                if (tcpClient.Connected)
                {
                    //log.Debug("Connected socket to " + address.ToString() + ":" + port.ToString());
                    int command = 0x0052;
                    Stream stream = tcpClient.GetStream();
                    byte[] buffer = new byte[16];// { 0x55, 0xaa, 0, 0, 0x79, 0x19, 0x52, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
                    buffer[0] = (byte)(START_DWORD & 0xff);
                    buffer[1] = (byte)(START_DWORD >> 8);

                    buffer[2] = (byte)(NID & 0xff);
                    buffer[3] = (byte)(NID >> 8);

                    buffer[4] = (byte)(END_DWORD & 0xff);
                    buffer[5] = (byte)(END_DWORD >> 8);

                    buffer[6] = (byte)(command & 0xff);
                    buffer[7] = (byte)(command >> 8);

                    buffer[8] = (byte)(Password & 0xff);
                    buffer[9] = (byte)(Password >> 8);
                    CalculateChecksum(ref buffer);

                    byte[] outputBuffer1 = new byte[8];
                    byte[] outputBuffer2 = new byte[14];
                    int bytesRead = 0;
                    //log.Debug("Send handshake command");
                    stream.Write(buffer, 0, buffer.Length);
                    try
                    {
                        bytesRead = stream.Read(outputBuffer1, 0, outputBuffer1.Length);
                        if (bytesRead < outputBuffer1.Length)
                        {
                            //log.Error(string.Format("Response bytes less than requested: {0}/{1}", bytesRead, outputBuffer1.Length));
                        }
                        //log.Debug("Handshake command replied");
                        int connectionStatus = outputBuffer1[4] + ((int)outputBuffer1[5] << 8);
                        int responseNID = outputBuffer1[2] + ((int)outputBuffer1[3] << 8);
                        if (NID == responseNID && connectionStatus == 1)
                        {
                            bytesRead = stream.Read(outputBuffer2, 0, outputBuffer2.Length);
                            if (bytesRead < outputBuffer1.Length)
                            {
                                //log.Error(string.Format("Response bytes less than requested: {0}/{1}", bytesRead, outputBuffer2.Length));
                            }
                            //log.Debug("Handshake command validated");
                            if (outputBuffer2[0] == buffer[1] && outputBuffer2[1] == buffer[0] && outputBuffer2[2] == buffer[2] && outputBuffer2[3] == buffer[3])
                            {
                                //log.Debug("Connection established");
                                return true;
                            }
                        }
                        else
                        {
                            if (NID != responseNID)
                            {
                                //log.Error("Invalid NID of device with IP address " + address + ". Please change NID on configuration file from " + NID + " to " + responseNID);
                            }
                        }
                    }
                    catch (System.Net.Sockets.SocketException)
                    {
						tcpClient.Close();
                        //log.Error("Error when connecting to device " + address.ToString() + ":" + port.ToString());
                        return false;
                    }
                    catch (IOException)
                    {
						tcpClient.Close();
                        //log.Error("Error when connecting to device " + address.ToString() + ":" + port.ToString());
                        return false;
                    }
                    catch (Exception ex)
                    {
						tcpClient.Close();
                        //log.Error("Error when connecting to device " + address.ToString() + ":" + port.ToString(), ex);
                        return false;
                    }
                }
            }
            //Program.Devices[this.NID].Status = "Destination host unreachable";
            //log.Debug("Destination host [" + address.ToString() + "] unreachable");
            return false;
        }

        public bool Connect(string connectionString)
        {
            int nID;
            int password;
            nID = this.NID;
            password = this.Password;
            var settings = connectionString.Split(new char[] { ';' });
            foreach (var setting in settings)
            {
                var kv = setting.Split(new char[] { '=' });
                if (kv.Length == 2)
                {
                    switch (kv[0].Trim().ToUpper())
                    {
                        case "IPADDRESS":
                            ipAddress = kv[1].Trim().ToUpper();
                            break;
                        case "PORT":
                            int.TryParse(kv[1].Trim(), out port);
                            break;
                        case "NID":
                            if (int.TryParse(kv[1].Trim(), out nID))
                            {
                                this.NID = nID;
                            }
                            break;
                        case "PASSWORD":
                            if (int.TryParse(kv[1].Trim(), out password))
                            {
                                this.Password = password;
                            }
                            break;
                    }
                }
            }
            return Connect(System.Net.IPAddress.Parse(ipAddress), port);
        }

        public void Disconnect()
        {
            if (this.tcpClient.Connected)
            {
                //log.Debug("Disconnecting object " + this.GetHashCode());
                this.tcpClient.Close();
            }
        }

        public string GetProductCode()
        {
            if (!tcpClient.Connected)
            {
                // TODO: throw exception
                return null;
            }
            int command = 0x0114;
            Stream stream = tcpClient.GetStream();
            byte[] buffer = new byte[16];// { 0x55, 0xaa, 0, 0, 0x79, 0x19, 0x14, 0x01, 0, 0, 0, 0, 0, 0, 0, 0 };

            buffer[0] = (byte)(START_DWORD & 0xff);
            buffer[1] = (byte)(START_DWORD >> 8);

            buffer[2] = (byte)(NID & 0xff);
            buffer[3] = (byte)(NID >> 8);

            buffer[4] = (byte)(END_DWORD & 0xff);
            buffer[5] = (byte)(END_DWORD >> 8);

            buffer[6] = (byte)(command & 0xff);
            buffer[7] = (byte)(command >> 8);

            buffer[8] = (byte)(Password & 0xff);
            buffer[9] = (byte)(Password >> 8);

            CalculateChecksum(ref buffer);
            byte[] outputBuffer1 = new byte[8];
            byte[] outputBuffer2 = new byte[14];
            byte[] outputBuffer = new byte[38];
            stream.Write(buffer, 0, buffer.Length);
            var task1 = System.Threading.Tasks.Task<int>.Factory.FromAsync(stream.BeginRead, stream.EndRead, outputBuffer1, 0, outputBuffer1.Length, null);
            task1.Wait();
            if (outputBuffer1[0] == 0x5a && outputBuffer1[1] == 0xa5 && outputBuffer1[2] == buffer[2] && outputBuffer1[3] == buffer[3] && outputBuffer1[4] == 0x1 && outputBuffer1[5] == 0x0)
            {
                int available = tcpClient.Available;
                if (available >= 14)
                {
                    var task2 = System.Threading.Tasks.Task<int>.Factory.FromAsync(stream.BeginRead, stream.EndRead, outputBuffer2, 0, outputBuffer2.Length, null);
                    task2.Wait();
                    if (outputBuffer2[0] == 0xaa && outputBuffer2[1] == 0x55 && outputBuffer2[2] == buffer[2] && outputBuffer2[3] == buffer[3] && outputBuffer2[4] == 0x0 && outputBuffer2[5] == 0x0 && outputBuffer2[6] == 0x1 && outputBuffer2[7] == 0x0 && outputBuffer2[8] == 0x0 && outputBuffer2[9] == 0x0 && outputBuffer2[10] == 0x0 && outputBuffer2[11] == 0x0)
                    {
                        if (tcpClient.Available >= 38)
                        {
                            var task3 = System.Threading.Tasks.Task<int>.Factory.FromAsync(stream.BeginRead, stream.EndRead, outputBuffer, 0, outputBuffer.Length, null);
                            task3.Wait();
                            return System.Text.Encoding.ASCII.GetString(outputBuffer, 4, outputBuffer.Length - 6).Trim(new char[] { '\x0' });
                        }
                    }
                }
            }
            return null;
        }

        public IEnumerable<AttendanceData> GetAttendanceData()
        {
            return GetAttendanceData(false);
        }
        public IEnumerable<AttendanceData> GetAttendanceData(bool getAllData)
        {
            bool isValid = true;
            //Program.Devices[this.NID].Status = "Getting attendance data";
            //log.Debug("Getting attendance data [" + this.NID + "]");
            if (!tcpClient.Connected)
            {
                // TODO: throw exception
                //Program.Devices[this.NID].Status = "TCP Client is not connected";
                return null;
            }
            if (getAllData)
            {
                if (GetAllDataCommand() == false)
                {
                    return null;
                }
            }
            
            int command = 0x010f;
            Stream stream = tcpClient.GetStream();
            
            byte[] buffer = new byte[16];// { 0x55, 0xaa, 0, 0, 0x79, 0x19, 0x0f, 0x01, 0, 0, 0, 0, 0, 0, 0, 0 };
            buffer[0] = (byte)(START_DWORD & 0xff);
            buffer[1] = (byte)(START_DWORD >> 8);

            buffer[2] = (byte)(NID & 0xff);
            buffer[3] = (byte)(NID >> 8);

            buffer[4] = (byte)(END_DWORD & 0xff);
            buffer[5] = (byte)(END_DWORD >> 8);

            buffer[6] = (byte)(command & 0xff);
            buffer[7] = (byte)(command >> 8);

            buffer[8] = (byte)(Password & 0xff);
            buffer[9] = (byte)(Password >> 8);

            CalculateChecksum(ref buffer);
            byte[] outputBuffer1 = new byte[8];
            byte[] outputBuffer2 = new byte[14];
            //log.Debug("Sending \"Get Data\" command [" + this.NID + "]");
            try {
				stream.Write(buffer, 0, buffer.Length);
				stream.Read(outputBuffer1, 0, outputBuffer1.Length);
				
				//log.Debug("Status package received [" + this.NID + "]");
				//log.Debug(BitConverter.ToString(outputBuffer1));
				int responseStatus = outputBuffer1[4] + ((int)outputBuffer1[5] << 8);
				int responseNID = outputBuffer1[2] + ((int)outputBuffer1[3] << 8);
				if (NID == responseNID && responseStatus == 1)
				{
					if (outputBuffer1[0] == 0x5a && outputBuffer1[1] == 0xa5 && outputBuffer1[2] == buffer[2] && outputBuffer1[3] == buffer[3] && outputBuffer1[4] == 0x1 && outputBuffer1[5] == 0x0)
					{
						stream.Read(outputBuffer2, 0, outputBuffer2.Length);
						//log.Debug("Header package received [" + this.NID + "]");
						//log.Debug(BitConverter.ToString(outputBuffer2));
						int count = BitConverter.ToInt32(outputBuffer2, 8);
						if (outputBuffer2[0] == 0xaa && outputBuffer2[1] == 0x55 && outputBuffer2[2] == buffer[2] && outputBuffer2[3] == buffer[3] && outputBuffer2[4] == 0x0 && outputBuffer2[5] == 0x0 && outputBuffer2[6] == 0x1 && outputBuffer2[7] == 0x0 && outputBuffer2[11] == 0x0)
						{
							//int count = outputBuffer2[8] + ((int)outputBuffer2[9] << 8) + ((int)outputBuffer2[10] << 16);
							int recordCount = count;
							List<AttendanceData> attandenceData = null;
							if (recordCount > 0)
							{
								attandenceData = new List<AttendanceData>();
							}
							while (count > 0)
							{
								int part = Math.Min(85, count);
								byte[] logDataBuffer = new byte[4 + (part * 12) + 2];
								stream.Read(logDataBuffer, 0, logDataBuffer.Length); // cannot read
								//log.Debug("Data package received [" + this.NID + "]");
								//log.Debug(BitConverter.ToString(logDataBuffer));
								if (!IsResponseValid(logDataBuffer))
								{
									//log.Error(string.Format("Invalid response data [{0}] {1}", this.NID, BitConverter.ToString(logDataBuffer)));
									isValid = false;
									if (getAllData)
									{
										System.Threading.Thread.Sleep(10);
										continue;
									}
									else
									{
										return null;
									}
								}
								for (int i = 0; i < part; i++)
								{
									//Program.Devices[this.NID].Status = string.Format("Processing attendance data part #{0}/{1}", (recordCount - count) + i + 1, recordCount);
									//log.Debug(string.Format("Processing attendance data #{0}/{1}, part #{2}/{3} [{4}]", (recordCount - count) + i + 1, recordCount, i + 1, part, this.NID));
									uint dateTimeTicks = 0;
									int userID = 0;
									int priv;
									int mode;
									int sensor;
									int fk;
									uint prop;
									dateTimeTicks = BitConverter.ToUInt32(logDataBuffer, 4 + (i * 12));
									userID = BitConverter.ToInt32(logDataBuffer, 8 + (i * 12));
									prop = BitConverter.ToUInt32(logDataBuffer, 12 + (i * 12));
									priv = (int)prop & 0x01;
									sensor = (int)(prop >> 1) & 0xff;
									mode = (int)(prop >> 9) & 0x0f;
									fk = (int)(prop >> 13) & 0xff;
									uint sec;
									uint min;
									uint hour;
									uint days;
									sec = dateTimeTicks % 60;
									dateTimeTicks /= 60;
									min = dateTimeTicks % 60;
									dateTimeTicks /= 60;
									hour = dateTimeTicks % 24;
									dateTimeTicks /= 24;
									days = dateTimeTicks;
									var dateTime = new DateTime(2000, 1, 1, (int)hour, (int)min, (int)sec).AddDays(days);
									var data = new AttendanceData
									{
										UserID = userID,
										DateTime = dateTime,
										UserType = (UserType)priv,
										SensorType = (SensorType)sensor,
										Mode = (Mode)mode,
										FunctionKey = (FunctionKey)(int)(fk / 10),
										FunctionNumber = (int)(fk % 10)
									};
									attandenceData.Add(data);
									//log.Debug(string.Format("{0}\t{1}\t\t{2}\t{3}\t{4}\t{5}\t{6}\t{7:yyyy-MM-dd HH:mm:ss}", (recordCount - count) + i + 1, data.UserID, (int)data.UserType, (int)data.SensorType, (int)data.Mode, ((int)data.FunctionKey * 10) + data.FunctionNumber, 0, data.DateTime));
								}
								count -= part;
								if (count > 0)
								{
									System.Threading.Thread.Sleep(7);
								}
								else
								{
									System.Threading.Thread.Sleep(1);
								}
							}
							if (recordCount > 0)
							{
								
								// delete data from device
								//log.Debug("Deleting data from device [" + this.NID + "]");
								byte[] deleteDataBuffer = new byte[] { 0x5a, 0xa5, 0, 0, 0x01, 0, 0, 0, 0, 0 };
								deleteDataBuffer[0] = outputBuffer1[0];
								deleteDataBuffer[1] = outputBuffer1[1];
								deleteDataBuffer[2] = outputBuffer1[2];
								deleteDataBuffer[3] = outputBuffer1[3];
								deleteDataBuffer[4] = outputBuffer2[8]; //(byte)(recordCount & 0xff);
								deleteDataBuffer[5] = outputBuffer2[9]; //(byte)(recordCount >> 8);
								deleteDataBuffer[6] = outputBuffer2[10]; //(byte)(recordCount >> 8);
								deleteDataBuffer[7] = outputBuffer2[11]; //(byte)(recordCount >> 8);

								CalculateChecksum(ref deleteDataBuffer);
								//log.Debug("Stream length = " + stream.Length);
								//log.Debug("Stream can read = " + stream.CanRead.ToString());
								stream.Write(deleteDataBuffer, 0, deleteDataBuffer.Length);
								//log.Debug("Stream length = " + stream.Length);
								//log.Debug("Stream can read = " + stream.CanRead.ToString());
								
								//System.Threading.Thread.Sleep(100);
								//log.Debug(string.Format("{0} record(s) collected", recordCount));
								//Program.Devices[this.NID].Status = string.Format("{0} record(s) collected", recordCount);
								return attandenceData;
							}
							else
							{
								//Program.Devices[this.NID].Status = "No data available";
								//log.Debug("No data available [" + this.NID + "]");
							}
						}
						else
						{
							//log.Debug("Invalid header [" + this.NID + "]");
						}
					}
					else
					{
						//log.Debug("Invalid package received [" + this.NID + "]");
					}
				}
				else
				{
					//log.Debug("Respon status = FAILED [" + this.NID + "]");
				}
			}
			catch (System.Net.Sockets.SocketException)
			{
				tcpClient.Close();
				//log.Error("Error when connecting to device " + address.ToString() + ":" + port.ToString());
			}
			catch (IOException)
			{
				tcpClient.Close();
				//log.Error("Error when connecting to device " + address.ToString() + ":" + port.ToString());
			}
			catch (Exception ex)
			{
				tcpClient.Close();
				//log.Error("Error when connecting to device " + address.ToString() + ":" + port.ToString(), ex);
			}
            return null;
        }

        private int GetUserCount()
        {
            //log.Debug("Initializing: get user list");
            if (!tcpClient.Connected)
            {
                // TODO: throw exception
                return 0;
            }
            int command;
            command = 0x0116;
            Stream stream = tcpClient.GetStream();
            // 55:aa:19:00:79:19:16:01:00:00:00:00:01:00:c2:01
            byte[] buffer = new byte[16];
            buffer[0] = (byte)(START_DWORD & 0xff);
            buffer[1] = (byte)(START_DWORD >> 8);

            buffer[2] = (byte)(NID & 0xff);
            buffer[3] = (byte)(NID >> 8);

            buffer[4] = (byte)(END_DWORD & 0xff);
            buffer[5] = (byte)(END_DWORD >> 8);

            buffer[6] = (byte)(command & 0xff);
            buffer[7] = (byte)(command >> 8);

            buffer[8] = (byte)(Password & 0xff);
            buffer[9] = (byte)(Password >> 8);

            buffer[10] = 0;
            buffer[11] = 0;

            buffer[12] = 0x01;
            buffer[13] = 0;

            CalculateChecksum(ref buffer);
            byte[] outputBuffer1 = new byte[8];
            byte[] outputBuffer2 = new byte[14];
            stream.Write(buffer, 0, buffer.Length);
            var task1 = System.Threading.Tasks.Task<int>.Factory.FromAsync(stream.BeginRead, stream.EndRead, outputBuffer1, 0, outputBuffer1.Length, null);
            task1.Wait();
            //log.Debug("Status package received");
            int responseStatus = outputBuffer1[4] + ((int)outputBuffer1[5] << 8);
            int responseNID = outputBuffer1[2] + ((int)outputBuffer1[3] << 8);

            if (NID == responseNID && responseStatus == 1)
            {
                if (outputBuffer1[0] == 0x5a && outputBuffer1[1] == 0xa5 && outputBuffer1[4] == 0x1 && outputBuffer1[5] == 0x0)
                {
                    var task2 = System.Threading.Tasks.Task<int>.Factory.FromAsync(stream.BeginRead, stream.EndRead, outputBuffer2, 0, outputBuffer2.Length, null);
                    task2.Wait();
                    //log.Debug("Header package received");
                    // aa:55:19:00:00:00:01:00:06:00:00:00:1f:01
                    responseStatus = outputBuffer2[6] + ((int)outputBuffer2[7] << 8);
                    responseNID = outputBuffer2[2] + ((int)outputBuffer2[3] << 8);
                    if (NID == responseNID && responseStatus == 1 && outputBuffer2[0] == 0xaa && outputBuffer2[1] == 0x55 && outputBuffer2[4] == 0x0 && outputBuffer2[5] == 0x0 && outputBuffer2[6] == 0x1 && outputBuffer2[7] == 0x0 && outputBuffer2[10] == 0x0 && outputBuffer2[11] == 0x0)
                    {
                        int count = outputBuffer2[8] + ((int)outputBuffer2[9] << 8);
                        return count;
                    }
                }
            }
            return 0;
        }
        private IEnumerable<int> GetUserIDs()
        {
            //log.Debug("Get user IDs");
            if (!tcpClient.Connected)
            {
                return null;
            }
            int userCount = GetUserCount();
            if (userCount == 0)
            {
                return null;
            }
            int command = 0x0109;
            Stream stream = tcpClient.GetStream();
            // 55:aa:19:00:79:19:09:01:00:00:00:00:00:00:b4:01 
            byte[] buffer = new byte[16];
            buffer[0] = (byte)(START_DWORD & 0xff);
            buffer[1] = (byte)(START_DWORD >> 8);

            buffer[2] = (byte)(NID & 0xff);
            buffer[3] = (byte)(NID >> 8);

            buffer[4] = (byte)(END_DWORD & 0xff);
            buffer[5] = (byte)(END_DWORD >> 8);

            buffer[6] = (byte)(command & 0xff);
            buffer[7] = (byte)(command >> 8);

            buffer[8] = (byte)(Password & 0xff);
            buffer[9] = (byte)(Password >> 8);

            CalculateChecksum(ref buffer);
            byte[] outputBuffer1 = new byte[8];
            byte[] outputBuffer2 = new byte[14];
            stream.Write(buffer, 0, buffer.Length);
            var task1 = System.Threading.Tasks.Task<int>.Factory.FromAsync(stream.BeginRead, stream.EndRead, outputBuffer1, 0, outputBuffer1.Length, null);
            task1.Wait();
            //log.Debug("Status package received");
            int responseStatus = outputBuffer1[4] + ((int)outputBuffer1[5] << 8);
            int responseNID = outputBuffer1[2] + ((int)outputBuffer1[3] << 8);
            if (NID == responseNID && responseStatus == 1)
            {
                if (outputBuffer1[0] == 0x5a && outputBuffer1[1] == 0xa5 && outputBuffer1[2] == buffer[2] && outputBuffer1[3] == buffer[3] && outputBuffer1[4] == 0x1 && outputBuffer1[5] == 0x0)
                {
                    var task2 = System.Threading.Tasks.Task<int>.Factory.FromAsync(stream.BeginRead, stream.EndRead, outputBuffer2, 0, outputBuffer2.Length, null);
                    task2.Wait();
                    //log.Debug("Header package received");
                    if (outputBuffer2[0] == 0xaa && outputBuffer2[1] == 0x55 && outputBuffer2[2] == buffer[2] && outputBuffer2[3] == buffer[3] && outputBuffer2[4] == 0x0 && outputBuffer2[5] == 0x0 && outputBuffer2[6] == 0x1 && outputBuffer2[7] == 0x0 && outputBuffer2[10] == 0x0 && outputBuffer2[11] == 0x0)
                    {
                        int count = outputBuffer2[8] + ((int)outputBuffer2[9] << 8);
                        int recordCount = count;
                        List<int> userData = null;
                        if (recordCount > 0)
                        {
                            userData = new List<int>();
                        }
                        while (count > 0)
                        {
                            int part = Math.Min(85, count);
                            byte[] logDataBuffer = new byte[4 + (part * 12) + 2];
                            var task3 = System.Threading.Tasks.Task<int>.Factory.FromAsync(stream.BeginRead, stream.EndRead, logDataBuffer, 0, logDataBuffer.Length, null);
                            task3.Wait();
                            //log.Debug("Data package received");
                            //log.Debug(BitConverter.ToString(logDataBuffer));
                            for (int i = 0; i < part; i++)
                            {
                                int dateTimeTicks = 0;
                                int userID = 0;
                                int priv;
                                int mode;
                                dateTimeTicks = (int)logDataBuffer[4 + (i * 8)];
                                dateTimeTicks += ((int)logDataBuffer[5 + (i * 12)]) << 8;
                                dateTimeTicks += ((int)logDataBuffer[6 + (i * 12)]) << 16;
                                dateTimeTicks += ((int)logDataBuffer[7 + (i * 12)]) << 24;
                                userID = (int)logDataBuffer[8 + (i * 12)];
                                userID += ((int)logDataBuffer[9 + (i * 12)]) << 8;
                                userID += ((int)logDataBuffer[10 + (i * 12)]) << 16;
                                userID += ((int)logDataBuffer[11 + (i * 12)]) << 24;
                                priv = (int)logDataBuffer[12 + (i * 12)];
                                mode = (int)logDataBuffer[13 + (i * 12)];
                                int sec;
                                int min;
                                int hour;
                                int days;
                                //int month;
                                //int year;
                                sec = dateTimeTicks % 60;
                                dateTimeTicks /= 60;
                                min = dateTimeTicks % 60;
                                dateTimeTicks /= 60;
                                hour = dateTimeTicks % 24;
                                dateTimeTicks /= 24;
                                days = dateTimeTicks;
                                //dateTimeTicks /= 31;
                                //month = (dateTimeTicks % 12) + 1;
                                //dateTimeTicks /= 12;
                                //year = 2000 + dateTimeTicks;
                                var dateTime = new DateTime(2000, 1, 1, hour, min, sec).AddDays(days);
                                //userData.Add(new AttendanceData { UserID = userID > 0 ? userID.ToString("00000000") : userID.ToString(), DateTime = dateTime, UserType = (UserType)(priv & 1), SensorType = (SensorType)(priv & 0x0e), Mode = (Mode)(mode & 0x0f) });
                            }
                            count -= part;
                        }
                        if (recordCount > 0)
                        {
                            // delete data from device
                            byte[] deleteDataBuffer = new byte[] { 0x5a, 0xa5, 0, 0, 0x01, 0, 0, 0, 0, 0 };
                            deleteDataBuffer[0] = outputBuffer1[0];
                            deleteDataBuffer[1] = outputBuffer1[1];
                            deleteDataBuffer[2] = outputBuffer1[2];
                            deleteDataBuffer[3] = outputBuffer1[3];
                            deleteDataBuffer[4] = (byte)(recordCount & 0xff);
                            deleteDataBuffer[5] = (byte)(recordCount >> 8);
                            CalculateChecksum(ref deleteDataBuffer);
                            stream.Write(deleteDataBuffer, 0, deleteDataBuffer.Length);
                            //System.Threading.Thread.Sleep(100);
                            //log.Debug(string.Format("{0} record(s) collected", count));
                            return userData;
                        }
                    }
                }
            }
            return null;
        }
        public IEnumerable<UserData> GetUsers()
        {
            //log.Debug("Getting user list");
            if (!tcpClient.Connected)
            {
                // TODO: throw exception
                return null;
            }
            int data = 1;
            // initialize
            int status = SendCommand(0x0116, out data, 0, 0, 1);
            if (status <= 0)
            {
                return null;
            }
            // get user list
            data = 0;
            status = SendCommand(0x0109, out data);
            if (status > 0)
            {
                Stream stream = tcpClient.GetStream();
                //log.Debug("Status package received");
                int count = data;
                int recordCount = count;
                List<UserData> userData = null;
                if (recordCount > 0)
                {
                    userData = new List<UserData>();
                }
                while (count > 0)
                {
                    int part = Math.Min(127, count);
                    byte[] logDataBuffer = new byte[4 + (part * 8) + 2];
                    var task = System.Threading.Tasks.Task<int>.Factory.FromAsync(stream.BeginRead, stream.EndRead, logDataBuffer, 0, logDataBuffer.Length, null);
                    task.Wait();
                    //log.Debug("Data package received");
                    //log.Debug(BitConverter.ToString(logDataBuffer));
                    for (int i = 0; i < part; i++)
                    {
                        int userID = 0;
                        int priv;
                        int mode;
                        userID = (int)logDataBuffer[4 + (i * 8)];
                        userID += ((int)logDataBuffer[5 + (i * 8)]) << 8;
                        userID += ((int)logDataBuffer[6 + (i * 8)]) << 16;
                        userID += ((int)logDataBuffer[7 + (i * 8)]) << 24;
                        priv = (int)logDataBuffer[8 + (i * 8)];
                        mode = (int)logDataBuffer[9 + (i * 8)];
                        var user = new UserData { UserID = userID, UserLevel = (UserLevel)priv, UserSensor = (UserSensor)mode };
                        userData.Add(user);
                    }
                    count -= part;
                }
                if (userData != null)
                {
                    for (int i = 0; i < userData.Count; i++)
                    {
                        byte[] userByteArray = new byte[2878];
                        status = RaedEnrollData(0x0103, ref userByteArray, (short)(userData[i].UserID & 0xffff), (short)(userData[i].UserID >> 16));
                        if (status == 1)
                        {
                            userData[i].EnrollData = userByteArray;
                            if (userByteArray[4] == 1)
                            {
                                int cardID = userByteArray[24] +
                                    ((int)userByteArray[25] << 8) +
                                    ((int)userByteArray[26] << 16) +
                                    ((int)userByteArray[27] << 24);
                                userData[i].CardID = cardID;
                            }

                        }
                    }
                }
                // send end command
                data = 0;
                SendCommand(0x010b, out data);
                //System.Threading.Thread.Sleep(100);
                //log.Debug(string.Format("{0} record(s) collected", count));
                return userData;
            }
            return null;
        }

        private bool GetAllDataCommand()
        {
            //log.Debug("Getting attendance data");
            if (!tcpClient.Connected)
            {
                // TODO: throw exception
                return false;
            }
            int command;
            command = 0x0111;
            Stream stream = tcpClient.GetStream();
            byte[] buffer = new byte[16];// { 0x55, 0xaa, 0, 0, 0x79, 0x19, 0x11, 0x01, 0, 0, 0, 0, 0, 0, 0, 0 };
            buffer[0] = (byte)(START_DWORD & 0xff);
            buffer[1] = (byte)(START_DWORD >> 8);

            buffer[2] = (byte)(NID & 0xff);
            buffer[3] = (byte)(NID >> 8);

            buffer[4] = (byte)(END_DWORD & 0xff);
            buffer[5] = (byte)(END_DWORD >> 8);

            buffer[6] = (byte)(command & 0xff);
            buffer[7] = (byte)(command >> 8);

            buffer[8] = (byte)(Password & 0xff);
            buffer[9] = (byte)(Password >> 8);

            CalculateChecksum(ref buffer);
            byte[] outputBuffer1 = new byte[8];
            byte[] outputBuffer2 = new byte[14];
            //log.Debug("Sending \"Get Data\" command");
            stream.Write(buffer, 0, buffer.Length);
            stream.Read(outputBuffer1, 0, outputBuffer1.Length);
            //log.Debug("Status package received");
            int responseStatus = outputBuffer1[4] + ((int)outputBuffer1[5] << 8);
            int responseNID = outputBuffer1[2] + ((int)outputBuffer1[3] << 8);

            if (NID == responseNID && responseStatus == 1)
            {
                if (outputBuffer1[0] == 0x5a && outputBuffer1[1] == 0xa5 && outputBuffer1[4] == 0x1 && outputBuffer1[5] == 0x0)
                {
                    stream.Read(outputBuffer2, 0, outputBuffer2.Length);
                    //log.Debug("Header package received");
                    responseStatus = outputBuffer2[6] + ((int)outputBuffer2[7] << 8);
                    responseNID = outputBuffer2[2] + ((int)outputBuffer2[3] << 8);
                    if (NID == responseNID && responseStatus == 1 && outputBuffer2[0] == 0xaa && outputBuffer2[1] == 0x55 && outputBuffer2[4] == 0x0 && outputBuffer2[5] == 0x0 && outputBuffer2[6] == 0x1 && outputBuffer2[7] == 0x0 && outputBuffer2[10] == 0x0 && outputBuffer2[11] == 0x0)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private int SendCommand(int command, out int data, params short[] parameters)
        {
            data = -1;
            //log.Debug("Send command: " + command.ToString("x"));
            if (!tcpClient.Connected)
            {
                return -1;
            }
            Stream stream = tcpClient.GetStream();
            byte[] buffer = new byte[16];
            buffer[0] = (byte)(START_DWORD & 0xff);
            buffer[1] = (byte)(START_DWORD >> 8);

            buffer[2] = (byte)(NID & 0xff);
            buffer[3] = (byte)(NID >> 8);

            buffer[4] = (byte)(END_DWORD & 0xff);
            buffer[5] = (byte)(END_DWORD >> 8);

            buffer[6] = (byte)(command & 0xff);
            buffer[7] = (byte)(command >> 8);
            for (int i = 0; i < parameters.Length && i < 4; i++)
            {
                buffer[8 + (i * 2)] = (byte)(parameters[i] & 0xff);
                buffer[9 + (i * 2)] = (byte)(parameters[i] >> 8);
            }
            CalculateChecksum(ref buffer);
            byte[] outputBuffer1 = new byte[8];
            byte[] outputBuffer2 = new byte[14];
            stream.Write(buffer, 0, buffer.Length);
            var task1 = System.Threading.Tasks.Task<int>.Factory.FromAsync(stream.BeginRead, stream.EndRead, outputBuffer1, 0, outputBuffer1.Length, null);
            task1.Wait();
            //log.Debug("Status package received");
            int responseStatus = outputBuffer1[4] + ((int)outputBuffer1[5] << 8);
            int responseNID = outputBuffer1[2] + ((int)outputBuffer1[3] << 8);

            if (NID == responseNID && responseStatus == 1)
            {
                if (outputBuffer1[0] == 0x5a && outputBuffer1[1] == 0xa5 && outputBuffer1[4] == 0x1 && outputBuffer1[5] == 0x0)
                {
                    var task2 = System.Threading.Tasks.Task<int>.Factory.FromAsync(stream.BeginRead, stream.EndRead, outputBuffer2, 0, outputBuffer2.Length, null);
                    task2.Wait();
                    //log.Debug("Header package received");
                    responseStatus = outputBuffer2[6] + ((int)outputBuffer2[7] << 8);
                    responseNID = outputBuffer2[2] + ((int)outputBuffer2[3] << 8);
                    if (NID == responseNID && responseStatus == 1 && outputBuffer2[0] == 0xaa && outputBuffer2[1] == 0x55)
                    {
                        int status = outputBuffer2[6] + ((int)outputBuffer2[7] << 8);
                        data = outputBuffer2[8] + ((int)outputBuffer2[9] << 8) + ((int)outputBuffer2[10] << 16) + ((int)outputBuffer2[11] << 24);
                        return status;
                    }
                }
            }
            return 0;
        }

        private int RaedEnrollData(int command, ref byte[] data, params short[] parameters)
        {
            int status = -1;
            //log.Debug("Send command: " + command.ToString("x"));
            if (!tcpClient.Connected)
            {
                // TODO: throw exception
                return status;
            }
            Stream stream = tcpClient.GetStream();
            byte[] buffer = new byte[16];
            buffer[0] = (byte)(START_DWORD & 0xff);
            buffer[1] = (byte)(START_DWORD >> 8);

            buffer[2] = (byte)(NID & 0xff);
            buffer[3] = (byte)(NID >> 8);

            buffer[4] = (byte)(END_DWORD & 0xff);
            buffer[5] = (byte)(END_DWORD >> 8);

            buffer[6] = (byte)(command & 0xff);
            buffer[7] = (byte)(command >> 8);

            for (int i = 0; i < parameters.Length && i < 4; i++)
            {
                buffer[8 + (i * 2)] = (byte)(parameters[i] & 0xff);
                buffer[9 + (i * 2)] = (byte)(parameters[i] >> 8);
            }

            CalculateChecksum(ref buffer);
            stream.Write(buffer, 0, buffer.Length);

            byte[] outputBuffer1 = new byte[8];
            byte[] part1 = new byte[1026];
            byte[] part2 = new byte[1026];
            byte[] part3 = new byte[830];
            using (var task1 = System.Threading.Tasks.Task<int>.Factory.FromAsync(stream.BeginRead, stream.EndRead, outputBuffer1, 0, outputBuffer1.Length, null))
            {
                task1.Wait();
                //log.Debug("Status package received");
                int responseStatus = outputBuffer1[4] + ((int)outputBuffer1[5] << 8);
                int responseNID = outputBuffer1[2] + ((int)outputBuffer1[3] << 8);

                if (NID == responseNID && responseStatus == 1)
                {
                    using (var task2 = System.Threading.Tasks.Task<int>.Factory.FromAsync(stream.BeginRead, stream.EndRead, part1, 0, part1.Length, null))
                    {
                        task2.Wait();
                        using (var task3 = System.Threading.Tasks.Task<int>.Factory.FromAsync(stream.BeginRead, stream.EndRead, part2, 0, part2.Length, null))
                        {
                            task3.Wait();
                            using (var task4 = System.Threading.Tasks.Task<int>.Factory.FromAsync(stream.BeginRead, stream.EndRead, part3, 0, part3.Length, null))
                            {
                                task4.Wait();
                                Array.Copy(part1, 4, data, 0, 1020);
                                Array.Copy(part2, 4, data, 1020, 1020);
                                Array.Copy(part2, 4, data, 2040, 824);
                                status = responseStatus;
                            }
                        }
                    }
                }
            }
            return status;
        }

        private bool PingHost(IPAddress address)
        {
            //IPAddress instance for holding the returned host

            //set the ping options, TTL 128
            PingOptions pingOptions = new PingOptions(128, true);

            //create a new ping instance
            Ping ping = new Ping();

            //32 byte buffer (create empty)
            byte[] buffer = new byte[32];
            try
            {
                PingReply pingReply = ping.Send(address, 1000, buffer, pingOptions);
                if (pingReply != null)
                {
                    return pingReply.Status == IPStatus.Success;
                }
            }
            catch (Exception)
            {
                return false;
            }
            return false;
        }

        private void CalculateChecksum(ref byte[] buffer)
        {
            int checksum = 0;
            if (buffer.Length > 2)
            {
                for (int i = 0; i < buffer.Length - 2; i++)
                {
                    checksum += buffer[i];
                }
                buffer[buffer.Length - 2] = (byte)(checksum & 0xff);
                buffer[buffer.Length - 1] = (byte)(checksum >> 8);
            }
        }
        private bool IsResponseValid(byte[] buffer)
        {
            ushort checksum = 0;
            ushort bufferChecksum = 0;
            if (buffer.Length > 2)
            {
                bufferChecksum = BitConverter.ToUInt16(buffer, buffer.Length - 2);
                for (int i = 0; i < buffer.Length - 2; i++)
                {
                    checksum += buffer[i];
                }
                return (checksum == bufferChecksum);
            }
            return false;
        }

        public void Dispose()
        {
            //log.Debug("Disposing object " + this.GetHashCode());
            try
            {
                if (this.tcpClient.Connected)
                {
                    //log.Debug("Disconnecting object " + this.GetHashCode());
                    this.tcpClient.Close();
                }
            }
            catch (ObjectDisposedException disposedException)
            {
                //log.Error(disposedException);
            }
            catch (Exception ex)
            {
                //log.Error(ex);
            }
            //log.Debug("Object disposed " + this.GetHashCode());
        }
    }

}

/*
 * Licensed to the Apache Software Foundation (ASF) under one or more
 * contributor license agreements.  See the NOTICE file distributed with
 * this work for additional information regarding copyright ownership.
 * The ASF licenses this file to You under the Apache License, Version 2.0
 * (the "License"); you may not use this file except in compliance with
 * the License.  You may obtain a copy of the License at
 * 
 * http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.Text;
using System.Net;

using NTCPMSG.Client;
using NTCPMSG.Event;

namespace NTCPMSG.Client
{
    /// <summary>
    /// This class bind multiple single tcp connection as one 
    /// logic connection cable.
    /// </summary>
    public class SingleConnectionCable : IDisposable
    {
        #region Fields

        LinkedList<SingleConnection> _WorkingConnections;
        Queue<SingleConnection> _PendingConnections;
        LinkedListNode<SingleConnection> _CurrentWorkingConnection;

        int _ASendCount = 0;
        int _SSendCount = 0;

        object _LockObj = new object();
        int _Capacity;
        int _TryConnectInterval = 1000; //in milliseconds. default is 1 second
        bool _AutoConnect;
        bool _TryToConnect = false;
        bool _Closing = false;

        System.Threading.Thread _ConnectThread = null;

        private bool Closing
        {
            get
            {
                lock (_LockObj)
                {
                    return _Closing;
                }
            }

            set
            {
                lock (_LockObj)
                {
                    _Closing = value;
                }
            }
        }

        private bool TryToConnect
        {
            get
            {
                lock (_LockObj)
                {
                    return _TryToConnect;
                }
            }

            set
            {
                lock (_LockObj)
                {
                    _TryToConnect = value;
                }
            }
        }

        #endregion

        #region public properties 

        /// <summary>
        /// Get the capacity of the single connections inside this cable. 
        /// </summary>
        public int Capacity
        {
            get
            {
                lock (_LockObj)
                {
                    return _Capacity;
                }
            }

            private set
            {
                lock (_LockObj)
                {
                    _Capacity = value;
                }
            }
        }

        /// <summary>
        /// Interval for try to connect to remote host.
        /// In milliseconds
        /// </summary>
        public int TryConnectInterval
        {
            get
            {
                lock (_LockObj)
                {
                    return _TryConnectInterval;
                }
            }

            private set
            {
                lock (_LockObj)
                {
                    if (value <= 100)
                    {
                        _TryConnectInterval = 100;
                    }
                    else
                    {
                        _TryConnectInterval = value;
                    }
                }
            }
        }

        /// <summary>
        /// Get or set do connect to remote server automatically or not.
        /// If set to true, this class will try to connect to server automatically
        /// after disconnect.
        /// </summary>
        public bool AutoConnect
        {
            get
            {
                lock (_LockObj)
                {
                    return _AutoConnect;
                }
            }

            private set
            {
                lock (_LockObj)
                {
                    _AutoConnect = value;
                }
            }
        }

        /// <summary>
        /// Server IP end point
        /// </summary>
        public IPEndPoint RemoteIPEndPoint { get; private set; }

        /// <summary>
        /// Get current connection is connected or not.
        /// True: at least one single connection is connected.
        /// </summary>
        public bool Connected
        {
            get
            {
                lock (_LockObj)
                {
                    foreach (SingleConnection sConn in _WorkingConnections)
                    {
                        if (sConn.Connected)
                        {
                            return true;
                        }
                    }

                    return false;
                }
            }
        }

        #endregion

        #region Constractor

        public SingleConnectionCable(IPEndPoint remoteIPEndPoint)
            :this(remoteIPEndPoint, 6)
        {

        }

        public SingleConnectionCable(IPEndPoint remoteIPEndPoint, int capacity)
        {
            RemoteIPEndPoint = remoteIPEndPoint;

            if (capacity <= 0)
            {
                throw new ArgumentException("Capacity must be large than 0");
            }

            _WorkingConnections = new LinkedList<SingleConnection>();
            _PendingConnections = new Queue<SingleConnection>();
            _CurrentWorkingConnection = null;

            for (int i = 0; i < capacity; i++)
            {
                SingleConnection conn = new SingleConnection(remoteIPEndPoint);

                conn.ErrorEventHandler += InnerErrorEventHandler;

                conn.ReceiveEventHandler += InnerReceiveEventHandler;

                conn.RemoteDisconnected += InnerRemoteDisconnected;

                _PendingConnections.Enqueue(conn);
            }

            _ConnectThread = new System.Threading.Thread(ConnectThreadProc);
            _ConnectThread.IsBackground = true;
            _ConnectThread.Start();
        }

        ~SingleConnectionCable()
        {
            Dispose();
        }

        #endregion

        #region Private methods

        private void ConnectThreadProc()
        {
            while (true)
            {
                if (Closing)
                {
                    return;
                }

                if (AutoConnect)
                {
                    InnerConnect(30 * 1000);
                }

                System.Threading.Thread.Sleep(TryConnectInterval);
            }
        }


        private SingleConnection GetAWorkingConnection(bool sync)
        {
            lock (_LockObj)
            {
                if (_WorkingConnections.Count <= 0)
                {
                    return null;
                }

                LinkedListNode<SingleConnection> cur;

                if (_CurrentWorkingConnection == null)
                {
                    _CurrentWorkingConnection = _WorkingConnections.First;
                    cur = _CurrentWorkingConnection;
                }
                else
                {
                    int sendCount;

                    if (sync)
                    {
                        return _WorkingConnections.First.Value;
                    }
                    else
                    {
                        sendCount = System.Threading.Interlocked.Increment(ref _ASendCount);

                        if (sendCount % 100 != 0)
                        {
                            return _CurrentWorkingConnection.Value;
                        }
                    }


                    cur = _CurrentWorkingConnection.Next;

                    if (cur == null)
                    {
                        cur = _WorkingConnections.First;
                    }
                }

                while (!cur.Value.Connected && _WorkingConnections.Count > 0)
                {
                    LinkedListNode<SingleConnection> next = cur.Next;
                    
                    _PendingConnections.Enqueue(cur.Value);

                    _WorkingConnections.Remove(cur);

                    if (next == null)
                    {
                        next = _WorkingConnections.First;

                        if (next == null)
                        {
                            break;
                        }
                    }

                    cur = next;
                }

                if (_WorkingConnections.Count <= 0)
                {
                    _CurrentWorkingConnection = null;

                    OnDisconnectEvent();

                    return null;
                }
                else
                {
                    _CurrentWorkingConnection = cur;
                    return cur.Value;
                }
            }
        }

        private void InnerConnect(int millisecondsTimeout)
        {
            if (Closing)
            {
                throw new NTcpException("Can't operate SingleConnectionCable when it is closing.", ErrorCode.Closing);
            }

            if (TryToConnect)
            {
                throw new NTcpException("Try to connect by other tread now.", ErrorCode.TryToConenct);
            }

            try
            {
                while (true)
                {
                    SingleConnection pendingConn;

                    lock (_LockObj)
                    {
                        if (_PendingConnections.Count <= 0)
                        {
                            return;
                        }

                        pendingConn = _PendingConnections.Dequeue();
                    }

                    try
                    {
                        pendingConn.Connect(millisecondsTimeout);

                        lock (_LockObj)
                        {
                            _WorkingConnections.AddLast(pendingConn);
                        }
                    }
                    catch (Exception e)
                    {
                        _PendingConnections.Enqueue(pendingConn);

                        OnErrorEvent("InnerConnect", e);

                        return;
                    }
                }
            }
            finally
            {
                TryToConnect = false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="flag"></param>
        /// <param name="evt"></param>
        /// <param name="group"></param>
        /// <param name="channel"></param>
        /// <param name="data"></param>
        /// <exception cref="TcpException"></exception>
        /// <exception cref="socketException"></exception>
        private void InnerASend(UInt32 evt, UInt16 group, byte[] data)
        {
            if (Closing)
            {
                throw new NTcpException("Can't operate SingleConnectionCable when it is closing.", ErrorCode.Closing);
            }

            SingleConnection singleConn = GetAWorkingConnection(false);

            if (singleConn == null)
            {
                throw new NTcpException("Tcp disconnected", ErrorCode.Disconnected);
            }

            singleConn.ASend(evt, group, data);
        }

        /// <summary>
        /// Send syncronization message
        /// </summary>
        /// <param name="evt">event</param>
        /// <param name="group">group no</param>
        /// <param name="data">data need to send</param>
        /// <param name="timeout">waitting timeout. In millisecond</param>
        /// <returns>data return from client</returns>
        private byte[] InnerSSend(UInt32 evt, UInt16 group, byte[] data, int timeout)
        {
            if (Closing)
            {
                throw new NTcpException("Can't operate SingleConnectionCable when it is closing.", ErrorCode.Closing);
            }

            SingleConnection singleConn = GetAWorkingConnection(true);

            if (singleConn == null)
            {
                throw new NTcpException("Tcp disconnected", ErrorCode.Disconnected);
            }

            return singleConn.SSend(evt, group, data, timeout);
        }

        #endregion

        #region Events

        /// <summary>
        /// Event occurred when some error raised during sending message.
        /// </summary>
        public event EventHandler<Event.ErrorEventArgs> ErrorEventHandler;

        private void OnErrorEvent(string func, Exception e)
        {
            EventHandler<Event.ErrorEventArgs> errorEventHandler = ErrorEventHandler;

            if (errorEventHandler != null)
            {
                try
                {
                    errorEventHandler(this, new NTCPMSG.Event.ErrorEventArgs(func, e));
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// Event occurred when remote socket disconnected
        /// </summary>
        public event EventHandler<Event.DisconnectEventArgs> RemoteDisconnected;

        private void InnerErrorEventHandler(object sender, ErrorEventArgs args)
        {
            OnErrorEvent(args.Func, args.ErrorException);
        }

        private void InnerRemoteDisconnected(object sender, DisconnectEventArgs args)
        {
            GetAWorkingConnection(true);
        }

        private void OnDisconnectEvent()
        {
            EventHandler<Event.DisconnectEventArgs> disconnectEventHandler = RemoteDisconnected;

            if (disconnectEventHandler != null)
            {
                try
                {
                    disconnectEventHandler(this, new Event.DisconnectEventArgs(RemoteIPEndPoint));
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// Event occurred when data received from server.
        /// </summary>
        public event EventHandler<Event.ReceiveEventArgs> ReceiveEventHandler;
        
        private void InnerReceiveEventHandler(object sender, ReceiveEventArgs args)
        {
            EventHandler<Event.ReceiveEventArgs> receiveEventHandler = ReceiveEventHandler;

            if (receiveEventHandler != null)
            {
                try
                {
                    receiveEventHandler(this, args);
                }
                catch
                {

                }
            }
        }

        #endregion


        #region Public methods

        /// <summary>
        /// Connect to remote host specified in RemoteIPEndPoint
        /// </summary>
        public void Connect()
        {
            Connect(30 * 1000);
        }

        /// <summary>
        /// Connect to remote host specified in RemoteIPEndPoint
        /// </summary>
        /// <param name="millisecondsTimeout">The number of milliseconds to wait, or Timeout.Infinite (-1) to wait indefinitely. </param>
        public void Connect(int millisecondsTimeout)
        {
            Connect(millisecondsTimeout, true);
        }

        /// <summary>
        /// Connect to remote host specified in RemoteIPEndPoint
        /// </summary>
        /// <param name="millisecondsTimeout">The number of milliseconds to wait, or Timeout.Infinite (-1) to wait indefinitely. </param>
        /// <param name="autoConnect">set the AutoConnect Mode</param>
        public void Connect(int millisecondsTimeout, bool autoConnect)
        {
            AutoConnect = autoConnect;

            if (!AutoConnect)
            {
                InnerConnect(millisecondsTimeout);
            }
            else
            {
                int times = 0;

                while (++times <= millisecondsTimeout / 100)
                {
                    if (Connected)
                    {
                        return;
                    }

                    System.Threading.Thread.Sleep(100);
                }

                throw new NTcpException("Tcp disconnected", ErrorCode.Disconnected);
            }
        }

        /// <summary>
        /// Disconnect all of the SingleConnections including in this cable.
        /// </summary>
        /// <remarks>If AutoConnect = true, will throw a exception</remarks>
        public void Disconnect()
        {
            if (AutoConnect)
            {
                throw new NTcpException("Can't disconnect in AutoConnect Mode. Need set AutoConnect to false.",
                     ErrorCode.AutoConnect);
            }

            lock (_LockObj)
            {
                foreach (SingleConnection conn in _WorkingConnections)
                {
                    conn.Disconnect();

                    _PendingConnections.Enqueue(conn);
                }

                _WorkingConnections.Clear();
                _CurrentWorkingConnection = null;

                OnDisconnectEvent();
            }
        }

        public void Close()
        {
            if (_ConnectThread != null)
            {
                try
                {
                    Closing = true;
                    if (!_ConnectThread.Join(1000))
                    {
                        _ConnectThread.Abort();
                    }
                }
                catch
                {
                }
                finally
                {
                    _ConnectThread = null;
                }
            }

            try
            {
                AutoConnect = false;
                Disconnect();
            }
            catch
            {
            }
        }

        /// <summary>
        /// Send asyncronization message
        /// </summary>
        /// <param name="evt">event</param>
        /// <param name="data">data need to send</param>
        public void ASend(UInt32 evt, byte[] data)
        {
            ASend(evt, 0, data);
        }

        /// <summary>
        /// Send asyncronization message
        /// </summary>
        /// <param name="ipEndPoint">ip end point of client</param>        /// <param name="evt">event</param>
        /// <param name="group">group No.</param>
        /// <param name="channel">channel no</param>
        /// <param name="data">data need to send</param>
        public void ASend(UInt32 evt, UInt16 group, byte[] data)
        {
            InnerASend(evt, group, data);
        }

        /// <summary>
        /// Synchronously sends data to the remote host specified in the RemoteIPEndPoint
        /// </summary>
        /// <param name="evt">message event</param>
        /// <param name="data">An array of type Byte  that contains the data to be sent. </param>
        /// <returns>An array of type Byte  that contains the data that return from remote host</returns>
        public byte[] SSend(UInt32 evt, byte[] data)
        {
            return InnerSSend(evt, 0, data, System.Threading.Timeout.Infinite);
        }

        /// <summary>
        /// Synchronously sends data to the remote host specified in the RemoteIPEndPoint
        /// </summary>
        /// <param name="evt">message event</param>
        /// <param name="data">An array of type Byte  that contains the data to be sent. </param>
        /// <param name="millisecondsTimeout">The number of milliseconds to wait, or Timeout.Infinite (-1) to wait indefinitely. </param>
        /// <returns>An array of type Byte  that contains the data that return from remote host</returns>
        public byte[] SSend(UInt32 evt, byte[] data, int millisecondsTimeout)
        {
            return InnerSSend(evt, 0, data, millisecondsTimeout);
        }


        /// <summary>
        /// Synchronously sends data to the remote host specified in the RemoteIPEndPoint
        /// </summary>
        /// <param name="evt">message event</param>
        /// <param name="group">group No.</param>
        /// <param name="data">An array of type Byte  that contains the data to be sent. </param>
        /// <param name="millisecondsTimeout">The number of milliseconds to wait, or Timeout.Infinite (-1) to wait indefinitely. </param>
        /// <returns>An array of type Byte  that contains the data that return from remote host</returns>
        public byte[] SSend(UInt32 evt, UInt16 group, byte[] data, int millisecondsTimeout)
        {
            return InnerSSend(evt, group, data, millisecondsTimeout);
        }
   

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            Close();
        }

        #endregion
    }
}

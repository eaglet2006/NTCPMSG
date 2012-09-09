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
using System.Net.Sockets;
using System.Threading;

namespace NTCPMSG.Client
{
    /// <summary>
    /// This class provide one tcp connection link with multiple channel.
    /// </summary>
    public class SingleConnection : IDisposable
    {
        class SyncBlock
        {
            static Queue<AutoResetEvent> _AutoEventQueue = new Queue<AutoResetEvent>();
            static object _sLockObj = new object();

            static AutoResetEvent Get()
            {
                lock (_sLockObj)
                {
                    if (_AutoEventQueue.Count == 0)
                    {
                        return new AutoResetEvent(false);
                    }
                    else
                    {
                        return _AutoEventQueue.Dequeue();
                    }
                }
            }

            static internal void ClearAutoEvents()
            {
                lock (_sLockObj)
                {
                    while (_AutoEventQueue.Count > 0)
                    {
                        System.Threading.AutoResetEvent aEvent = _AutoEventQueue.Dequeue();
                        aEvent.Set();
                    }
                }
            }

            static void Return(AutoResetEvent autoEvent)
            {
                lock (_sLockObj)
                {
                    _AutoEventQueue.Enqueue(autoEvent);
                }
            }

            private bool _Closed;
            internal AutoResetEvent AutoEvent;
            internal byte[] RetData;

            internal SyncBlock()
            {
                RetData = null;
                _Closed = false;
                AutoEvent = Get();
            }

            ~SyncBlock()
            {
                Close();
            }

            internal bool WaitOne(int millisecondsTimeout)
            {
                return AutoEvent.WaitOne(millisecondsTimeout);
            }

            internal void Close()
            {
                if (!_Closed)
                {
                    try
                    {
                        Return(AutoEvent);
                    }
                    catch
                    {

                    }
                    finally
                    {
                        _Closed = true;
                    }
                }
            }
        }

        #region Fields
        object _ConnectLock = new object();

        System.Threading.AutoResetEvent _ConnectEvent;
        Exception _ConnectException;
        SCB _SCB;
        Socket _Socket;
        SendMessageQueue _SendMessageQueue;
        bool _Connected;
        bool _Closed;

        object _LockObj;

        object _ChannelSync;
        UInt32 _CurChannel;

        object _SyncMessageLock;
        Dictionary<UInt32, SyncBlock> _SyncMessageDict;
 
        #endregion

        #region private Properties

        private UInt32 CurChannel
        {
            get
            {
                lock (_ChannelSync)
                {
                    return _CurChannel;
                }
            }
        }

        private UInt32 IncCurChannel()
        {
            lock (_ChannelSync)
            {
                if (_CurChannel >= int.MaxValue)
                {
                    //the value large than max value of int is reserved by server side channel.
                    _CurChannel = 0;
                }
                else
                {
                    _CurChannel++;
                }

                return _CurChannel;
            }
        }

        private bool Closed
        {
            get
            {
                lock (_LockObj)
                {
                    return _Closed;
                }
            }

            set
            {
                lock (_LockObj)
                {
                    _Closed = value;
                }
            }
        }

       


        #endregion

        #region public Properties
        
        /// <summary>
        /// Get tcp connected or not.
        /// </summary>
        public bool Connected
        {
            get
            {
                lock (_LockObj)
                {
                    if (_Connected)
                    {
                        if (_SendMessageQueue.Closed)
                        {
                            Close();
                        }
                    }

                    return _Connected;
                }
            }

            private set
            {
                lock (_LockObj)
                {
                    _Connected = value;
                }
            }
        }

        /// <summary>
        ///IP End Point that be bound 
        /// </summary>
        public IPEndPoint BindIPEndPoint { get; private set; }

        /// <summary>
        /// Server IP end point
        /// </summary>
        public IPEndPoint RemoteIPEndPoint { get; private set; }

        #endregion

        #region private methods

        private void InitVar()
        {
            _ConnectEvent = new AutoResetEvent(false);
            _ConnectException = null;
            _SCB = null;
            _Socket = null;
            _SendMessageQueue = null;
            _Connected = false;
            _Closed = false;

            _LockObj = new object();

            _ChannelSync = new object();
            _CurChannel = 0;

            _SyncMessageLock = new object();
            _SyncMessageDict = new Dictionary<uint, SyncBlock>();
        }

        bool TryGetSyncChannel(UInt32 channel, out SyncBlock syncBlock)
        {
            lock (_SyncMessageLock)
            {
                return _SyncMessageDict.TryGetValue(channel, out syncBlock);
            }
        }

        UInt32 GetChannelForSync(out SyncBlock syncBlock)
        {
            lock (_SyncMessageLock)
            {
                UInt32 channel = IncCurChannel();

                while (_SyncMessageDict.ContainsKey(channel))
                {
                    channel = IncCurChannel();
                }

                syncBlock = new SyncBlock();

                _SyncMessageDict.Add(channel, syncBlock);

                return channel;
            }
        }

        void ReturnChannelForSync(UInt32 channel)
        {
            lock (_SyncMessageLock)
            {
                if (_SyncMessageDict.ContainsKey(channel))
                {
                    try
                    {
                        _SyncMessageDict[channel].Close();
                    }
                    catch
                    {

                    }
                    _SyncMessageDict.Remove(channel);
                }
            }
        }

        void ClearChannelForSync()
        {
            lock (_SyncMessageLock)
            {
                foreach (SyncBlock syncBlock in _SyncMessageDict.Values)
                {
                    try
                    {
                        syncBlock.Close();
                    }
                    catch
                    {
                    }
                }

                _SyncMessageDict.Clear();
            }

            SyncBlock.ClearAutoEvents();
        }

        private void Async_Connection(IAsyncResult ar)
        {
            try
            {
                Socket socket = ar.AsyncState as Socket;

                if (socket != null)
                {
                    socket.EndConnect(ar);
                }
            }
            catch (Exception ex)
            {
                _ConnectException = ex;
            }
            finally
            {
                _ConnectEvent.Set();
            }
        }

        void OnReadyToSend(byte[] data, int length)
        {
            _SCB.ASend(data, 0, length);
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
        private void InnerASend(MessageFlag flag, UInt32 evt, UInt16 group, byte[] data)
        {
            if (!Connected)
            {
                throw new NTcpException("Tcp disconnected", ErrorCode.Disconnected);
            }

            IncCurChannel();

            _SendMessageQueue.ASend(flag, evt, group, CurChannel, data);

            //SCB scb = _SCB;
            //scb.ASend(flag, evt, group, channel, data);
        }

        /// <summary>
        /// Send syncronization message
        /// </summary>
        /// <param name="evt">event</param>
        /// <param name="group">group no</param>
        /// <param name="data">data need to send</param>
        /// <param name="timeout">waitting timeout. In millisecond</param>
        /// <returns>data return from client</returns>
        private byte[] InnerSSend(MessageFlag flag, UInt32 evt, UInt16 group, byte[] data, int timeout)
        {
            if (!Connected)
            {
                throw new NTcpException("Tcp disconnected", ErrorCode.Disconnected);
            }

            SyncBlock syncBlock;
            UInt32 channel = GetChannelForSync(out syncBlock);

            _SendMessageQueue.ASend(flag, evt, group, channel, data);

            bool bSuccess;
            byte[] retData;

            try
            {
                bSuccess = syncBlock.WaitOne(timeout);
                if (bSuccess)
                {
                    if (!Connected)
                    {
                        throw new NTcpException("Tcp disconnected during ssend", ErrorCode.Disconnected);
                    }

                    retData = syncBlock.RetData;
                }
            }
            catch (NTcpException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new NTcpException(string.Format("Tcp disconnected during ssend, err:{0}",
                    e), ErrorCode.Disconnected);
            }
            finally
            {
                syncBlock.Close();
                ReturnChannelForSync(channel);
            }

            if (bSuccess)
            {
                return syncBlock.RetData;
            }
            else
            {
                throw new TimeoutException("SSend timeout!");
            }
        }


        #endregion

        #region contractor

        /// <summary>
        /// Constractor
        /// </summary>
        /// <param name="remoteIPAddress">remote server ip address</param>
        /// <param name="remotePort">remote server tcp port</param>
        public SingleConnection(string remoteIPAddress, int remotePort)
            : this(new IPEndPoint(IPAddress.Parse(remoteIPAddress), remotePort))
        {

        }

        /// <summary>
        /// Constractor
        /// </summary>
        /// <param name="remoteIPAddress">remote server ip address</param>
        /// <param name="remotePort">remote server tcp port</param>
        public SingleConnection(IPAddress remoteIPAddress, int remotePort)
            : this(new IPEndPoint(remoteIPAddress, remotePort))
        {

        }

        /// <summary>
        /// Constractor
        /// </summary>
        /// <param name="remoteIPEndPoint">remote server IPEndPoint</param>
        public SingleConnection(IPEndPoint remoteIPEndPoint)
        {
            IPAddress bindIP = IPAddress.Any;
            this.BindIPEndPoint = new IPEndPoint(bindIP, 0);
            this.RemoteIPEndPoint = remoteIPEndPoint;
        }

        /// <summary>
        /// Constractor
        /// </summary>
        /// <param name="bindIPAddress">local ip address bind in this socket</param>
        /// <param name="remoteIPEndPoint">remote IPEndPoint</param>
        public SingleConnection(IPAddress bindIPAddress, IPEndPoint remoteIPEndPoint)
        {
            this.BindIPEndPoint = new IPEndPoint(bindIPAddress, 0);
            this.RemoteIPEndPoint = remoteIPEndPoint;
        }

        /// <summary>
        /// Constractor
        /// </summary>
        /// <param name="bindIPAddress">local ip address bind in this socket</param>
        /// <param name="remoteIPAddress">remote server ip address</param>
        /// <param name="remotePort">remote server tcp port</param>
        public SingleConnection(IPAddress bindIPAddress, IPAddress remoteIPAddress, int remotePort)
            :this(bindIPAddress, new IPEndPoint(remoteIPAddress, remotePort))
        {
            
        }

        /// <summary>
        /// Constractor
        /// </summary>
        /// <param name="bindIPAddress">local ip address bind in this socket</param>
        /// <param name="remoteIPAddress">remote server ip address</param>
        /// <param name="remotePort">remote server tcp port</param>
        public SingleConnection(IPAddress bindIPAddress, string remoteIPAddress, int remotePort)
            : this(bindIPAddress, new IPEndPoint(IPAddress.Parse(remoteIPAddress), remotePort))
        {

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

        private void OnDisconnectEvent(SCB scb)
        {
            if (_SCB != scb)
            {
                //if connect immediately after call disconnect.
                //the scb is the last connection and ignore it.
                return;
            }

            Connected = false;

            ClearChannelForSync();

            EventHandler<Event.DisconnectEventArgs> disconnectEventHandler = RemoteDisconnected;

            if (disconnectEventHandler != null)
            {
                try
                {
                    disconnectEventHandler(this, new Event.DisconnectEventArgs(scb.RemoteIPEndPoint));
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

        private void OnReceiveEvent(SCB scb, MessageFlag flag, UInt32 evt, UInt16 group, 
            UInt32 channel, byte[] data)
        {
            SyncBlock syncBlock;
            if ((flag & MessageFlag.Sync) != 0)
            {
                if (TryGetSyncChannel(channel, out syncBlock))
                {
                    syncBlock.RetData = data;
                    syncBlock.AutoEvent.Set();
                }
            }
            else
            {
                EventHandler<Event.ReceiveEventArgs> receiveEventHandler = ReceiveEventHandler;

                if (receiveEventHandler != null)
                {
                    try
                    {
                        receiveEventHandler(this, new Event.ReceiveEventArgs(scb.Id,
                            scb.RemoteIPEndPoint, flag, evt, group, channel, data));
                    }
                    catch
                    {
                    }
                }
            }
        }

        #endregion

        #region public methods

        /// <summary>
        /// Connect to server
        /// </summary>
        public void Connect()
        {
            Connect(30000); //Default timeout is 30 seconds
        }

        /// <summary>
        /// Connect to server
        /// </summary>
        /// <param name="millisecondsTimeout">connect timeout, in millisecond</param>
        public void Connect(int millisecondsTimeout)
        {
            lock (_ConnectLock)
            {
                if (millisecondsTimeout < 0)
                {
                    throw new ArgumentException("milliseconds can't be negative");
                }

                InitVar();

                _Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _Socket.Bind(this.BindIPEndPoint);

                _Socket.BeginConnect(this.RemoteIPEndPoint, Async_Connection, _Socket);
                if (!_ConnectEvent.WaitOne(millisecondsTimeout))
                {
                    Disconnect();
                    throw new NTcpException(string.Format("Try to connect to remote server {0} timeout", this.RemoteIPEndPoint), 
                        ErrorCode.ConnectTimeout);
                }
                else if (_ConnectException != null)
                {
                    Disconnect();
                    throw _ConnectException;
                }
                
                _Socket.NoDelay = true;
                _Socket.SendBufferSize = 16 * 1024;

                _SCB = new SCB(_Socket);
                _SCB.OnReceive = OnReceiveEvent;
                _SCB.OnError = OnErrorEvent;
                _SCB.OnDisconnect = OnDisconnectEvent;

                _SendMessageQueue = new SendMessageQueue(OnReadyToSend);

                Connected = true;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void Disconnect()
        {
            try
            {
                Connected = false;

                if (_SendMessageQueue != null)
                {
                    if (!_SendMessageQueue.Closed)
                    {
                        _SendMessageQueue.Close();
                    }
                }
            }
            catch
            {
            }

            try
            {
                ClearChannelForSync();
            }
            catch
            {

            }

            if (_SendMessageQueue != null)
            {
                _SendMessageQueue.Join(10000);
            }

            try
            {
                if (_Socket != null)
                {
                    _Socket.Close();
                }
            }
            catch
            {

            }
            finally
            {
                _SendMessageQueue = null;
                _Socket = null;
            }
        }

        /// <summary>
        /// Close
        /// </summary>
        public void Close()
        {
            if (Closed)
            {
                return;
            }

            try
            {
                Disconnect();
            }
            catch
            {

            }
            finally
            {
                Closed = true;
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
            InnerASend(MessageFlag.None, evt, group, data);
        }

        /// <summary>
        /// Synchronously sends data to the remote host specified in the RemoteIPEndPoint
        /// </summary>
        /// <param name="evt">message event</param>
        /// <param name="data">An array of type Byte  that contains the data to be sent. </param>
        /// <returns>An array of type Byte  that contains the data that return from remote host</returns>
        public byte[] SSend(UInt32 evt, byte[] data)
        {
            return InnerSSend(MessageFlag.Sync, evt, 0, data, Timeout.Infinite);
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
            return InnerSSend(MessageFlag.Sync, evt, 0, data, millisecondsTimeout);
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
            return InnerSSend(MessageFlag.Sync, evt, group, data, millisecondsTimeout);
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

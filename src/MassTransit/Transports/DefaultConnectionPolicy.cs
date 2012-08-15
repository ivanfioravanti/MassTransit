// Copyright 2007-2011 Chris Patterson, Dru Sellers, Travis Smith, et. al.
//  
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use 
// this file except in compliance with the License. You may obtain a copy of the 
// License at 
// 
//     http://www.apache.org/licenses/LICENSE-2.0 
// 
// Unless required by applicable law or agreed to in writing, software distributed 
// under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR 
// CONDITIONS OF ANY KIND, either express or implied. See the License for the 
// specific language governing permissions and limitations under the License.
namespace MassTransit.Transports
{
    using System;
    using System.Threading;
    using Logging;
    using Magnum.Extensions;

    public class DefaultConnectionPolicy :
        ConnectionPolicy
    {
        readonly ConnectionHandler _connectionHandler;
        readonly TimeSpan _reconnectDelay;
        readonly ILog _log = Logger.Get(typeof(DefaultConnectionPolicy));
        readonly ReaderWriterLockSlim _connectionlLock = new ReaderWriterLockSlim();

        public DefaultConnectionPolicy(ConnectionHandler connectionHandler)
        {
            _connectionHandler = connectionHandler;
            _reconnectDelay = 1.Seconds();
        }

        public void Execute(Action callback)
        {
            try
            {
                try
                {
                    // wait here so we can be sure that there is not a reconnect in progress
                    _connectionlLock.EnterReadLock();
                    callback();
                }
                finally
                {
                    _connectionlLock.ExitReadLock();
                }
            }
            catch (InvalidConnectionException ex)
            {
                _log.Warn("Invalid Connection when executing callback", ex.InnerException);

                Reconnect();

                if (_log.IsDebugEnabled)
                {
                    _log.Debug("Retrying callback after reconnect.");
                }

                try
                {
                    _connectionlLock.EnterReadLock();
                    callback();
                }
                finally
                {
                    _connectionlLock.ExitReadLock();
                }
            }
        }

        private void Reconnect()
        {
            if (_connectionlLock.TryEnterWriteLock(100))
            {
                try
                {
                    if (_log.IsDebugEnabled)
                    {
                        _log.Debug("Disconnecting connection handler.");
                    }
                    _connectionHandler.Disconnect();

                    if (_reconnectDelay > TimeSpan.Zero)
                        Thread.Sleep(_reconnectDelay);

                    if (_log.IsDebugEnabled)
                    {
                        _log.Debug("Re-connecting connection handler...");
                    }
                    _connectionHandler.Connect();
                }
                finally
                {
                    _connectionlLock.ExitWriteLock();
                }
            }
            else
            {
                try
                {
                    _connectionlLock.EnterReadLock();
                    if (_log.IsDebugEnabled)
                    {
                        _log.Debug("Waiting for reconnect in another thread.");
                    }
                }
                finally
                {
                    _connectionlLock.ExitReadLock();
                }
            }
        }
    }
}
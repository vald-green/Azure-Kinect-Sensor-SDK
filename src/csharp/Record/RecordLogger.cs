﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Microsoft.Azure.Kinect.Sensor.Record
{
    /// <summary>
    /// The Azure Kinect logging system. Enables access to the debug messages from the the Azure Kinect Record and Playback API.
    /// </summary>
    public static class RecordLogger
    {
        private static readonly object SyncRoot = new object();
        private static readonly NativeMethods.k4a_logging_message_cb_t DebugMessageHandler = OnDebugMessage;
        private static bool isInitialized;

        private static event Action<LogMessage> LogMessageHandlers;

        private class RecordLoggerProvider : ILoggingProvider
        {
            public event Action<LogMessage> LogMessage
            {
                add
                {
                    RecordLogger.LogMessage += value;
                }
                remove
                {
                    RecordLogger.LogMessage -= value;
                }
            }
        }

        private readonly static RecordLoggerProvider loggerProvider = new RecordLoggerProvider();

        public static ILoggingProvider LogProvider
        {
            get
            {
                return RecordLogger.loggerProvider;
            }
        }

        /// <summary>
        /// Occurs when the Azure Kinect Sensor Record and Playback SDK delivers a debug message.
        /// </summary>
        public static event Action<LogMessage> LogMessage
        {
            add
            {
                lock (SyncRoot)
                {
                    if (!RecordLogger.isInitialized)
                    {
                        RecordLogger.Initialize();
                    }

                    LogMessageHandlers += value;
                }
            }

            remove
            {
                lock (SyncRoot)
                {
                    LogMessageHandlers -= value;
                }
            }
        }

        /// <summary>
        /// Initializes the <see cref="RecordLogger"/> class to begin receiving messages from the Azure Kinect Sensor SDK.
        /// </summary>
        public static void Initialize()
        {
            lock (SyncRoot)
            {
                if (RecordLogger.isInitialized)
                {
                    return;
                }

                AppDomain.CurrentDomain.ProcessExit += CurrentDomain_Exit;
                AppDomain.CurrentDomain.DomainUnload += CurrentDomain_Exit;
                NativeMethods.k4a_result_t result = NativeMethods.k4a_record_set_debug_message_handler(DebugMessageHandler, IntPtr.Zero, LogLevel.Trace);
                if (result != NativeMethods.k4a_result_t.K4A_RESULT_SUCCEEDED)
                {
                    throw new AzureKinectException("Failed to set the Debug Message Handler");
                }

                RecordLogger.isInitialized = true;
            }
        }

        /// <summary>
        /// Resets the logger to an uninitialized state. This is used in the Unit Tests to ensure that the
        /// initialization is run during the unit tests.
        /// </summary>
        internal static void Reset()
        {
            lock (SyncRoot)
            {
                if (!RecordLogger.isInitialized)
                {
                    return;
                }

                AppDomain.CurrentDomain.ProcessExit -= CurrentDomain_Exit;
                AppDomain.CurrentDomain.DomainUnload -= CurrentDomain_Exit;

                // TODO: This won't work as Raise Error has an invalid pointer.
                ////NativeMethods.k4a_result_t result = NativeMethods.k4a_set_debug_message_handler(null, IntPtr.Zero, LogLevel.Trace);
                ////if (result != NativeMethods.k4a_result_t.K4A_RESULT_SUCCEEDED)
                ////{
                ////    throw new AzureKinectException("Failed to set the Debug Message Handler");
                ////}

                RecordLogger.isInitialized = false;
            }
        }

        private static void OnDebugMessage(IntPtr context, LogLevel level, string file, int line, string message)
        {
            LogMessage data = new LogMessage(DateTime.Now, level, file, line, message);

            Action<LogMessage> eventhandler = LogMessageHandlers;
            if (eventhandler != null)
            {
                eventhandler(data);
            }
        }

        private static void CurrentDomain_Exit(object sender, EventArgs e)
        {
            NativeMethods.k4a_result_t result = NativeMethods.k4a_record_set_debug_message_handler(null, IntPtr.Zero, LogLevel.Off);
            if (result != NativeMethods.k4a_result_t.K4A_RESULT_SUCCEEDED)
            {
                Trace.WriteLine("Failed to close the debug message handler");
            }
        }
    }
}
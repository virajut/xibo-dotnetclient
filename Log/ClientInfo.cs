﻿/**
 * Copyright (C) 2020 Xibo Signage Ltd
 *
 * Xibo - Digital Signage - http://www.xibo.org.uk
 *
 * This file is part of Xibo.
 *
 * Xibo is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * any later version.
 *
 * Xibo is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with Xibo.  If not, see <http://www.gnu.org/licenses/>.
 */
using Newtonsoft.Json;
using System;
using System.Device.Location;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace XiboClient.Log
{
    public sealed class ClientInfo
    {
        public static object _locker = new object();

        private static readonly Lazy<ClientInfo>
            lazy =
            new Lazy<ClientInfo>
            (() => new ClientInfo());

        public static ClientInfo Instance { get { return lazy.Value; } }

        /// <summary>
        /// Set the schedule status
        /// </summary>
        public string ScheduleStatus;

        /// <summary>
        /// Set the required files status
        /// </summary>
        public string RequiredFilesStatus;

        /// <summary>
        /// Set the required files List
        /// </summary>
        public string RequiredFilesList;

        /// <summary>
        /// Set the schedule manager status
        /// </summary>
        public string ScheduleManagerStatus;

        /// <summary>
        /// Current Layout Id
        /// </summary>
        public string CurrentLayoutId;

        /// <summary>
        /// XMR Status
        /// </summary>
        public string XmrSubscriberStatus;

        /// <summary>
        /// Control Count
        /// </summary>
        public int ControlCount;

        /// <summary>
        /// What is currently playing
        /// </summary>
        public string CurrentlyPlaying { get; set; }

        /// <summary>
        /// Log messages
        /// </summary>
        public ConcurrentCircularBuffer LogMessages;

        /// <summary>
        /// Players current Width
        /// </summary>
        public int PlayerWidth { get; set; }

        /// <summary>
        /// Players current Height
        /// </summary>
        public int PlayerHeight { get; set; }

        /// <summary>
        /// Players current GeoCoordinate.
        /// </summary>
        public GeoCoordinate CurrentGeoLocation { get; set; }

        /// <summary>
        /// Client Info Object
        /// </summary>
        private ClientInfo()
        {
            this.LogMessages = new ConcurrentCircularBuffer(10);
        }

        /// <summary>
        /// Adds a log message
        /// </summary>
        /// <param name="message"></param>
        public void AddToLogGrid(string message, LogType logType)
        {
            LogMessage logMessage;
            try
            {
                logMessage = new LogMessage(message);
            }
            catch
            {
                logMessage = new LogMessage("Unknown", message);
            }

            this.LogMessages.Put(logMessage);
        }

        /// <summary>
        /// Update the required files text box
        /// </summary>
        public void UpdateRequiredFiles(string requiredFilesString)
        {
            RequiredFilesList = requiredFilesString;
        }

        /// <summary>
        /// Update Status Marker File
        /// </summary>
        public void UpdateStatusMarkerFile()
        {
            lock (_locker)
            {
                try
                {
                    using (FileStream file = new FileStream(Path.Combine(ApplicationSettings.Default.LibraryPath, "status.json"), FileMode.Create, FileAccess.Write, FileShare.Read))
                    {
                        using (StreamWriter sw = new StreamWriter(file))
                        {
                            using (JsonWriter writer = new JsonTextWriter(sw))
                            {
                                writer.Formatting = Formatting.Indented;
                                writer.WriteStartObject();
                                writer.WritePropertyName("lastActivity");
                                writer.WriteValue(DateTime.Now.ToString());
                                writer.WritePropertyName("state");
                                writer.WriteValue(App.Current.Dispatcher.Thread.ThreadState.ToString());
                                writer.WritePropertyName("xmdsLastActivity");
                                writer.WriteValue(ApplicationSettings.Default.XmdsLastConnection.ToString());
                                writer.WritePropertyName("xmdsCollectInterval");
                                writer.WriteValue(ApplicationSettings.Default.CollectInterval.ToString());
                                writer.WriteEndObject();
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Trace.WriteLine(new LogMessage("ClientInfo - updateStatusFile", "Failed to update status file. e = " + e.Message), LogType.Error.ToString());
                }
            }
        }

        /// <summary>
        /// Notify Status to XMDS
        /// </summary>
        public void NotifyStatusToXmds()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                using (StringWriter sw = new StringWriter(sb))
                using (JsonWriter writer = new JsonTextWriter(sw))
                {
                    writer.Formatting = Formatting.None;
                    writer.WriteStartObject();
                    writer.WritePropertyName("lastActivity");
                    writer.WriteValue(DateTime.Now.ToString());
                    writer.WritePropertyName("applicationState");
                    writer.WriteValue(Thread.CurrentThread.ThreadState.ToString());
                    writer.WritePropertyName("xmdsLastActivity");
                    writer.WriteValue(ApplicationSettings.Default.XmdsLastConnection.ToString());
                    writer.WritePropertyName("scheduleStatus");
                    writer.WriteValue(ScheduleManagerStatus);
                    writer.WritePropertyName("requiredFilesStatus");
                    writer.WriteValue(RequiredFilesStatus);
                    writer.WritePropertyName("xmrStatus");
                    writer.WriteValue(XmrSubscriberStatus);
                    writer.WriteEndObject();
                }

                StringBuilder finalSb = new StringBuilder();
                using (StringWriter sw = new StringWriter(finalSb))
                using (JsonWriter writer = new JsonTextWriter(sw))
                {
                    writer.Formatting = Formatting.None;
                    writer.WriteStartObject();
                    writer.WritePropertyName("statusDialog");
                    writer.WriteValue(sb.ToString());
                    writer.WriteEndObject();
                }

                // Notify the state of the command (success or failure)
                using (xmds.xmds statusXmds = new xmds.xmds())
                {
                    statusXmds.Url = ApplicationSettings.Default.XiboClient_xmds_xmds + "&method=notifyStatus";
                    statusXmds.NotifyStatusAsync(ApplicationSettings.Default.ServerKey, ApplicationSettings.Default.HardwareKey, finalSb.ToString());
                }

                sb.Clear();
                finalSb.Clear();
            }
            catch (Exception e)
            {
                Trace.WriteLine(new LogMessage("ClientInfo - notifyStatusToXmds", "Failed to notify status to XMDS. e = " + e.Message), LogType.Error.ToString());
            }
        }
    }
}
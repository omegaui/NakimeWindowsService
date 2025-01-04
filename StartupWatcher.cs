using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Text.Json;

/// <summary>
/// Nakime Windows Service
/// This service is responsible for tracking system's uptime timeline.
/// It overrites the session start time to a file ".live-session" in the Nakime's data folder 
/// on receiving these events: service start, service continue and system resume suspend.
/// It also saves the session timeline to a file named as "dd-mm-yyyy.json" in the Nakime's data folder
/// on receiving these events: system suspend, system shutdown, service pause and service stop.
/// The session timeline is saved in the C:\ProgramData\Nakime folder in the json format.
/// 
/// The service covers the code for both Laptop and Desktop Device's uptime tracking.
/// For Laptops: Windows 11 (as of today: 1/1/2025) usually enters into suspend state when shutdown is requested.
/// For Desktops: Windows 11 (as of today: 1/1/2025) usually enters into actual shutdown state when shutdown is requested.
/// <author> @omegaui </author>
/// </summary>
namespace NakimeWindowsService
{
    public partial class StartupWatcher : ServiceBase
    {
        class Session
        {
            public int Id {get; set;}
            public string SessionStartDay {get; set; }
            public string SessionEndDay {get; set; }
            public string SessionStartTime { get; set; }
            public string SessionEndTime { get; set; }

            public override string ToString()
            {
                return SessionStartDay + "~" + SessionStartTime + "/" + SessionEndDay + "~" + SessionEndTime;
            }
        }

        private DateTime startTime;
        private EventLog _eventLog1;
        private static readonly string nakimeAppDataDir = "C:\\ProgramData\\Nakime";
        private static readonly string liveFile = nakimeAppDataDir + "\\.live-session";

        public StartupWatcher()
        {
            InitializeComponent();
            _eventLog1 = new EventLog();
            if (!EventLog.SourceExists("Nakime"))
            {
                EventLog.CreateEventSource("Nakime", "Logs");
            }
            _eventLog1.Source = "Nakime";
            _eventLog1.Log = "Logs";
            _eventLog1.WriteEntry("Service initialized ...");
        }

        private void MakeSureStorageExists()
        {
            _eventLog1.WriteEntry("Checking if Storage exists ...");
            // Creating Nakime's Data Folder if it doesn't exist
            if (!Directory.Exists(nakimeAppDataDir))
            {
                _eventLog1.WriteEntry("Initializing storage ...");
                Directory.CreateDirectory(nakimeAppDataDir);
            }
        }

        /// <summary>
        ///  OnStart: This method is called when the service is started or when system is restarted.
        /// </summary>
        /// <param name="args"></param>
        protected override void OnStart(string[] args)
        {
            _eventLog1.WriteEntry("Service started ...");
            WriteSessionStartupData();
            base.OnStart(args);
        }

        /// <summary>
        /// OnContinue: This method is called when the service is continued after being paused.
        /// Nakime's service doesn't pause by itself, but this method and more in this file are implemented for the sake of completeness.
        /// </summary>
        protected override void OnContinue()
        {
            _eventLog1.WriteEntry("Service continued ...");
            WriteSessionStartupData();
            base.OnContinue();
        }


        /// <summary>
        /// OnStop: This method is called when the service is stopped or when system is shutting down.
        /// Nakime's service doesn't stop by itself.
        /// </summary>
        protected override void OnStop()
        {
            RequestAdditionalTime(1000 * 60 * 2);
            _eventLog1.WriteEntry("Service stopping...");
            SaveSession();
            base.OnStop();
        }

        /// <summary>
        /// OnPause: This method is called when the service is paused.
        /// Nakime's service doesn't stop by itself.
        /// </summary>
        protected override void OnPause()
        {
            _eventLog1.WriteEntry("Service pausing...");
            SaveSession();
            base.OnPause();
        }

        /// <summary>
        /// OnShutdown: This method is called when the system is shutting down, at this time,
        /// the service saves the uptime session timeline.
        /// </summary>
        protected override void OnShutdown()
        {
            RequestAdditionalTime(1000 * 60 * 2);
            _eventLog1.WriteEntry("Service shutting down ...");
            SaveSession();
            base.OnShutdown();
        }

        /// <summary>
        /// OnPowerEvent: This method is called when the system is going to suspend or resuming from suspend state.
        /// Nakime's service saves the uptime session timeline on system suspend and refreshes the session timeline on system resume,
        /// this is usually effective for Laptops.
        /// </summary>
        protected override bool OnPowerEvent(PowerBroadcastStatus powerStatus)
        {
            if (powerStatus == PowerBroadcastStatus.Suspend)
            {
                _eventLog1.WriteEntry("System going to suspend state ...");
                SaveSession();
            } 
            else if (powerStatus == PowerBroadcastStatus.ResumeSuspend)
            {
                _eventLog1.WriteEntry("System resuming from suspend state ...");
                WriteSessionStartupData();
            }
            return base.OnPowerEvent(powerStatus);
        }
        
        /// <summary>
        /// WriteSessionStartupData: This method writes the session startup time to a file named ".live-session"
        /// </summary>
        private void WriteSessionStartupData()
        {
            _eventLog1.WriteEntry("Saving live session timeline ...");
            startTime = DateTime.Now;
            // Creating nakime's storage point
            MakeSureStorageExists();
            // Write the startup time to a file ".live-session",
            // so that, Nakime's UI can get it.
            var stream = File.CreateText(liveFile);
            stream.WriteLine(DateToFileStamp(startTime));
            stream.WriteLine(DateToTimeEntry(startTime));
            stream.Flush();
            stream.Close();
            _eventLog1.WriteEntry("Saved live session timeline: " + DateToFileStamp(startTime) + "(" + DateToTimeEntry(startTime) + ")");
        }

        /// <summary>
        /// SaveSession: This method saves the session timeline to a file named as "dd-mm-yyyy.json".
        /// </summary>
        private void SaveSession()
        {
            _eventLog1.WriteEntry("Attempting to save completed session history ...");
            // Capture session timeline
            var endTime = DateTime.Now;
            var session = new Session {
                SessionStartDay = DateToFileStamp(startTime),
                SessionEndDay = DateToFileStamp(endTime),
                SessionStartTime = DateToTimeEntry(startTime),
                SessionEndTime = DateToTimeEntry(endTime),
            };
            _eventLog1.WriteEntry("Session Timeline: " + session.ToString());
            // Read Existing Sessions if any
            var sessionFile = nakimeAppDataDir + "\\" + session.SessionStartDay + ".json";
            var sessions = new List<Session>();
            if (File.Exists(sessionFile))
            {
                _eventLog1.WriteEntry("Loading previous sessions ...");
                var data = File.ReadAllText(sessionFile);
                sessions = JsonSerializer.Deserialize<List<Session>>(data);
            }
            session.Id = sessions.Count + 1;
            sessions.Add(session);
            _eventLog1.WriteEntry("Saving session: " + session.Id);
            // Save Session Data
            FileStream stream = File.Create(sessionFile);
            JsonSerializer.Serialize(stream, sessions);
            stream.Flush();
            stream.Close();
            _eventLog1.WriteEntry("Session timeline saved.");
        }

        /// <summary>
        /// Converts [date] object into "dd/mm/yyyy" format
        /// </summary>
        /// <param name="date"></param>
        /// <returns></returns>
        private string DateToFileStamp(DateTime date)
        {
            return date.Day + "-" + date.Month + "-" + date.Year;
        }

        /// <summary>
        /// Converts [date] object into "hh:mm:ss" format
        /// </summary>
        /// <param name="date"></param>
        /// <returns></returns>
        private string DateToTimeEntry(DateTime date)
        {
            return date.Hour + ":" + date.Minute + ":" + date.Second;
        }
    }
}

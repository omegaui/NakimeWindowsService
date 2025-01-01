using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.ServiceProcess;
using System.Text.Json;

/// Scenarios to Handle:
/// 1. System is turned on and off on the same day.
/// 2. System is turned on and off on different days.
/// 3. System is turned on and is alive yet.
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

        protected override void OnStart(string[] args)
        {
            _eventLog1.WriteEntry("Service started ...");
            WriteSessionStartupData();
        }

        protected override void OnContinue()
        {
            _eventLog1.WriteEntry("Service continued ...");
            WriteSessionStartupData();
        }

        protected override void OnStop()
        {
            _eventLog1.WriteEntry("Service stopping...");
            SaveSession();
        }

        protected override void OnPause()
        {
            _eventLog1.WriteEntry("Service pausing...");
            SaveSession();
        }

        protected override void OnShutdown()
        {
            _eventLog1.WriteEntry("Service shutting down ...");
            SaveSession();
        }

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

        protected override void OnSessionChange(SessionChangeDescription changeDescription)
        {
            if(changeDescription.Reason == SessionChangeReason.SessionLogon)
            {
                _eventLog1.WriteEntry("User logged in ...");
                WriteSessionStartupData();
            }
        }

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

        // Converts [date] object into "dd/mm/yyyy" format
        private string DateToFileStamp(DateTime date)
        {
            return date.Day + "-" + date.Month + "-" + date.Year;
        }

        // Converts [date] object into "hh:mm:ss" format
        private string DateToTimeEntry(DateTime date)
        {
            return date.Hour + ":" + date.Minute + ":" + date.Second;
        }
    }
}

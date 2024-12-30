using System;
using System.Collections.Generic;
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
        }

        private readonly DateTime startTime;
        private static readonly string nakimeAppDataDir = "C:\\Users\\Default\\AppData\\Local\\Nakime";
        private static readonly string liveFile = nakimeAppDataDir + "\\.live-session";

        public StartupWatcher()
        {
            InitializeComponent();
            startTime = DateTime.Now;
        }

        private void MakeSureStorageExists()
        {
            // Creating Nakime's Data Folder if it doesn't exist
            if (!Directory.Exists(nakimeAppDataDir))
            {
                Directory.CreateDirectory(nakimeAppDataDir);
            }
        }

        protected override void OnStart(string[] args)
        {
            // Creating nakime's storage point
            MakeSureStorageExists();
            // Write the startup time to a file ".live-session",
            // so that, Nakime's UI can get it.
            var stream = File.CreateText(liveFile);
            stream.WriteLine(DateToFileStamp(startTime));
            stream.WriteLine(DateToTimeEntry(startTime));
            stream.Flush();
            stream.Close();
        }

        protected override void OnStop()
        {
            SaveSession();
        }

        protected override bool OnPowerEvent(PowerBroadcastStatus powerStatus)
        {
            if (powerStatus == PowerBroadcastStatus.Suspend)
            {
                SaveSession();
            }
            return base.OnPowerEvent(powerStatus);
        }

        private void SaveSession()
        {
            // Capture session timeline
            var endTime = DateTime.Now;
            var session = new Session {
                SessionStartDay = DateToFileStamp(startTime),
                SessionEndDay = DateToFileStamp(endTime),
                SessionStartTime = DateToTimeEntry(startTime),
                SessionEndTime = DateToTimeEntry(startTime),
            };
            // Read Existing Sessions if any
            var sessionFile = session.SessionStartDay + ".json";
            var sessions = new List<Session>();
            if (File.Exists(sessionFile)) 
            {
                var data = File.ReadAllText(sessionFile);
                sessions = JsonSerializer.Deserialize<List<Session>>(data);
            }
            session.Id = sessions.Count + 1;
            sessions.Add(session);
            // Save Session Data
            FileStream stream = File.Create(sessionFile);
            JsonSerializer.Serialize(stream, sessions);
            stream.Flush();
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

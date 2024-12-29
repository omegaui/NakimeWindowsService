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
        class Timeline
        {
            public string Start { get; set; }
            public string End { get; set; }

            public override string ToString()
            {
                return Start + ">" + End;
            }
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
            SaveSessionTimeline();
        }

        protected override bool OnPowerEvent(PowerBroadcastStatus powerStatus)
        {
            if (powerStatus == PowerBroadcastStatus.Suspend)
            {
                SaveSessionTimeline();
            }
            return base.OnPowerEvent(powerStatus);
        }

        private void SaveSessionTimeline()
        {
            // current session's timeline
            var timeline = new Timeline
            {
                Start = DateToTimeEntry(startTime),
                End = DateToTimeEntry(DateTime.Now)
            }.ToString();
            var sessionStartDateFile = nakimeAppDataDir + "\\" + DateToFileStamp(startTime) + ".json";
            var previousSessions = "";
            if (File.Exists(sessionStartDateFile))
            {
                previousSessions = File.ReadAllText(sessionStartDateFile) + "\n";
            }
            previousSessions += timeline;
            File.WriteAllText(sessionStartDateFile, previousSessions);
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

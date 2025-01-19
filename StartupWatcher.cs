using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text.Json;
using System.Timers;

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
            public string Tag { get; set; }
            public string SessionStartDay {get; set; }
            public string SessionEndDay {get; set; }
            public string SessionStartTime { get; set; }
            public string SessionEndTime { get; set; }

            public override string ToString()
            {
                return SessionStartDay + "~" + SessionStartTime + "/" + SessionEndDay + "~" + SessionEndTime + " (tag: " + Tag + ")";
            }
        }

        private DateTime sessionStartedAt;
        private EventLog _eventLog1;
        private static readonly string nakimeAppDataDir = "C:\\ProgramData\\Nakime";
        private static readonly string liveFile = nakimeAppDataDir + "\\.live-session";
        private static readonly string pollFile = nakimeAppDataDir + "\\.poll-session";
        private static Timer pollingTimer;

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
            WriteSessionStartupData(tryToRetainPreviousSession: true);
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
            if (pollingTimer != null)
            {
                pollingTimer.Close();
            }
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
            if (pollingTimer != null)
            {
                pollingTimer.Close();
            }
            SaveSession();
            base.OnPause();
        }

        /// <summary>
        /// OnShutdown: This method is called when the system is shutting down, at this time,
        /// the service saves the uptime session timeline.
        /// Executes normally when shutdown is requested by the user, but not when restart is requested.
        /// </summary>
        protected override void OnShutdown()
        {
            _eventLog1.WriteEntry("Service shutting down ...");
            if (pollingTimer != null)
            {
                pollingTimer.Close();
            }
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
                if (pollingTimer != null)
                {
                    pollingTimer.Close();
                }
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
        private void WriteSessionStartupData(bool tryToRetainPreviousSession = false)
        {
            _eventLog1.WriteEntry("Saving live session timeline ...");
            sessionStartedAt = DateTime.Now;
            // Creating nakime's storage point
            MakeSureStorageExists();
            // check if triggered by OnStart
            // this session may be a restart of the previous session
            // as a result, previous session was't saved successfully
            // because of time limit inforcement of Windows OS
            if (tryToRetainPreviousSession)
            {
                _eventLog1.WriteEntry("System might be opening after restart ...");
                // check if there's a live polling file for previous session
                if (File.Exists(pollFile))
                {
                    _eventLog1.WriteEntry("Polling file exists will try to recover the last session ...");
                    // try to read previous session stats
                    var content = File.ReadAllLines(pollFile);
                    // validate previous session stats
                    if (content.Length == 2 && content[0].Contains("start$=") && content[1].Contains("end$="))
                    {
                        _eventLog1.WriteEntry("Polling data validation successful.\nPolling Data: " + content[0] + "\n" + content[1]);
                        // parse start and end timelines
                        var start = content[0];
                        var startDay = start.Substring(start.IndexOf("=") + 1, start.IndexOf('/') - start.IndexOf("=") - 1);
                        var startTime = start.Substring(start.IndexOf('/') + 1);
                        var end = content[1];
                        var endDay = end.Substring(end.IndexOf("=") + 1, end.IndexOf('/') - end.IndexOf("=") - 1);
                        var endTime = end.Substring(end.IndexOf('/') + 1);
                        // next save this recovered session
                        var session = new Session
                        {
                            SessionStartDay = startDay,
                            SessionStartTime = startTime,
                            SessionEndDay = endDay,
                            SessionEndTime = endTime,
                            Tag = "session-recovered" // add `session-recovered` tag for Nakime's UI to denote fluctuation of this data
                        };
                        _eventLog1.WriteEntry("Attempting to recover lost session: " + session.ToString());
                        SaveSession(session);
                    }
                    else
                    {
                        var data = "";
                        foreach(string dx in content)
                        {
                            data += dx + "\n";
                        }
                        _eventLog1.WriteEntry("Polling data is incorrect!! Aborting Session Recovery.\nPolling Data: " + data, EventLogEntryType.Error);
                    }
                }
            }
            // now, let's start a polling timer
            // because on Windows restart, session history is not saved because of time limit issue.
            pollingTimer = new Timer
            {
                Interval = 60000
            };
            pollingTimer.Elapsed += new ElapsedEventHandler(OnTimer);
            pollingTimer.Start();
            // Next, write the startup time to a file ".live-session",
            // so that, Nakime's UI can get it.
            var stream = File.CreateText(liveFile);
            stream.WriteLine(DateToFileStamp(sessionStartedAt));
            stream.WriteLine(DateToTimeEntry(sessionStartedAt));
            stream.Flush();
            stream.Close();
            _eventLog1.WriteEntry("Saved live session start time: " + DateToFileStamp(sessionStartedAt) + "(" + DateToTimeEntry(sessionStartedAt) + ")");
        }

        private void OnTimer(object sender, ElapsedEventArgs args)
        {
            // Write session stats to polling file every minute
            var sessionEndedAt = DateTime.Now;
            var startDay = DateToFileStamp(sessionStartedAt);
            var startTime = DateToTimeEntry(sessionStartedAt);
            var endDay = DateToFileStamp(sessionEndedAt);
            var endTime = DateToTimeEntry(sessionEndedAt);

            var start = "start$=" + startDay + "/" + startTime;
            var end = "end$=" + endDay + "/" + endTime;

            var stream = File.CreateText(pollFile);
            stream.WriteLine(start);
            stream.Write(end);
            stream.Flush();
            stream.Close();
        }

        /// <summary>
        /// SaveSession: This method saves the session timeline to a file named as "dd-mm-yyyy.json".
        /// </summary>
        private void SaveSession(Session previous = null)
        {
            _eventLog1.WriteEntry("Attempting to save completed session history ...");
            // Capture session timeline
            var endTime = DateTime.Now;
            var session = previous == null ? new Session {
                SessionStartDay = DateToFileStamp(sessionStartedAt),
                SessionEndDay = DateToFileStamp(endTime),
                SessionStartTime = DateToTimeEntry(sessionStartedAt),
                SessionEndTime = DateToTimeEntry(endTime),
                Tag = "" // no tag for non-restarted session
            } : previous;
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
            _eventLog1.WriteEntry("Saving session: " + session.Id + " in " + sessionFile);
            // Save Session Data
            try
            {
                FileStream stream = File.Create(sessionFile);
                JsonSerializer.Serialize(stream, sessions);
                stream.Flush();
                stream.Close();
                _eventLog1.WriteEntry("Session timeline saved.");
            } 
            catch (Exception e)
            {
                _eventLog1.WriteEntry("Failed to save Session timeline: " + e.ToString(), EventLogEntryType.Error);
            }
            // save session was a success
            // there's no need to keep the polling file
            if (File.Exists(pollFile))
            {
                File.Delete(pollFile);
                _eventLog1.WriteEntry("Deleted unnecessary polling data ...");
            }
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

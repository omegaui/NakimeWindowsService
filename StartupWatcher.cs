using System;
using System.Collections.Generic;
using System.IO;
using System.ServiceProcess;
using System.Text.Json;

namespace NakimeWindowsService
{
    public partial class StartupWatcher : ServiceBase
    {
        class StartupEntry
        {
            public string startTime { get; set; }
            public string endTime { get; set; }
        }

        private List<StartupEntry> entries = new List<StartupEntry>();
        private FileStream stream;
        private DateTime startTime;

        public StartupWatcher()
        {
            InitializeComponent();
            startTime = DateTime.Now;
        }

        protected override void OnStart(string[] args)
        {
            string todaysFileStamp = DateToFileStamp(startTime);
            string username = "Default";

            // check if AppData\Local\Nakime directory exists
            string parentDirPath = "C:\\Users\\" + username + "\\AppData\\Local\\Nakime";
            if (!Directory.Exists(parentDirPath))
            {
                Directory.CreateDirectory(parentDirPath);
            }

            string path = parentDirPath + "\\" + todaysFileStamp + ".json";
            if (File.Exists(path))
            {
                var data = File.ReadAllText(path);
                entries = JsonSerializer.Deserialize<List<StartupEntry>>(data);
            }
            stream = File.Create(path);
        }

        protected override void OnStop()
        {
            var upTime = DateToStartUpEntry(startTime);
            var downTime = DateToStartUpEntry(DateTime.Now);
            entries.Add(new StartupEntry() {
                startTime = upTime,
                endTime = downTime,
            });
            JsonSerializer.Serialize(stream, entries);
            stream.Flush();
            stream.Close();
        }

        private string DateToFileStamp(DateTime date)
        {
            return date.Day + "-" + date.Month + "-" + date.Year;
        }

        private string DateToStartUpEntry(DateTime date)
        {
            return date.Hour + ":" + date.Minute+ ":" + date.Second;
        }
    }
}

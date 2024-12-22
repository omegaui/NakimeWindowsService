using System;
using System.Collections.Generic;
using System.IO;
using System.ServiceProcess;
using System.Text.Json;

/// Nakime's namespace
/// [StartupWatcher] service doesn't actually watches the system
/// This is a simple system managed service that reads and write system startup records,
/// i.e. When windows boots up, this service is also started, at this time, 
/// [OnStart] method is called which captures the exact time this service started,
/// which is roughly the exact time when user boots up Windows.
/// And when, Windows is shutting down, [onStop] functions writes the entry to a file
/// named as [current-date].json at C:\Users\Default\AppData\Local\Nakime.
/// This way we keep the record of every system startup and shutdown times.
/// Using this data, the frontend of the Service i.e. the actual Nakime App
/// can show various metrics to the user such as how many times the system was opened in a day,
/// the overall uptime in hours, etc.
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
        private readonly DateTime startTime;

        public StartupWatcher()
        {
            InitializeComponent();
            startTime = DateTime.Now;
        }

        protected override void OnStart(string[] args)
        {
            // Get file name from startup date
            string todaysFileStamp = DateToFileStamp(startTime);
            // Writing records to an open directory
            string username = "Default";

            // check if AppData\Local\Nakime directory exists
            string parentDirPath = "C:\\Users\\" + username + "\\AppData\\Local\\Nakime";
            if (!Directory.Exists(parentDirPath))
            {
                // if parent folder doesn't exist
                // then, we create one.
                Directory.CreateDirectory(parentDirPath);
            }

            // The path to Today's record file
            string path = parentDirPath + "\\" + todaysFileStamp + ".json";
            if (File.Exists(path))
            {
                // If record already exists,
                // then, we fetch the existing records
                // as the [OnStop] function will overrite the file, which will cause data loss.
                var data = File.ReadAllText(path);
                entries = JsonSerializer.Deserialize<List<StartupEntry>>(data);
            }
            // Whether Today's record exists or not,
            // We are going to create one.
            stream = File.Create(path);
        }

        protected override void OnStop()
        {
            // Get up time in format "hh:mm:ss" (startup time)
            var upTime = DateToStartUpEntry(startTime);
            // Get down time in format "hh:mm:ss" (shutdown time)
            var downTime = DateToStartUpEntry(DateTime.Now);
            // Let's now save the new record by appending it to the previous set of records for Today
            entries.Add(new StartupEntry() {
                startTime = upTime,
                endTime = downTime,
            });
            // [StartupEntry] contains field types as string
            // so as to omit the need to create a data adapter for DateTime serialization and deserialization.
            JsonSerializer.Serialize(stream, entries);
            // When done, first we flush the new data
            stream.Flush();
            // and lastly, close the file stream and terminate the service.
            stream.Close();
        }

        // Converts [date] object into "dd/mm/yyyy" format
        private string DateToFileStamp(DateTime date)
        {
            return date.Day + "-" + date.Month + "-" + date.Year;
        }

        // Converts [date] object into "hh:mm:ss" format
        private string DateToStartUpEntry(DateTime date)
        {
            return date.Hour + ":" + date.Minute+ ":" + date.Second;
        }
    }
}

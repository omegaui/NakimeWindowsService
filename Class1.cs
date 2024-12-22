using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace NakimeWindowsService
{
    internal class Class1
    {
        class StartupEntry
        {
            public string startTime;
            public string endTime;

            public StartupEntry(string startTime, string endTime)
            {
                this.startTime = startTime;
                this.endTime = endTime;
            }
        }

        private static List<StartupEntry> entries = new List<StartupEntry>();

        public static void Main(string[] args)
        {
            string todaysFileStamp = "DateToFileStamp(startTime)";
            string path = "C:\\Users\\arham\\Downloads\\" + todaysFileStamp + ".json";
            var stream = File.Create(path);
            var upTime = "a";
            var downTime = "b";
            var startupEntry = new StartupEntry(upTime, downTime);
            entries.Add(startupEntry);
            JsonSerializer.Serialize(stream, entries);
            stream.Flush();
            stream.Close();
        }
    }
}

# NakimeWindowsService
Keeps a record of system startups and shutdown times, everyday.
Nakime is suitable for both PCs and Laptops.

## Building from Source
- Make sure to select "Release" build flavour in project properties.
- Build the project normally from project right click popup menu in solution explorer.

## Installing
For now, either compile the project using Microsoft Visual Studio (With C# Workload)
or get the most recently compiled service file from Releases.

Make sure to have Developer Powershell installed on your edition of Windows,
with Developer Mode Enabled, also enable "sudo" mode to make these command work.

Open Developer Powershell and navigate to repo root:
```pwsh
cd bin\Release
sudo installutil .\NakimeWindowsService.exe
```

The above will install Nakime's Windows Service in your system,
then, simply do a restart for the changes to take effect.

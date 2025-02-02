
https://github.com/user-attachments/assets/8f6274f1-18b9-4f26-8694-257ce0428bbc

<div align="center">
  <img src="assets/icons/nakime-256.png"/>
  <p>Nakime Windows Service</p>
</div>

# NakimeWindowsService
Keeps a record of system uptime.
This service is suitable for both PCs and Laptops.
Check out [Nakime](https://github.com/omegaui/nakime)

## Building from Source
- Make sure to select "Release" build flavour in project properties.
- Build the project normally from project right click popup menu in solution explorer.

## Installing
Compile the project using Microsoft Visual Studio (With C# Workload).

Make sure to have Developer Powershell installed on your edition of Windows,
with Developer Mode Enabled, also enable "sudo" mode to make these command work.

Open Developer Powershell and navigate to repo root:
```pwsh
cd bin\Release
sudo installutil .\NakimeWindowsService.exe
```

The above will install Nakime's Windows Service in your system,
then, simply do a restart for the changes to take effect.

## Uninstalling
Open Developer Powershell and navigate to repo root and run:

```pwsh
cd bin\Release
sudo sc.exe stop .\NakimeWindowsService.exe
sudo sc.exe delete .\NakimeWindowsService.exe
```
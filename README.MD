<p align="center">
<img height=350px src="GithubResources\Lighthouse.png" alt="Lighthouse"></a>
</p>
<h1 align="center">Manager the status of the power for Lighthouses V2</h3>

## Instruction manual ##

* Pair the lighthouses to your pc via Bluetooth;
* Press the `start` or `stop` buttons to change the power of the Lighthouses;
* If you want the Lighthouses to turn on and off with SteamVR click on the `SteamVR add` button and change the autorun setting in SteamVR;
* If you no longer need to enable Lighthouses with SteamVR then delete the app manifest from SteamVR by clicking the `SteamVR del` button or turn off the autorun in SteamVR setting.
> Open the program, it will find all the paired base stations, then you can use the buttons to turn on and off the base stations. If you added the program manifest to SteamVR, the program will be turned on together with SteamVR, and then send a command to the Lighthouses to turn on, as well as a command to turn off when SteamVR is turned off.

Launch arguments 
| Argument   | Description                                                  |
|------------|--------------------------------------------------------------|
| `--powerOn`  | Wakes up all available base stations.                        |
| `--powerOff` | Transfers into a sleep mode are all available base stations. |
| `--reg` | Add app manifest into steamVR. |
| `--rm` | Remove app manifest. |

### Demo Video ###
[![instruction.png](GithubResources/instruction.png)](https://youtu.be/6hI87n5p4gk)

### How to use in your custom project ###

* Use LighthousePowerControl.dll to get functionality;
* Use application launch arguments to use application functionality.
***
## Potential problem ##
* Loss of connection with Windows Mixed Reality controllers;
* Unknown errors.
>The application finds Lighthouses, if they were paired in Windows, even if they are turned off.
***
## Assembly requirements ##
* [Windows.winmd from Win 10 SDK (10.0.19041.0)](https://developer.microsoft.com/ru-ru/windows/downloads/sdk-archive/)
* [openvr_api.dll](https://github.com/ValveSoftware/openvr/blob/master/bin/win64/openvr_api.dll)

[github]:https://github.com/D0rG/LighthouseV2PowerControl
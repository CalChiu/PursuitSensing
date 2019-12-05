# Pursuit Sensing
This repository is provided as an appendix to the following research paper, currently pending final review for publication in the proceedings of SUI ’19: Symposium on Spatial User Interaction

Pursuit Sensing: Extending Hand Tracking Space in Mobile VR Applications

authored by P. Chiu, K. Takashima, K. Fujita, Y. Kitamura

# Compatibility/Requirements
- Unity 2017.3.0f3
- Leap Motion SDK (Orion software v. 4.0.0+52238)
- SteamVR (Software + Unity plugin)

## Hardware Requirements
- VR headset, equipment, and setup
- Leap Motion hand tracking sensor
- STorM 32 controller and gimbal (Version 1.3 Firmware)
- Appropriate power supply

# Architecture and Contents
The repository contains the entire Unity3D project as used during the technical evaluation presented in the paper. The different experimental conditions can be reproduced by slightly changing parameters in the code (PrototypeWearableVer.cs) and (un)commenting some relevant parts, mainly:
- shiftAmplitude line 33
- angularSpeed line 336
- commented lines 334-337 (Enabling focus shift in this version causes the 3D hand to render at the shifted target, which can be corrected by using the Leap Motion PalmPosition in the appropriate referential instead of newPos at line 370. As stated in the paper, the camera latency was measured by using its normal axis i.e. newPos)

## Scripts
All core logic and relevant sources can be found under the **Assets/SCRIPTS/** folder as follows:

- OptionMenuWearableVer: Handles a user input menu for various parameters and runtime commands based on the variables located in PrototypeWearableVer.cs.
- PrototypeWearableVer: Contains the core Pursuit Sensing algorithm as presented in the paper.
- RobotController: Handles the Unity client-side communication with the industrial robot used during evaluation as ground truth. It has been included for context only as the script communicates with a specifically developed API running in a separate process.

## 3D Scene
The included 3D scene (**Wearable Prototype**) can be tested as-is or as an example of use regarding how the scripts were attached to GameObjects and so forth.

# Installation
In order to enable serial communication through Unity3D with System.IO.Ports, please set Unity's Api Compatibility Level to .NET 4.0 (through Player settings).

Integration of the Leap Motion in Unity3D requires the Unity Core Assets available [here](https://developer.leapmotion.com/unity/#5436356).

Integration of VR development requires SteamVR and the SteamVR Unity plugin available [here](https://assetstore.unity.com/packages/tools/integration/steamvr-plugin-32647).

# License
MIT

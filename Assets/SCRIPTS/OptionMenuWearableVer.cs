/*
 * Script Code containing the option menu behaviour for user parameters for the Pursuit Sensing prototype demonstration featured in:
 *
 * Pursuit Sensing: Extending Hand Tracking Space in Mobile VR Applications
 * In SUI ’19: Symposium on Spatial User Interaction, October 19–20, 2019, New Orleans.
 *
 * Example of use is available from the Unity 2017.3.1f1 project also provided as Additional Material
 */

using System.Collections;
using UnityEngine;
using Leap;
using Leap.Unity.Attributes;

public class OptionMenuWearableVer : MonoBehaviour {

    public GUISkin guiSkin;

    private string clicked = "";
    private float speed = 1.0f;
    private bool menuOpen = false;
    private Rect WindowRect = new Rect((Screen.width / 2) - 130, Screen.height / 2 -250, 260, 500);
    //private static string[] optionsPort = new string[] { " COM1", " COM2", " COM3", " COM4", " COM5", " COM6", " COM7", " COM8" };
    private static string[] optionsTracking = new string[] { " 1H", " 2H", " Auto" };

    private void Start() {

    }

    private void Update() {
        if (Input.GetKeyDown(KeyCode.Escape) && !menuOpen)
            menuOpen = true;
        else if (Input.GetKeyDown(KeyCode.Escape) && menuOpen)
            menuOpen = false;
    }

    private void OnGUI() {
        GUI.skin = guiSkin;

        GUI.Box(new Rect (0, 10, Screen.width, 25), PrototypeWearableVer.messageStatus);
        GUI.Label(new Rect(10, 40, 500, 20), "FPS: " + PrototypeWearableVer.leapFPS);
        GUI.Label(new Rect(10, 55, 500, 20), "Pitch - DELTA - Latitude : " + PrototypeWearableVer.getPitch());
        GUI.Label(new Rect(10, 70, 500, 20), "Yaw - THETA - Longitude : " + PrototypeWearableVer.getYaw());
        GUI.Label(new Rect(10, 85, 500, 20), "Palm Speed : " + PrototypeWearableVer.getSpeed());
        GUI.Label(new Rect(10, 100, 500, 20), "Tracking Mode : " + PrototypeWearableVer.trackingMode);

        if (clicked == "" && menuOpen) {
            WindowRect = GUI.Window(0, WindowRect, menuFunc, "Prototype Menu");
        } else if (clicked == "options" && menuOpen) {
            WindowRect = GUI.Window(1, WindowRect, optionsFunc, "Options");
        }
    }

    private void optionsFunc(int id) {
        GUILayout.Box("Speed");
        speed = GUILayout.HorizontalSlider(speed ,0.0f,1.0f);

        GUILayout.Label("");

        PrototypeWearableVer.enableSerial = GUILayout.Toggle(PrototypeWearableVer.enableSerial, "  Enable Prototype");
        PrototypeWearableVer.invertAxis = GUILayout.Toggle(PrototypeWearableVer.invertAxis, "  Invert Prototype Coordinates");

        GUILayout.Label("");

        //GUILayout.Box("Prototype Gimball Serial Port");
        //PrototypeWearableVer.serialPort = GUILayout.SelectionGrid(PrototypeWearableVer.serialPort, optionsPort, 4, GUI.skin.toggle);

        //GUILayout.Label("");

        GUILayout.Box("Prototype Tracking Mode");
        PrototypeWearableVer.trackingMode = GUILayout.SelectionGrid(PrototypeWearableVer.trackingMode, optionsTracking, optionsTracking.Length, GUI.skin.toggle);

        GUILayout.Label("");

        if (GUILayout.Button("Reset Prototype position")) {
            PrototypeWearableVer.resetPosition();
        }
        if (GUILayout.Button("Back")) {
            clicked = "";
        }
    }

    private void menuFunc(int id) {
        if (GUILayout.Button("Resume")) {
            menuOpen = false;
        }
        if (GUILayout.Button("Options")) {
            clicked = "options";
        }
        if (GUILayout.Button("Quit")) {
            Application.Quit();
        }
    }
}

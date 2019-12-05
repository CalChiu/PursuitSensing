/*
 * Script Code containing the reorientation algorithm for the Pursuit Sensing prototype as described in the original paper:
 *
 * Pursuit Sensing: Extending Hand Tracking Space in Mobile VR Applications
 * In SUI ’19: Symposium on Spatial User Interaction, October 19–20, 2019, New Orleans.
 *
 * Example of use is available from the Unity 2017.3.1f1 project also provided as Additional Material
 */

using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.IO;
using System.IO.Ports;
using Leap;
using Leap.Unity.Attributes;

public class PrototypeWearableVer : MonoBehaviour {

    static Controller controller = new Controller(); //Controller Object from Leap Motion SDK

    static SerialPort storm; //Serial Port to gimbal
    private Vector3 bodyOrientation; //Torso orientation vector normal to the prototype's base

    private static string crc = "3334"; //Dummy crc value for gimbal communication potocol
    private static float pitchBuffer = 0, rollBuffer = 0, yawBuffer = 0; //gimbal position in each angle updated for each frame
    private static int pitchLimit = 110, rollLimit = 75, yawLimit = 35; //Physical limits for each gimbal DoF in Abs value
    private static float palmSpeed = 0f, shiftAmplitude = 400f; //Leap Motion palm speed and focus shift distance respectively
    private static float recordStart; //Timestamp of starting frame for data recording for evaluation
    private int chestPosition = 0; //Chest yaw angle retrieved from gimbal IMU

    public static int trackingMode = 0; //Hand tracking mode (0 = single, 1 = dual)
    public static bool enableSerial, invertAxis; //UI switches to manually enable communication with gimbal and invert Leap Motion height axis respectively
    private bool enabledSerial; //boolean to revert gimbal position when enableSerial has been invoked
    private bool trackLoss; //Tracking loss boolean to revert to base position
    private bool recording; //Recording boolean to output evaluation data
    private bool pitchBounded, yawBounded; //If the gimbal is physically at its reorientation limit, we use the Leap's hand position directly
    private List<string[]> recData = new List<string[]>(); //Evaluation data structure

    public static string messageStatus, leapFPS; //Data to be displayed over the Unity3D application screen
    
    [SerializeField] private GameObject referential = null;
    [SerializeField] private GameObject rightHand = null, rightPalm = null, rightCenter = null;
    [SerializeField] private GameObject leftHand = null, leftPalm = null, leftCenter = null;
    [SerializeField] private float chestOffset = 0; //Offset in height between HMD and gimbal, can be set in Unity Inspector
    [SerializeField] private string serialPort = "";

    // ================================================================================ MATH FUNCTIONS AND TOOLS

    // Converts a provided String representing a hex number, to an appropriate Byte array
    /// Returns:
    /// The corresponding Byte array
    private static byte[] HexToByteArray(String hex) {
        int numberChars = hex.Length;
        byte[] bytes = new byte[numberChars / 2];
        for (int i = 0; i < numberChars; i += 2)
            bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
        return bytes;
    }

    // Converts an array of Bytes to a string format
    /// Returns:
    /// The corresponding string
    private static string ByteArrayToString(byte[] ba) {
        StringBuilder hex = new StringBuilder(ba.Length * 2);
        foreach (byte b in ba)
            hex.AppendFormat("{0:x2}", b);
        return hex.ToString();
    }

    // Converts a floating point number to a hexadecimal string
    /// Returns:
    /// A hexadecimal string corresponding to the float parameter
    private static string FloatToHex(float f) {
        StringBuilder sb = new StringBuilder();
        Byte[] ba = BitConverter.GetBytes(f);
        foreach (Byte b in ba)
            for (int i = 0; i < 8; i++) {
                sb.Insert(0, ((b >> i) & 1) == 1 ? "1" : "0");
            }

        string r = sb.ToString();
        byte[] bytes = new byte[4];
        for (int i = 0; i < 4; i++) {
            bytes[i] = Convert.ToByte(r.Substring(i*8, 8), 2);
        }

        return ByteArrayToString(bytes);
    }

    // Converts a binary string (Byte) to an integer
    /// Returns:
    /// The 32-encoded integer
    static int GetIntegerFromBinaryString(string binary, int bitCount) {
        if (binary.Length == bitCount && binary[0] == '1')
            return Convert.ToInt32(binary.PadLeft(32, '1'),2);
        else
            return Convert.ToInt32(binary,2);
    }

    // Method to find a specific substring in-between two provided substrings
    /// Returns:
    /// The in-between substring
    public static string getBetween(string strSource, string strStart, string strEnd) {
        int Start, End;
        if (strSource.Contains(strStart) && strSource.Contains(strEnd))
        {
            Start = strSource.IndexOf(strStart, 0) + strStart.Length;
            End = strSource.IndexOf(strEnd, Start);
            return strSource.Substring(Start, End - Start);
        }
        else
        {
            return "";
        }
    }

	// ================================================================================ Unity3D START FUNCTION
	void Start () {
        enableSerial = true; invertAxis = true; enabledSerial = true;
        trackLoss = false; recording = false; pitchBounded = false; yawBounded = false;

        controller.FrameReady += onFrame;
        controller.SetPolicy(Controller.PolicyFlag.POLICY_OPTIMIZE_HMD);
        Application.runInBackground = true;

        try {
            storm = new SerialPort(serialPort, 115200);
            if (!storm.IsOpen) storm.Open();
        } catch (Exception e) {
            Debug.LogWarning(e);
        }
    }

	// ================================================================================ Unity3D UPDATE FUNCTION
	void Update () {
        //Position reset for gimbal in case of manual serial communication switch
        if(!enableSerial && enabledSerial) {
            resetPosition();
            enabledSerial = false;
        }
        if (enableSerial && !enabledSerial) enabledSerial = true;

        //Runtime update for torso orientation and HMD height offset
        referential.transform.position = new Vector3(transform.position.x, transform.position.y - chestOffset, transform.position.z);
        referential.transform.Rotate(new Vector3(0, -(getBodyAngle()/100)*2.5f, 0), Space.World);

        //Forcing coordinate referential orientations for both hands
        rightCenter.transform.forward = -rightCenter.transform.parent.transform.up;
        leftCenter.transform.forward = leftCenter.transform.parent.transform.up;

        //User input to manually revert the gimbal
        if (Input.GetKeyDown(KeyCode.Space) && enabledSerial && storm.IsOpen) resetPosition();

        //User input to start/stop data recording
        if (Input.GetKeyDown(KeyCode.R) && !recording && enabledSerial && storm.IsOpen) {
            recording = true;
            recordStart = Time.time;
        } else if (Input.GetKeyDown(KeyCode.R) && recording && enabledSerial && storm.IsOpen) {
            outputRecording();
            recording = false;
        }
	}

    // ================================================================================ ON QUIT
    void OnApplicationQuit() {
        if (storm.IsOpen) storm.Close();
    }

    // ================================================================================ GETTERS
    public static float getPitch() {
        return pitchBuffer;
    }

    public static float getRoll() {
        return rollBuffer;
    }

    public static float getYaw() {
        return yawBuffer;
    }

    public static float getSpeed() {
        return palmSpeed;
    }

    // ================================================================================ RUNTIME COMMANDS
    // Sends a positioning command through the serial port, corresponding to the gimbal's default position
    public static void resetPosition() {
        if(pitchBuffer != 0 || rollBuffer != 0 || yawBuffer != 0) {
            byte[] resetCommand = HexToByteArray("FA0612" + "000000000000" + crc);
            pitchBuffer = 0; rollBuffer = 0; yawBuffer = 0;
            if (storm.IsOpen) {
                storm.Write(resetCommand, 0, resetCommand.Length);
            } else {
                Debug.LogWarning("[PrototypeWearableVer] Port " + storm.PortName + " is not open.");
            }
        }
    }

    // Records data for evaluation upon user input: frame timestamp, vertical position, horizontal position
    private void recordDistance(float leapSpeed, float x, float z) {
        float time = Time.time - recordStart;
        double d = Math.Sqrt(x * x + z * z);

        string[] row = new string[4];
        row[0] = time.ToString();
        row[1] = d.ToString();
        row[3] = leapSpeed.ToString();

        recData.Add(row);
    }

    // Records data for evaluation upon user input: frame timestamp, Leap Motion hand speed, Palm 3D position in meters, Ground Truth 3D position in meters
    private void recordPosition(float leapSpeed, float x, float y, float z) {
        float time = Time.time - recordStart;
        float[] truth = new float[3];
        if (RobotController.started) truth = RobotController.GetPose();

        string[] row = new string[8];

        row[0] = time.ToString();
        row[1] = leapSpeed.ToString();
        row[2] = (x * 1000).ToString();
        row[3] = (y * 1000).ToString();
        row[4] = (z * 1000).ToString();
        row[5] = truth[0].ToString();
        row[6] = truth[1].ToString();
        row[7] = truth[2].ToString();

        recData.Add(row);
    }

    // Outputs the recorded data structure into a csv file
    private void outputRecording() {
        string[][] output = new string[recData.Count][];

        for(int i = 0; i < output.Length; i++) {
            output[i] = recData[i];
        }

        int length = output.GetLength(0);
        string delimiter = ",";

        StringBuilder sb = new StringBuilder();

        for (int index = 0; index < length; index++)
            sb.AppendLine(string.Join(delimiter, output[index]));

        StreamWriter outStream = System.IO.File.CreateText(Application.dataPath + "/data4.csv");
        outStream.WriteLine(sb);
        outStream.Close();

        recData.Clear();
    }

    // Polling IMU data from the gimbal to retreive torso orientation. The retreived value corresponds to the controller board IMU's yaw angle
    private int getBodyAngle() {
        string res = "test"; //String to store hex value substring corresponding to desired data
        int positionBuffer = chestPosition; //Current torso orientation angle relative to default position (at program start)
        int difference; //Angle difference to apply update threshold
        byte[] response = new byte[70]; //Data recieved from gimbal upon query
        byte[] getCommand = HexToByteArray("FA0206" + "0001" + crc); //IMU data query sent to gimbal

        //GIMBAL QUERY
        if (enableSerial && storm.IsOpen) {
            storm.Write(getCommand, 0, getCommand.Length);
            storm.Read(response, 0, 70);
        }
        //Debug.Log(BitConverter.ToString(response));
        res = getBetween(BitConverter.ToString(response), "FB-08-06-00-01-", "-00-00").Substring(0, 23);

        //TORSO ORIENTATION NORMALIZATION
        if(res.Length != 0) {
            string yaw = res.Substring(15, 2) + res.Substring(12, 2);
            chestPosition = Convert.ToInt32(yaw, 16);
            if(chestPosition > 18000) { //Constraining Angle value to plus/minus 180 degrees
                yaw = Convert.ToString(Convert.ToInt32(yaw, 16), 2).PadLeft(4, '0');
                chestPosition = GetIntegerFromBinaryString(yaw, yaw.Length);
            }
            chestPosition += 18000;
        }

        //Threshold for IMU sensitivity/jitter elimination (Tested manually, depends on gimbal calibration)
        if(Math.Abs(chestPosition - positionBuffer) > 120) {
            difference = chestPosition - positionBuffer;
        } else {
            difference = 0;
        }

        //Threshold to ignore outlier spikes (Tested manually, depends on gimbal calibration)
        if (Math.Abs(difference) > 300) difference = 0;
        //Debug.Log(difference);
        return difference;
    }

    // ================================================================================ LEAP ONFRAME / MAIN ALGORITHM LOOP
    private void onFrame(object sender, FrameEventArgs args) {

        //LEAP MOTION FRAME RETRIEVING
        Frame currentFrame = args.frame;
        leapFPS = currentFrame.CurrentFramesPerSecond.ToString();

        //SINGLE HAND TRACKING MODE (EITHER RIGHT OR LEFT AUTOMATICALLY)
        if (currentFrame.Hands.Count != 0 && trackingMode == 0) {
            trackLoss = false;

            Hand hand = currentFrame.Hands[0];
            Vector pos = hand.PalmPosition;
            Vector3 posBuffer = new Vector3(pos.x, pos.y, pos.z);

            messageStatus = "Single hand detected";
            if (hand.IsLeft) {
                if (!leftHand.activeSelf) leftHand.SetActive(true);
                if (rightHand.activeSelf) rightHand.SetActive(false);
            } else {
                if (!rightHand.activeSelf) rightHand.SetActive(true);
                if (leftHand.activeSelf) leftHand.SetActive(false);
            }

            float vAngle = 0;
            float hAngle = 0;
            int angleThreshold = 8; float angularSpeed = 1f;

            //LEAP MOTION PALM SPEED POLLING
            palmSpeed = hand.PalmVelocity.Magnitude;

            //FOCUS SHIFT (COMMENT TO DISABLE)
            //if(palmSpeed > 500f) {
            //    pos += (hand.PalmVelocity.Normalized * shiftAmplitude);
            //    angularSpeed = 4f;
            //}

            //POSITION COMPUTING
            if (invertAxis) {
                vAngle = Convert.ToSingle((Math.Atan(pos.z / pos.y)) * (180 / Math.PI));
                hAngle = Convert.ToSingle((Math.Atan(pos.x / pos.y)) * (180 / Math.PI));
            } else {
                vAngle = Convert.ToSingle((Math.Atan(-pos.z / pos.y)) * (180 / Math.PI));
                hAngle = Convert.ToSingle((Math.Atan(-pos.x / pos.y)) * (180 / Math.PI));
            }

            //CENTER CONICAL BUFFER
            if (vAngle > -angleThreshold && vAngle < angleThreshold) vAngle = 0;
            if (hAngle > -angleThreshold && hAngle < angleThreshold) hAngle = 0;

            //OVERWRITING WITH SPEED VALUE TO SEND AS COMMAND
            if (vAngle < 0) vAngle = -angularSpeed;
            else if (vAngle > 0) vAngle = angularSpeed;
            if (hAngle < 0) hAngle = -angularSpeed;
            else if (hAngle > 0) hAngle = angularSpeed;

            pitchBuffer = Convert.ToSingle(pitchBuffer + vAngle); //DELTA angle in spherical
            yawBuffer = Convert.ToSingle(yawBuffer + hAngle); //THETA angle in spherical

            //PHYSICAL LIMITS CONSTRAINTS
            pitchBounded = false; yawBounded = false;
            if (pitchBuffer > pitchLimit) { pitchBuffer = pitchLimit; pitchBounded = true; }
            else if (pitchBuffer < -pitchLimit) { pitchBuffer = -pitchLimit; pitchBounded = true; }

            if (yawBuffer > yawLimit) { yawBuffer = yawLimit; yawBounded = true; }
            else if (yawBuffer < -yawLimit) { yawBuffer = -yawLimit; yawBounded = true; }

            //SPHERICAL COORDINATES CALCULATION
            Vector3 newPos;
            newPos.z = (float)((pos.y / 1000) * Math.Cos(-pitchBuffer * Math.PI / 180) * Math.Cos(yawBuffer * Math.PI / 180));
            newPos.x = (float)(-(pos.y / 1000) * Math.Cos(-pitchBuffer * Math.PI / 180) * Math.Sin(yawBuffer * Math.PI / 180));
            newPos.y = (float)((pos.y / 1000) * Math.Sin(-pitchBuffer * Math.PI / 180));

            if (pitchBounded) newPos.y += pos.z / 1000;
            if (yawBounded) newPos.x -= pos.x / 1000;

            //HMD POSITION CALCULATION
            newPos = referential.transform.TransformPoint(newPos + new Vector3(0, 0, chestOffset));

            //HAND COORDINATE REFERENTIAL POSITION UPDATE
            float dist = ((hand.PalmPosition - hand.WristPosition) / 1000).Magnitude;
            rightCenter.transform.localPosition = new Vector3(dist, 0, 0.015f);
            leftCenter.transform.localPosition = new Vector3(-dist, 0, -0.01f);

            //VE HAND DISPLAYING (the SetPosition function was written specifically to force runtime hand model position update)
            Leap.Unity.RiggedHand.SetPosition(newPos);

            //DATA RECORDING IF REQUESTED
            if (recording) recordPosition(palmSpeed, newPos.x, newPos.y, newPos.z);

            //SENDING COMMAND TO GIMBALL (Please refer to SToRM32 controller documentation for command format)
            if (hand.IsRight || hand.IsLeft) { //Evaluation was performed with either single hand trackable, switching manually as mentioned in the paper can be easily implemented here

                string pitchCommand = FloatToHex(pitchBuffer).Substring(6, 2) + FloatToHex(pitchBuffer).Substring(4, 2) + FloatToHex(pitchBuffer).Substring(2, 2) + FloatToHex(pitchBuffer).Substring(0, 2);
                string rollCommand = FloatToHex(rollBuffer).Substring(6, 2) + FloatToHex(rollBuffer).Substring(4, 2) + FloatToHex(rollBuffer).Substring(2, 2) + FloatToHex(rollBuffer).Substring(0, 2);
                string yawCommand = FloatToHex(yawBuffer).Substring(6, 2) + FloatToHex(yawBuffer).Substring(4, 2) + FloatToHex(yawBuffer).Substring(2, 2) + FloatToHex(yawBuffer).Substring(0, 2);
                string flagstype = "0000";

                byte[] command = HexToByteArray("FA0E11" + pitchCommand + rollCommand + yawCommand + flagstype + crc);

                if (enableSerial && storm.IsOpen) storm.Write(command, 0, command.Length);
            }
        } else if (currentFrame.Hands.Count >= 2 && trackingMode == 1) { //DUAL HAND TRACKING MODE
            trackLoss = false;

            Hand hand1 = currentFrame.Hands[0]; Hand hand2 = currentFrame.Hands[1];
            if(hand1.IsRight != hand2.IsRight) {
                messageStatus = "Both hand detected";
                Vector pos1 = hand1.PalmPosition; Vector pos2 = hand2.PalmPosition;
                Vector mid = (pos2 + pos1) / 2;

                //POSITION COMPUTING
                float vAngle = 0;
                float hAngle = 0;

                if (invertAxis) {
                    vAngle = Convert.ToSingle((Math.Atan(mid.z / mid.y)) * (180 / Math.PI));
                    hAngle = Convert.ToSingle((Math.Atan(mid.x / mid.y)) * (180 / Math.PI));
                } else {
                    vAngle = Convert.ToSingle((Math.Atan(-mid.z / mid.y)) * (180 / Math.PI));
                    hAngle = Convert.ToSingle((Math.Atan(-mid.x / mid.y)) * (180 / Math.PI));
                }

                int angleThreshold = 8; int angularSpeed = 1; int hThreshold = 35;

                if (vAngle > -angleThreshold && vAngle < angleThreshold) vAngle = 0;
                if (hAngle > -angleThreshold && hAngle < angleThreshold) hAngle = 0;
                if (hAngle < -hThreshold || hAngle > hThreshold) angularSpeed *= 2;

                if (vAngle < 0) vAngle = -angularSpeed;
                else if (vAngle > 0) vAngle = angularSpeed;
                if (hAngle < 0) hAngle = -angularSpeed;
                else if (hAngle > 0) hAngle = angularSpeed;

                pitchBuffer = Convert.ToInt32(pitchBuffer + vAngle); //DELTA
                yawBuffer = Convert.ToInt32(yawBuffer + hAngle); //THETA

                if(pitchBuffer > pitchLimit) {
                    pitchBuffer = pitchLimit;
                } else if (pitchBuffer < -pitchLimit) {
                    pitchBuffer = -pitchLimit;
                }
                if(yawBuffer > yawLimit) {
                    yawBuffer = yawLimit;
                } else if (yawBuffer < -yawLimit) {
                    yawBuffer = -yawLimit;
                }

                Vector3 newPos;
                newPos.z = (float)((mid.y / 1000) * Math.Cos(-pitchBuffer * Math.PI / 180) * Math.Cos(yawBuffer * Math.PI / 180));
                newPos.x = (float)(-(mid.y / 1000) * Math.Cos(-pitchBuffer * Math.PI / 180) * Math.Sin(yawBuffer * Math.PI / 180));
                newPos.y = (float)((mid.y / 1000) * Math.Sin(-pitchBuffer * Math.PI / 180));

                if (hand1.IsLeft) {
                    rightCenter.transform.localPosition = new Vector3(((hand2.PalmPosition - hand2.WristPosition) / 1000).Magnitude, 0, 0.015f);
                    leftCenter.transform.localPosition = new Vector3(-((hand1.PalmPosition - hand1.WristPosition) / 1000).Magnitude, 0, -0.01f);
                } else {
                    rightCenter.transform.localPosition = new Vector3(((hand1.PalmPosition - hand1.WristPosition) / 1000).Magnitude, 0, 0.015f);
                    leftCenter.transform.localPosition = new Vector3(-((hand2.PalmPosition - hand2.WristPosition) / 1000).Magnitude, 0, -0.01f);
                }

                //COMMAND TO GIMBALL
                string pitchCommand = FloatToHex(pitchBuffer).Substring(6, 2) + FloatToHex(pitchBuffer).Substring(4, 2) + FloatToHex(pitchBuffer).Substring(2, 2) + FloatToHex(pitchBuffer).Substring(0, 2);
                string rollCommand = "00000000";
                string yawCommand = FloatToHex(yawBuffer).Substring(6, 2) + FloatToHex(yawBuffer).Substring(4, 2) + FloatToHex(yawBuffer).Substring(2, 2) + FloatToHex(yawBuffer).Substring(0, 2);
                string flagstype = "0000";

                byte[] command = HexToByteArray("FA0E11" + pitchCommand + rollCommand + yawCommand + flagstype + crc);

                if (enableSerial && storm.IsOpen) {
                    storm.Write(command, 0, command.Length);
                }
            }
        } else {
            messageStatus = "No Hands detected";

            if(!trackLoss) {
                trackLoss = true;
                resetPosition();
            }
        }
    }
}

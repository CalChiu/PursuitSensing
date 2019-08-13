using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

public class RobotController : MonoBehaviour {

    struct NR_POSE
    {
        public float fX;
        public float fY;
        public float fZ;
        public float fRoll;
        public float fPitch;
        public float fYaw;
    }

    static TcpClient c = new TcpClient();

    private int forcedRate = 10, currentFrame; //18
    private Vector3 position;
    public static bool started = false;

    // Use this for initialization
    IEnumerator Start()
    {
        position = new Vector3();

        Process proc = new Process();
        proc.StartInfo.UseShellExecute = false;
        proc.StartInfo.CreateNoWindow = true;
        proc.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
        proc.StartInfo.FileName = "RobotReader.exe";
        proc.StartInfo.Arguments = string.Format(@"\\SYS25 create MySvc binPath= C:\mysvc.exe");
        proc.StartInfo.RedirectStandardError = false;
        proc.StartInfo.RedirectStandardOutput = false;
        proc.StartInfo.UseShellExecute = false;
        proc.StartInfo.CreateNoWindow = true;
        proc.Start();

        yield return new WaitForSeconds(2);
        print("try connect");
        c.Connect("localhost", 8888);
        print("connected");

        started = true;
        SwitchRobot(true);

        yield return new WaitForSeconds(1);
    }

    private float[] initialPose = new float[6] { 550, 0, 380, -180, 90, -180 };
    float lastRecord;
    bool currentRobot;

	// Update is called once per frame
	void Update () {
        if (Input.GetKey(KeyCode.UpArrow)) {
            SwitchRobot(true);
            SetLocation(initialPose);

            position = Vector3.zero;
        }
    }
    
    void OnDestroy()
    {
        if (c.Connected)
            c.Close();
    }

    public void SwitchRobot(bool on)
    {
        List<byte> bytes = new List<byte>();
        bytes.Add(101);
        bytes.Add(101);
        if (on)
            bytes.Add(1);
        else
            bytes.Add(0);
        c.GetStream().Write(bytes.ToArray(), 0, bytes.Count);
        currentRobot = on;
    }
    public void SetLocation(float[] data) {
        for (int i = 0; i < data.Length; i++)
        {
            data[i] += initialPose[i];
        }
        List<byte> bytes = new List<byte>();
        bytes.Add(102);
        bytes.Add(102);
        foreach (var f in data)
        {
            bytes.AddRange(BitConverter.GetBytes(f));
        }
        c.GetStream().Write(bytes.ToArray(), 0, bytes.Count);
    }

    public static float[] GetPose() {
        byte[] res = new byte[1024];
        List<byte> bytes = new List<byte>();
        bytes.Add(102);
        bytes.Add(102);
        bytes.Add(107);
        c.GetStream().Write(bytes.ToArray(), 0, bytes.Count);
        c.GetStream().Read(res, 0, res.Length);

        float[] floats = new float[res.Length / 4];

        for (int i = 0; i < res.Length / 4; i++)
            floats[i] = BitConverter.ToSingle(res, i * 4);

        UnityEngine.Debug.Log(floats[0]);
        return floats;
    }
}

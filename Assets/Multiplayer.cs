using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Networking.Types;
using UnityEngine.VR.WSA;
using UnityEngine.VR.WSA.Sharing;

public class Multiplayer : MonoBehaviour {

	private int key = 1;
	private int version = 1;
	private int subversion = 1;
	private int broadcastInterval = 100; // ms
	private int port = 8888;
	private int hostId, reliable, reliableFrag, unreliable;
	private Dictionary<int, string> otherPlayerConnections;
	private MicrophoneManager micMan;
	string deviceId;
	public bool ignoreSelf = true;
	public bool recieve = true;
	public bool send = true;
	private bool myWorldAnchorStatus = false;
	private Dictionary<int, bool> otherPlayerWorldAnchorStatus;
	private const uint MinTrustworthySerializedAnchorDataSize = 500000;
	private List<byte> exportingAnchorBytes;
	private List<byte> recievingAnchorBytes;
	private int recievingAnchorBytesLength;
	private int currentRecieveIndex = 0;
	private int currentRecieveCon = 0;
	private bool exporting = false, sending = false;
	ConnectionConfig config;

	// Use this for initialization
	void Start ()
	{
		otherPlayerConnections = new Dictionary<int, string>();
		otherPlayerWorldAnchorStatus = new Dictionary<int, bool>();
		micMan = gameObject.GetComponent<MicrophoneManager>();
		byte error;
		NetworkTransport.Init();
		config = new ConnectionConfig();
		reliable = config.AddChannel(QosType.Reliable);
		reliableFrag = config.AddChannel(QosType.ReliableSequenced);
		unreliable = config.AddChannel(QosType.Unreliable);
		HostTopology topology = new HostTopology(config, 100);
		hostId = NetworkTransport.AddHost(topology, port);
		deviceId = SystemInfo.deviceUniqueIdentifier;
		byte[] bytes = Encoding.ASCII.GetBytes(deviceId);
		NetworkTransport.StartBroadcastDiscovery(hostId, port, key, version, subversion, bytes, bytes.Length, broadcastInterval, out error);
		NetworkTransport.SetBroadcastCredentials(hostId, key, version, subversion, out error);
		//Invoke("TestBC", 1);
		Invoke("TestSendBigData", .5f);
		InvokeRepeating("BroadcastWorldAnchorStatus", 1, 1);
		InvokeRepeating("BroadcastWorldAnchor", 2, 2);
	}

	void BroadcastWorldAnchorStatus()
	{
		if (myWorldAnchorStatus)
		{
			BroadcastSpeech("I have a world anchor");
		}
		else
		{
			BroadcastSpeech("I need a world anchor");
		}
	}

	void BroadcastWorldAnchor()
	{
		if (send && !sending)
		{
			if (myWorldAnchorStatus)
			{
				byte[] bytes = exportingAnchorBytes.ToArray();
				byte error;
				foreach (var player in otherPlayerConnections)
				{
					if (ignoreSelf || !otherPlayerWorldAnchorStatus.ContainsKey(player.Key) || !otherPlayerWorldAnchorStatus[player.Key])
					{
						// I have a world anchor and this new player needs it
						sending = true;
						Debug.Log("Sending watb of length " + bytes.Length + " to conId:" + player.Key + ":" + player.Value);
						byte[] sizeByteArray = BitConverter.GetBytes(bytes.Length);
						NetworkTransport.Send(hostId, player.Key, reliableFrag, sizeByteArray, sizeByteArray.Length, out error);
						int chunks = (int)Math.Ceiling(bytes.Length / 1024f);
						Debug.Log("breaking " + bytes.Length + " into " + chunks + " chunks");
						for (int i = 0; i < chunks; i++)
						{
							byte[] chunk = bytes.Skip(i * 1024).Take(1024).ToArray();
							NetworkTransport.Send(hostId, player.Key, reliableFrag, chunk, chunk.Length, out error);
						}
						Debug.Log("Sent watb of length " + bytes.Length + " to conId:" + player.Key + ":" + player.Value + " - " + (NetworkError)error);
					}
				}
			}
			else
			{
				if (!otherPlayerWorldAnchorStatus.ContainsValue(true) && !exporting)
				{
					// No-one has a world anchor. I'll make one.
					ExportWorldAnchor();
					exporting = true;
				}
			}
		}
	}

	private void ExportWorldAnchor()
	{
		var watb = new WorldAnchorTransferBatch();
		watb.AddWorldAnchor("gordon", gameObject.GetComponent<WorldAnchor>());
		exportingAnchorBytes = new List<byte>();
		WorldAnchorTransferBatch.ExportAsync(watb, OnExportDataAvailable, OnExportComplete);
	}

	private void OnExportDataAvailable(byte[] data)
	{
		Debug.Log(data.Length);
		exportingAnchorBytes.AddRange(data);
	}

	private void OnExportComplete(SerializationCompletionReason completionReason)
	{
		if (completionReason == SerializationCompletionReason.Succeeded && exportingAnchorBytes.Count > MinTrustworthySerializedAnchorDataSize)
		{
			Debug.Log("watb export success - length: " + exportingAnchorBytes.Count);
			myWorldAnchorStatus = true;
		}
		else
		{
			if (otherPlayerWorldAnchorStatus.ContainsValue(true))
			{
				Debug.Log("watb export failed, but in the meantime someone else successfully made one. Will wait for them to send it to me");
			}
			else
			{
				Debug.Log("watb export failed, retrying");
				ExportWorldAnchor();
			}
		}
	}

	void TestBC()
	{
		BroadcastSpeech("Test!");
	}

	void TestSendBigData()
	{
		exportingAnchorBytes = new List<byte>(new byte[ushort.MaxValue * 2]);
		myWorldAnchorStatus = true;
		BroadcastWorldAnchor();
	}

	public void BroadcastSpeech(string text)
	{
		if (send)
		{
			byte error;
			byte[] bytes = Encoding.ASCII.GetBytes(text);
			foreach (var player in otherPlayerConnections)
			{
				NetworkTransport.Send(hostId, player.Key, reliable, bytes, bytes.Length, out error);
				Debug.Log("Sent " + text + " to conId:" + player.Key + ":" + player.Value + " - " + (NetworkError)error);
			}
		}
	}
	
	// Update is called once per frame
	void Update () {
		int connectionId;
		int channelId;
		byte[] recBuffer = new byte[1024];
		int bufferSize = 1024;
		int dataSize;
		byte error;
		int recPort;
		string addr;
		NetworkEventType recData = NetworkTransport.ReceiveFromHost(hostId, out connectionId, out channelId, recBuffer, bufferSize, out dataSize, out error);
		switch (recData)
		{
			case NetworkEventType.Nothing:         //1
				break;
			case NetworkEventType.ConnectEvent:    //2
				NetworkID network;
				NodeID dstNode;
				NetworkTransport.GetConnectionInfo(hostId, connectionId, out addr, out recPort, out network, out dstNode, out error);
				Debug.Log("Connect request: " + (NetworkError)error + " on conId " + connectionId + " addr " + addr + " port " + recPort + " net " + network + " dstNode " + dstNode);
				if (!otherPlayerConnections.ContainsValue(addr))
				{
					otherPlayerConnections[connectionId] = addr;
				}
				break;
			case NetworkEventType.DataEvent:       //3
				if (recieve)
				{
					if (channelId == reliable)
					{
						string data = Encoding.ASCII.GetString(recBuffer, 0, dataSize);
						if (data == "I have a world anchor")
						{
							otherPlayerWorldAnchorStatus[connectionId] = true;
						}
						else if (data == "I need a world anchor")
						{
							otherPlayerWorldAnchorStatus[connectionId] = false;
						}
						else
						{
							Debug.Log("Got data: " + data + " on conId " + connectionId);
							micMan.HandleSpeech(data);
						}
					}
					else if (channelId == reliableFrag)
					{
						if (connectionId != currentRecieveCon)
						{
							recievingAnchorBytes = new List<byte>();
							recievingAnchorBytesLength = BitConverter.ToInt32(recBuffer, 0);
							currentRecieveCon = connectionId;
						}
						else
						{
							if (connectionId == currentRecieveCon)
							{
								recievingAnchorBytes.AddRange(recBuffer);
								if (recievingAnchorBytes.Count() >= recievingAnchorBytesLength)
								{
									Debug.Log("Got watb of length " + recievingAnchorBytes.Count + ", was expecting " + recievingAnchorBytesLength);
									WorldAnchorTransferBatch.ImportAsync(recievingAnchorBytes.ToArray(), OnImportComplete);
								}
							}
						}
					}
				}
				break;
			case NetworkEventType.DisconnectEvent: //4
				Debug.Log("Disconnect: " + (NetworkError)error + " on conId " + connectionId);
				otherPlayerConnections.Remove(connectionId);
				otherPlayerWorldAnchorStatus.Remove(connectionId);
				break;
			case NetworkEventType.BroadcastEvent:
				addr = NetworkTransport.GetBroadcastConnectionInfo(hostId, out recPort, out error);
				int recSize;
				NetworkTransport.GetBroadcastConnectionMessage(hostId, recBuffer, bufferSize, out recSize, out error);
				string message = Encoding.ASCII.GetString(recBuffer, 0, recSize);
				//Debug.Log("saw broadcast from " + addr + " with message of len " + message.Length +  ":" +  message + "|");
				if (message != deviceId || !ignoreSelf)
				{
					if (!otherPlayerConnections.ContainsValue(addr))
					{
						int conId = NetworkTransport.Connect(hostId, addr, port, 0, out error);
						Debug.Log("Connect attempt to " + addr + " result: " + (NetworkError)error + " conid:" + conId);
						otherPlayerConnections[conId] = addr;
					}
				}
				break;
		}
	}

	private void OnImportComplete(SerializationCompletionReason completionReason, WorldAnchorTransferBatch deserializedTransferBatch)
	{
		if (completionReason == SerializationCompletionReason.Succeeded)
		{
			deserializedTransferBatch.LockObject("gordon", gameObject);
		}
		else
		{
			Debug.LogError("watb import failed due to " + completionReason);
		}
	}
}

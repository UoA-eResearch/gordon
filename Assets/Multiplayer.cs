﻿using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Networking.Types;

public class Multiplayer : MonoBehaviour {

	private int key = 1;
	private int version = 1;
	private int subversion = 1;
	private int broadcastInterval = 100; // ms
	private int port = 8888;
	private int hostId, reliable, unreliable;
	private Dictionary<int, string> otherPlayerConnections;
	private MicrophoneManager micMan;
	string deviceId;
	public bool ignoreSelf = true;
	public bool recieve = true;
	public bool send = true;
	
	// Use this for initialization
	void Start ()
	{
		otherPlayerConnections = new Dictionary<int, string>();
		micMan = gameObject.GetComponent<MicrophoneManager>();
		byte error;
		NetworkTransport.Init();
		ConnectionConfig config = new ConnectionConfig();
		reliable = config.AddChannel(QosType.Reliable);
		unreliable = config.AddChannel(QosType.Unreliable);
		HostTopology topology = new HostTopology(config, 100);
		hostId = NetworkTransport.AddHost(topology, port);
		deviceId = SystemInfo.deviceUniqueIdentifier;
		byte[] bytes = Encoding.ASCII.GetBytes(deviceId);
		NetworkTransport.StartBroadcastDiscovery(hostId, port, key, version, subversion, bytes, bytes.Length, broadcastInterval, out error);
		NetworkTransport.SetBroadcastCredentials(hostId, key, version, subversion, out error);
		//Invoke("TestBC", 1);
	}

	void TestBC()
	{
		BroadcastSpeech("Test!");
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
				Debug.Log("Sent " + text + " to conId:" + player.Key + ":" + player.Value);
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
				string data = Encoding.ASCII.GetString(recBuffer);
				Debug.Log("Got data: " + data + " on conId " + connectionId);
				if (recieve)
				{
					micMan.HandleSpeech(data);
				}
				break;
			case NetworkEventType.DisconnectEvent: //4
				Debug.Log("Disconnect: " + (NetworkError)error + " on conId " + connectionId);
				otherPlayerConnections.Remove(connectionId);
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
}

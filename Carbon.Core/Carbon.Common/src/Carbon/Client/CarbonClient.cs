﻿using System.Diagnostics;
using Carbon.Client.Contracts;
using Carbon.Client.Packets;
using Network;
using Newtonsoft.Json;
using ProtoBuf;
using static Carbon.Client.RPC;

namespace Carbon.Client;

public class CarbonClient : ICommunication, IDisposable
{
	public static Dictionary<Network.Connection, CarbonClient> clients { get; internal set; } = new();
	public static CommunityEntity community => RPC.SERVER ? CommunityEntity.ServerInstance : CommunityEntity.ClientInstance;

	public Network.Connection Connection { get; internal set; }

	public bool IsConnected => Connection != null && Connection.active;
	public bool HasCarbonClient { get; internal set; }

	public int ScreenWidth { get;set; }
	public int ScreenHeight { get;set; }

	#region RPCs

	[Method("clientinfo")]
	private static void ClientInfo(BasePlayer player, Network.Message message)
	{
		var info = Receive<ClientInfo>(message);
		var client = Get(player);

		client.ScreenWidth = info.ScreenWidth;
		client.ScreenHeight = info.ScreenHeight;
	}

	#endregion

	#region Methods

	public bool Send(RPC rpc, IPacket packet = default, bool checks = true)
	{
		if (checks && !IsValid()) return false;

		try
		{
			if (packet == null) CommunityEntity.ServerInstance.ClientRPCEx(new SendInfo(Connection), null, rpc.Name);
			else CommunityEntity.ServerInstance.ClientRPCEx(new SendInfo(Connection), null, rpc.Name, JsonConvert.SerializeObject(packet.Serialize()));

			// return true;
			// 
			// var write = Net.sv.StartWrite();
			// write.PacketID(Message.Type.RPCMessage);
			// write.EntityID(CommunityEntity.ServerInstance.net.ID);
			// write.UInt32(rpc.Id);
			// if (packet != null) write.BytesWithSize(packet.Serialize());
			// write.Send(new SendInfo(Connection)
			// {
			// 	priority = Network.Priority.Immediate
			// });
		}
		catch (Exception ex)
		{
			ex = ex.Demystify();
			UnityEngine.Debug.LogError($"Failed sending Carbon client RPC {rpc.Name}[{rpc.Id}] to {Connection.username}[{Connection.userid}] ({ex.Message})\n{ex.StackTrace}");
			return false;
		}

		return true;
	}
	public static void SendPing(Network.Connection connection)
	{
		var client = Get(connection);

		if (!client.IsConnected)
		{
			Logger.Warn($"Client {client.Connection?.username}[{client.Connection?.userid}] is not connected to deliver ping.");
			return;
		}

		if (client.HasCarbonClient) return;

		client.Send(RPC.Get("ping"), RPCList.Get(), checks: false);
	}
	public static T Receive<T>(Network.Message message)
	{
		using var ms = new MemoryStream(JsonConvert.DeserializeObject<byte[]>(message.read.StringRaw()));
		var array = ms.ToArray();
		return Serializer.Deserialize<T>(new ReadOnlySpan<byte>(array, 0, array.Length));

		// if (!message.read.TemporaryBytesWithSize(out var buffer, out var length)) return default;
		// 
		// using var ms = new MemoryStream(buffer);
		// return Serializer.Deserialize<T>(new ReadOnlySpan<byte>(ms.ToArray(), 0, length));
	}

	#endregion

	#region Helpers

	public static bool Exists(Network.Connection connection)
	{
		return clients.ContainsKey(connection);
	}
	public static CarbonClient Make(Network.Connection connection)
	{
		return new CarbonClient
		{
			Connection = connection
		};
	}
	public static CarbonClient Get(BasePlayer player)
	{
		return Get(player.Connection);
	}
	public static CarbonClient Get(Network.Connection connection)
	{
		if(!clients.TryGetValue(connection, out var value))
		{
			clients.Add(connection, value = Make(connection));
		}

		return value;
	}
	public static void Dispose(CarbonClient client)
	{
		var connection = client.Connection;
		if (connection == null) return;

		client.Dispose();
		clients.Remove(connection);
	}

	#endregion

	public bool IsValid()
	{
		return IsConnected && HasCarbonClient;
	}

	public void Dispose()
	{

	}
}

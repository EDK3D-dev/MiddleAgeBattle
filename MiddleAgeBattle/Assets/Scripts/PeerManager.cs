﻿using ExitGames.Client.Photon.LoadBalancing;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PeerManager: IDisposable
{
	private const string MASTER_IP_M = "192.168.112.1";
	private const string MASTER_IP_W = "192.168.52.1";
	private const string APP_ID = "e8e8f196-4c80-43eb-81a6-13ad9e8b58de";

	private const string USERNAME_KEY = "UserNameKey";
	private const string TOKEN_KEY = "TokenKey";

	private string _userName;
	private string _userToken;

	private LoadBalancingClient _loadBalancingClient;

	public event Action<string> OnStateChangeAction;
	public event Action<string> OnOpResponseAction;
	public event Action<string> OnEventAction;
	public event Action<bool> OnFindGame;
	public event Action<int> OnGameEntered;
	public event Action<int> OnGameLeft;
	public event Action<int> OnPlayerJoined;
	public event Action<int> OnPlayerLeft;

	private Player _playerLocal;
	public Player PeerPlayerLocal { get { return _playerLocal; } }

	private Player _playerRemote;
	public Player PeerPlayerRemote{ get { return _playerRemote; }}


	public PeerManager(string userName, string token)
	{
		_userName = userName;
		_userToken = token;
		_loadBalancingClient = new LoadBalancingClient(MASTER_IP_M, APP_ID, "1.0");
	}

	public static PeerManager Create(string userName, string token)
	{
		return new PeerManager(userName, token);
	}

	public void Initialize()
	{
		Subscribe();
	}

	public void Dispose()
	{
		Unsubscribe();
	}

	public void Connect()
	{
		AuthenticationValues customAuth = new AuthenticationValues();
		customAuth.AddAuthParameter("username", _userName);  // expected by the demo custom auth service
		customAuth.AddAuthParameter("token", _userToken);    // expected by the demo custom auth service
		_loadBalancingClient.AuthValues = customAuth;

		_loadBalancingClient.AutoJoinLobby = false;
		_loadBalancingClient.ConnectToRegionMaster("eu");
	}

	public void Create()
	{
		var roomOpts = _getRoomOptionsForGameType(MAX_PLAYERS, BET_MINIMAL, Connecting.GameType.OneVsOne);
		// get the lobby
		var lobby = LobbyMain;
		// try to create the game 
		_loadBalancingClient.OpCreateRoom("Room_" + UnityEngine.Random.Range(0, 99), roomOpts, lobby);
	}


	public void Join()
	{
		var roomProperties = GetExpectedRoomProperties(Connecting.GameType.OneVsOne, BET_MINIMAL);
		var lobby = LobbyMain;
		_loadBalancingClient.OpJoinRandomRoom(roomProperties, MAX_PLAYERS, MatchmakingMode.FillRoom, lobby, null);
	}

	public void UpdateService()
	{
		_loadBalancingClient.Service();
	}

	private void Subscribe()
	{
		_loadBalancingClient.OnStateChangeAction += OnStateChangeActionHandler;
		_loadBalancingClient.OnOpResponseAction += OnOpResponseActionHandler;
		_loadBalancingClient.OnEventAction += OnEventActionHandler;
	}

	private void OnEventActionHandler(ExitGames.Client.Photon.EventData eventData)
	{

		string eventCode = eventData.Code.ToString();
		var evParams = eventData.Parameters;
		int actorNr = 0;
		ExitGames.Client.Photon.LoadBalancing.Player originatingPlayer = null;
		if (eventData.Parameters.ContainsKey(ParameterCode.ActorNr))
		{
			actorNr = (int)eventData[ParameterCode.ActorNr];
			if (_loadBalancingClient.CurrentRoom != null)
			{
				originatingPlayer = _loadBalancingClient.CurrentRoom.GetPlayer(actorNr);
			}
		}

		switch (eventData.Code)
		{
			case EventCode.Join:
				{
					string name = "";
					// we only want to deal with events from the game server
					if (_loadBalancingClient.Server == ServerConnection.GameServer)
					{
						var playerProps = (ExitGames.Client.Photon.Hashtable)eventData[ParameterCode.PlayerProperties];

						var userId = (string)playerProps[ActorProperties.UserId];
						var userName = (string)playerProps[ActorProperties.PlayerName];
						

						if (userId == _loadBalancingClient.UserId)
						{
							// local user
							_playerLocal = Player.Create(_userName, _userToken, true, actorNr);
							if (OnGameEntered != null)
								OnGameEntered(actorNr);
							eventCode = "EventCode: Join, Local, " + userName + " ID: " + userId;
						}
						//else
						{
							// other players
							_playerRemote = Player.Create(_userName, _userToken, false, actorNr);
							if (OnPlayerJoined != null)
								OnPlayerJoined(actorNr);
							eventCode = "EventCode: Join, Other, " + userName + " ID: " + userId;
						}
					}

					break;
				}
			case EventCode.Leave:
				{
					// we only want to deal with events from the game server
					if (_loadBalancingClient.Server == ServerConnection.GameServer)
					{
						// check if we have props
						if (evParams.ContainsKey(ParameterCode.PlayerProperties))
						{
							var playerProps = (Hashtable)eventData[ParameterCode.PlayerProperties];
							var userId = (string)playerProps[ActorProperties.UserId];
							if (userId == _loadBalancingClient.UserId)
							{
								// local user
								if (OnGameLeft != null)
									OnGameLeft(actorNr);
							}
							else
							{
								// other players
								if (OnPlayerLeft != null)
									OnPlayerLeft(actorNr);
							}
						}
						else
						{
							// all other players - just call default listener
							if (OnPlayerLeft != null)
								OnPlayerLeft(actorNr);
						}
					}
					eventCode = "EventCode: Leave, " + actorNr;
					break;
				}

			case EventCode.AppStats:
				{
					// get the parameters
					string text = "AppStats: paramCount";

					foreach (var param in evParams)
					{
						text += "param Key:" + param.Key + " param Val:" + param.Value.ToString() + "\n";
					}

					eventCode = text;
					//eventCode += evParams != null ? evParams.Count.ToString() : "AppStats without Parameters";
					break;
				}
			default:
				{
					// all custom events are sent out
					if (eventData.Code <= 200)
					{
						//if (OnGameEvent != null) OnGameEvent(aEvent);
					}
					break;
				}

		}

		if (OnEventAction != null)
			OnEventAction(eventCode);
		//_roomInfo.text = eventCode;
	}

	private void OnOpResponseActionHandler(ExitGames.Client.Photon.OperationResponse response)
	{
		string operationCode = response.OperationCode.ToString();

		switch (response.OperationCode)
		{
			case OperationCode.Authenticate:
				{
					// did we get errors?
					if (response.ReturnCode != 0)
					{
						// sigh - let the listener know!
						//if (OnConnectError != null) OnConnectError(aOpResponse.ToString());
					}
					operationCode = "OperationCode: Authenciate";
					break;
				}
			case OperationCode.JoinLobby:
				{
					// call our listeners
					//if (OnConnect != null) OnConnect();
					operationCode = "OperationCode: JoinLobby";
					break;
				}
			case OperationCode.JoinRandomGame:
				{
					// did we find a game?
					if (response.ReturnCode != 0)
					{
						// sigh - let the listener know!
						if (OnFindGame != null) OnFindGame(false);
					}
					else
					{
						// yaay!
						if (OnFindGame != null) OnFindGame(true);
					}

					operationCode = "OperationCode: JoinRandomGame";
					break;
				}
			case OperationCode.SetProperties:
				{
					// dump room info
					operationCode = "OperationCode: SetProperties";
					var playerList = _loadBalancingClient.CurrentRoom.Players;
					//Log.Debug("PHOTON: PLAYER: ROOMINFO: Players =>\n{0}", JsonConvert.SerializeObject(playerList));
					break;
				}
			default: break;
		}

		if (OnOpResponseAction != null)
			OnOpResponseAction(operationCode);
		//_lobbyInfo.text = operationCode;
	}

	private void OnStateChangeActionHandler(ClientState state)
	{
		//_serverStatusInfo.text = state.ToString();
		if (OnStateChangeAction != null)
			OnStateChangeAction(state.ToString());
	}

	private void Unsubscribe()
	{
		//_connectButton.onClick.RemoveAllListeners();
		_loadBalancingClient.OnStateChangeAction -= OnStateChangeActionHandler;
		_loadBalancingClient.OnOpResponseAction -= OnOpResponseActionHandler;

		//_createGameButton.onClick.RemoveAllListeners();
	}

	private const string BET_AMOUNT_PROP = "bc";
	private const string GAME_TYPE_PROP = "gt";

	/// <summary>
	/// Gets the room options needed for the game properties.
	/// </summary>
	/// <param name="aType"></param>
	/// <param name="aMaxPlayers"></param>
	/// <returns></returns>
	protected RoomOptions _getRoomOptionsForGameType(byte aMaxPlayers, int betCount, Connecting.GameType gameType)
	{
		var roomOptions = new RoomOptions();
		roomOptions.CheckUserOnJoin = true;             // no duplicate users allowed
		roomOptions.IsOpen = true;                      // ready for all comers
		roomOptions.IsVisible = true;    // Private games can only be joined by knowing table names.
		roomOptions.MaxPlayers = aMaxPlayers;           // max. players allowed
		roomOptions.PublishUserId = true;               // we allow everyone to see other player's user ids															
		roomOptions.PlayerTtl = 60 * 1000;              // player slots are reserved and kept alive for this duration
		roomOptions.EmptyRoomTtl = 60 * 1000;           // empty rooms are kept alive for this duration
														// set custom properties
		roomOptions.CustomRoomProperties = new ExitGames.Client.Photon.Hashtable();
		roomOptions.CustomRoomProperties.Add(BET_AMOUNT_PROP, betCount);
		roomOptions.CustomRoomProperties.Add(GAME_TYPE_PROP, gameType);
		// set lobby properties
		roomOptions.CustomRoomPropertiesForLobby = new string[]
		{
				GAME_TYPE_PROP,
				BET_AMOUNT_PROP
		};
		return (roomOptions);
	}

	private const int MAX_PLAYERS = 2;
	private const int BET_MINIMAL = 500;
	public static readonly TypedLobby LobbyMain = new TypedLobby("main", LobbyType.Default);

	protected ExitGames.Client.Photon.Hashtable GetExpectedRoomProperties(Connecting.GameType gameType, int betAmount)
	{
		var expectedRoomProps = new ExitGames.Client.Photon.Hashtable();
		expectedRoomProps.Add(BET_AMOUNT_PROP, betAmount);
		expectedRoomProps.Add(GAME_TYPE_PROP, gameType);
		return (expectedRoomProps);
	}
}


public interface IPeerManager
{

}
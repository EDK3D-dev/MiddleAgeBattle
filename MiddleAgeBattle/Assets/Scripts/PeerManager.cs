using ExitGames.Client.Photon.LoadBalancing;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PeerManager
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
	public event Action<string> OnServerIpChangedAction;

	private Player _playerLocal;
	public Player PeerPlayerLocal { get { return _playerLocal; } }

	private Player _playerRemote;
	public Player PeerPlayerRemote{ get { return _playerRemote; }}


	public PeerManager(string userName, string token)
	{
		_userName = userName;
		_userToken = token;
		_loadBalancingClient = new LoadBalancingClient(MASTER_IP_W, APP_ID, "1.0");
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
		_loadBalancingClient.UserId = _userToken;
		AuthenticationValues customAuth = new AuthenticationValues(_loadBalancingClient.UserId);
		customAuth.AuthType = CustomAuthenticationType.Custom;
		customAuth.AddAuthParameter("username", _userName);  // expected by the demo custom auth service
		customAuth.AddAuthParameter("token", _userToken);    // expected by the demo custom auth service
		_loadBalancingClient.AuthValues = customAuth;
		_loadBalancingClient.NickName = _userName;

		_loadBalancingClient.AutoJoinLobby = false;
		_loadBalancingClient.ConnectToRegionMaster("eu");
	}

	private string _roomName = "Room_";

	public void Create()
	{
		var roomOpts = _getRoomOptionsForGameType(MAX_PLAYERS, BET_MINIMAL, Connecting.GameType.OneVsOne);
		// get the lobby
		var lobby = LobbyMain;
		// try to create the game 
		_loadBalancingClient.OpCreateRoom(_roomName+ UnityEngine.Random.Range(0, 99), roomOpts, lobby);
	}


	public void Join()
	{
		var roomProperties = GetExpectedRoomProperties(Connecting.GameType.OneVsOne, BET_MINIMAL);
		var lobby = LobbyMain;
		//_loadBalancingClient.OpJoinRoom(_roomName);
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
					var masterServerAdress = _loadBalancingClient.MasterServerAddress;
					Debug.Log(masterServerAdress);
					if (OnServerIpChangedAction != null)
						OnServerIpChangedAction(masterServerAdress);
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

			case OperationCode.GetGameList:
				{
					ShowRoomInfoData();
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
	public static readonly TypedLobby LobbyMain = new TypedLobby("main", LobbyType.SqlLobby);

	protected ExitGames.Client.Photon.Hashtable GetExpectedRoomProperties(Connecting.GameType gameType, int betAmount)
	{
		var expectedRoomProps = new ExitGames.Client.Photon.Hashtable();
		expectedRoomProps.Add(BET_AMOUNT_PROP, betAmount);
		expectedRoomProps.Add(GAME_TYPE_PROP, gameType);
		return (expectedRoomProps);
	}

	public void UpdateRoomInfoData()
	{
		string sql = string.Format("{0} = {1}", BET_AMOUNT_PROP, BET_MINIMAL);
		_loadBalancingClient.OpGetGameList(LobbyMain, sql);
		
	}

	private void ShowRoomInfoData()
	{
		var roomList = _loadBalancingClient.RoomInfoList;
		foreach (var room in roomList)
		{
			string key = room.Key;
			var roomValue = room.Value;

			Debug.LogFormat("Key: {0}, roomInfo: Name {1}, PlayerCount: {2}, properties: {3}", key, roomValue.Name, roomValue.PlayerCount, roomValue.CustomProperties);
		}
	}
}


public class GameController
{
	public bool IsReadyToPlay;
	public int PlayerCount;
	private int _playerCount;

	public int PlayerInGame;
}

public class Turn
{
	
}

public class PlayerDice
{
	public string Name { get; private set; }
	public readonly DiceRoll DiceRoll;
}


public sealed class DiceRoll
{
	private const int DICE_MIN = 1;
	private const int DICE_MAX = 6;
	public int DiceCount { get; private set; }
	public int Sum { get; private set; }

	public int[] DiceRollResult { get; private set; }
	public DiceRoll(int diceCount)
	{
		DiceCount = diceCount;
	}

	public void DiceRollProcess()
	{
		Sum = 0;
		DiceRollResult = new int[DiceCount];
		for (int i = 0; i < DiceCount; i++)
		{
			int diceRollResult = ThrowDice();
			Sum += diceRollResult;
			DiceRollResult[i] = diceRollResult;
		}
	}

	public DiceRoll Create(int diceCount)
	{
		return new DiceRoll(diceCount);
	}

	public override string ToString()
	{
		string result = string.Format("Dice Count: {0};", DiceCount);
		if (DiceRollResult != null && DiceRollResult.Length > 0)
		{
			int i = 1;
			foreach (var dice in DiceRollResult)
			{
				result += " Dice[" + i + "] = " + dice + ";";
				i++;
			}
		}
		result += " Sum: " + Sum;
		return result;
	}

	private int ThrowDice()
	{
		return UnityEngine.Random.Range(DICE_MIN, DICE_MAX);
	}
}

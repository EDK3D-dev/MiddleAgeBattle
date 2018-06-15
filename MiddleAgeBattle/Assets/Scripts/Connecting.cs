using ExitGames.Client.Photon.LoadBalancing;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Connecting : MonoBehaviour
{
	private const string MASTER_IP_M = "192.168.112.1";
	private const string MASTER_IP_W = "192.168.52.1";
	private const string APP_ID = "e8e8f196-4c80-43eb-81a6-13ad9e8b58de";

	private const string USERNAME_KEY = "UserNameKey";
	private const string TOKEN_KEY = "TokenKey";

	[SerializeField]
	private Button _connectButton;

	[SerializeField]
	private Button _createGameButton;

	[SerializeField]
	private Button _joinGameButton;

	[SerializeField]
	private Text _serverStatusInfo;

	[SerializeField]
	private Text _lobbyInfo;

	[SerializeField]
	private Text _userNameText;

	[SerializeField]
	private Text _userTokenText;

	[SerializeField]
	private Text _roomInfo;

	private string _userName;
	private string _userToken;

	private LoadBalancingClient _loadBalancingClient;

	private void Start()
	{
		_loadBalancingClient = new LoadBalancingClient(MASTER_IP_M, APP_ID, "1.0"); // the master server address is not used when connecting via nameserver
		if (IsUserDataExist())
		{
			LoadUserData();
		}
		else
		{
			_userName = "User_" + UnityEngine.Random.Range(0, 99);
			_userToken = Guid.NewGuid().ToString();
			SaveUserData();
		}
		SetViewUserData();
		Subscribe();
	}

	private void Subscribe()
	{
		_connectButton.onClick.AddListener(OnConnectButtonClickHandler);
		_loadBalancingClient.OnStateChangeAction += OnStateChangeActionHandler;
		_loadBalancingClient.OnOpResponseAction += OnOpResponseActionHandler;
		_loadBalancingClient.OnEventAction += OnEventActionHandler;

		_createGameButton.onClick.AddListener(CreateGame);
		_joinGameButton.onClick.AddListener(JoinGame);
	}

	private void OnEventActionHandler(ExitGames.Client.Photon.EventData eventData)
	{

		string eventCode = eventData.Code.ToString();

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
							//if (OnGameEntered != null) OnGameEntered(actorNr);
							eventCode = "EventCode: Join, Local, " + userName + " ID: " + userId;
						}
						//else
						{
							// other players
							//if (OnPlayerJoined != null) OnPlayerJoined(actorNr);
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
						//if (eventData.ContainsKey(ParameterCode.PlayerProperties))
						{
							var playerProps = (Hashtable)eventData[ParameterCode.PlayerProperties];
							var userId = (string)playerProps[ActorProperties.UserId];
							if (userId == _loadBalancingClient.UserId)
							{
								// local user
								//if (OnGameLeft != null) OnGameLeft(actorNr);
							}
							else
							{
								// other players
								//if (OnPlayerLeft != null) OnPlayerLeft(actorNr);
							}
						}
						//else
						{
							// all other players - just call default listener
							//if (OnPlayerLeft != null) OnPlayerLeft(actorNr);
						}
					}
					eventCode = "EventCode: Leave";
					break;
				}

			case EventCode.AppStats:
				{
					// get the parameters
					var evParams = eventData.Parameters;
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
		_roomInfo.text = eventCode;
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
						//if (OnFindGame != null) OnFindGame(false);
					}
					else
					{
						// yaay!
						//if (OnFindGame != null) OnFindGame(true);
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
		_lobbyInfo.text = operationCode;
	}

	private void OnStateChangeActionHandler(ClientState state)
	{
		_serverStatusInfo.text = state.ToString();
	}

	private void Unsubscribe()
	{
		_connectButton.onClick.RemoveAllListeners();
		_loadBalancingClient.OnStateChangeAction -= OnStateChangeActionHandler;
		_loadBalancingClient.OnOpResponseAction -= OnOpResponseActionHandler;

		_createGameButton.onClick.RemoveAllListeners();
	}

	private void SaveUserData()
	{
		PlayerPrefs.SetString(USERNAME_KEY, _userName);
		PlayerPrefs.SetString(TOKEN_KEY, _userToken);
	}

	private bool IsUserDataExist()
	{
		return PlayerPrefs.HasKey(USERNAME_KEY);
	}

	private void LoadUserData()
	{
		_userName = PlayerPrefs.GetString(USERNAME_KEY);
		_userToken = PlayerPrefs.GetString(TOKEN_KEY);
	}

	private void Update()
	{
		if (_loadBalancingClient != null)
		{
			_loadBalancingClient.Service();  // easy but ineffective. should be refined to using dispatch every frame and sendoutgoing on demand
		}
	}

	private void OnApplicationQuit()
	{
		if (_loadBalancingClient != null) _loadBalancingClient.Disconnect();
	}

	private void OnConnectButtonClickHandler()
	{
		Connect();
	}

	private void Connect()
	{
		AuthenticationValues customAuth = new AuthenticationValues();
		customAuth.AddAuthParameter("username", _userName);  // expected by the demo custom auth service
		customAuth.AddAuthParameter("token", _userToken);    // expected by the demo custom auth service
		_loadBalancingClient.AuthValues = customAuth;

		_loadBalancingClient.AutoJoinLobby = false;
		_loadBalancingClient.ConnectToRegionMaster("eu");
	}

	private void SetViewUserData()
	{
		_userNameText.text = _userName;
		_userTokenText.text = _userToken;
	}

	public enum GameType
	{
		OneVsOne,
		TwoVsTwo
	}

	private const string BET_AMOUNT_PROP = "bc";
	private const string GAME_TYPE_PROP = "gt";

	/// <summary>
	/// Gets the room options needed for the game properties.
	/// </summary>
	/// <param name="aType"></param>
	/// <param name="aMaxPlayers"></param>
	/// <returns></returns>
	protected RoomOptions _getRoomOptionsForGameType(byte aMaxPlayers, int betCount, GameType gameType)
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


	private void CreateGame()
	{
		var roomOpts = _getRoomOptionsForGameType(MAX_PLAYERS, BET_MINIMAL, GameType.OneVsOne);
		// get the lobby
		var lobby = LobbyMain;
		// try to create the game 
		_loadBalancingClient.OpCreateRoom("Room_" + UnityEngine.Random.Range(0,99), roomOpts, lobby);
	}

	private void JoinGame()
	{
		var roomProperties = GetExpectedRoomProperties(GameType.OneVsOne, BET_MINIMAL);
		var lobby = LobbyMain;
		_loadBalancingClient.OpJoinRandomRoom(roomProperties, MAX_PLAYERS, MatchmakingMode.FillRoom, lobby, null);
	}

	protected ExitGames.Client.Photon.Hashtable GetExpectedRoomProperties(GameType gameType, int betAmount)
	{
		var expectedRoomProps = new ExitGames.Client.Photon.Hashtable();
		expectedRoomProps.Add(BET_AMOUNT_PROP, betAmount);
		expectedRoomProps.Add(GAME_TYPE_PROP, gameType);
		return (expectedRoomProps);
	}
}

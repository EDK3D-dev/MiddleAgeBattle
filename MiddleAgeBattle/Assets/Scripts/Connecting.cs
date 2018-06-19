using ExitGames.Client.Photon.LoadBalancing;
using System;
using System.Collections.Generic;
using System.Linq;
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

	[SerializeField]
	private GameObject _playerViewPrefab;

	[SerializeField]
	private GameObject _playerViewParent;

	private string _userName;
	private string _userToken;

	private LoadBalancingClient _loadBalancingClient;
	private PeerManager _peerManager;

	private List<PlayerView> _players = new List<PlayerView>();

	private void Start()
	{
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
		_players.Clear();
		_peerManager = PeerManager.Create(_userName, _userToken);
		_peerManager.Initialize();
		SetViewUserData();
		Subscribe();
	}

	private void Subscribe()
	{
		_connectButton.onClick.AddListener(OnConnectButtonClickHandler);
		_peerManager.OnStateChangeAction += OnStateChangeActionHandler;
		_peerManager.OnOpResponseAction += OnOpResponseActionHandler;
		_peerManager.OnEventAction += OnEventActionHandler;
		_peerManager.OnGameEntered += OnGameEnteredHandler;
		_peerManager.OnPlayerJoined += OnPlayerJoinedHandler;
		_peerManager.OnPlayerLeft += OnPlayerLeftHandler;
		_peerManager.OnGameLeft += OnGameLeftHandler;

		_createGameButton.onClick.AddListener(CreateGame);
		_joinGameButton.onClick.AddListener(JoinGame);
	}

	private void OnGameLeftHandler(int userID)
	{
		var playerView = GetPlayerViewByUserID(userID);
		_players.Remove(playerView);
		Debug.LogFormat("[Connecting] OnGameLeftHandler, was left player with ID: {0}, player: {1}", userID, playerView.PeerPlayer);
		Destroy(playerView.gameObject);
	}

	private void OnPlayerLeftHandler(int userID)
	{
		var playerView = GetPlayerViewByUserID(userID);
		_players.Remove(playerView);
		Debug.LogFormat("[Connecting] OnPlayerLeftHandler, was lefted player with ID: {0}, player: {1}", userID, playerView.PeerPlayer);
		Destroy(playerView.gameObject);
	}

	private void OnDestroy()
	{
		_peerManager.Dispose();
		Unsubscribe();
	}

	private void OnPlayerJoinedHandler(int userID)
	{
		bool playerExist = PlayerWithIDExist(userID);
		var playerView = playerExist ? GetPlayerViewByUserID(userID) : CreatePlayer(_peerManager.PeerPlayerLocal);
		if (!playerExist)
			_players.Add(playerView);
		Debug.LogFormat("[Connecting] OnPlayerJoinedHandler, was joined player with ID: {0}, player: {1}", userID, playerView.PeerPlayer);
	}

	private void OnGameEnteredHandler(int userID)
	{
		bool playerExist = PlayerWithIDExist(userID);
		var playerView = playerExist ? GetPlayerViewByUserID(userID) : CreatePlayer(_peerManager.PeerPlayerLocal);
		if (!playerExist)
			_players.Add(playerView);
		Debug.LogFormat("[Connecting] OnGameEnteredHandler, was joined player with ID: {0}, player: {1}", userID, playerView.PeerPlayer);
	}

	private void OnEventActionHandler(string eventData)
	{
		_roomInfo.text = eventData;
	}

	private void OnOpResponseActionHandler(string response)
	{
		_lobbyInfo.text = response;
	}

	private void OnStateChangeActionHandler(string state)
	{
		_serverStatusInfo.text = state;
	}

	private void Unsubscribe()
	{
		_connectButton.onClick.RemoveAllListeners();
		_peerManager.OnStateChangeAction -= OnStateChangeActionHandler;
		_peerManager.OnOpResponseAction -= OnOpResponseActionHandler;
		_peerManager.OnGameEntered -= OnGameEnteredHandler;
		_peerManager.OnPlayerJoined -= OnPlayerJoinedHandler;
		_peerManager.OnPlayerLeft -= OnPlayerLeftHandler;
		_peerManager.OnGameLeft -= OnGameLeftHandler;

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
		if (_peerManager != null)
		{
			_peerManager.UpdateService();  // easy but ineffective. should be refined to using dispatch every frame and sendoutgoing on demand
		}
	}

	private void OnApplicationQuit()
	{
		if (_peerManager != null) _peerManager.Dispose();
	}

	private void OnConnectButtonClickHandler()
	{
		Connect();
	}

	private void Connect()
	{
		_peerManager.Connect();
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

	private void CreateGame()
	{
		_peerManager.Create();
	}

	private void JoinGame()
	{
		_peerManager.Create();
	}

	private PlayerView CreatePlayer(Player player)
	{
		PlayerView playerView = null;
		var go = Instantiate(_playerViewPrefab, _playerViewParent.transform);
		go.SetActive(true);
		playerView = go.GetComponent<PlayerView>();
		playerView.Initialize(player);
		return playerView;
	}

	private PlayerView GetPlayerViewByUserID(int userID)
	{
		return _players.Find(player => player.PeerPlayer.UserID == userID);
	}

	private bool PlayerWithIDExist(int userID)
	{
		return _players.Any(player => player.PeerPlayer.UserID == userID);
	}
}

public class Player
{
	public string Name { get; private set; }
	public string TokenUID { get; private set; }
	public bool IsLocal { get; private set; }

	public int UserID { get; private set; }

	public Player(string name, string tokenUID, bool isLocal, int userID)
	{
		Name = name;
		TokenUID = tokenUID;
		IsLocal = isLocal;
		UserID = userID;
	}

	public static Player Create(string name, string tokenUID, bool isLocal, int userID)
	{
		return new Player(name, tokenUID, isLocal, userID);
	}

	public override string ToString()
	{
		string str = string.Format("Player - Name: {0}, IsLocal: {1}, UserID: {2}, Token: {3}", Name, IsLocal, UserID, TokenUID);
		return str;
	}
}

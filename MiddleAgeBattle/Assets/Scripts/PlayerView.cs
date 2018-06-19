using UnityEngine;
using UnityEngine.UI;


public class PlayerView : MonoBehaviour
{
	[SerializeField]
	private Text _playerName;

	[SerializeField]
	private Text _playerToken;

	[SerializeField]
	private Text _isLocalText;

	[SerializeField]
	private Text _userIDText;

	public Player PeerPlayer { get { return _player; } }
	private Player _player;

	private void Start()
	{

	}

	public void Initialize(Player player)
	{
		_player = player;
		_playerName.text = player.Name;
		_playerToken.text = player.TokenUID;
		_isLocalText.text = player.IsLocal.ToString();
		_userIDText.text = player.UserID.ToString();
	}
}

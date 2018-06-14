using ExitGames.Client.Photon.LoadBalancing;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Connecting : MonoBehaviour
{
	private const string MASTER_IP = "192.168.112.1";
	private const string APP_ID = "e8e8f196-4c80-43eb-81a6-13ad9e8b58de";

	private const string USERNAME_KEY = "UserNameKey";
	private const string TOKEN_KEY = "TokenKey";

	private string _userName;
	private string _userToken;

	private LoadBalancingClient loadBalancingClient;

	private void Start()
	{
		loadBalancingClient = new LoadBalancingClient(MASTER_IP, APP_ID, "1.0"); // the master server address is not used when connecting via nameserver
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

		AuthenticationValues customAuth = new AuthenticationValues();
		customAuth.AddAuthParameter("username", _userName);  // expected by the demo custom auth service
		customAuth.AddAuthParameter("token", _userToken);    // expected by the demo custom auth service
		loadBalancingClient.AuthValues = customAuth;

		loadBalancingClient.AutoJoinLobby = false;
		loadBalancingClient.ConnectToRegionMaster("eu");
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
		if (loadBalancingClient != null)
		{
			loadBalancingClient.Service();  // easy but ineffective. should be refined to using dispatch every frame and sendoutgoing on demand
		}
	}

	private void OnApplicationQuit()
	{
		if (loadBalancingClient != null) loadBalancingClient.Disconnect();
	}
}

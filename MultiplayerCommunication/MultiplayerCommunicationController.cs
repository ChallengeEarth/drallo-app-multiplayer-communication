using System;
using Newtonsoft.Json;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Generic;
using Drallo.ChallengeEngine.Activity.Record;
using Drallo.ChallengeEngine.Activity.Event;
using Drallo.MultiPlayer.Messages;
using Drallo.ChallengeEngine.Feedback;

namespace MultiplayerCommunication
{
	public class MultiplayerCommunicationController
	{
		private string userName;

		public event Action<string> Closed;
		public event Action<string> InvitationReceived;
		public event Action<string> JoinAcceptedReceived;
		public event Action<string> JoinRejectedReceived;
		public event Action<TimeLineEntry> TimeLineEntryReceived;
		public event Action<object> Error;
		public event Action Reconnecting;
		public event Action Reconnected;

		public List<TimeLineEntry> TimeLineEntries { get; set; }

		ConnectionService connectionService;
		private JsonSerializerSettings jsonSerializerSettings;

		public MultiplayerCommunicationController(string connectionUri, string userName = "Randy")
		{
			this.userName = userName;
			connectionService = new ConnectionService(connectionUri);
			connectionService.Closed += OnConnectionClosed;
			connectionService.Error += OnConnectionError;
			connectionService.Received += OnMessageReceived;
			connectionService.Reconnecting += OnConnectionReconnecting;
			connectionService.Reconnected += OnConnectionReconnected;
			jsonSerializerSettings = new JsonSerializerSettings() {
				TypeNameHandling = TypeNameHandling.All
			};

			TimeLineEntries = new List<TimeLineEntry>();
		}

		public async Task Connect()
		{
			try
			{
				await connectionService.Connect();
				Debug.WriteLine("Connected To Multiplayer-Server");
			}
			catch (Exception e)
			{

				Debug.WriteLine("could not connect: " + e.Message);
			}
		}

		public async Task Send(string msg)
		{
			try
			{
				Debug.WriteLine("--------> send: " + msg);
				await connectionService.Send(msg);
			}
			catch (Exception e)
			{
				Debug.WriteLine("exception in Send() happened:  " + e.Message);
			}
		}

		public async Task Send(ActivityRecord activityRecord)
		{
			var message = new ActivityRecordMessage(activityRecord);
			var jsonMessage = JsonConvert.SerializeObject(message, jsonSerializerSettings);

			Debug.WriteLine("Send Activity-Record To Multiplayer-Server", message);
			await Send(jsonMessage);
		}

		public async Task Send(ActivityEvent activityEvent)
		{
			var message = new ActivityEventMessage(activityEvent);
			var jsonMessage = JsonConvert.SerializeObject(message, jsonSerializerSettings);

			Debug.WriteLine("Send Activity-Event To Multiplayer-Server", jsonMessage);
			await Send(jsonMessage);
		}


		public async void Register(string deviceId, Guid multiplayerChallengeId)
		{
			try
			{
				var registerMsg = new RegisterMessage(deviceId, multiplayerChallengeId);
				string message = JsonConvert.SerializeObject(registerMsg, jsonSerializerSettings);
				await connectionService.Send(message);
				Debug.WriteLine("sent register message: " + message);
			}
			catch (Exception e)
			{
				Debug.WriteLine("registering failed: " + e.Message);
			}
		}

		public async void Deregister(string deviceId, Guid multiplayerChallengeId)
		{
			try
			{
				var deregisterMsg = new DeregisterMessage(deviceId, multiplayerChallengeId);
				string message = JsonConvert.SerializeObject(deregisterMsg, jsonSerializerSettings);
				await connectionService.Send(message);
				Debug.WriteLine("sent deregister message");
			}
			catch (Exception e)
			{
				Debug.WriteLine("Deregistering failed: " + e.Message);
			}
		}

		public async void Join(Guid multiplayerChallengeId)
		{
			try
			{
				var joinMsg = new JoinMessage(userName, multiplayerChallengeId);
				string message = JsonConvert.SerializeObject(joinMsg, jsonSerializerSettings);
				await connectionService.Send(message);
				Debug.WriteLine("sent join message");
			}
			catch (Exception e)
			{
				Debug.WriteLine("Joining failed: " + e.Message);
			}
		}

		private void OnMessageReceived(string message)
		{ 
			object receivedObject;
			Debug.WriteLine("<--------- received message:   " + message);
			try
			{
				receivedObject = JsonConvert.DeserializeObject(message, jsonSerializerSettings);
			}
			catch (JsonSerializationException serializationException)
			{
				Debug.WriteLine("Exception caught while trying to deserialize message: " + serializationException.Message); 
				return;
			}
			try
			{
				var switchDictionary = new Dictionary<Type, Action> { { 
						typeof(InviteMessage), () =>
						{
							InvitationReceived("invitation received: " + receivedObject.ToString());
							Debug.WriteLine("RECEIVED INVITE");
						}
					}, { 
						typeof(JoinAcceptMessage), () =>
						{
							JoinAcceptedReceived("join accepted: " + receivedObject.ToString());
							Debug.WriteLine("RECEIVED JOIN ACCEPT");
						} 
					}, { 
						typeof(JoinRejectMessage), () =>
						{
							JoinRejectedReceived("join rejected: " + receivedObject.ToString());
							Debug.WriteLine("RECEIVED JOIN REJECT");
						} 
					}, { 
						typeof(TimeLineEntryMessage), () =>
						{
							TimeLineEntryMessage timelineEntryMessage = receivedObject as TimeLineEntryMessage;
							TimeLineEntries.Add(timelineEntryMessage.TimeLineEntry);
							TimeLineEntryReceived(timelineEntryMessage.TimeLineEntry);
							Debug.WriteLine("TIMELINE-ENTRY RECEIVED");
						} 
					}
				};
				if (switchDictionary.ContainsKey(receivedObject.GetType()))
				{
					switchDictionary[receivedObject.GetType()]();
				}
				else
				{
					Debug.WriteLine("UNKNOWN TYPE RECEIVED");
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex.Message);
			}
		}

		private void OnConnectionReconnecting()
		{
			if (Reconnecting != null)
				Reconnecting();
			Debug.WriteLine("Reconnecting...");
		}

		private void OnConnectionReconnected()
		{
			if (Reconnected != null)
				Reconnected();
			Debug.WriteLine("Reconnected! ");
		}


		private void OnConnectionClosed()
		{
			if (Closed != null)
				Closed("connection closed");
			Debug.WriteLine("Closed Websocket");
		}

		private void OnConnectionError(Exception e)
		{
			if (Error != null)
				Error(e);
			Debug.WriteLine("Error occured: " + e.Message);
		}

	}
}
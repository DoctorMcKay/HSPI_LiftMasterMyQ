using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Web.Script.Serialization;
using HomeSeerAPI;

namespace HSPI_LiftMasterMyQ
{
	public class MyQClient
	{
		public const int STATUS_OK = 0;
		public const int STATUS_MYQ_DOWN = 1;
		public const int STATUS_UNAUTHORIZED = 2;
		public int ClientStatus { get; private set; }

		private string _clientStatusString;
		public string ClientStatusString {
			get { return _clientStatusString; }
			private set {
				_clientStatusString = value;
				Program.WriteLog(LogType.Verbose, "ClientStatusString changed to: " + value);
			}
		}

		public long LoginThrottledAt { get; private set; }

		public List<MyQDevice> Devices;
		public long DevicesLastUpdated;
		
		private const string BASE_URL = "https://myqexternal.myqdevice.com";
		private const string BASE_URL_CRAFTSMAN = "https://craftexternal.myqdevice.com";

		private const string APP_ID = "NWknvuBd7LoFHfXmKNMBcgajXtZEgKUh4V7WNzMidrpUUluDpVYVZx+xT4PCM5Kx";
		private const string APP_ID_CRAFTSMAN = "eU97d99kMG4t3STJZO/Mu2wt69yTQwM0WXZA5oZ74/ascQ2xQrLD/yjeVhEQccBZ";

		private const int ACTION_CLOSE_DOOR = 0;
		private const int ACTION_OPEN_DOOR = 1;
		
		private readonly HttpClient httpClient;
		private readonly JavaScriptSerializer jsonSerializer;

		private string username;
		private string password;
		private string authToken = null;

		private int loginThrottleAttempts = 0;
		private Timer loginThrottle;

		public MyQClient(MyQMake make = MyQMake.LiftMaster) {
			httpClient = new HttpClient();

			if (make == MyQMake.LiftMaster || make == MyQMake.Chamberlain) {
				httpClient.BaseAddress = new Uri(BASE_URL);
				httpClient.DefaultRequestHeaders.Add("MyQApplicationId", APP_ID);
			} else if (make == MyQMake.Craftsman) {
				httpClient.BaseAddress = new Uri(BASE_URL_CRAFTSMAN);
				httpClient.DefaultRequestHeaders.Add("MyQApplicationId", APP_ID_CRAFTSMAN);
			}

			jsonSerializer = new JavaScriptSerializer();
			
			ClientStatus = STATUS_OK;

			loginThrottle = new Timer(2000) {AutoReset = false};
			loginThrottle.Elapsed += (object src, ElapsedEventArgs a) => {
				loginThrottleAttempts = 0;
				Program.WriteLog(LogType.Verbose, "Resetting login throttle attempts");
			};
		}

		/// <summary>
		/// Execute a login to the MyQ service. String that's returned is empty on success, or error message on fail.
		/// </summary>
		/// <param name="username"></param>
		/// <param name="password"></param>
		/// <param name="overrideThrottle"></param>
		/// <param name="retryCount"></param>
		/// <returns>string</returns>
		public async Task<string> Login(string username, string password, bool overrideThrottle = false, int retryCount = 0) {
			if (overrideThrottle) {
				loginThrottleAttempts = 0;
				loginThrottle.Stop();
			}

			Program.WriteLog(LogType.Verbose, "Attempting to login to MyQ");
			
			if (++loginThrottleAttempts >= 3 || retryCount > 3) {
				LoginThrottledAt = (long) (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0)).TotalSeconds;
				ClientStatus = STATUS_UNAUTHORIZED;
				ClientStatusString = "Login attempts throttled";

				Timer retry = new Timer(30000) {AutoReset = false};
#pragma warning disable 4014
				retry.Elapsed += (object src, ElapsedEventArgs args) => { Login(username, password); };
#pragma warning restore 4014
				retry.Start();

				return ClientStatusString;
			}
			
			loginThrottle.Start();
			
			this.username = username;
			this.password = password;

			var body = new Dictionary<string, string> {
				{"username", username},
				{"password", password}
			};
			
			var req = new HttpRequestMessage(HttpMethod.Post, "/api/v4/User/Validate");
			req.Content = new StringContent(jsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

			Program.WriteLog(LogType.Debug, "Logging into MyQ");
			dynamic content = null;
			try {
				HttpResponseMessage res = await httpClient.SendAsync(req);
				if (!res.IsSuccessStatusCode) {
					res.Dispose();
					ClientStatus = STATUS_MYQ_DOWN;
					return ClientStatusString = "Got failure response code from MyQ: " + res.StatusCode;
				}
				
				var responseString = await res.Content.ReadAsStringAsync();
				Program.WriteLog(LogType.Console, responseString);
				content = jsonSerializer.DeserializeObject(responseString);
				res.Dispose();
			}
			catch (Exception ex) {
				if (ex.Message.Contains("ConnectFailure")) {
					// Don't hold this against our login throttle
					loginThrottleAttempts--;
				}
				
				ClientStatus = STATUS_MYQ_DOWN;
				ClientStatusString = ex.Message;
			}

			if (content == null) {
				await Task.Delay(5000);
				return await Login(username, password, overrideThrottle, retryCount + 1);
			}

			try {
				// https://github.com/thomasmunduchira/myq-api/blob/master/src/myq.js#L141
				int returnCode = int.Parse(content["ReturnCode"]);
				switch (returnCode) {
					case 203:
						ClientStatus = STATUS_UNAUTHORIZED;
						return ClientStatusString = "MyQ username and/or password were incorrect.";

					case 205:
						ClientStatus = STATUS_UNAUTHORIZED;
						return ClientStatusString = "MyQ username and/or password were incorrect. 1 attempt left before lockout.";

					case 207:
						ClientStatus = STATUS_UNAUTHORIZED;
						return ClientStatusString = "MyQ account is locked out. Please reset password.";
				}
				authToken = content["SecurityToken"];
				httpClient.DefaultRequestHeaders.Add("SecurityToken", authToken);
				ClientStatus = STATUS_OK;
				
#if DEBUG
				Program.WriteLog(LogType.Debug, "Logged in with auth token " + authToken.Substring(0, 6) + "...");
#else
				Program.WriteLog(LogType.Info, "Logged into MyQ");
#endif

				return "";
			}
			catch (Exception ex) {
				ClientStatus = STATUS_MYQ_DOWN;
				return ClientStatusString = "MyQ service is temporarily unavailable. " + ex.Message;
			}
		}

		public async Task<string> getDevices() {
			Program.WriteLog(LogType.Verbose, "Requesting list of devices from MyQ");
			HttpResponseMessage res = await httpClient.GetAsync("/api/v4/userdevicedetails/get");
			if (!res.IsSuccessStatusCode) {
				res.Dispose();
				ClientStatus = STATUS_MYQ_DOWN;
				return ClientStatusString = "Got failure response code from MyQ device list: " + res.StatusCode;
			}

			var responseString = await res.Content.ReadAsStringAsync();
			Program.WriteLog(LogType.Console, responseString);
			dynamic content = jsonSerializer.DeserializeObject(responseString);
			res.Dispose();

			var devices = new List<MyQDevice>();
			var allowedDeviceTypes = new[] {
				MyQDeviceType.GDO,
				MyQDeviceType.Gate,
				MyQDeviceType.VGDO_GarageDoor,
				MyQDeviceType.CommercialDoorOperator,
				MyQDeviceType.WGDO_GarageDoor
			};

			try {
				int returnCode = int.Parse(content["ReturnCode"]);
				switch (returnCode) {
					case -3333:
						// Not logged in
						try {
							Program.WriteLog(LogType.Error, "MyQ error: " + content["ErrorMessage"]);
						}
						catch (Exception) {
							// silently swallow
						}
						
						var errorMsg = await Login(username, password);
						if (errorMsg != "") {
							return errorMsg;
						}
						else {
							return await getDevices(); // try again!
						}
					
					case 216:
						// Unauthorized
						ClientStatus = STATUS_UNAUTHORIZED;
						ClientStatusString = content["ErrorMessage"];
						return content["ErrorMessage"];
				}
				
				foreach (var deviceInfo in content["Devices"]) {
					if (Array.IndexOf(allowedDeviceTypes, (MyQDeviceType) deviceInfo["MyQDeviceTypeId"]) == -1) {
						continue; // not a GDO
					}

					devices.Add(new MyQDevice(deviceInfo));
				}

				ClientStatus = STATUS_OK;

				Devices = devices;
				DevicesLastUpdated = Helpers.GetUnixTimeSeconds();
			}
			catch (Exception ex) {
				Program.WriteLog(LogType.Error, ex.Message + "\n" + ex.StackTrace);
				
				ClientStatus = STATUS_MYQ_DOWN;
				return ClientStatusString = "MyQ service is temporarily unavailable. " + ex.Message;
			}
			
			return "";
		}

		public async Task<string> moveDoor(int myqId, MyQDoorState desiredState) {
			int attribValue;
			switch (desiredState) {
				case MyQDoorState.Closed:
					attribValue = ACTION_CLOSE_DOOR;
					break;
				
				case MyQDoorState.Open:
					attribValue = ACTION_OPEN_DOOR;
					break;
				
				default:
					return "Bad desired state " + desiredState;
			}

			// The myqId value actually needs to be an int so our string,string dictionary won't work here
			// I'm tired so let's just do this
			var json = "{\"MyQDeviceId\":" + myqId + ",\"AttributeName\":\"desireddoorstate\",\"AttributeValue\":\"" +
			           attribValue + "\"}";
			var req = new HttpRequestMessage(HttpMethod.Put, "/api/v4/deviceattribute/putdeviceattribute");
			req.Content = new StringContent(json, Encoding.UTF8, "application/json");

			Program.WriteLog(LogType.Info, "Writing door state " + attribValue + " for door id " + myqId);
			HttpResponseMessage res = await httpClient.SendAsync(req);
			if (!res.IsSuccessStatusCode) {
				res.Dispose();
				ClientStatus = STATUS_MYQ_DOWN;
				return ClientStatusString = "Got failure response code from MyQ: " + res.StatusCode;
			}
			
			// Someday we should probably handle the response, but not this day
			var responseString = await res.Content.ReadAsStringAsync();
			Program.WriteLog(LogType.Verbose, responseString);
			
			res.Dispose();
			return "";
		}
	}

	public enum MyQMake
	{
		LiftMaster = 1,
		Chamberlain = 2,
		Craftsman = 3,
	}
}

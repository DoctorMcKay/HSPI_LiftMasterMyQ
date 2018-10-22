using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Microsoft.CSharp.RuntimeBinder;

namespace HSPI_LiftMasterMyQ
{
	public class MyQClient
	{
		private const string BASE_URL = "https://myqexternal.myqdevice.com";
		
		private readonly HttpClient httpClient;
		private readonly JavaScriptSerializer jsonSerializer;

		public string authToken = null;

		public MyQClient() {
			httpClient = new HttpClient();
			httpClient.BaseAddress = new Uri(BASE_URL);
			httpClient.DefaultRequestHeaders.Add("MyQApplicationId", "NWknvuBd7LoFHfXmKNMBcgajXtZEgKUh4V7WNzMidrpUUluDpVYVZx+xT4PCM5Kx");
			
			jsonSerializer = new JavaScriptSerializer();
		}

		/// <summary>
		/// Execute a login to the MyQ service. String that's returned is empty on success, or error message on fail.
		/// </summary>
		/// <param name="username"></param>
		/// <param name="password"></param>
		/// <returns>string</returns>
		public async Task<string> login(string username, string password) {
			var body = new Dictionary<string, string> {
				{"username", username},
				{"password", password}
			};
			
			var req = new HttpRequestMessage(HttpMethod.Post, "/api/v4/User/Validate");
			req.Content = new StringContent(jsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

			Debug.WriteLine("Logging into MyQ");
			HttpResponseMessage res = await httpClient.SendAsync(req);
			if (!res.IsSuccessStatusCode) {
				res.Dispose();
				return "Got failure response code from MyQ: " + res.StatusCode;
			}

			var responseString = await res.Content.ReadAsStringAsync();
			Debug.WriteLine(responseString);
			dynamic content = jsonSerializer.DeserializeObject(responseString);
			res.Dispose();

			try {
				// https://github.com/thomasmunduchira/myq-api/blob/master/src/myq.js#L141
				int returnCode = int.Parse(content["ReturnCode"]);
				switch (returnCode) {
					case 203:
						return "MyQ username and/or password were incorrect.";

					case 205:
						return "MyQ username and/or password were incorrect. 1 attempt left before lockout.";

					case 207:
						return "MyQ account is locked out. Please reset password.";
				}

				authToken = content["SecurityToken"];
				httpClient.DefaultRequestHeaders.Add("SecurityToken", authToken);

				return "";
			}
			catch (RuntimeBinderException ex) {
				return "MyQ service is temporarily unavailable. " + ex.Message;
			}
		}

		public async Task<string> getDevices() {
			
		}
	}
}

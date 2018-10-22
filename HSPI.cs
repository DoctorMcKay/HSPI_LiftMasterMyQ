using System.Collections.Generic;
using System.Text;
using System.Web;
using System.Web.Script.Serialization;
using Scheduler;

namespace HSPI_LiftMasterMyQ
{
	// ReSharper disable once InconsistentNaming
	public class HSPI : HspiBase
	{
		public HSPI() {
			this.Name = "LiftMaster MyQ";
			this.PluginIsFree = true;
		}

		public override string InitIO(string port) {
			Debug.WriteLine("InitIO");

			hs.RegisterPage("LiftMasterMyQSettings", this.Name, this.InstanceFriendlyName());
			callbacks.RegisterLink(new HomeSeerAPI.WebPageDesc {
				plugInName = this.Name,
				link = "LiftMasterMyQSettings",
				linktext = "Settings",
				order = 1,
				page_title = "LiftMaster MyQ Settings",
				plugInInstance = this.InstanceFriendlyName()
			});
			
			return "";
		}

		public override string GetPagePlugin(string pageName, string user, int userRights, string queryString) {
			Debug.WriteLine("Requested page name " + pageName + " by user " + user + " with rights " + userRights);

			switch (pageName) {
				case "LiftMasterMyQSettings":
					return BuildSettingsPage(user, userRights, queryString);
			}

			return "";
		}

		private string BuildSettingsPage(string user, int userRights, string queryString,
			string messageBox = null, string messageBoxClass = null) {
			
			var sb = new StringBuilder();
			sb.Append(PageBuilderAndMenu.clsPageBuilder.DivStart("myq_settings", ""));
			if (messageBox != null) {
				sb.Append("<div" + (messageBoxClass != null ? " class=\"" + messageBoxClass + "\"" : "") + ">");
				sb.Append(messageBox);
				sb.Append("</div>");
			}
			
			sb.Append(PageBuilderAndMenu.clsPageBuilder.FormStart("myq_settings_form",
				"myq_settings_form", "post"));

			sb.Append(@"
<style>
	#myq_settings_form label {
		font-weight: bold;
		text-align: right;
		width: 150px;
		margin-right: 10px;
		display: inline-block;
	}

	#myq_settings_form button {
		margin-left: 163px;
	}

	.myq_message_box {
		padding: 10px;
		border-radius: 10px;
		display: inline-block;
		margin-bottom: 10px;
	}

	.myq_success_message {
		background-color: rgba(50, 255, 50, 0.8);
	}

	.myq_error_message {
		color: white;
		background-color: rgba(255, 50, 50, 0.8);
	}
</style>

<div>
	<label for=""myq_username"">MyQ Email</label>
	<input type=""email"" name=""myq_username"" id=""myq_username"" />
</div>

<div>
	<label for=""myq_password"">MyQ Password</label>
	<input type=""password"" name=""myq_password"" id=""myq_password"" />
</div>

<div>
	<label for=""myq_poll_frequency"">Poll Frequency (ms)</label>
	<input type=""number"" name=""myq_poll_frequency"" id=""myq_poll_frequency"" step=""1"" min=""5000"" />
</div>

<button type=""submit"">Submit</button>
");
			sb.Append(PageBuilderAndMenu.clsPageBuilder.FormEnd());

			var savedSettings = new Dictionary<string, string> {
				{"myq_username", hs.GetINISetting("Authentication", "myq_username", "", this.IniFilename)},
				{"myq_password", GetMyQPassword(true)},
				{"myq_poll_frequency", hs.GetINISetting("Options", "myq_poll_frequency", "10000", this.IniFilename)}
			};
			
			sb.Append("<script>var myqSavedSettings = ");
			sb.Append(new JavaScriptSerializer().Serialize(savedSettings));
			sb.Append(";");
			sb.Append(@"
for (var i in myqSavedSettings) {
	if (myqSavedSettings.hasOwnProperty(i)) {
		document.getElementById(i).value = myqSavedSettings[i];
	}
}
</script>");
					
			var builder = new PageBuilderAndMenu.clsPageBuilder("LiftMasterMyQSettings");
			builder.reset();
			builder.AddHeader(hs.GetPageHeader("LiftMasterMyQSettings", "LiftMaster MyQ Settings", "", "", false, true));

			builder.AddBody(sb.ToString());
			builder.AddFooter(hs.GetPageFooter());
			builder.suppressDefaultFooter = true;
			
			return builder.BuildPage();
		}

		public override string PostBackProc(string pageName, string data, string user, int userRights) {
			Debug.WriteLine("PostBackProc for page " + pageName + " with data " + data + " by user " + user + " with rights " + userRights);
			switch (pageName) {
				case "LiftMasterMyQSettings":
					if ((userRights & 2) != 2) {
						// User is not an admin
						return BuildSettingsPage(user, userRights, "",
							"Access denied: You are not an administrative user", "myq_message_box myq_error_message");
					}

					var authData = new string[] {
						"myq_username",
						"myq_password"
					};

					var qs = HttpUtility.ParseQueryString(data);
					var authCredsChanged = false;
					foreach (var key in authData) {
						var oldValue = hs.GetINISetting("MyQ Authentication", key, "", this.IniFilename);
						var newValue = qs.Get(key);
						if (key == "myq_username") {
							newValue = newValue.Trim();
						}

						if (newValue != "*****" && oldValue != newValue) {
							authCredsChanged = true;
						}
					}

					if (authCredsChanged) {
						var username = qs.Get("myq_username").Trim();
						var password = qs.Get("myq_password");
						
						hs.SaveINISetting("Authentication", "myq_username", username.Trim(), this.IniFilename);
						if (password != "*****") {
							// This doesn't provide any actual security, but at least the password isn't in
							// plaintext on the disk. Base64 is juuuuust barely not plaintext, but what're ya
							// gonna do?
							var encoded = System.Convert.ToBase64String(Encoding.UTF8.GetBytes(password));
							hs.SaveINISetting("Authentication", "myq_password", encoded, this.IniFilename);							
						}
					}

					var pollFrequency = qs.Get("myq_poll_frequency");
					int n;
					if (pollFrequency != null && int.TryParse(pollFrequency, out n) && n >= 5000) {
						hs.SaveINISetting("Options", "myq_poll_frequency", qs.Get("myq_poll_frequency"), this.IniFilename);
					}

					if (authCredsChanged) {
						// TODO try authenticating						
					}
					else {
						return BuildSettingsPage(user, userRights, "", "Settings have been saved successfully.",
							"myq_message_box myq_success_message");
					}

					return "";
			}
			
			return "";
		}
		
		/// <summary>
		/// Get the saved MyQ password from INI
		/// </summary>
		/// <param name="censor">If true, only return "*****" if a password is saved.</param>
		/// <returns>string</returns>
		private string GetMyQPassword(bool censor = true) {
			var password = hs.GetINISetting("Authentication", "myq_password", "", this.IniFilename);
			Debug.WriteLine("Retrieved password from INI: " + password);
			
			if (password.Length == 0) {
				return password;
			} else if (censor) {
				return "*****";
			} else {
				var decoded = Encoding.UTF8.GetString(System.Convert.FromBase64String(password));
				Debug.WriteLine("Decoded base64 password: " + decoded);
				return decoded;
			}
		}
	}
}
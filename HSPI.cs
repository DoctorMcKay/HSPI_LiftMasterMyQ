using System;
using System.Collections.Generic;
using System.Text;
using System.Timers;
using System.Web;
using System.Web.Script.Serialization;
using HomeSeerAPI;
using Scheduler;
using Scheduler.Classes;

namespace HSPI_LiftMasterMyQ
{
	// ReSharper disable once InconsistentNaming
	public class HSPI : HspiBase
	{
		private readonly MyQClient myqClient;
		private Timer pollTimer;
		private readonly Dictionary<string, int> serialToRef;
		private readonly Dictionary<int, int> refToMyqId;
		
		public HSPI() {
			Name = "LiftMaster MyQ";
			PluginIsFree = true;
			
			myqClient = new MyQClient();
			serialToRef = new Dictionary<string, int>();
			refToMyqId = new Dictionary<int, int>();
		}

		public override string InitIO(string port) {
			Debug.WriteLine("InitIO");

			hs.RegisterPage("LiftMasterMyQSettings", Name, InstanceFriendlyName());
			var configLink = new HomeSeerAPI.WebPageDesc {
				plugInName = Name,
				link = "LiftMasterMyQSettings",
				linktext = "Settings",
				order = 1,
				page_title = "LiftMaster MyQ Settings",
				plugInInstance = InstanceFriendlyName()
			};
			callbacks.RegisterConfigLink(configLink);
			callbacks.RegisterLink(configLink);
			
			var myqUsername = hs.GetINISetting("Authentication", "myq_username", "", IniFilename);
			var myqPassword = getMyQPassword(false);
			if (myqUsername.Length > 0 && myqPassword.Length > 0) {
				myqClient.login(myqUsername, myqPassword).ContinueWith(t => {
					if (t.Result == "") {
						// no error occurred
						syncDevices();
					}
				});
			}
			
			pollTimer = new Timer(double.Parse(hs.GetINISetting("Options", "myq_poll_frequency", "10000", IniFilename)));
			pollTimer.Elapsed += (Object source, ElapsedEventArgs e) => { syncDevices(); };
			pollTimer.AutoReset = false;
			// don't enable just yet

			return "";
		}

		public override void SetIOMulti(List<CAPI.CAPIControl> colSend) {
			foreach (var upd in colSend) {
				Program.WriteLog("Debug", "Ref " + upd.Ref + " set to " + upd.ControlValue);
				int myqId;
				if (!refToMyqId.TryGetValue(upd.Ref, out myqId)) {
					Program.WriteLog("Warn", "No MyQ ID for ref " + upd.Ref + "!!");
					continue;
				}

				myqClient.moveDoor(myqId, (MyQDoorState) upd.ControlValue).ContinueWith(t => {
					Program.WriteLog("Debug", "Move door command completed" + (t.Result.Length > 0 ? " with error: " + t.Result : ""));

					Timer timer = new Timer(1000);
					timer.AutoReset = false;
					timer.Elapsed += (Object source, ElapsedEventArgs e) => { syncDevices(); };
					timer.Start();
				});
			}
		}

		public override string GetPagePlugin(string pageName, string user, int userRights, string queryString) {
			Program.WriteLog("Debug", "Requested page name " + pageName + " by user " + user + " with rights " + userRights);

			switch (pageName) {
				case "LiftMasterMyQSettings":
					return buildSettingsPage(user, userRights, queryString);
			}

			return "";
		}

		private string buildSettingsPage(string user, int userRights, string queryString,
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
				{"myq_username", hs.GetINISetting("Authentication", "myq_username", "", IniFilename)},
				{"myq_password", getMyQPassword(true)},
				{"myq_poll_frequency", hs.GetINISetting("Options", "myq_poll_frequency", "10000", IniFilename)}
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
						return buildSettingsPage(user, userRights, "",
							"Access denied: You are not an administrative user", "myq_message_box myq_error_message");
					}

					var authData = new string[] {
						"myq_username",
						"myq_password"
					};

					var qs = HttpUtility.ParseQueryString(data);
					var authCredsChanged = false;
					foreach (var key in authData) {
						var oldValue = hs.GetINISetting("Authentication", key, "", IniFilename);
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
						
						hs.SaveINISetting("Authentication", "myq_username", username.Trim(), IniFilename);
						if (password != "*****") {
							// This doesn't provide any actual security, but at least the password isn't in
							// plaintext on the disk. Base64 is juuuuust barely not plaintext, but what're ya
							// gonna do?
							var encoded = System.Convert.ToBase64String(Encoding.UTF8.GetBytes((string) password));
							hs.SaveINISetting("Authentication", "myq_password", encoded, IniFilename);							
						}
					}

					var pollFrequency = qs.Get("myq_poll_frequency");
					int n;
					if (pollFrequency != null && int.TryParse(pollFrequency, out n) && n >= 5000) {
						hs.SaveINISetting("Options", "myq_poll_frequency", pollFrequency, IniFilename);
						pollTimer.Interval = n;
					}

					if (authCredsChanged) {
						var authTask = myqClient.login(hs.GetINISetting("Authentication", "myq_username", "", IniFilename),
							getMyQPassword(false), true);
						authTask.Wait();
						if (authTask.Result.Length > 0) {
							return buildSettingsPage(user, userRights, "", authTask.Result,
								"myq_message_box myq_error_message");
						}
						else {
							syncDevices();
							return buildSettingsPage(user, userRights, "",
								"Settings have been saved successfully. Authentication success.",
								"myq_message_box myq_success_message");
						}
					}
					else {
						return buildSettingsPage(user, userRights, "", "Settings have been saved successfully.",
							"myq_message_box myq_success_message");
					}
			}
			
			return "";
		}

		public override IPlugInAPI.strInterfaceStatus InterfaceStatus() {
			// Do we have a password set?
			if (hs.GetINISetting("Authentication", "myq_username", "", IniFilename).Length == 0 ||
			    getMyQPassword().Length == 0) {
				return new IPlugInAPI.strInterfaceStatus {
					intStatus = IPlugInAPI.enumInterfaceStatus.CRITICAL,
					sStatus = "Missing MyQ credentials"
				};
			}

			switch (myqClient.ClientStatus) {
				case MyQClient.STATUS_OK:
					return new IPlugInAPI.strInterfaceStatus {
						intStatus = IPlugInAPI.enumInterfaceStatus.OK
					};
				
				case MyQClient.STATUS_MYQ_DOWN:
					return new IPlugInAPI.strInterfaceStatus {
						intStatus = IPlugInAPI.enumInterfaceStatus.WARNING,
						sStatus = myqClient.ClientStatusString.Length == 0
							? "MyQ appears to be down"
							: myqClient.ClientStatusString
					};
				
				case MyQClient.STATUS_UNAUTHORIZED:
					return new IPlugInAPI.strInterfaceStatus {
						intStatus = IPlugInAPI.enumInterfaceStatus.CRITICAL,
						sStatus = myqClient.ClientStatusString.Length == 0
							? "MyQ credentials are incorrect"
							: myqClient.ClientStatusString
					};
				
				default:
					return new IPlugInAPI.strInterfaceStatus {
						intStatus = IPlugInAPI.enumInterfaceStatus.INFO,
						sStatus = "Unknown status " + myqClient.ClientStatus
					};
			}
		}

		private async void syncDevices() {
			Program.WriteLog("Debug", "Syncing MyQ devices");
			var errorMsg = await myqClient.getDevices();
			pollTimer.Start(); // enqueue the next poll
			if (errorMsg != "") {
				// Something went wrong!
				Program.WriteLog("Warn", "Cannot retrieve device list from MyQ: " + errorMsg);
				return;
			}

			Program.WriteLog("Debug", "Got list of " + myqClient.Devices.Count + " devices");
			foreach (MyQDevice dev in myqClient.Devices) {
				int devRef = 0;
				if (!serialToRef.TryGetValue(dev.DeviceSerialNumber, out devRef)) {
					// We need to look it up in HS3, and maybe create the device
					var enumerator = (clsDeviceEnumeration) hs.GetDeviceEnumerator();
					do {
						DeviceClass enumDev = enumerator.GetNext();
						if (enumDev == null) {
							break;
						}

						if (enumDev.get_Address(hs).Split('-')[0] == dev.DeviceSerialNumber && enumDev.get_Interface(hs) == Name) {
							// found it!
							devRef = enumDev.get_Ref(hs);
							Program.WriteLog("Debug", "Found existing device for GDO " + dev.DeviceSerialNumber + " with ref " + devRef);
							serialToRef.Add(dev.DeviceSerialNumber, devRef);
							break;
						}
					} while (!enumerator.Finished);
					
					if (devRef == 0) {
						Program.WriteLog("Debug", "Creating new device in HS3 for GDO serial " + dev.DeviceSerialNumber);
						
						// Didn't find an existing device; create one
						devRef = hs.NewDeviceRef(dev.DeviceTypeName);
						var hsDev = (DeviceClass) hs.GetDeviceByRef(devRef);
						hsDev.set_Address(hs, dev.DeviceSerialNumber);
						hsDev.set_Interface(hs, Name);
						hsDev.set_InterfaceInstance(hs, InstanceFriendlyName());
						hsDev.set_Device_Type_String(hs, "LiftMaster MyQ Garage Door Opener");
						hsDev.set_DeviceType_Set(hs, new DeviceTypeInfo_m.DeviceTypeInfo {
							Device_API = DeviceTypeInfo_m.DeviceTypeInfo.eDeviceAPI.Plug_In
						});
						
						// Create buttons
						VSVGPairs.VSPair openBtn = new VSVGPairs.VSPair(ePairStatusControl.Both);
						openBtn.PairType = VSVGPairs.VSVGPairType.SingleValue;
						openBtn.Render = Enums.CAPIControlType.Button;
						openBtn.Status = "Open";
						openBtn.Value = (int) MyQDoorState.Open;

						VSVGPairs.VSPair closeBtn = new VSVGPairs.VSPair(ePairStatusControl.Control);
						closeBtn.PairType = VSVGPairs.VSVGPairType.SingleValue;
						closeBtn.Render = Enums.CAPIControlType.Button;
						closeBtn.Status = "Close";
						closeBtn.Value = (int) MyQDoorState.Closed;
						
						VSVGPairs.VSPair closedStatus = new VSVGPairs.VSPair(ePairStatusControl.Status);
						closedStatus.PairType = VSVGPairs.VSVGPairType.SingleValue;
						closedStatus.Status = "Closed";
						closedStatus.Value = (int) MyQDoorState.Closed;
						
						VSVGPairs.VSPair openingStatus = new VSVGPairs.VSPair(ePairStatusControl.Status);
						openingStatus.PairType = VSVGPairs.VSVGPairType.SingleValue;
						openingStatus.Status = "Opening";
						openingStatus.Value = (int) MyQDoorState.GoingUp;

						VSVGPairs.VSPair closingStatus = new VSVGPairs.VSPair(ePairStatusControl.Status);
						closingStatus.PairType = VSVGPairs.VSVGPairType.SingleValue;
						closingStatus.Status = "Closing";
						closingStatus.Value = (int) MyQDoorState.GoingDown;
						
						VSVGPairs.VSPair stoppedStatus = new VSVGPairs.VSPair(ePairStatusControl.Status);
						stoppedStatus.PairType = VSVGPairs.VSVGPairType.SingleValue;
						stoppedStatus.Status = "Stopped";
						stoppedStatus.Value = (int) MyQDoorState.Stopped;
						
						VSVGPairs.VSPair notClosedStatus = new VSVGPairs.VSPair(ePairStatusControl.Status);
						notClosedStatus.PairType = VSVGPairs.VSVGPairType.SingleValue;
						notClosedStatus.Status = "Not Closed";
						notClosedStatus.Value = (int) MyQDoorState.NotClosed;

						hs.DeviceVSP_AddPair(devRef, openBtn);
						hs.DeviceVSP_AddPair(devRef, closeBtn);
						hs.DeviceVSP_AddPair(devRef, closedStatus);
						hs.DeviceVSP_AddPair(devRef, openingStatus);
						hs.DeviceVSP_AddPair(devRef, closingStatus);
						hs.DeviceVSP_AddPair(devRef, stoppedStatus);
						hs.DeviceVSP_AddPair(devRef, notClosedStatus);
						
						// Status images
						foreach (var state in (MyQDoorState[]) Enum.GetValues(typeof(MyQDoorState))) {
							hs.DeviceVGP_AddPair(devRef, new VSVGPairs.VGPair {
								PairType = VSVGPairs.VSVGPairType.SingleValue,
								Set_Value = (int) state,
								Graphic = MyQDevice.GetDeviceStatusImage(state)
							});
						}
						
						hsDev.MISC_Set(hs, Enums.dvMISC.SHOW_VALUES);
					}
				}

				if (devRef == 0) {
					Program.WriteLog("Warn", "Somehow we still ended up with devRef == 0 for door " + dev.DeviceSerialNumber);
					continue;
				}
				
				int devIdTry;
				if (!refToMyqId.TryGetValue(devRef, out devIdTry) || devIdTry != dev.DeviceId) {
					if (devIdTry != dev.DeviceId) {
						refToMyqId.Remove(devRef);
					}
					
					refToMyqId.Add(devRef, dev.DeviceId);
				}

				if (hs.DeviceValue(devRef) != (int) dev.DoorState) {
					hs.SetDeviceValueByRef(devRef, (int) dev.DoorState, true);
				}
			}
		}

		/// <summary>
		/// Get the saved MyQ password from INI
		/// </summary>
		/// <param name="censor">If true, only return "*****" if a password is saved.</param>
		/// <returns>string</returns>
		private string getMyQPassword(bool censor = true) {
			var password = hs.GetINISetting("Authentication", "myq_password", "", IniFilename);
			//Debug.WriteLine("Retrieved password from INI: " + password);
			
			if (password.Length == 0) {
				return password;
			} else if (censor) {
				return "*****";
			} else {
				var decoded = Encoding.UTF8.GetString(System.Convert.FromBase64String(password));
				//Debug.WriteLine("Decoded base64 password: " + decoded);
				return decoded;
			}
		}
	}
}
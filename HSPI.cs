using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;
using System.Timers;
using System.Web;
using System.Web.Script.Serialization;
using HomeSeerAPI;
using HSPI_LiftMasterMyQ.DataContainers;
using HSPI_LiftMasterMyQ.Enums;
using Scheduler;
using Scheduler.Classes;

namespace HSPI_LiftMasterMyQ
{
	// ReSharper disable once InconsistentNaming
	public class HSPI : HspiBase
	{
		public const string PLUGIN_NAME = "LiftMaster MyQ";
		
		private MyQClient myqClient;
		private Timer pollTimer;
		private readonly Dictionary<string, int> serialToRef;
		private readonly Dictionary<int, int> refToMyqId;
		private readonly Dictionary<int, bool> refLastOnlineStatus;

		private bool currentlySyncingDevices = false;

		private const string DEFAULT_POLL_INTERVAL = "10000";
		
		public HSPI() {
			Name = "LiftMaster MyQ";
			PluginIsFree = true;
			PluginActionCount = 1;
			
			serialToRef = new Dictionary<string, int>();
			refToMyqId = new Dictionary<int, int>();
			refLastOnlineStatus = new Dictionary<int, bool>();
		}

		public override string InitIO(string port) {
			Program.WriteLog(LogType.Verbose, "InitIO");

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
			
			myqClient = new MyQClient(hs.GetINISetting("Options", "myq_use_craftsman", "0", IniFilename) == "1" ? MyQMake.Craftsman : MyQMake.LiftMaster);
			var myqUsername = hs.GetINISetting("Authentication", "myq_username", "", IniFilename);
			var myqPassword = getMyQPassword(false);
			if (myqUsername.Length > 0 && myqPassword.Length > 0) {
				myqClient.Login(myqUsername, myqPassword).ContinueWith(t => {
					if (t.Result == "") {
						// no error occurred
						syncDevices();
					}
				});
			}
			
			pollTimer = new Timer(double.Parse(hs.GetINISetting("Options", "myq_poll_frequency", DEFAULT_POLL_INTERVAL, IniFilename)));
			pollTimer.Elapsed += (Object source, ElapsedEventArgs e) => { syncDevices(); };
			pollTimer.AutoReset = false;
			// don't enable just yet
			
			Timer sanityCheck = new Timer(60000) {AutoReset = true};
			sanityCheck.Elapsed += (object src, ElapsedEventArgs a) => {
				long lastUpdatedTime = Helpers.GetUnixTimeSeconds() - myqClient.DevicesLastUpdated;
				if (lastUpdatedTime > 60) {
					// Devices last updated 60 seconds ago, so something broke.
					Program.WriteLog(LogType.Warn, "MyQ devices last updated " + lastUpdatedTime + " seconds ago; running poll now");
					syncDevices();
				}
			};
			sanityCheck.Start();

			return "";
		}

		public override void SetIOMulti(List<CAPI.CAPIControl> colSend) {
			foreach (var upd in colSend) {
				Program.WriteLog(LogType.Debug, "Ref " + upd.Ref + " set to " + upd.ControlValue);
				int myqId;
				if (!refToMyqId.TryGetValue(upd.Ref, out myqId)) {
					Program.WriteLog(LogType.Error, "No MyQ ID for ref " + upd.Ref + "!!");
					continue;
				}

				myqClient.moveDoor(myqId, (MyQDoorState) upd.ControlValue).ContinueWith(t => {
					Program.WriteLog(LogType.Debug, "Move door command completed" + (t.Result.Length > 0 ? " with error: " + t.Result : ""));

					Timer timer = new Timer(1000);
					timer.AutoReset = false;
					timer.Elapsed += (Object source, ElapsedEventArgs e) => { syncDevices(); };
					timer.Start();
				});
			}
		}

		public override string get_ActionName(int actionNumber) {
			switch (actionNumber) {
				case 1:
					return Name + " Actions";
				
				default:
					return "UNKNOWN ACTION";
			}
		}

		public override string ActionBuildUI(string unique, IPlugInAPI.strTrigActInfo actInfo) {
			if (actInfo.TANumber != 1) {
				return "Bad action number " + actInfo.TANumber + "," + actInfo.SubTANumber;
			}
			
			StringBuilder builder = new StringBuilder();
			SubAction selectedSubAction = (SubAction) actInfo.SubTANumber;

			// Sub-action dropdown
			clsJQuery.jqDropList actionSelector = new clsJQuery.jqDropList("SubAction" + unique, "events", true);
			foreach (SubAction subAction in Enum.GetValues(typeof(SubAction))) {
				string actName = Helpers.AddSpacesToCamelCase(Enum.GetName(typeof(SubAction), subAction));
				if (actName == "Invalid") {
					actName = "(Choose A " + Name + " Action)";
				}

				actionSelector.AddItem(actName, ((int) subAction).ToString(), selectedSubAction == subAction);
			}

			builder.Append(actionSelector.Build());

			if (selectedSubAction == SubAction.SetPollingInterval) {
				SetPollTimeData eventData = SetPollTimeData.Unserialize(actInfo.DataIn);

				if (ActionAdvancedMode) {
					clsJQuery.jqTextBox textBox = new clsJQuery.jqTextBox(
						"PollingInterval_txt" + unique,
						"number",
						eventData.PollIntervalMilliseconds == 0 ? "" : eventData.PollIntervalMilliseconds.ToString(),
						"events",
						30,
						true
					);
					textBox.dialogCaption = "Polling Interval";
					textBox.promptText = "Enter the new polling interval in milliseconds.";
					builder.Append("<br />Polling Interval (ms): ");
					builder.Append(textBox.Build());
				} else {
					clsJQuery.jqTimeSpanPicker timePicker =
						new clsJQuery.jqTimeSpanPicker("PollingInterval" + unique, "Polling Interval:", "events", true);
					timePicker.showDays = false;
					timePicker.defaultTimeSpan = TimeSpan.FromMilliseconds(eventData.PollIntervalMilliseconds);
					builder.Append(timePicker.Build());
				}
			}

			return builder.ToString();
		}

		public override IPlugInAPI.strMultiReturn ActionProcessPostUI(NameValueCollection postData,
			IPlugInAPI.strTrigActInfo actInfo) {

			if (actInfo.TANumber != 1) {
				throw new Exception("Unknown action number " + actInfo.TANumber);
			}
			
			IPlugInAPI.strMultiReturn output = new IPlugInAPI.strMultiReturn();
			output.TrigActInfo.TANumber = actInfo.TANumber;
			output.TrigActInfo.SubTANumber = actInfo.SubTANumber;
			output.DataOut = actInfo.DataIn;

			SubAction selectedSubAction = (SubAction) actInfo.SubTANumber;

			foreach (string key in postData.AllKeys) {
				string[] parts = key.Split('_');
				if (parts.Length > 1) {
					postData.Add(parts[0], postData.Get(key));
				}
			}
			
			// Are we changing the sub action?
			SubAction newSubAction = (SubAction) int.Parse(postData.Get("SubAction"));
			if (newSubAction != selectedSubAction) {
				output.TrigActInfo.SubTANumber = (int) newSubAction;
				// We don't need to clear the action data since we only have one action data class right now.
				// If we add another one, then we might need to.
				return output;
			}

			switch ((SubAction) actInfo.SubTANumber) {
				case SubAction.SetPollingInterval:
					SetPollTimeData actData = SetPollTimeData.Unserialize(actInfo.DataIn);

					string temp;
					if ((temp = postData.Get("PollingInterval")) != null) {
						uint? newPollInterval = Helpers.DecodeTimeSpanToMilliseconds(temp);
						if (newPollInterval != null && newPollInterval >= 1000) {
							actData.PollIntervalMilliseconds = (uint) newPollInterval;							
						}
					}

					output.DataOut = actData.Serialize();
					break;
			}

			return output;
		}

		public override bool ActionConfigured(IPlugInAPI.strTrigActInfo actInfo) {
			switch ((SubAction) actInfo.SubTANumber) {
				case SubAction.PollNow:
				case SubAction.ResetPollingIntervalToConfiguredValue:
					return true;
				
				case SubAction.SetPollingInterval:
					SetPollTimeData actData = SetPollTimeData.Unserialize(actInfo.DataIn);
					return actData.PollIntervalMilliseconds >= 1000;
				
				default:
					return false;
			}
		}

		public override string ActionFormatUI(IPlugInAPI.strTrigActInfo actInfo) {
			if (actInfo.TANumber != 1) {
				return "Unknown action number " + actInfo.TANumber;
			}

			StringBuilder builder = new StringBuilder();

			switch ((SubAction) actInfo.SubTANumber) {
				case SubAction.PollNow:
					builder.Append("<span class=\"event_Txt_Selection\">Poll MyQ device status</span> immediately.");
					break;
				
				case SubAction.ResetPollingIntervalToConfiguredValue:
					builder.Append("<span class=\"event_Txt_Selection\">Reset MyQ poll interval</span> to the value configured on the plugin settings page (");
					builder.Append("<span class=\"event_Txt_Option\">");
					builder.Append(double.Parse(hs.GetINISetting("Options", "myq_poll_frequency", DEFAULT_POLL_INTERVAL, IniFilename)) / 1000.0);
					builder.Append(" seconds</span>).");
					break;
				
				case SubAction.SetPollingInterval:
					SetPollTimeData actData = SetPollTimeData.Unserialize(actInfo.DataIn);
					builder.Append(
						"<span class=\"event_Txt_Selection\">Set MyQ poll interval</span> to <span class=\"event_Txt_Option\">");
					builder.Append(actData.PollIntervalMilliseconds / 1000.0);
					builder.Append(" seconds</span>.");
					break;
				
				default:
					return "Unknown sub-action " + actInfo.SubTANumber;
			}

			return builder.ToString();
		}

		public override bool HandleAction(IPlugInAPI.strTrigActInfo actInfo) {
			if (actInfo.TANumber != 1) {
				Program.WriteLog(LogType.Error,
					"Bad action number " + actInfo.TANumber + " for event " + actInfo.evRef);
				return false;
			}

			switch ((SubAction) actInfo.SubTANumber) {
				case SubAction.PollNow:
					syncDevices();
					return true;
				
				case SubAction.ResetPollingIntervalToConfiguredValue:
					pollTimer.Interval = double.Parse(hs.GetINISetting("Options", "myq_poll_frequency", DEFAULT_POLL_INTERVAL, IniFilename));
					Program.WriteLog(LogType.Info, "Resetting poll interval to " + pollTimer.Interval);
					return true;
				
				case SubAction.SetPollingInterval:
					SetPollTimeData actData = SetPollTimeData.Unserialize(actInfo.DataIn);
					Program.WriteLog(LogType.Info, "Setting poll interval to " + actData.PollIntervalMilliseconds);
					pollTimer.Interval = actData.PollIntervalMilliseconds;
					return true;
				
				default:
					Program.WriteLog(LogType.Error, "Bad sub-action number " + actInfo.SubTANumber + " for event " + actInfo.evRef);
					return false;
			}
		}

		public override string GetPagePlugin(string pageName, string user, int userRights, string queryString) {
			Program.WriteLog(LogType.Verbose, "Requested page name " + pageName + " by user " + user + " with rights " + userRights);

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

<div>
	<label for=""myq_use_craftsman"">Brand</label>
	<select name=""myq_use_craftsman"" id=""myq_use_craftsman"">
		<option value=""0"">LiftMaster / Chamberlain</option>
		<option value=""1"">Craftsman</option>
	</select>
</div>

<button type=""submit"">Submit</button>
");
			sb.Append(PageBuilderAndMenu.clsPageBuilder.FormEnd());

			var savedSettings = new Dictionary<string, string> {
				{"myq_username", hs.GetINISetting("Authentication", "myq_username", "", IniFilename)},
				{"myq_password", getMyQPassword(true)},
				{"myq_poll_frequency", hs.GetINISetting("Options", "myq_poll_frequency", DEFAULT_POLL_INTERVAL, IniFilename)},
				{"myq_use_craftsman", hs.GetINISetting("Options", "myq_use_craftsman", "0", IniFilename)},
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
			Program.WriteLog(LogType.Verbose, "PostBackProc for page " + pageName + " with data " + data + " by user " + user + " with rights " + userRights);
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

					var prevUseCraftsman = hs.GetINISetting("Options", "myq_use_craftsman", "0", IniFilename) == "1";
					var newUseCraftsman = prevUseCraftsman;
					string useCraftsman;

					if ((useCraftsman = qs.Get("myq_use_craftsman")) != null) {
						newUseCraftsman = useCraftsman == "1";
						hs.SaveINISetting("Options", "myq_use_craftsman", useCraftsman, IniFilename);
						if (newUseCraftsman != prevUseCraftsman) {
							myqClient = new MyQClient(useCraftsman == "1" ? MyQMake.Craftsman : MyQMake.LiftMaster);
						}
					}

					if (authCredsChanged || newUseCraftsman != prevUseCraftsman) {
						var authTask = myqClient.Login(hs.GetINISetting("Authentication", "myq_username", "", IniFilename),
							getMyQPassword(false), true);
						authTask.Wait();
						if (authTask.Result.Length > 0) {
							return buildSettingsPage(user, userRights, "", authTask.Result,
								"myq_message_box myq_error_message");
						} else {
							syncDevices();
							return buildSettingsPage(user, userRights, "",
								"Settings have been saved successfully. Authentication success.",
								"myq_message_box myq_success_message");
						}
					} else {
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
				
				case MyQClient.STATUS_THROTTLED:
					return new IPlugInAPI.strInterfaceStatus {
						intStatus = IPlugInAPI.enumInterfaceStatus.CRITICAL,
						sStatus = myqClient.ClientStatusString.Length == 0
							? "Login attempts throttled"
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
			if (currentlySyncingDevices) {
				Program.WriteLog(LogType.Debug, "Suppressing MyQ sync because we're currently syncing devices");
			}

			currentlySyncingDevices = true;
			Program.WriteLog(LogType.Verbose, "Syncing MyQ devices");
			var errorMsg = await myqClient.getDevices();
			currentlySyncingDevices = false;
			
			pollTimer.Stop();
			pollTimer.Start(); // enqueue the next poll
			if (errorMsg != "") {
				// Something went wrong!
				Program.WriteLog(LogType.Error, "Cannot retrieve device list from MyQ: " + errorMsg);
				return;
			}

			Program.WriteLog(LogType.Verbose, "Got list of " + myqClient.Devices.Count + " devices");
			foreach (MyQDevice dev in myqClient.Devices) {
				int devRef = 0;
				if (!serialToRef.TryGetValue(dev.DeviceSerialNumber, out devRef)) {
					// We need to look it up in HS3, and maybe create the device
					devRef = hs.DeviceExistsAddress(dev.DeviceSerialNumber, false);
					if (devRef == -1) {
						devRef = 0;
					} else {
						Program.WriteLog(LogType.Debug, "Found existing device for GDO " + dev.DeviceSerialNumber + " with ref " + devRef);
						serialToRef.Add(dev.DeviceSerialNumber, devRef);
					}
					
					if (devRef == 0) {
						Program.WriteLog(LogType.Debug, "Creating new device in HS3 for GDO serial " + dev.DeviceSerialNumber);
						
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
						openBtn.Render = HomeSeerAPI.Enums.CAPIControlType.Button;
						openBtn.Status = "Open";
						openBtn.Value = (int) MyQDoorState.Open;
						openBtn.ControlUse = ePairControlUse._DoorUnLock;

						VSVGPairs.VSPair closeBtn = new VSVGPairs.VSPair(ePairStatusControl.Control);
						closeBtn.PairType = VSVGPairs.VSVGPairType.SingleValue;
						closeBtn.Render = HomeSeerAPI.Enums.CAPIControlType.Button;
						closeBtn.Status = "Close";
						closeBtn.Value = (int) MyQDoorState.Closed;
						closeBtn.ControlUse = ePairControlUse._DoorLock;
						
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
						
						hsDev.MISC_Set(hs, HomeSeerAPI.Enums.dvMISC.SHOW_VALUES);
						hsDev.MISC_Set(hs, HomeSeerAPI.Enums.dvMISC.AUTO_VOICE_COMMAND);
					}
				}

				if (devRef == 0) {
					Program.WriteLog(LogType.Warn, "Somehow we still ended up with devRef == 0 for door " + dev.DeviceSerialNumber);
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

				bool deviceLastSeenOnline;
				if (!refLastOnlineStatus.TryGetValue(devRef, out deviceLastSeenOnline)) {
					// No last known online status, so set the "last known status" to the inverse of what it is now
					// That way we update HS3
					deviceLastSeenOnline = !dev.IsOnline;
				}
				
				if (!dev.IsOnline && deviceLastSeenOnline) {
					var hsDevice = (DeviceClass) hs.GetDeviceByRef(devRef);
					Program.WriteLog(LogType.Warn, "Device ref " + devRef + " (MyQ ID " + dev.DeviceId + ") is offline");
					hsDevice.set_Attention(hs, "The device is offline. Please check the power and network connections.");
				}
				else if (dev.IsOnline && !deviceLastSeenOnline) {
					// It's online
					var hsDevice = (DeviceClass) hs.GetDeviceByRef(devRef);
					Program.WriteLog(LogType.Info, "Device ref " + devRef + " (MyQ ID " + dev.DeviceId + ") is now online");
					hsDevice.set_Attention(hs, null);
				}

				refLastOnlineStatus[devRef] = dev.IsOnline;
			}
		}

		/// <summary>
		/// Get the saved MyQ password from INI
		/// </summary>
		/// <param name="censor">If true, only return "*****" if a password is saved.</param>
		/// <returns>string</returns>
		private string getMyQPassword(bool censor = true) {
			var password = hs.GetINISetting("Authentication", "myq_password", "", IniFilename);
			
			if (password.Length == 0) {
				return password;
			} else if (censor) {
				return "*****";
			} else {
				var decoded = Encoding.UTF8.GetString(System.Convert.FromBase64String(password));
				return decoded;
			}
		}
	}
}
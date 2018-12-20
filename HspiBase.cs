using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using HomeSeerAPI;
using HSCF.Communication.Scs.Communication.EndPoints.Tcp;
using HSCF.Communication.ScsServices.Client;

namespace HSPI_LiftMasterMyQ
{
	public abstract class HspiBase : IPlugInAPI
	{	
		#region Plugin configuration properties
		
		/// <summary> Is this a free plugin? If false, requires a paid license to operate. </summary>
		protected bool PluginIsFree { get; set; }
		
		/// <summary> What are the capabilities used by this plugin? Bitfield of HomeSeerAPI.Enums.eCapabilities </summary>
		protected int PluginCapabilities { get; set; }

		/// <summary> Does the plugin allow users to add devices for it manually? </summary>
		protected bool PluginSupportsAddDevice { get; set; }
		
		/// <summary> Does the plugin support manually configuring its owned devices on the device utility page? </summary>
		protected bool PluginSupportsConfigDevice { get; set; }
		
		/// <summary> Does the plugin support manually configuring all devices, not just its own? </summary>
		protected bool PluginSupportsConfigDeviceAll { get; set; }
		
		/// <summary> Does the plugin support running multiple instances? </summary>
		protected bool PluginSupportsMultipleInstances { get; set; }

		/// <summary> Does the plugin support running multiple instances using a single executable? </summary>
		protected bool PluginSupportsMultipleInstancesSingleExecutable { get; set; }
		
		/// <summary> Does the plugin support generic callbacks? </summary>
		protected bool PluginRaisesGenericCallbacks { get; set; }

		/// <summary> How many actions are supported by the plugin? </summary>
		protected int PluginActionCount { get; set; }
		
		#endregion
		
		#region HomeSeer plugin metadata properties
		
		/// <summary> The name for this plugin, as displayed in HomeSeer. </summary>
		public string Name { get; protected set; }
		
		/// <summary> Does this plugin require a COM port for talking to hardware? </summary>
		public bool HSCOMPort { get; protected set; }
		
		/// <summary> Does this plugin have any triggers? </summary>
		public bool HasTriggers { get { return this.TriggerCount > 0; } }
		
		/// <summary> How many triggers does this plugin have? </summary>
		public int TriggerCount { get; protected set; }

		#endregion
		
		#region Convenience properties

		protected string IniFilename {
			get { return this.Name + ".ini"; }
		}
		
		#endregion
		
		#region Methods used by HS3 to get plugin data

		/// <summary>
		/// Get the instance name for this plugin. Just return empty string if this plugin doesn't support multiple
		/// instances.
		/// </summary>
		/// <returns>string</returns>
		public virtual string InstanceFriendlyName() {
			return "";
		}

		/// <summary>
		/// Called by HS3 at any time to get the plugin status. Should be overridden if it's possible for this
		/// plugin to have a fail or warn condition.
		/// </summary>
		/// <returns>IPlugInAPI.strInterfaceStatus</returns>
		public virtual IPlugInAPI.strInterfaceStatus InterfaceStatus() {
			return new IPlugInAPI.strInterfaceStatus {intStatus = IPlugInAPI.enumInterfaceStatus.OK};
		}

		/// <summary>
		/// Return the capabilities of this plugin.
		/// </summary>
		/// <returns>int</returns>
		public int Capabilities() {
			return this.PluginCapabilities;
		}

		/// <summary>
		/// Return whether the plugin requires a paid license or not.
		/// </summary>
		/// <returns>int</returns>
		public int AccessLevel() {
			return this.PluginIsFree ? 1 : 2;
		}

		public bool SupportsAddDevice() {
			return this.PluginSupportsAddDevice;
		}

		public bool SupportsConfigDevice() {
			return this.PluginSupportsConfigDevice;
		}

		public bool SupportsConfigDeviceAll() {
			return this.PluginSupportsConfigDeviceAll;
		}

		public bool SupportsMultipleInstances() {
			return this.PluginSupportsMultipleInstances;
		}

		public bool SupportsMultipleInstancesSingleEXE() {
			return this.PluginSupportsMultipleInstancesSingleExecutable;
		}

		public bool RaisesGenericCallbacks() {
			return this.PluginRaisesGenericCallbacks;
		}
		
		#endregion
		
		#region Methods called by HS3 during regular operation

		/// <summary>
		/// This is called when an event happens, provided you called RegisterEventCB first.
		/// </summary>
		/// <param name="eventType">The type of the event that happened</param>
		/// <param name="parameters">Parameters for the event</param>
		public virtual void HSEvent(HomeSeerAPI.Enums.HSEvent eventType, object[] parameters) {}

		/// <summary>
		/// Called when the plugin initializes. Here you should init communication and threads and whatnot.
		/// Return error message, or empty string on success.
		/// </summary>
		/// <param name="port">Configured COM port, if applicable</param>
		/// <returns>string</returns>
		public virtual string InitIO(string port) {			
			return "";
		}

		/// <summary>
		/// Called when HS3 wants to manually poll the status of a device.
		/// </summary>
		/// <param name="deviceRef">Device reference ID</param>
		/// <returns>IPlugInAPI.PollResultInfo</returns>
		public virtual IPlugInAPI.PollResultInfo PollDevice(int deviceRef) {
			return new IPlugInAPI.PollResultInfo {
				Result = IPlugInAPI.enumPollResult.Device_Not_Found,
				Value = 0
			};
		}

		/// <summary>
		/// Called by HS3 when a device this plugin owns is updated.
		/// </summary>
		/// <param name="colSend">
		/// Collection of CAPIControl objects. Each entry is one device that's been updated.
		/// Check out ControlValue to get the value it was set to.
		/// </param>
		public virtual void SetIOMulti(List<CAPI.CAPIControl> colSend) {}

		/// <summary>
		/// Called when HS3 is unloading the plugin. It should clean up after itself here.
		/// </summary>
		public virtual void ShutdownIO() {
			this.Shutdown = true;
		}
		
		#endregion
		
		#region HS3 generic methods

		/// <summary>
		/// Called when the user searches HS3. Can and should return anything that matches the search query;
		/// devices, actions, etc.
		/// </summary>
		/// <param name="searchString">The search query</param>
		/// <param name="regEx">Specifies whether the query is a regex</param>
		/// <returns>SearchResult[]</returns>
		public virtual SearchReturn[] Search(string searchString, bool regEx) {
			return null;
		}

		/// <summary>
		/// If the plugin is a speak proxy, this is called when HS3 is asked to speak something.
		/// The plugin can modify this or just pass it as-is to SpeakProxy.
		/// </summary>
		/// <param name="device">Device that's to be used for speaking</param>
		/// <param name="text">Text to speak</param>
		/// <param name="wait">This parameter tells HomeSeer whether to continue processing commands immediately or to wait until the speak command is finished - pass this parameter unchanged to SpeakProxy</param>
		/// <param name="host">List of host:instances to speak on. * indicates all hosts.</param>
		public virtual void SpeakIn(int device, string text, bool wait, string host) {}
		
		#endregion
		
		#region HS3 action methods and stuff

		/// <summary> Specifies whether or not the user wants all the advanced action data. </summary>
		public bool ActionAdvancedMode { get; set; }

		/// <summary>
		/// Called from the HS3 event page when an event is being edited.
		/// Return HTML controls so the user can make selections to choose an action.
		/// </summary>
		/// <param name="uniqueId">Unique string to identify HTML controls</param>
		/// <param name="actInfo">Object containing information about the action like what's currently selected</param>
		/// <returns>string</returns>
		public virtual string ActionBuildUI(string uniqueId, IPlugInAPI.strTrigActInfo actInfo) {
			return "";
		}

		/// <summary>
		/// Return true if the given action has been properly configured.
		/// Returning false prevents the action from being saved.
		/// </summary>
		/// <param name="actInfo">Object containing details about the action</param>
		/// <returns>bool</returns>
		public virtual bool ActionConfigured(IPlugInAPI.strTrigActInfo actInfo) {
			return true;
		}

		/// <summary>
		/// How many actions are supported by the plugin?
		/// </summary>
		/// <returns>int</returns>
		public int ActionCount() {
			return this.PluginActionCount;
		}

		public virtual string ActionFormatUI(IPlugInAPI.strTrigActInfo actInfo) {
			return "";
		}

		/// <summary>
		/// Called when the user edits event actions.
		/// </summary>
		/// <param name="postData">Collection of name/value pairs for the user's selections</param>
		/// <param name="trigInfo">Information about the action</param>
		/// <returns>Parsed information for the action(s), which will be saved by HS3.</returns>
		public virtual IPlugInAPI.strMultiReturn ActionProcessPostUI(NameValueCollection postData,
			IPlugInAPI.strTrigActInfo trigInfo) {
			return new IPlugInAPI.strMultiReturn();
		}

		/// <summary>
		/// Indicates whether the given device is referenced by the given action.
		/// </summary>
		/// <param name="actInfo">Info about the action</param>
		/// <param name="deviceRef">Device ref</param>
		/// <returns>bool</returns>
		public virtual bool ActionReferencesDevice(IPlugInAPI.strTrigActInfo actInfo, int deviceRef) {
			return false;
		}

		/// <summary>
		/// Return the name of the given action.
		/// </summary>
		/// <param name="actionNumber">The number of the action, starting at 1</param>
		/// <returns>string</returns>
		public virtual string get_ActionName(int actionNumber) {
			return "";
		}

		/// <summary>
		/// Run an action.
		/// </summary>
		/// <param name="actInfo">The action to execute</param>
		/// <returns>bool</returns>
		public virtual bool HandleAction(IPlugInAPI.strTrigActInfo actInfo) {
			return false;
		}
		
		#endregion
		
		#region HS3 trigger methods and stuff

		public virtual bool get_Condition(IPlugInAPI.strTrigActInfo trigInfo) {
			return false;
		}

		public virtual void set_Condition(IPlugInAPI.strTrigActInfo trigInfo, bool value) {}

		/// <summary>
		/// Returns whether the given trigger can also be used as a condition.
		/// </summary>
		/// <param name="triggerNumber">The number of the trigger</param>
		/// <returns>bool</returns>
		public virtual bool get_HasConditions(int triggerNumber) {
			return false;
		}

		/// <summary>
		/// Return HTML controls for a given trigger.
		/// </summary>
		/// <param name="uniqueId">Unique ID for your triggers</param>
		/// <param name="trigInfo">Details for the triggers.</param>
		/// <returns>string</returns>
		public virtual string TriggerBuildUI(string uniqueId, IPlugInAPI.strTrigActInfo trigInfo) {
			return "";
		}

		/// <summary>
		/// Display configured trigger string
		/// </summary>
		/// <param name="trigInfo">Details about the configured trigger</param>
		/// <returns>string</returns>
		public virtual string TriggerFormatUI(IPlugInAPI.strTrigActInfo trigInfo) {
			return "";
		}

		/// <summary>
		/// Process a post from the events page into a data format we can process.
		/// </summary>
		/// <param name="postData">Key/value pairs for submitted data</param>
		/// <param name="trigInfo">Trigger info</param>
		/// <returns>IPlugInAPI.strMultiReturn</returns>
		public virtual IPlugInAPI.strMultiReturn TriggerProcessPostUI(NameValueCollection postData,
			IPlugInAPI.strTrigActInfo trigInfo) {
			return new IPlugInAPI.strMultiReturn();
		}

		/// <summary>
		/// Indicate whether the given trigger references the given device.
		/// </summary>
		/// <param name="trigInfo">Trigger details</param>
		/// <param name="deviceRef">Device ref id</param>
		/// <returns>bool</returns>
		public virtual bool TriggerReferencesDevice(IPlugInAPI.strTrigActInfo trigInfo, int deviceRef) {
			return false;
		}

		/// <summary>
		/// Called when a trigger is used as a condition, to determine whether the trigger condition is true or not.
		/// Not called when HS3 is evaluating if a trigger should fire; use TriggerFire for that.
		/// </summary>
		/// <param name="trigInfo">Details of the trigger</param>
		/// <returns>bool</returns>
		public virtual bool TriggerTrue(IPlugInAPI.strTrigActInfo trigInfo) {
			return false;
		}

		/// <summary>
		/// Return the number of sub-triggers supported by this trigger ID.
		/// </summary>
		/// <param name="triggerNumber">Trigger ID, starting at 1</param>
		/// <returns>int</returns>
		public virtual int get_SubTriggerCount(int triggerNumber) {
			return 0;
		}

		/// <summary>
		/// Return the name of a given sub-trigger.
		/// </summary>
		/// <param name="triggerNumber">Parent trigger ID</param>
		/// <param name="subTriggerNumber">Sub-trigger ID within the parent trigger ID</param>
		/// <returns>string</returns>
		public virtual string get_SubTriggerName(int triggerNumber, int subTriggerNumber) {
			return "";
		}

		/// <summary>
		/// Return whether the given trigger is configured properly.
		/// </summary>
		/// <param name="trigInfo">Trigger details</param>
		/// <returns>bool</returns>
		public virtual bool get_TriggerConfigured(IPlugInAPI.strTrigActInfo trigInfo) {
			return false;
		}

		/// <summary>
		/// Get the name of a trigger.
		/// </summary>
		/// <param name="triggerNumber">Trigger ID</param>
		/// <returns>string</returns>
		public virtual string get_TriggerName(int triggerNumber) {
			return "";
		}
		
		
		
		#endregion
		
		#region HS3 UI methods and stuff

		/// <summary>
		/// For backwards-compatibility with HS2.
		/// </summary>
		/// <param name="link"></param>
		/// <returns>string</returns>
		public virtual string GenPage(string link) {
			return "";
		}

		/// <summary>
		/// Called to handle HTTP PUT if the plugin page has form elements in it.
		/// </summary>
		/// <param name="data">Submitted data</param>
		/// <returns>string</returns>
		public virtual string PagePut(string data) {
			return "";
		}

		/// <summary>
		/// Pages that use clsPageBuilder and are registered with hs.RegisterLink and hs.RegisterConfigLink are
		/// called through this function. You need to return a complete web page.
		/// </summary>
		/// <param name="page">Name of the page as was passed to hs.RegisterLink</param>
		/// <param name="user">Name of the logged in user</param>
		/// <param name="userRights">The rights of the logged in user</param>
		/// <param name="queryString">The query string in the URL</param>
		/// <returns>string</returns>
		public virtual string GetPagePlugin(string page, string user, int userRights, string queryString) {
			return "";
		}

		/// <summary>
		/// When the user clicks a control on a web page, this is called with the post data.
		/// </summary>
		/// <param name="page">The name of the page as was passed to hs.RegisterLink</param>
		/// <param name="data">The submitted data</param>
		/// <param name="user">Name of the logged in user</param>
		/// <param name="userRights">The rights of the logged in user</param>
		/// <returns>string</returns>
		public virtual string PostBackProc(string page, string data, string user, int userRights) {
			return "";
		}

		/// <summary>
		/// If PluginSupportsConfigDevice is <c>true</c>, this is called when the device properties are displayed on
		/// the Device Utility page.
		/// Return HTML you'd like displayed.
		/// </summary>
		/// <param name="ref">Device reference id</param>
		/// <param name="user">Name of the logged in user</param>
		/// <param name="userRights">Rights of the logged in user</param>
		/// <param name="newDevice">Specifies whether this is a new device being created for the first time</param>
		/// <returns>string</returns>
		public virtual string ConfigDevice(int @ref, string user, int userRights, bool newDevice) {
			return "";
		}

		/// <summary>
		/// Called when a user posts information from the plugin tab on the device utility page.
		/// </summary>
		/// <param name="ref">Device reference id</param>
		/// <param name="data">Posted data</param>
		/// <param name="user">Name of the logged in user</param>
		/// <param name="userRights">Rights of the logged in user</param>
		/// <returns>HomeSeerAPI.Enums.ConfigDevicePostReturn</returns>
		public virtual HomeSeerAPI.Enums.ConfigDevicePostReturn
			ConfigDevicePost(int @ref, string data, string user, int userRights) {
			return HomeSeerAPI.Enums.ConfigDevicePostReturn.DoneAndCancel;
		}

		/// <summary>
		/// Call a function in the plugin.
		/// </summary>
		/// <param name="functionName">Name of the function to call</param>
		/// <param name="parameters">Parameters for the function call</param>
		/// <returns>object</returns>
		public virtual object PluginFunction(string functionName, object[] parameters) {
			return null;
		}

		/// <summary>
		/// Get a property from the plugin.
		/// </summary>
		/// <param name="propertyName">Name of the property to get</param>
		/// <param name="parameters">Parameters for the function call</param>
		/// <returns>object</returns>
		public virtual object PluginPropertyGet(string propertyName, object[] parameters) {
			return null;
		}

		/// <summary>
		/// Set a property of the plugin.
		/// </summary>
		/// <param name="propertyName">Name of the property to set</param>
		/// <param name="value">Value to set the property to</param>
		public virtual void PluginPropertySet(string propertyName, object value) {}
		
		#endregion
		
		#region Plugin bootstrap

		protected IScsServiceClient<IHSApplication> hsClient;
		protected IScsServiceClient<IAppCallbackAPI> callbackClient;
		protected HomeSeerAPI.IHSApplication hs;
		protected HomeSeerAPI.IAppCallbackAPI callbacks;

		public virtual bool Connected {
			get {
				return hsClient.CommunicationState ==
				       HSCF.Communication.Scs.Communication.CommunicationStates.Connected;
			}
		}
		
		public bool Shutdown { get; protected set; }

		public virtual void Connect(string serverAddress, int serverPort) {
			hsClient = ScsServiceClientBuilder.CreateClient<IHSApplication>(
				new ScsTcpEndPoint(serverAddress, serverPort), this);
			hsClient.Connect();

			hs = hsClient.ServiceProxy;
			Program.HsClient = hs;
			// make sure we're connected successfully
			double apiVersion = hs.APIVersion;

			callbackClient =
				ScsServiceClientBuilder.CreateClient<IAppCallbackAPI>(new ScsTcpEndPoint(serverAddress, serverPort),
					this);
			callbackClient.Connect();
			callbacks = callbackClient.ServiceProxy;
			apiVersion = callbacks.APIVersion;

			hs.Connect(this.Name, this.InstanceFriendlyName());
		}
		
		#endregion

		protected HspiBase() {
			this.Name = "Uninitialized HS3 Plugin";
			this.PluginIsFree = true;
			this.HSCOMPort = false;
			this.TriggerCount = 0;
			this.PluginActionCount = 0;
			this.PluginCapabilities = (int) HomeSeerAPI.Enums.eCapabilities.CA_IO;
			this.PluginSupportsAddDevice = false;
			this.PluginSupportsConfigDevice = false;
			this.PluginSupportsConfigDeviceAll = false;
			this.PluginSupportsMultipleInstances = false;
			this.PluginSupportsMultipleInstancesSingleExecutable = false;
			this.PluginRaisesGenericCallbacks = false;
			this.Shutdown = false;
		}
	}
}

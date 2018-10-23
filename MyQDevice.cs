namespace HSPI_LiftMasterMyQ
{
	public class MyQDevice
	{
		public int DeviceId { get; private set; }
		public MyQDeviceType DeviceTypeId { get; private set; }
		public string DeviceTypeName { get; private set; }
		public string DeviceSerialNumber { get; private set; }
		public bool IsOnline { get; private set; }
		public MyQDoorState DoorState { get; private set; }
		public bool CanOpen { get; private set; }
		public bool CanClose { get; private set; }

		public MyQDevice(dynamic deviceInfo) {
			DeviceId = deviceInfo["MyQDeviceId"];
			DeviceTypeId = (MyQDeviceType) deviceInfo["MyQDeviceTypeId"];
			DeviceTypeName = deviceInfo["MyQDeviceTypeName"];
			DeviceSerialNumber = deviceInfo["SerialNumber"];
			
			// defaults
			IsOnline = false;
			DoorState = MyQDoorState.Closed;
			CanOpen = false;
			CanClose = false;

			foreach (dynamic attrib in deviceInfo["Attributes"]) {
				switch (((string) attrib["AttributeDisplayName"]).ToLower()) {
					case "online":
						IsOnline = ((string) attrib["Value"]).ToLower() == "true";
						break;
					
					case "doorstate":
						DoorState = (MyQDoorState) int.Parse(attrib["Value"]);
						break;
					
					case "isunattendedopenallowed":
						CanOpen = attrib["Value"] == "1";
						break;
					
					case "isunattendedcloseallowed":
						CanClose = attrib["Value"] == "1";
						break;
				}
			}
		}
	}
}

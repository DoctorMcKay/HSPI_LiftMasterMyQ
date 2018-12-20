using System;
using System.Runtime.CompilerServices;

namespace HSPI_LiftMasterMyQ
{
	public class Program
	{
		private const string DEFAULT_SERVER_ADDRESS = "127.0.0.1";
		private const int DEFAULT_SERVER_PORT = 10400;

		public static HomeSeerAPI.IHSApplication HsClient;

		public static void Main(string[] args) {
			string serverAddress = DEFAULT_SERVER_ADDRESS;
			int serverPort = DEFAULT_SERVER_PORT;
			
			foreach (string arg in args) {
				string[] parts = arg.Split('=');
				switch (parts[0].ToLower()) {
					case "server":
						serverAddress = parts[1];
						break;
					
					default:
						Console.WriteLine("Warning: Unknown command line argument " + parts[0]);
						break;
				}
			}

			HSPI plugin = new HSPI();
			Console.WriteLine("Plugin " + plugin.Name + " is connecting to HS3 at " + serverAddress + ":" + serverPort);
			try {
				plugin.Connect(serverAddress, serverPort);
				Console.WriteLine("Connection established");
			}
			catch (Exception ex) {
				Console.WriteLine("Unable to connect to HS3: " + ex.Message);
				return;
			}

			try {
				while (true) {
					System.Threading.Thread.Sleep(250);
					if (!plugin.Connected) {
						Console.WriteLine("Connection to HS3 lost!");
						break;
					}

					if (plugin.Shutdown) {
						Console.WriteLine("Plugin has been shut down; exiting");
						break;
					}
				}
			}
			catch (Exception ex) {
				Console.WriteLine("Unhandled exception: " + ex.Message);
			}
		}

		public static void WriteLog(LogType logType, string message, [CallerLineNumber] int lineNumber = 0, [CallerMemberName] string caller = null) {
#if !DEBUG
			if (logType <= LogType.Verbose) {
				// Don't log Console, Silly, and Verbose messages in production builds
				return;
			}
#endif

			string type = logType.ToString().ToLower();

#if DEBUG
			if (logType != LogType.Console) {
				// Log to HS3 log
				string hs3LogType = HSPI.PLUGIN_NAME;
				if (logType == LogType.Silly) {
					hs3LogType += " Silly";
				}

				HsClient.WriteLog(hs3LogType, type + ": [" + caller + ":" + lineNumber + "] " + message);
			}

			Console.WriteLine("[" + type + "] " + message);
#else
			string hs3LogType = HSPI.PLUGIN_NAME;
			if (logType == LogType.Debug) {
				hs3LogType += " Debug";
			}
			
			HsClient.WriteLog(hs3LogType, type + ": " + message);
#endif
		}
	}
	
	public enum LogType
	{
		Console = 1,				// DEBUG ONLY: Printed to the console
		Silly = 2,					// DEBUG ONLY: Logged to HS3 log under type "PluginName Silly"
		Verbose = 3,				// DEBUG ONLY: Logged to HS3 log under normal type
		Debug = 4,					// In debug builds, logged to HS3 log under normal type. In production builds, logged to HS3 log under type "PluginName Debug"
		Info = 5,
		Warn = 6,
		Error = 7,
		Critical = 8,
	}
}

using System;

namespace HSPI_LiftMasterMyQ
{
	public class Program
	{
		public static string serverAddress = "127.0.0.1";
		public static int serverPort = 10400;
		
		public static void Main(string[] args) {
			foreach (string arg in args) {
				string[] parts = arg.Split('=');
				switch (parts[0].ToLower()) {
					case "server":
						serverAddress = parts[1];
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

			System.Environment.Exit(0);
		}
	}
}

using System.Runtime.CompilerServices;

namespace HSPI_LiftMasterMyQ
{
	public class Debug
	{
		public static void WriteToConsole(string line, [CallerLineNumber] int lineNumber = 0, [CallerMemberName] string caller = null) {
#if DEBUG
			System.Console.WriteLine("[" + caller + ":" + lineNumber + "] " + line);
#endif
		}
	}
}

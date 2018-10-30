using System.Runtime.CompilerServices;

namespace HSPI_LiftMasterMyQ
{
	public class Debug
	{
		public static void WriteLine(string line, bool suppressLog = false, [CallerLineNumber] int lineNumber = 0, [CallerMemberName] string caller = null) {
#if DEBUG
			System.Console.WriteLine(line);
#endif

			if (!suppressLog) {
				// ReSharper disable once ExplicitCallerInfoArgument
				Program.WriteLog("Debug", line, lineNumber, caller);
			}
		}
	}
}

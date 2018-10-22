namespace HSPI_LiftMasterMyQ
{
	public class Debug
	{
		public static void WriteLine(string line) {
#if DEBUG
			System.Console.WriteLine(line);
#endif
		}
	}
}

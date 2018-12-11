using System;

namespace HSPI_LiftMasterMyQ
{
	public class Helpers
	{
		/// <summary> Get the current Unix time in seconds. </summary>
		/// <returns>long</returns>
		public static long GetUnixTimeSeconds() {
			return (long) (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0)).TotalSeconds;
		}
	}
}

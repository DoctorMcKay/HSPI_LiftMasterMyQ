using System.Text;

namespace HSPI_LiftMasterMyQ
{
	public class Helpers
	{
		public static string AddSpacesToCamelCase(string input) {
			StringBuilder output = new StringBuilder();
			foreach (char c in input) {
				if (char.IsUpper(c)) {
					output.Append(' ');
				}

				output.Append(c);
			}

			return output.ToString().Trim();
		}

		public static uint? DecodeTimeSpanToMilliseconds(string timeSpan) {
			if (uint.TryParse(timeSpan, out uint val)) {
				return val;
			}

			string[] parts = timeSpan.Replace('.', ':').Split(':');
			if (parts.Length < 4) {
				return null;
			}
			
			uint days = uint.Parse(parts[0]);
			uint hours = uint.Parse(parts[1]);
			uint minutes = uint.Parse(parts[2]);
			uint seconds = uint.Parse(parts[3]);

			seconds += minutes * 60;
			seconds += hours * 60 * 60;
			seconds += days * 60 * 60 * 24;
			return seconds * 1000;
		}
	}
}

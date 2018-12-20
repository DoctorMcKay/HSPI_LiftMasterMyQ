using System;
using System.IO;

namespace HSPI_LiftMasterMyQ.DataContainers
{
	public class SetPollTimeData
	{
		public byte Version { get; set; }
		public uint PollIntervalMilliseconds { get; set; }

		/// <summary> SetPollTimeData constructor. </summary>
		public SetPollTimeData() {
			Version = 1;
			PollIntervalMilliseconds = 0;
		}

		/// <summary> Unserialize a SetPollTimeData binary object. </summary>
		/// <param name="input">Byte array containing serialized data.</param>
		/// <returns>SetPollTimeData</returns>
		public static SetPollTimeData Unserialize(byte[] input) {
			SetPollTimeData output = new SetPollTimeData();
			if (input == null || input.Length == 0) {
				return output;
			}
			
			MemoryStream stream = new MemoryStream(input);
			BinaryReader reader = new BinaryReader(stream);

			byte version = reader.ReadByte();
			output.Version = version;
			switch (version) {
				case 1:
					output.PollIntervalMilliseconds = reader.ReadUInt32();
					break;
				
				default:
					throw new Exception("Unknown version " + version);
			}
			
			reader.Dispose();
			stream.Dispose();
			return output;
		}

		/// <summary> Serialize this SetPollTimeData to binary. </summary>
		/// <returns>byte[]</returns>
		public byte[] Serialize() {
			MemoryStream stream = new MemoryStream();
			BinaryWriter writer = new BinaryWriter(stream);

			writer.Write(Version);
			writer.Write(PollIntervalMilliseconds);
			
			byte[] output = stream.ToArray();
			writer.Dispose();
			stream.Dispose();
			return output;
		}
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KERBALISMLITE
{
	public class PartData
	{
		public uint FlightId { get; private set; }

		public PartData(Part part)
		{
			FlightId = part.flightID;
		}

		public PartData(ProtoPartSnapshot protopart)
		{
			FlightId = protopart.flightID;
		}
	}
}

using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISMLITE
{
    public static class DB
    {
        public static void Load(ConfigNode node)
        {
            // get version (or use current one for new savegames)
            string versionStr = Lib.ConfigValue(node, "version", Lib.KerbalismVersion.ToString());
            // sanitize old saves (pre 3.1) format (X.X.X.X) to new format (X.X)
            if (versionStr.Split('.').Length > 2) versionStr = versionStr.Split('.')[0] + "." + versionStr.Split('.')[1];
            version = new Version(versionStr);

            // if this is an unsupported version, print warning
            if (version <= new Version(1, 2)) //Lib.Log("loading save from unsupported version " + version);

            // get unique id (or generate one for new savegames)
            uid = Lib.ConfigValue(node, "uid", Lib.RandomInt(int.MaxValue));

			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.DB.Load.Vessels");
			vessels.Clear();
			// flightstate will be null when first creating the game
			if (HighLogic.CurrentGame.flightState != null)
			{
				ConfigNode vesselsNode = node.GetNode("vessels2");
				if (vesselsNode == null)
					vesselsNode = new ConfigNode();
				// HighLogic.CurrentGame.flightState.protoVessels is what is used by KSP to persist vessels
				// It is always available and synchronized in OnLoad, no matter the scene, excepted on the first OnLoad in a new game
				foreach (ProtoVessel pv in HighLogic.CurrentGame.flightState.protoVessels)
				{
					if (pv.vesselID == Guid.Empty)
					{
						// It seems flags are saved with an empty GUID. skip them.
						//Lib.LogDebug("Skipping VesselData load for vessel with empty GUID :" + pv.vesselName);
						continue;
					}

					VesselData vd = new VesselData(pv, vesselsNode.GetNode(pv.vesselID.ToString()));
					vessels.Add(pv.vesselID, vd);
					//Lib.LogDebug("VesselData loaded for vessel " + pv.vesselName);
				}
			}
			UnityEngine.Profiling.Profiler.EndSample();

			// for compatibility with old saves, convert drives data (it's now saved in PartData)
			if (node.HasNode("drives"))
			{
				Dictionary<uint, PartData> allParts = new Dictionary<uint, PartData>();
				foreach (VesselData vesselData in vessels.Values)
				{
					foreach (PartData partData in vesselData.PartDatas)
					{
						// we had a case of someone having a save with multiple parts having the same flightID
						// 5 duplicates, all were asteroids.
						if (!allParts.ContainsKey(partData.FlightId))
						{
							allParts.Add(partData.FlightId, partData);
						}
					}
				}
			}
        }

        public static void Save(ConfigNode node)
        {
            // save version
            node.AddValue("version", Lib.KerbalismVersion.ToString());

            // save unique id
            node.AddValue("uid", uid);

			// only persist vessels that exists in KSP own vessel persistence
			// this prevent creating junk data without going into the mess of using gameevents
			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.DB.Save.Vessels");
			ConfigNode vesselsNode = node.AddNode("vessels2");
			foreach (ProtoVessel pv in HighLogic.CurrentGame.flightState.protoVessels)
			{
				if (pv.vesselID == Guid.Empty)
				{
					// It seems flags are saved with an empty GUID. skip them.
					//Lib.LogDebug("Skipping VesselData save for vessel with empty GUID :" + pv.vesselName);
					continue;
				}

				VesselData vd = pv.KerbalismData();
				ConfigNode vesselNode = vesselsNode.AddNode(pv.vesselID.ToString());
				vd.Save(vesselNode);
			}
			UnityEngine.Profiling.Profiler.EndSample();

            // save bodies data
            var bodies_node = node.AddNode("bodies");
        }

		public static VesselData KerbalismData(this Vessel vessel)
		{
			VesselData vd;
			if (!vessels.TryGetValue(vessel.id, out vd))
			{
				//Lib.LogDebug("Creating Vesseldata for new vessel " + vessel.vesselName);
				vd = new VesselData(vessel);
				vessels.Add(vessel.id, vd);
			}
			return vd;
		}

		public static VesselData KerbalismData(this ProtoVessel protoVessel)
		{
			VesselData vd;
			if (!vessels.TryGetValue(protoVessel.vesselID, out vd))
			{
				//Lib.Log("VesselData for protovessel " + protoVessel.vesselName + ", ID=" + protoVessel.vesselID + " doesn't exist !", //Lib.LogLevel.Warning);
				vd = new VesselData(protoVessel, null);
				vessels.Add(protoVessel.vesselID, vd);
			}
			return vd;
		}

		/// <summary>shortcut for VesselData.IsValid. False in the following cases : asteroid, debris, flag, deployed ground part, dead eva, rescue</summary>
		public static bool KerbalismIsValid(this Vessel vessel)
        {
            return KerbalismData(vessel).IsSimulated;
        }

		public static Dictionary<Guid, VesselData>.ValueCollection VesselDatas => vessels.Values;

        public static string To_safe_key(string key) { return key.Replace(" ", "___"); }
        public static string From_safe_key(string key) { return key.Replace("___", " "); }

        public static Version version;                         // savegame version
        public static int uid;                                 // savegame unique id
        private static Dictionary<Guid, VesselData> vessels = new Dictionary<Guid, VesselData>();    // store data per-vessel
    }


} // KERBALISMLITE




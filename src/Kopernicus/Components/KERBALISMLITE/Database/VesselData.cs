using System;
using System.Collections.Generic;


namespace KERBALISMLITE
{
	public class VesselData
	{
		// references
		public Guid VesselId { get; private set; }
		public Vessel Vessel { get; private set; }

		// validity
		/// <summary> True if the vessel exists in FlightGlobals. will be false in the editor</summary>
		public bool ExistsInFlight { get; private set; }
		public bool is_vessel;              // true if this is a valid vessel

		/// <summary>False in the following cases : asteroid, debris, flag, deployed ground part, dead eva, rescue</summary>
		public bool IsSimulated { get; private set; }

		// time since last update
		private double secSinceLastEval;


		#region non-evaluated non-persisted fields
		// there are probably a lot of candidates for this in the current codebase

		#endregion

		#region non-evaluated persisted fields

		// other persisted fields
		private List<ResourceUpdateDelegate> resourceUpdateDelegates = null; // all part modules that have a ResourceUpdate method
		private Dictionary<uint, PartData> parts; // all parts by flightID
		public Dictionary<uint, PartData>.ValueCollection PartDatas => parts.Values;
		public PartData GetPartData(uint flightID)
		{
			PartData pd;
			// in some cases (KIS added parts), we might try to get partdata before it is added by part-adding events
			// so we implement a fallback here
			if (!parts.TryGetValue(flightID, out pd))
			{
				foreach (Part p in Vessel.parts)
				{
					if (p.flightID == flightID)
					{
						pd = new PartData(p);
						parts.Add(flightID, pd);
					}
				}
			}
			return pd;
		}

		private Dictionary<string, SupplyData> supplies; // supplies data
		public List<uint> scansat_id; // used to remember scansat sensors that were disabled
		public double scienceTransmitted;
		#endregion

		#region evaluated environment properties
		// Things like vessel situation, sunlight, temperature, radiation, 

		/// <summary> [environment] true if inside ocean</summary>
		public bool EnvUnderwater => underwater; bool underwater;

		/// <summary> [environment] true if inside breathable atmosphere</summary>
		public bool EnvBreathable => breathable; bool breathable;

		/// <summary> [environment] true if on the surface of a body</summary>
		public bool EnvLanded => landed; bool landed;

		/// <summary> Is the vessel inside an atmosphere ?</summary>
		public bool EnvInAtmosphere => inAtmosphere; bool inAtmosphere;

		/// <summary> [environment] true if in zero g</summary>
		public bool EnvZeroG => zeroG; bool zeroG;

		/// <summary> [environment] solar flux reflected from the nearest body</summary>
		public double EnvAlbedoFlux => albedoFlux; double albedoFlux;

		/// <summary> [environment] infrared radiative flux from the nearest body</summary>
		public double EnvBodyFlux => bodyFlux; double bodyFlux;

		/// <summary> [environment] total flux at vessel position</summary>
		public double EnvTotalFlux => totalFlux; double totalFlux;

		/// <summary> [environment] temperature ar vessel position</summary>
		public double EnvTemperature => temperature; double temperature;

		/// <summary> [environment] true if vessel is inside thermosphere</summary>
		public bool EnvThermosphere => thermosphere; bool thermosphere;

		/// <summary> [environment] true if vessel is inside exosphere</summary>
		public bool EnvExosphere => exosphere; bool exosphere;

		/// <summary> [environment] Bodies whose apparent diameter from the vessel POV is greater than ~10 arcmin (~0.003 radians)</summary>
		// real apparent diameters at earth : sun/moon =~ 30 arcmin, Venus =~ 1 arcmin
		public List<CelestialBody> EnvVisibleBodies => visibleBodies; List<CelestialBody> visibleBodies;

		/// <summary> [environment] Sun that send the highest nominal solar flux (in W/m²) at vessel position</summary>
		public SunInfo EnvMainSun => mainSun; SunInfo mainSun;

		/// <summary> [environment] Angle of the main sun on the surface at vessel position</summary>
		public double EnvSunBodyAngle => sunBodyAngle; double sunBodyAngle;

		/// <summary>
		///  [environment] total solar flux from all stars at vessel position in W/m², include atmospheric absorption if inside an atmosphere (atmo_factor)
		/// <para/> zero when the vessel is in shadow while evaluation is non-analytic (low timewarp rates)
		/// <para/> in analytic evaluation, this include fractional sunlight factor
		/// </summary>
		public double EnvSolarFluxTotal => solarFluxTotal; double solarFluxTotal;

		/// <summary> similar to solar flux total but doesn't account for atmo absorbtion nor occlusion</summary>
		private double rawSolarFluxTotal;

		/// <summary> [environment] Average time spend in sunlight, including sunlight from all suns/stars. Each sun/star influence is pondered by its flux intensity</summary>
		public double EnvSunlightFactor => sunlightFactor; double sunlightFactor;

		/// <summary> [environment] true if the vessel is currently in sunlight, or at least half the time when in analytic mode</summary>
		public bool EnvInSunlight => sunlightFactor > 0.49;

		/// <summary> [environment] true if the vessel is currently in shadow, or least 90% of the time when in analytic mode</summary>
		// this threshold is also used to ignore light coming from distant/weak stars 
		public bool EnvInFullShadow => sunlightFactor < 0.1;

		/// <summary> [environment] List of all stars/suns and the related data/calculations for the current vessel</summary>
		public List<SunInfo> EnvSunsInfo => sunsInfo; List<SunInfo> sunsInfo;

		public class SunInfo
		{
			/// <summary> reference to the sun/star</summary>
			public Sim.SunData SunData => sunData; Sim.SunData sunData;

			/// <summary> normalized vector from vessel to sun</summary>
			public Vector3d Direction => direction; Vector3d direction;

			/// <summary> distance from vessel to sun surface</summary>
			public double Distance => distance; double distance;

			/// <summary>
			/// return 1.0 when the vessel is in direct sunlight, 0.0 when in shadow
			/// <para/> in analytic evaluation, this is a scalar of representing the fraction of time spent in sunlight
			/// </summary>
			// current limitations :
			// - the result is dependant on the vessel altitude at the time of evaluation, 
			//   consequently it gives inconsistent behavior with highly eccentric orbits
			// - this totally ignore the orbit inclinaison, polar orbits will be treated as equatorial orbits
			public double SunlightFactor => sunlightFactor; double sunlightFactor;

			/// <summary>
			/// solar flux at vessel position in W/m², include atmospheric absorption if inside an atmosphere (atmo_factor)
			/// <para/> zero when the vessel is in shadow while evaluation is non-analytic (low timewarp rates)
			/// <para/> in analytic evaluation, this include fractional sunlight / atmo absorbtion
			/// </summary>
			public double SolarFlux => solarFlux; double solarFlux;

			/// <summary>
			/// scalar for solar flux absorbtion by atmosphere at vessel position, not meant to be used directly (use solar_flux instead)
			/// <para/> if integrated over orbit (analytic evaluation), average atmospheric absorption factor over the daylight period (not the whole day)
			/// </summary>
			public double AtmoFactor => atmoFactor; double atmoFactor;

			/// <summary> proportion of this sun flux in the total flux at the vessel position (ignoring atmoshere and occlusion) </summary>
			public double FluxProportion => fluxProportion; double fluxProportion;

			/// <summary> similar to solar flux but doesn't account for atmo absorbtion nor occlusion</summary>
			private double rawSolarFlux;

			public SunInfo(Sim.SunData sunData)
			{
				this.sunData = sunData;
			}

			/// <summary>
			/// Update the 'sunsInfo' list and the 'mainSun', 'solarFluxTotal' variables.
			/// Uses discrete or analytic (for high timewarp speeds) evaluation methods based on the isAnalytic bool.
			/// Require the 'visibleBodies' variable to be set.
			/// </summary>
			// at the two highest timewarp speed, the number of sun visibility samples drop to the point that
			// the quantization error first became noticeable, and then exceed 100%, to solve this:
			// - we switch to an analytical estimation of the sunlight/shadow period
			// - atmo_factor become an average atmospheric absorption factor over the daylight period (not the whole day)
			public static void UpdateSunsInfo(VesselData vd, Vector3d vesselPosition, double elapsedSeconds)
			{
				Vessel v = vd.Vessel;
				double lastSolarFlux = 0.0;

				vd.sunsInfo = new List<SunInfo>(Sim.suns.Count);
				vd.solarFluxTotal = 0.0;
				vd.rawSolarFluxTotal = 0.0;

				foreach (Sim.SunData sunData in Sim.suns)
				{
					SunInfo sunInfo = new SunInfo(sunData);

					// determine if in sunlight, calculate sun direction and distance
					sunInfo.sunlightFactor = Sim.IsBodyVisible(v, vesselPosition, sunData.body, vd.visibleBodies, out sunInfo.direction, out sunInfo.distance) ? 1.0 : 0.0;
					// get atmospheric absorbtion
					sunInfo.atmoFactor = Sim.AtmosphereFactor(v.mainBody, vesselPosition, sunInfo.direction);

					// get resulting solar flux in W/m²
					sunInfo.rawSolarFlux = sunInfo.sunData.SolarFlux(sunInfo.distance);
					sunInfo.solarFlux = sunInfo.rawSolarFlux * sunInfo.sunlightFactor * sunInfo.atmoFactor;
					// increment total flux from all stars
					vd.rawSolarFluxTotal += sunInfo.rawSolarFlux;
					vd.solarFluxTotal += sunInfo.solarFlux;
					// add the star to the list
					vd.sunsInfo.Add(sunInfo);
					// the most powerful star will be our "default" sun. Uses raw flux before atmo / sunlight factor
					if (sunInfo.rawSolarFlux > lastSolarFlux)
					{
						lastSolarFlux = sunInfo.rawSolarFlux;
						vd.mainSun = sunInfo;
					}
				}

				vd.sunlightFactor = 0.0;
				foreach (SunInfo sunInfo in vd.sunsInfo)
				{
					sunInfo.fluxProportion = sunInfo.rawSolarFlux / vd.rawSolarFluxTotal;
					vd.sunlightFactor += sunInfo.SunlightFactor * sunInfo.fluxProportion;
				}
				// avoid rounding errors
				if (vd.sunlightFactor > 0.99) vd.sunlightFactor = 1.0;
			}
		}
		#endregion

		#region evaluated vessel state information properties

		/// <summary>true if vessel is powered</summary>
		public bool Powered => powered; bool powered;

		/// <summary>evaluated on loaded vessels based on the data pushed by SolarPanelFixer. This doesn't change for unloaded vessel, so the value is persisted</summary>
		public double SolarPanelsAverageExposure => solarPanelsAverageExposure; double solarPanelsAverageExposure = -1.0;
		private List<double> solarPanelsExposure = new List<double>(); // values are added by SolarPanelFixer, then cleared by VesselData once solarPanelsAverageExposure has been computed
		public void SaveSolarPanelExposure(double exposure) => solarPanelsExposure.Add(exposure); // meant to be called by SolarPanelFixer

		#endregion

		#region core update handling

		/// <summary> Garanteed to be called for every VesselData in DB before any other method (FixedUpdate/Evaluate) is called </summary>
		public void EarlyUpdate()
		{
			ExistsInFlight = false;
		}

		/// <summary>Called every FixedUpdate for all existing flightglobal vessels </summary>
		public void Update(Vessel v)
		{
			bool isInit = Vessel == null; // debug

			Vessel = v;
			ExistsInFlight = true;

			if (!ExistsInFlight || !CheckIfSimulated())
			{
				IsSimulated = false;
			}
			else
			{
				// if vessel wasn't simulated previously : update everything immediately.
				if (!IsSimulated)
				{
					IsSimulated = true;
					Evaluate(true, Lib.RandomDouble());
				}
			}
		}

		private bool CheckIfSimulated()
		{
			// determine if this is a valid vessel
			is_vessel = Lib.IsVessel(Vessel);

			return is_vessel;
		}

		/// <summary>
		/// Evaluate Status and Conditions. Called from Kerbalism.FixedUpdate :
		/// <para/> - for loaded vessels : every gametime second 
		/// <para/> - for unloaded vessels : at the beginning of every background update
		/// </summary>
		public void Evaluate(bool forced, double elapsedSeconds)
		{
			if (!IsSimulated) return;

			secSinceLastEval += elapsedSeconds;

			// don't update more than every second of game time
			if (!forced && secSinceLastEval < 1.0)
				return;

			EvaluateEnvironment(secSinceLastEval);
			EvaluateStatus();
			secSinceLastEval = 0.0;
		}

		/// <summary>
		/// Call ResourceUpdate on all part modules that have that method
		/// </summary>
		public void ResourceUpdate(VesselResources resources, double elapsed_s)
		{
			// only do this for loaded vessels. unloaded vessels will be handled in Background.cs
			if (!Vessel.loaded) return;

			if(resourceUpdateDelegates == null)
			{
				resourceUpdateDelegates = new List<ResourceUpdateDelegate>();
				foreach(var part in Vessel.parts)
				{
					foreach(var module in part.Modules)
					{
						if (!module.isEnabled) continue;
						var resourceUpdateDelegate = ResourceUpdateDelegate.Instance(module);
						if (resourceUpdateDelegate != null) resourceUpdateDelegates.Add(resourceUpdateDelegate);
					}
				}
			}

			if (resourceUpdateDelegates.Count == 0) return;

			List<ResourceInfo> allResources = resources.GetAllResources(Vessel); // there might be some performance to be gained by caching the list of all resource

			Dictionary<string, double> availableResources = new Dictionary<string, double>();
			foreach (var ri in allResources)
				availableResources[ri.ResourceName] = ri.Amount;
			List<KeyValuePair<string, double>> resourceChangeRequests = new List<KeyValuePair<string, double>>();

			foreach(var resourceUpdateDelegate in resourceUpdateDelegates)
			{
				resourceChangeRequests.Clear();
				string title = resourceUpdateDelegate.invoke(availableResources, resourceChangeRequests);
				ResourceBroker broker = ResourceBroker.GetOrCreate(title);
				foreach (var rc in resourceChangeRequests)
				{
					if (rc.Value > 0) resources.Produce(Vessel, rc.Key, rc.Value * elapsed_s, broker);
					if (rc.Value < 0) resources.Consume(Vessel, rc.Key, -rc.Value * elapsed_s, broker);
				}
			}
		}

		#endregion

		#region events handling

		public void UpdateOnVesselModified()
		{
			if (!IsSimulated)
				return;

			resourceUpdateDelegates = null;
			EvaluateStatus();
		}

		/// <summary> Called by GameEvents.onVesselsUndocking, just after 2 vessels have undocked </summary>
		internal static void OnDecoupleOrUndock(Vessel oldVessel, Vessel newVessel)
		{
			//Lib.LogDebug("Decoupling vessel '{0}' from vessel '{1}'", Lib.LogLevel.Message, newVessel.vesselName, oldVessel.vesselName);

			VesselData oldVD = oldVessel.KerbalismData();
			VesselData newVD = newVessel.KerbalismData();

			// remove all partdata on the new vessel
			newVD.parts.Clear();

			foreach (Part part in newVessel.Parts)
			{
				PartData pd;
				// for all parts in the new vessel, move the corresponding partdata from the old vessel to the new vessel
				if (oldVD.parts.TryGetValue(part.flightID, out pd))
				{
					newVD.parts.Add(part.flightID, pd);
					oldVD.parts.Remove(part.flightID);
				}
			}

			newVD.UpdateOnVesselModified();
			oldVD.UpdateOnVesselModified();

			//Lib.LogDebug("Decoupling complete for new vessel, vd.partcount={1}, v.partcount={2} ({0})", Lib.LogLevel.Message, newVessel.vesselName, newVD.parts.Count, newVessel.parts.Count);
			//Lib.LogDebug("Decoupling complete for old vessel, vd.partcount={1}, v.partcount={2} ({0})", Lib.LogLevel.Message, oldVessel.vesselName, oldVD.parts.Count, oldVessel.parts.Count);
		}

		// This is for mods (KIS), won't be used in a stock game (the docking is handled in the OnDock method
		internal static void OnPartCouple(GameEvents.FromToAction<Part, Part> data)
		{
			//Lib.LogDebug("Coupling part '{0}' from vessel '{1}' to vessel '{2}'", Lib.LogLevel.Message, data.from.partInfo.title, data.from.vessel.vesselName, data.to.vessel.vesselName);

			Vessel fromVessel = data.from.vessel;
			Vessel toVessel = data.to.vessel;

			VesselData fromVD = fromVessel.KerbalismData();
			VesselData toVD = toVessel.KerbalismData();

			// GameEvents.onPartCouple may be fired by mods (KIS) that add new parts to an existing vessel
			// In the case of KIS, the part vessel is already set to the destination vessel when the event is fired
			// so we just add the part.
			if (fromVD == toVD)
			{
				if (!toVD.parts.ContainsKey(data.from.flightID))
				{
					toVD.parts.Add(data.from.flightID, new PartData(data.from));
					//Lib.LogDebug("VesselData : newly created part '{0}' added to vessel '{1}'", Lib.LogLevel.Message, data.from.partInfo.title, data.to.vessel.vesselName);
				}
				return;
			}

			// add all partdata of the docking vessel to the docked to vessel
			foreach (PartData partData in fromVD.parts.Values)
			{
				toVD.parts.Add(partData.FlightId, partData);
			}
			// remove all partdata from the docking vessel
			fromVD.parts.Clear();

			// reset a few things on the docked to vessel
			toVD.supplies.Clear();
			toVD.scansat_id.Clear();
			toVD.UpdateOnVesselModified();

			//Lib.LogDebug("Coupling complete to   vessel, vd.partcount={1}, v.partcount={2} ({0})", Lib.LogLevel.Message, toVessel.vesselName, toVD.parts.Count, toVessel.parts.Count);
			//Lib.LogDebug("Coupling complete from vessel, vd.partcount={1}, v.partcount={2} ({0})", Lib.LogLevel.Message, fromVessel.vesselName, fromVD.parts.Count, fromVessel.parts.Count);
		}

		internal static void OnPartWillDie(Part part)
		{
			VesselData vd = part.vessel.KerbalismData();
			vd.parts.Remove(part.flightID);
			vd.UpdateOnVesselModified();
			//Lib.LogDebug("Removing dead part, vd.partcount={0}, v.partcount={1} (part '{2}' in vessel '{3}')", Lib.LogLevel.Message, vd.parts.Count, part.vessel.parts.Count, part.partInfo.title, part.vessel.vesselName);
		}

		#endregion

		#region ctor / init / persistence

		/// <summary> This ctor is to be used for newly created vessels </summary>
		public VesselData(Vessel vessel)
		{
			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.VesselData.Ctor");

			ExistsInFlight = true;	// vessel exists
			IsSimulated = false;	// will be evaluated in next fixedupdate

			Vessel = vessel;
			VesselId = Vessel.id;
			
			parts = new Dictionary<uint, PartData>();
			if (Vessel.loaded)
				foreach (Part part in Vessel.Parts)
					parts.Add(part.flightID, new PartData(part));
			else
				// vessels can be created unloaded, asteroids for example
				foreach (ProtoPartSnapshot protopart in Vessel.protoVessel.protoPartSnapshots)
					parts.Add(protopart.flightID, new PartData(protopart));


			FieldsDefaultInit(vessel.protoVessel);

			//Lib.LogDebug("VesselData ctor (new vessel) : id '" + VesselId + "' (" + Vessel.vesselName + "), part count : " + parts.Count);
			UnityEngine.Profiling.Profiler.EndSample();
		}

		/// <summary>
		/// This ctor is meant to be used in OnLoad only, but can be used as a fallback
		/// with a null ConfigNode to create VesselData from a protovessel. 
		/// The Vessel reference will be acquired in the next fixedupdate
		/// </summary>
		public VesselData(ProtoVessel protoVessel, ConfigNode node)
		{
			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.VesselData.Ctor");
			ExistsInFlight = false;
			IsSimulated = false;

			VesselId = protoVessel.vesselID;

			parts = new Dictionary<uint, PartData>();
			foreach (ProtoPartSnapshot protopart in protoVessel.protoPartSnapshots)
				parts.Add(protopart.flightID, new PartData(protopart));

			if (node == null)
			{
				FieldsDefaultInit(protoVessel);
				//Lib.LogDebug("VesselData ctor (created from protovessel) : id '" + VesselId + "' (" + protoVessel.vesselName + "), part count : " + parts.Count);
			}
			else
			{
				Load(node);
				//Lib.LogDebug("VesselData ctor (loaded from database) : id '" + VesselId + "' (" + protoVessel.vesselName + "), part count : " + parts.Count);
			}
			UnityEngine.Profiling.Profiler.EndSample();
		}

		private void FieldsDefaultInit(ProtoVessel pv)
		{
			supplies = new Dictionary<string, SupplyData>();
			scansat_id = new List<uint>();
		}

		private void Load(ConfigNode node)
		{

			solarPanelsAverageExposure = Lib.ConfigValue(node, "solarPanelsAverageExposure", -1.0);
			scienceTransmitted = Lib.ConfigValue(node, "scienceTransmitted", 0.0);

			supplies = new Dictionary<string, SupplyData>();
			foreach (var supply_node in node.GetNode("supplies").GetNodes())
			{
				supplies.Add(DB.From_safe_key(supply_node.name), new SupplyData(supply_node));
			}

			scansat_id = new List<uint>();
			foreach (string s in node.GetValues("scansat_id"))
			{
				scansat_id.Add(Lib.Parse.ToUInt(s));
			}

			ConfigNode partsNode = new ConfigNode();
		}

		public void Save(ConfigNode node)
		{


			node.AddValue("solarPanelsAverageExposure", solarPanelsAverageExposure);
			node.AddValue("scienceTransmitted", scienceTransmitted);

			var supplies_node = node.AddNode("supplies");
			foreach (var p in supplies)
			{
				p.Value.Save(supplies_node.AddNode(DB.To_safe_key(p.Key)));
			}

			foreach (uint id in scansat_id)
			{
				node.AddValue("scansat_id", id.ToString());
			}

			ConfigNode partsNode = node.AddNode("parts");

		}

		#endregion

		public SupplyData Supply(string name)
		{
			if (!supplies.ContainsKey(name))
			{
				supplies.Add(name, new SupplyData());
			}
			return supplies[name];
		}

		#region vessel state evaluation
		private void EvaluateStatus()
		{
			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.VesselData.EvaluateStatus");
			// determine if there is enough EC for a powered state
			powered = Lib.IsPowered(Vessel);

			// solar panels data
			if (Vessel.loaded)
			{
				solarPanelsAverageExposure = KopernicusSolarPanels.GetSolarPanelsAverageExposure(solarPanelsExposure);
				solarPanelsExposure.Clear();
			}
			UnityEngine.Profiling.Profiler.EndSample();
		}
		#endregion

		#region environment evaluation
		private void EvaluateEnvironment(double elapsedSeconds)
		{
			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.VesselData.EvaluateStatus");

			// get vessel position
			Vector3d position = Lib.VesselPosition(Vessel);

			// this should never happen again
			if (Vector3d.Distance(position, Vessel.mainBody.position) < 1.0)
			{
				throw new Exception("Shit hit the fan for vessel " + Vessel.vesselName);
			}

			// situation
			underwater = Sim.Underwater(Vessel);
			breathable = Sim.Breathable(Vessel, EnvUnderwater);
			landed = Lib.Landed(Vessel);
			
			inAtmosphere = Vessel.mainBody.atmosphere && Vessel.altitude < Vessel.mainBody.atmosphereDepth;
			zeroG = !EnvLanded && !inAtmosphere;

			visibleBodies = Sim.GetLargeBodies(position);

			// get solar info (with multiple stars / Kopernicus support)
			// get the 'visibleBodies' and 'sunsInfo' lists, the 'mainSun', 'solarFluxTotal' variables.
			// require the situation variables to be evaluated first
			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.VesselData.Sunlight");
			SunInfo.UpdateSunsInfo(this, position, elapsedSeconds);
			UnityEngine.Profiling.Profiler.EndSample();
			sunBodyAngle = Sim.SunBodyAngle(Vessel, position, mainSun.SunData.body);

			// temperature at vessel position
			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.VesselData.Temperature");
			temperature = Sim.Temperature(Vessel, position, solarFluxTotal, out albedoFlux, out bodyFlux, out totalFlux);
			UnityEngine.Profiling.Profiler.EndSample();

			thermosphere = Sim.InsideThermosphere(Vessel);
			exosphere = Sim.InsideExosphere(Vessel);;

			// other stuff
			UnityEngine.Profiling.Profiler.EndSample();
		}

		#endregion
	}
} // KERBALISMLITE

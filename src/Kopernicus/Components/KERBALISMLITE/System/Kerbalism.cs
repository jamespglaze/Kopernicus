using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using KSP.UI.Screens;
using KSP.Localization;

namespace KERBALISMLITE
{
	/// <summary>
	/// Main initialization class : for everything that isn't save-game dependant.
	/// For save-dependant things, or things that require the game to be loaded do it in Kerbalism.OnLoad()
	/// </summary>
	[KSPAddon(KSPAddon.Startup.MainMenu, false)]
	public class KerbalismCoreSystems : MonoBehaviour
	{
		public void Start()
		{
			// reset the save game initialized flag
			Kerbalism.IsSaveGameInitDone = false;

			// things in here will be only called once per KSP launch, after loading
			// nearly everything is available at this point, including the Kopernicus patched bodies.
			if (!Kerbalism.IsCoreMainMenuInitDone)
			{
				Kerbalism.IsCoreMainMenuInitDone = true;
			}
		}
	}

	[KSPScenario(ScenarioCreationOptions.AddToAllGames, new[] { GameScenes.SPACECENTER, GameScenes.TRACKSTATION, GameScenes.FLIGHT, GameScenes.EDITOR })]
	public sealed class Kerbalism : ScenarioModule
	{
		#region declarations

		/// <summary> global access </summary>
		public static Kerbalism Fetch { get; private set; } = null;

		/// <summary> Is the one-time main menu init done. Becomes true after loading, when the the main menu is shown, and never becomes false again</summary>
		public static bool IsCoreMainMenuInitDone { get; set; } = false;

		/// <summary> Is the one-time on game load init done. Becomes true after the first OnLoad() of a game, and never becomes false again</summary>
		public static bool IsCoreGameInitDone { get; set; } = false;

		/// <summary> Is the savegame (or new game) first load done. Becomes true after the first OnLoad(), and false when returning to the main menu</summary>
		public static bool IsSaveGameInitDone { get; set; } = false;

		// equivalent to TimeWarp.fixedDeltaTime
		// note: stored here to avoid converting it to double every time
		public static double elapsed_s;

		// number of steps from last warp blending
		private static uint warp_blending;

		/// <summary>Are we in an intermediary timewarp speed ?</summary>
		public static bool WarpBlending => warp_blending > 2u;

		// last savegame unique id
		static int savegame_uid;

		/// <summary> real time of last game loaded event </summary>
		public static float gameLoadTime = 0.0f;

		public static bool SerenityEnabled { get; private set; }

		#endregion

		#region initialization & save/load

		//  constructor
		public Kerbalism()
		{
			// enable global access
			Fetch = this;

			SerenityEnabled = Expansions.ExpansionsLoader.IsExpansionInstalled("Serenity");
		}

		private void OnDestroy()
		{
			Fetch = null;
		}

		public override void OnLoad(ConfigNode node)
		{
			// everything in there will be called only one time : the first time a game is loaded from the main menu
			if (!IsCoreGameInitDone)
			{
				// core game systems
				Sim.Init();         // find suns (Kopernicus support)

				IsCoreGameInitDone = true;
			}

			// everything in there will be called every time a savegame (or a new game) is loaded from the main menu
			if (!IsSaveGameInitDone)
			{
				Cache.Init();
				ResourceCache.Init();

				IsSaveGameInitDone = true;
				;
			}

			// eveything else will be called on every OnLoad() call :
			// - save/load
			// - every scene change
			// - in various semi-random situations (thanks KSP)

			// always clear the caches
			Cache.Clear();
			ResourceCache.Clear();

			// deserialize our database
			try
			{
				UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.DB.Load");
				DB.Load(node);
				UnityEngine.Profiling.Profiler.EndSample();
			}
			catch (Exception e)
			{
				string fatalError = "FATAL ERROR : Kerbalism save game load has failed :" + "\n" + e.ToString();
				LoadFailedPopup(fatalError);
			}

			// detect if this is a different savegame
			if (DB.uid != savegame_uid)
			{

				// remember savegame id
				savegame_uid = DB.uid;
			}

			Kerbalism.gameLoadTime = Time.time;
		}

		public override void OnSave(ConfigNode node)
		{
			if (!enabled) return;

			// serialize data
			UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.DB.Save");
			DB.Save(node);
			UnityEngine.Profiling.Profiler.EndSample();
		}

		private void LoadFailedPopup(string error)
		{
			string popupMsg = "Kerbalism has encountered an unrecoverable error and KSP must be closed\n\n";
			popupMsg += "Report it at <b>kerbalism.github.io</b>, in the <b>kerbalism discord</b> or at the KSP forums thread\n\n";
			popupMsg += "Please provide a screenshot of this message, and your ksp.log file found in your KSP install folder\n\n";
			popupMsg += error;
		}

		#endregion

		#region fixedupdate

		void FixedUpdate()
		{
			// remove control locks in any case
			Misc.ClearLocks();

			// do nothing if paused
			if (Lib.IsPaused())
				return;

			// convert elapsed time to double only once
			double fixedDeltaTime = TimeWarp.fixedDeltaTime;

			// and detect warp blending
			if (Math.Abs(fixedDeltaTime - elapsed_s) < 0.001)
				warp_blending = 0;
			else
				++warp_blending;

			// update elapsed time
			elapsed_s = fixedDeltaTime;

			// store info for oldest unloaded vessel
			double last_time = 0.0;
			Guid last_id = Guid.Empty;
			Vessel last_v = null;
			VesselData last_vd = null;
			VesselResources last_resources = null;

			foreach (VesselData vd in DB.VesselDatas)
			{
				vd.EarlyUpdate();
			}

			// for each vessel
			foreach (Vessel v in FlightGlobals.Vessels)
			{
				// get vessel data
				VesselData vd = v.KerbalismData();

				// update the vessel data validity
				vd.Update(v);

				// do nothing else for invalid vessels
				if (!vd.IsSimulated)
					continue;

				// get resource cache
				VesselResources resources = ResourceCache.Get(v);

				// if loaded
				if (v.loaded)
				{
					//UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.FixedUpdate.Loaded.VesselDataEval");
					// update the vessel info
					vd.Evaluate(false, elapsed_s);
					//UnityEngine.Profiling.Profiler.EndSample();

					// get most used resource
					ResourceInfo ec = resources.GetResource(v, "ElectricCharge");

					UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.FixedUpdate.Loaded.Profile");
					UnityEngine.Profiling.Profiler.EndSample();

					UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.FixedUpdate.Loaded.Profile");
					// part module resource updates
					vd.ResourceUpdate(resources, elapsed_s);
					UnityEngine.Profiling.Profiler.EndSample();

					UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.FixedUpdate.Loaded.Resource");
					// apply deferred requests
					resources.Sync(v, vd, elapsed_s);
					UnityEngine.Profiling.Profiler.EndSample();
				}
			}

			// at most one vessel gets background processing per physics tick :
			// if there is a vessel that is not the currently loaded vessel, then
			// we will update the vessel whose most recent background update is the oldest
			if (last_v != null)
			{
				//UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.FixedUpdate.Unloaded.VesselDataEval");
				// update the vessel info (high timewarp speeds reevaluation)
				last_vd.Evaluate(false, last_time);
				//UnityEngine.Profiling.Profiler.EndSample();

				// get most used resource
				ResourceInfo last_ec = last_resources.GetResource(last_v, "ElectricCharge");

				UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.FixedUpdate.Unloaded.Background");
				// simulate modules in background
				Background.Update(last_v, last_vd, last_resources, last_time);
				UnityEngine.Profiling.Profiler.EndSample();

				UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.FixedUpdate.Unloaded.Resource");
				// apply deferred requests
				last_resources.Sync(last_v, last_vd, last_time);
				UnityEngine.Profiling.Profiler.EndSample();
			}
		}

		#endregion
	}

	public sealed class MapCameraScript : MonoBehaviour
	{
		void OnPostRender()
		{
			// do nothing when not in map view
			// - avoid weird situation when in some user installation MapIsEnabled is true in the space center
			if (!MapView.MapIsEnabled || HighLogic.LoadedScene == GameScenes.SPACECENTER)
				return;
		}
	}

	// misc functions
	public static class Misc
	{
		public static void ClearLocks()
		{
			// remove control locks
			InputLockManager.RemoveControlLock("eva_dead_lock");
			InputLockManager.RemoveControlLock("no_signal_lock");
		}

		public static void TechDescriptions()
		{
			var rnd = RDController.Instance;
			if (rnd == null)
				return;
			var selected = RDController.Instance.node_selected;
			if (selected == null)
				return;
			var techID = selected.tech.techID;
			if (rnd.node_description.text.IndexOf("<i></i>\n", StringComparison.Ordinal) == -1) //< check for state in the string
			{
				rnd.node_description.text += "<i></i>\n"; //< store state in the string

				// collect unique configure-related unlocks
				HashSet<string> labels = new HashSet<string>();
				foreach (AvailablePart p in PartLoader.LoadedPartsList)
				{
					// workaround for FindModulesImplementing nullrefs in 1.8 when called on the strange kerbalEVA_RD_Exp prefab
					// due to the (private) cachedModuleLists being null on it
					if (p.partPrefab.Modules.Count == 0)
						continue;
				}

				// add unique configure-related unlocks
				// avoid printing text over the "available parts" section
				int i = 0;
				foreach (string label in labels)
				{
					rnd.node_description.text += Lib.BuildString("\n• <color=#00ffff>", label, "</color>");
					i++;
					if (i >= 5 && labels.Count > i + 1)
					{
						rnd.node_description.text += Lib.BuildString("\n• <color=#00ffff>(+", (labels.Count - i).ToString(), " more)</color>");
						break;
					}
				}
			}
		}

		public static void PartPrefabsTweaks()
		{
			List<string> partSequence = new List<string>();

			partSequence.Add("kerbalism-container-inline-prosemian-full-0625");
			partSequence.Add("kerbalism-container-inline-prosemian-full-125");
			partSequence.Add("kerbalism-container-inline-prosemian-full-250");
			partSequence.Add("kerbalism-container-inline-prosemian-full-375");

			partSequence.Add("kerbalism-container-inline-prosemian-half-125");
			partSequence.Add("kerbalism-container-inline-prosemian-half-250");
			partSequence.Add("kerbalism-container-inline-prosemian-half-375");

			partSequence.Add("kerbalism-container-radial-box-prosemian-small");
			partSequence.Add("kerbalism-container-radial-box-prosemian-normal");
			partSequence.Add("kerbalism-container-radial-box-prosemian-large");

			partSequence.Add("kerbalism-container-radial-pressurized-prosemian-small");
			partSequence.Add("kerbalism-container-radial-pressurized-prosemian-medium");
			partSequence.Add("kerbalism-container-radial-pressurized-prosemian-big");
			partSequence.Add("kerbalism-container-radial-pressurized-prosemian-huge");

			partSequence.Add("kerbalism-solenoid-short-small");
			partSequence.Add("kerbalism-solenoid-long-small");
			partSequence.Add("kerbalism-solenoid-short-large");
			partSequence.Add("kerbalism-solenoid-long-large");

			partSequence.Add("kerbalism-greenhouse");
			partSequence.Add("kerbalism-gravityring");
			partSequence.Add("kerbalism-activeshield");
			partSequence.Add("kerbalism-chemicalplant");


			Dictionary<string, float> iconScales = new Dictionary<string, float>();

			iconScales["kerbalism-container-inline-prosemian-full-0625"] = 0.6f;
			iconScales["kerbalism-container-radial-pressurized-prosemian-small"] = 0.6f;
			iconScales["kerbalism-container-radial-box-prosemian-small"] = 0.6f;

			iconScales["kerbalism-container-inline-prosemian-full-125"] = 0.85f;
			iconScales["kerbalism-container-inline-prosemian-half-125"] = 0.85f;
			iconScales["kerbalism-container-radial-pressurized-prosemian-medium"] = 0.85f;
			iconScales["kerbalism-container-radial-box-prosemian-normal"] = 0.85f;
			iconScales["kerbalism-solenoid-short-small"] = 0.85f;
			iconScales["kerbalism-solenoid-long-small"] = 0.85f;

			iconScales["kerbalism-container-inline-prosemian-full-250"] = 1.1f;
			iconScales["kerbalism-container-inline-prosemian-half-250"] = 1.1f;
			iconScales["kerbalism-container-radial-pressurized-prosemian-big"] = 1.1f;
			iconScales["kerbalism-container-radial-box-prosemian-large"] = 1.1f;

			iconScales["kerbalism-container-inline-prosemian-full-375"] = 1.33f;
			iconScales["kerbalism-container-inline-prosemian-half-375"] = 1.33f;
			iconScales["kerbalism-container-radial-pressurized-prosemian-huge"] = 1.33f;
			iconScales["kerbalism-solenoid-short-large"] = 1.33f;
			iconScales["kerbalism-solenoid-long-large"] = 1.33f;


			foreach (AvailablePart ap in PartLoader.LoadedPartsList)
			{
				// scale part icons of the radial container variants
				if (iconScales.ContainsKey(ap.name))
				{
					float scale = iconScales[ap.name];
					ap.iconPrefab.transform.GetChild(0).localScale *= scale;
					ap.iconScale *= scale;
				}

				// force a non-lexical order in the editor
				if (partSequence.Contains(ap.name))
				{
					int index = partSequence.IndexOf(ap.name);
					ap.title = Lib.BuildString("<size=1><color=#00000000>" + index.ToString("00") + "</color></size>", ap.title);
				}

				// recompile some part infos (this is normally done by KSP on loading, after each part prefab is compiled)
				// This is needed because :
				// - We can't check interdependent modules when OnLoad() is called, since the other modules may not be loaded yet
				// - The science DB needs the system/bodies to be instantiated, which is done after the part compilation
				bool partNeedsInfoRecompile = false;

				// for some reason this crashes on the EVA kerbals parts
				if (partNeedsInfoRecompile && !ap.name.StartsWith("kerbalEVA"))
				{
					ap.moduleInfos.Clear();
					ap.resourceInfos.Clear();
					Lib.ReflectionCall(PartLoader.Instance, "CompilePartInfo", new Type[] { typeof(AvailablePart), typeof(Part) }, new object[] { ap, ap.partPrefab });
				}
			}
		}
	}
}
// KERBALISMLITE

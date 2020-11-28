using System.Collections.Generic;

namespace KERBALISMLITE
{
	public abstract class DeviceEC
	{
		public KeyValuePair<bool, double> GetConsume()
		{
			return new KeyValuePair<bool, double>(IsConsuming, actualCost);
		}

		protected abstract bool IsConsuming { get; }

		public abstract void GUI_Update(bool isEnabled);

		public abstract void FixModule(bool isEnabled);

		public void ToggleActions(PartModule partModule, bool value)
		{
			foreach (BaseAction ac in partModule.Actions)
			{
				ac.active = value;
			}
		}

		// Return
		public double actualCost;
		public double extra_Cost;
		public double extra_Deploy;
	}
}

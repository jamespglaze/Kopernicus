Kopernicus Bleeding Edge
==============================
July 7th, 2022
* Created by: BryceSchroeder and Teknoman117 (aka. Nathaniel R. Lewis)
* Maintained by: Thomas P., NathanKell and KillAshley
* Further maintained by Prestja, R-T-B
* Additional Content by: Gravitasi, aftokino, KCreator, Padishar, Kragrathea, OvenProofMars, zengei, MrHappyFace, Sigma88, Majiir (CompatibilityChecker)
* Much thanks to Sarbian for ModuleManager and ModularFlightIntegrator
* Corrected thermodynamics and effects by yours truly, LudicrousFun.

The Bleeding Edge is where beta code is trialed by fire, so if you want to help: Welcome!  If you want a stable experience, please use the stable brance at https://github.com/Kopernicus/Kopernicus instead.

New in this latest version UBE-85:

1.) Rewrote large parts of the scatter system to use a new clever caching logic developed in conjunction with @gotmachine.  Major thanks to him, as this realizes major performance benefits!

2.) Kopernicus_Config.cfg parameters ScatterDistanceLimit and ScatterCountLimit are removed, and will be scrubbed from your cfgs automatically.  This level of tweaking is no longer needed or required with the new high performance scatter system.

3.) We now depend on KSPHarmony framework to do a few small things that would be messy to do via reflection.  Please ensure you extract your release zip fully, it includes everything you need.

Known Bugs:

1.) Not exactly a bug, but worth mentioning: The Kopernicus_Config.cfg file is rewritten when the game exits. This means any edits made while playing the game will not be preserved. Edit the file only with the game exited, please.

2.) At interstellar ranges, heat can sometimes behave strangely, sometimes related to map zoom (be careful zooming out). It is best to turn off part heating when traveling far far away.

3.) When zooming out all the way out in map view at interstellar ranges, the navball furthermore sometimes behaves oddly. We are working on this and monitoring all the interstellar bugs actively.

4.) Very Old craft files may complain about a missing module. This is a cosmetic error and can be ignored. Reload and re-save the craft to remove the error.

Known Caveats:

1.) The 1.12.x release series works on 1.12.x,1.11.x,1.10.x, and 1.9.x. The 1.8 release is for 1.8.x.

2.) Multistar Solar panel support requires an additional config file, attached to release.

3.) As of release-107, scatter density underwent a bugfix on all bodies globally that results in densities acting more dense than before on some select configs.  Some mods may need to adjust.  Normally we'd not change things like this, but this is technically the correct stock behavior of the node so...  if you need the old behavior, see config option UseIncorrectScatterDensityLogic.

4.) As of Release-119, LandControl createColors is no longer obeyed, it is forced on to avoid another bug.  Very few mods to my knowledge use this parameter, but a few do (JNSQ for example).  You can work around this if affected by setting your LandControl color to be all zeroes. See attatched cfg for a mod that does this.

5.) The "collider fix" as it's called, which fixes the event in which you sink into the terrain on distant bodies, is off by default.  If you have a system larger than stock, please see Kopernicus_Config.cfg option DisableFarAwayColliders, read about the fix/workaround, and set it as you feel appropriate.

Building
----------
To build Kopernicus from source, **don't edit the project file**.

Instead, define a **Reference Path** pointing to the **root** of your local KSP install.

In Visual Studio and Rider, this can be done within the IDE UI, by going to the project properties window and then in the `Reference Path` tab.
If you want to set it manually, create a `Kopernicus.csproj.user` file next to the `src\Kopernicus\Kopernicus.csproj` file with the following content :
```xml
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="Current" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <PropertyGroup>
    <ReferencePath>Absolute\Path\To\Your\KSP\Install\Folder\Root\</ReferencePath>
  </PropertyGroup>
</Project>
```

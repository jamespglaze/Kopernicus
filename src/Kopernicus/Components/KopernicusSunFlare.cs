/**
 * Kopernicus Planetary System Modifier
 * ------------------------------------------------------------- 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public
 * License along with this library; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston,
 * MA 02110-1301  USA
 * 
 * This library is intended to be used as a plugin for Kerbal Space Program
 * which is copyright of TakeTwo Interactive. Your usage of Kerbal Space Program
 * itself is governed by the terms of its EULA, not the license above.
 * 
 * https://kerbalspaceprogram.com
 */

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;

namespace Kopernicus.Components
{
    /// <summary>
    /// Modifications for the SunFlare component
    /// </summary>
    public class KopernicusSunFlare : SunFlare
    {
        protected override void Awake()
        {
            Camera.onPreCull += PreCull;
        }

        [SuppressMessage("ReSharper", "Unity.IncorrectMethodSignature")]
        private void PreCull(Camera camera)
        {
            Vector3d scaledSpace = target.transform.position - ScaledSpace.LocalToScaledSpace(sun.position);
            sunDirection = scaledSpace.normalized;
            if (sunDirection != Vector3d.zero)
            {
                transform.forward = sunDirection;
            }
        }

        [SuppressMessage("ReSharper", "DelegateSubtraction")]
        protected override void OnDestroy()
        {
            Camera.onPreCull -= PreCull;
            base.OnDestroy();
        }

        [Obsolete("Unused by any script")]
        private bool CheckRaySphereIntersection(Vector3d rayDir, Vector3d offset, double radius)
        {
            double dir = Vector3d.Dot(rayDir, offset);
            Vector3d ClosestPoint = offset - dir * rayDir;
            return ClosestPoint.sqrMagnitude > radius * radius || (dir <= 0 && offset.sqrMagnitude > radius * radius);
        }
        [Obsolete("Does not work as intended")]
        private Boolean RecursiveCheck(CelestialBody toCheck, Boolean prior, Vector3d toCamera)
        {
            if (toCheck == null)
                return prior;
            List<CelestialBody> children = toCheck.orbitingBodies;
            if (toCheck == sun)
            {
                if (children == null || children.Count == 0)
                    return prior;
                foreach (CelestialBody b in children)
                    prior &= RecursiveCheck(b, prior, toCamera);
                return prior;
            }
            Vector3d targetDistance = PlanetariumCamera.fetch.transform.position - toCheck.transform.position;
            prior &= CheckRaySphereIntersection(toCamera, targetDistance, toCheck.Radius / ScaledSpace.ScaleFactor);
            if (prior && children != null && children.Count != 0)
                if (!CheckRaySphereIntersection(toCamera, targetDistance, toCheck.sphereOfInfluence / ScaledSpace.ScaleFactor))
                    foreach (CelestialBody b in children)
                        prior &= RecursiveCheck(b, prior, toCamera);
            return prior;
        }

        private void Start()
        {
            sunFlare.fadeSpeed = 10000f;
        }
        // Overload the stock LateUpdate function
        private void LateUpdate()
        {
            Vector3d position = target.position;
            sunDirection = (position - ScaledSpace.LocalToScaledSpace(sun.position)).normalized;
            transform.forward = sunDirection;
            sunFlare.brightness = brightnessMultiplier *
                                  brightnessCurve.Evaluate(
                                      (Single)(1.0 / (Vector3d.Distance(position,
                                                           ScaledSpace.LocalToScaledSpace(sun.position)) /
                                                       (AU * ScaledSpace.InverseScaleFactor))));

            if (PlanetariumCamera.fetch.target == null ||
                HighLogic.LoadedScene != GameScenes.TRACKSTATION && HighLogic.LoadedScene != GameScenes.FLIGHT)
            {
                return;
            }
        }
    }
}

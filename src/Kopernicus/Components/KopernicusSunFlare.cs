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
using System.Diagnostics.CodeAnalysis;
using UnityEngine;

namespace Kopernicus.Components
{
    /// <summary>
    /// Modifications for the SunFlare component
    /// </summary>
    public class KopernicusSunFlare : SunFlare
    {
        Node<CelestialBody> thisCelestial;
        Node<CelestialBody> root;
        protected override void Awake()
        {
            Camera.onPreCull += PreCull;
            System.Collections.Generic.List<Node<CelestialBody>> l = IniTreeGeneration.GetTree().elements;
            thisCelestial = l.Find(a => a.item == sun);
            root = l[0];
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

        private (Boolean, Boolean) CheckRaySphereIntersection(Vector3d rayDir, Vector3d offset, double radius)
        {
            double dir = Vector3d.Dot(rayDir, offset);
            Vector3d ClosestPoint = offset - dir * rayDir;
            return (ClosestPoint.sqrMagnitude > radius * radius, dir <= 0 && offset.sqrMagnitude > radius * radius);
        }

        private Boolean RecursiveCheck(Node<CelestialBody> toCheck, Boolean prior)
        {
            if (toCheck == null)
                return prior;

            if (toCheck.item.GetComponent<SphereCollider>() == null)
                return prior;

            if (!toCheck.item.GetComponent<MeshRenderer>().enabled)
                return prior;

            if (toCheck == thisCelestial)
                return prior;

            if (toCheck.item.transform.localScale.x < .001f)
                return prior;
            
            Vector3d targetDistance = PlanetariumCamera.fetch.transform.position - toCheck.item.transform.position;
            if (toCheck.children == null || toCheck.children.Count == 0) // No children anymore, check itself
            {
                (Boolean, Boolean) test = CheckRaySphereIntersection(sunDirection, targetDistance, toCheck.item.Radius / ScaledSpace.ScaleFactor);
                if (test.Item2) // Behind Object, ignore
                    return prior;
                return prior && test.Item1; // Returns either whatever it was told or false.
            }
            else
            {
                (Boolean, Boolean) test = CheckRaySphereIntersection(sunDirection, targetDistance, toCheck.item.sphereOfInfluence / ScaledSpace.ScaleFactor);
                if (test.Item2) // Behind SOI, ignore
                    return prior;
                if (!test.Item1) // Intersects SOI, check children
                    foreach (Node<CelestialBody> child in thisCelestial.children)
                        prior &= RecursiveCheck(child, prior); // Checks children recursively
                test = CheckRaySphereIntersection(sunDirection, targetDistance, toCheck.item.Radius / ScaledSpace.ScaleFactor);
                prior &= test.Item1 || test.Item2;
            }
            return prior;
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

            if (sunFlare.brightness == 0)
            {
                SunlightEnabled(false);
                return;
            }

            if (PlanetariumCamera.fetch.target == null ||
                HighLogic.LoadedScene != GameScenes.TRACKSTATION && HighLogic.LoadedScene != GameScenes.FLIGHT)
            {
                return;
            }

            SunlightEnabled(RecursiveCheck(root, true));
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Kopernicus.Components.ModularScatter
{
    [RequireComponent(typeof(MeshRenderer))]
    class ScatterDistanceCuller : MonoBehaviour
    {
        private MeshRenderer surfaceObject;
        private void Start()
        {
            surfaceObject = GetComponent<MeshRenderer>();
        }
        private void Update()
        {
            int maxdistance = Kopernicus.RuntimeUtility.RuntimeUtility.KopernicusConfig.ScatterCullDistance;
            int distance = 15000;
            if (HighLogic.LoadedSceneIsFlight)
            {
                try
                {
                    distance = (int)Vector3.Distance(FlightGlobals.ActiveVessel.transform.position, surfaceObject.transform.position);
                }
                catch
                {
                    distance = (int)Vector3.Distance(Camera.current.transform.position, surfaceObject.transform.position);
                    //If craft breaks up this prevents errors.
                }
            }
            else
            {
                distance = 0;
            }

            if (distance > maxdistance)
            {
                surfaceObject.enabled = false;
            }
            else
            {
                surfaceObject.enabled = true;
            }
        }
    }
}

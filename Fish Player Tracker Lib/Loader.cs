using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.SocialPlatforms;
using UnityEngine;

namespace Fish_Player_Tracker_Lib
{
    internal class Loader
    {
        public void Init()
        {
            if (!GameObject.Find("Loader") && GorillaLocomotion.Player.hasInstance)
            {
                GameObject Loader = new GameObject("Loader");
                Loader.AddComponent<Class1>();
            }
        }
        public void FixedUpdate()
        {
            if (!GameObject.Find("Loader"))
            {
                GameObject Loader = new GameObject("Loader");
                Loader.AddComponent<Class1>();
            }
        }
    }
}

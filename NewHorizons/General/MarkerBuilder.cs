﻿using NewHorizons.External;
using OWML.Utils;
using System.Reflection;
using UnityEngine;
using Logger = NewHorizons.Utility.Logger;

namespace NewHorizons.General
{
    static class MarkerBuilder
    {
        public static void Make(GameObject body, string name, bool isMoon)
        {
            MapMarker MM = body.AddComponent<MapMarker>();
            MM.SetValue("_labelID", (UITextType)Utility.AddToUITable.Add(name.ToUpper()));

            if (isMoon)
            {
                MM.SetValue("_markerType", MM.GetType().GetNestedType("MarkerType", BindingFlags.NonPublic).GetField("Moon").GetValue(MM));
            }
            else
            {
                MM.SetValue("_markerType", MM.GetType().GetNestedType("MarkerType", BindingFlags.NonPublic).GetField("Planet").GetValue(MM));
            }
        }
    }
}
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NewHorizons.External.Modules
{
    [JsonObject]
    public class ShockEffectModule
    {
        /// <summary>
        /// Does this planet have a shock effect when the nearest star goes supernova? Automatically disabled for stars, focal points, and stellar remnants.
        /// </summary>
        [DefaultValue(true)] public bool hasSupernovaShockEffect = true;

        /// <summary>
        /// Scale of the shock effect mesh
        /// </summary>
        [DefaultValue(1.1f)] public float scale = 1.1f;

        /// <summary>
        /// Asset Bundle that contains the shock effect mesh
        /// </summary>
        public string assetBundle;

        /// <summary>
        /// Path to the replacement mesh for the planet's supernova shock effect in the supplied asset bundle
        /// </summary>
        public string meshPath;
    }
}

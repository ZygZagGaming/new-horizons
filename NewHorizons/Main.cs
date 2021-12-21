﻿using NewHorizons.Atmosphere;
using NewHorizons.Body;
using NewHorizons.External;
using NewHorizons.General;
using NewHorizons.OrbitalPhysics;
using NewHorizons.Utility;
using OWML.Common;
using OWML.ModHelper;
using OWML.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using Logger = NewHorizons.Utility.Logger;

namespace NewHorizons
{
    public class Main : ModBehaviour
    {

        public static AssetBundle ShaderBundle;
        public static Main Instance { get; private set; }

        public static List<NewHorizonsBody> BodyList = new List<NewHorizonsBody>();
        public static List<NewHorizonsBody> AdditionalBodies = new List<NewHorizonsBody>();

        public override object GetApi()
        {
            return new NewHorizonsApi();
        }

        void Start()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            Instance = this;
            ShaderBundle = Main.Instance.ModHelper.Assets.LoadBundle("AssetBundle/shader");

            Utility.Patches.Apply();

            Logger.Log("Begin load of config files...", Logger.LogType.Log);

            try
            {
                LoadConfigs(this);
            }
            catch(Exception)
            {
                Logger.LogWarning("Couldn't find planets folder");
            }
        }

        void Destroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "SolarSystem") return;

            Instance.ModHelper.Events.Unity.FireOnNextUpdate(() => Locator.GetPlayerBody().gameObject.AddComponent<DebugRaycaster>());

            AstroObjectLocator.RefreshList();
            foreach(AstroObject ao in GameObject.FindObjectsOfType<AstroObject>())
            {
                AstroObjectLocator.AddAstroObject(ao);
            }

            // Stars then planets then moons
            BodyList = BodyList.OrderBy(b => 
                (b.Config.BuildPriority != -1 ? b.Config.BuildPriority : (b.Config.Star != null) ? 0 :
                (b.Config.Orbit.IsMoon ? 2 : 1)
                )).ToList();

            while(BodyList.Count != 0)
            {
                foreach (var body in BodyList)
                {
                    LoadBody(body);
                }
                BodyList = AdditionalBodies;
                AdditionalBodies = new List<NewHorizonsBody>();
            }
        }

        private void LoadBody(NewHorizonsBody body)
        {
            var stringID = body.Config.Name.ToUpper().Replace(" ", "_").Replace("'", "");
            if (stringID.Equals("ATTLEROCK")) stringID = "TIMBER_MOON";
            if (stringID.Equals("HOLLOWS_LANTERN")) stringID = "VOLCANIC_MOON";
            if (stringID.Equals("ASH_TWIN")) stringID = "TOWER_TWIN";
            if (stringID.Equals("EMBER_TWIN")) stringID = "CAVE_TWIN";
            if (stringID.Equals("INTERLOPER")) stringID = "COMET";

            AstroObject existingPlanet = null;
            try
            {
                existingPlanet = AstroObjectLocator.GetAstroObject(stringID);
                if (existingPlanet == null) existingPlanet = AstroObjectLocator.GetAstroObject(body.Config.Name.Replace(" ", ""));
            }
            catch (Exception e)
            {
                Logger.LogWarning($"Error when looking for {body.Config.Name}: {e.Message}, {e.StackTrace}");
            }

            if (existingPlanet != null)
            {
                try
                {
                    if (body.Config.Destroy)
                    {
                        Instance.ModHelper.Events.Unity.FireInNUpdates(() => PlanetDestroyer.RemoveBody(existingPlanet), 2);
                    }
                    else UpdateBody(body, existingPlanet);
                }
                catch (Exception e)
                {
                    Logger.LogError($"Couldn't update body {body.Config?.Name}: {e.Message}, {e.StackTrace}");
                }
            }
            else
            {
                try
                {
                    GameObject planetObject;
                    planetObject = GenerateBody(body);
                    planetObject.SetActive(true);
                }
                catch (Exception e)
                {
                    Logger.LogError($"Couldn't generate body {body.Config?.Name}: {e.Message}, {e.StackTrace}");
                }
            }
        }


        public void LoadConfigs(IModBehaviour mod)
        {
            var folder = mod.ModHelper.Manifest.ModFolderPath;
            foreach (var file in Directory.GetFiles(folder + @"planets\"))
            {
                try
                {
                    var config = mod.ModHelper.Storage.Load<PlanetConfig>(file.Replace(folder, ""));
                    Logger.Log($"Loaded {config.Name}");
                    BodyList.Add(new NewHorizonsBody(config, mod.ModHelper.Assets));
                }
                catch (Exception e)
                {
                    Logger.LogError($"Couldn't load {file}: {e.Message}, is your Json formatted correctly?");
                }
            }
        }

        public static GameObject UpdateBody(NewHorizonsBody body, AstroObject ao) 
        {
            Logger.Log($"Updating existing AstroObject {ao}");

            var go = ao.gameObject;

            var sector = go.GetComponentInChildren<Sector>();
            var rb = go.GetAttachedOWRigidbody();

            // Do stuff that's shared between generating new planets and updating old ones
            return SharedGenerateBody(body, go, sector, rb);
        }

        public static GameObject GenerateBody(NewHorizonsBody body)
        {
            Logger.Log("Begin generation sequence of [" + body.Config.Name + "] ...", Logger.LogType.Log);

            var go = new GameObject(body.Config.Name.Replace(" ", "").Replace("'", "") + "_Body");
            go.SetActive(false);

            if(body.Config.Base.GroundSize != 0) GeometryBuilder.Make(go, body.Config.Base.GroundSize);

            AstroObject primaryBody = AstroObjectLocator.GetAstroObject(body.Config.Orbit.PrimaryBody);
            if(primaryBody == null)
            {
                Logger.LogError($"Could not find AstroObject {body.Config.Orbit.PrimaryBody}, defaulting to SUN");
                primaryBody = AstroObjectLocator.GetAstroObject(AstroObject.Name.Sun);
            } 

            var atmoSize = body.Config.Atmosphere != null ? body.Config.Atmosphere.Size : 0f;
            float sphereOfInfluence = Mathf.Max(atmoSize, body.Config.Base.SurfaceSize * 2f);

            // Get initial position but set it at the end
            var falloffType = primaryBody.GetGravityVolume().GetFalloffType();
            
            /*
            var positionVector = OrbitalHelper.CartesianStateVectorsFromTrueAnomaly(
                0f,
                body.Config.Orbit.Eccentricity,
                body.Config.Orbit.SemiMajorAxis, 
                body.Config.Orbit.Inclination,
                body.Config.Orbit.LongitudeOfAscendingNode,
                body.Config.Orbit.ArgumentOfPeriapsis, 
                body.Config.Orbit.TrueAnomaly,
                falloffType).Position;
            */

            var rot = Quaternion.AngleAxis(body.Config.Orbit.LongitudeOfAscendingNode + body.Config.Orbit.TrueAnomaly + body.Config.Orbit.ArgumentOfPeriapsis + 180f, Vector3.up);

            // For now, eccentric orbits gotta start at apoapsis and cant be inclined
            if(body.Config.Orbit.Eccentricity != 0)
            {
                rot = Quaternion.AngleAxis(body.Config.Orbit.LongitudeOfAscendingNode + body.Config.Orbit.ArgumentOfPeriapsis + 180f, Vector3.up);
                body.Config.Orbit.Inclination = 0;
            }


            var incAxis = Quaternion.AngleAxis(body.Config.Orbit.LongitudeOfAscendingNode, Vector3.up) * Vector3.left;
            var incRot = Quaternion.AngleAxis(body.Config.Orbit.Inclination, incAxis);

            var positionVector = rot * incRot * Vector3.left * body.Config.Orbit.SemiMajorAxis * (1 + body.Config.Orbit.Eccentricity);

            var outputTuple = BaseBuilder.Make(go, primaryBody, positionVector, body.Config);
            var ao = (AstroObject)outputTuple.Items[0];
            var rb = (OWRigidbody)outputTuple.Items[1];

            if (body.Config.Base.SurfaceGravity != 0)
                GravityBuilder.Make(go, ao, body.Config.Base.SurfaceGravity, sphereOfInfluence, body.Config.Base.SurfaceSize, body.Config.Base.GravityFallOff);
            
            if(body.Config.Base.HasReferenceFrame)
                RFVolumeBuilder.Make(go, rb, sphereOfInfluence);

            if (body.Config.Base.HasMapMarker)
                MarkerBuilder.Make(go, body.Config.Name, body.Config.Orbit.IsMoon);

            if (body.Config.Base.HasAmbientLight)
                AmbientLightBuilder.Make(go, sphereOfInfluence);

            var sector = MakeSector.Make(go, rb, sphereOfInfluence);

            VolumesBuilder.Make(go, body.Config.Base.SurfaceSize, sphereOfInfluence);

            if (body.Config.HeightMap != null)
                HeightMapBuilder.Make(go, body.Config.HeightMap, body.Assets);

            if (body.Config.ProcGen != null)
                ProcGenBuilder.Make(go, body.Config.ProcGen);

            if (body.Config.Base.BlackHoleSize != 0)
                BlackHoleBuilder.Make(go, body.Config.Base, sector);

            /*
            if (body.Config.Star != null)
                StarBuilder.Make(go, sector, body.Config.Star);
            */

            // Do stuff that's shared between generating new planets and updating old ones
            go = SharedGenerateBody(body, go, sector, rb);

            body.Object = go;

            // Some things have to be done the second tick
            if(body.Config.Orbit != null && body.Config.Orbit.ShowOrbitLine)
                Instance.ModHelper.Events.Unity.FireOnNextUpdate(() => OrbitlineBuilder.Make(body.Object, ao, body.Config.Orbit.IsMoon, body.Config.Orbit));

            // Now that we're done move the planet into place
            go.transform.parent = Locator.GetRootTransform();
            go.transform.position = positionVector + primaryBody.transform.position;

            // Have to do this after setting position
            InitialMotionBuilder.Make(go, primaryBody, rb, body.Config.Orbit);

            // Spawning on other planets is a bit hacky so we do it last
            if (body.Config.Spawn != null)
            {
                SpawnPointBuilder.Make(go, body.Config.Spawn, rb);
            }

            if (ao.GetAstroObjectName() == AstroObject.Name.CustomString) AstroObjectLocator.RegisterCustomAstroObject(ao);

            Logger.Log("Generation of [" + body.Config.Name + "] completed.", Logger.LogType.Log);

            return go;
        }

        private static GameObject SharedGenerateBody(NewHorizonsBody body, GameObject go, Sector sector, OWRigidbody rb)
        {
            if (body.Config.Ring != null)
                RingBuilder.Make(go, body.Config.Ring, body.Assets);

            if (body.Config.AsteroidBelt != null)
                AsteroidBeltBuilder.Make(body.Config.Name, body.Config.AsteroidBelt, body.Assets);

            if (body.Config.Base.HasCometTail)
                CometTailBuilder.Make(go, body.Config.Base, go.GetComponent<AstroObject>().GetPrimaryBody());
            
            if(body.Config.Base != null)
            {
                if (body.Config.Base.LavaSize != 0)
                    LavaBuilder.Make(go, sector, rb, body.Config.Base.LavaSize);
                if (body.Config.Base.WaterSize != 0)
                    WaterBuilder.Make(go, sector, rb, body.Config.Base.WaterSize);
            }

            if (body.Config.Atmosphere != null)
            {
                AirBuilder.Make(go, body.Config.Atmosphere.Size, body.Config.Atmosphere.HasRain, body.Config.Atmosphere.HasOxygen);

                if (body.Config.Atmosphere.Cloud != null)
                {
                    CloudsBuilder.Make(go, sector, body.Config.Atmosphere, body.Assets);
                    SunOverrideBuilder.Make(go, sector, body.Config.Base.SurfaceSize, body.Config.Atmosphere);
                }

                if (body.Config.Atmosphere.HasRain || body.Config.Atmosphere.HasSnow)
                    EffectsBuilder.Make(go, sector, body.Config.Base.SurfaceSize, body.Config.Atmosphere.Size / 2f, body.Config.Atmosphere.HasRain, body.Config.Atmosphere.HasSnow);

                if (body.Config.Atmosphere.FogSize != 0)
                    FogBuilder.Make(go, sector, body.Config.Atmosphere);

                AtmosphereBuilder.Make(go, body.Config.Atmosphere);
            }

            return go;
        }
    }

    public class NewHorizonsApi
    {
        [Obsolete("Create(Dictionary<string, object> config) is deprecated, please use Create(Dictionary<string, object> config, IModBehaviour mod) instead")]
        public void Create(Dictionary<string, object> config)
        {
            Create(config, null);
        }

        public void Create(Dictionary<string, object> config, IModBehaviour mod)
        {
            Logger.Log("Recieved API request to create planet " + (string)config["Name"], Logger.LogType.Log);
            var planetConfig = new PlanetConfig(config);

            var body = new NewHorizonsBody(planetConfig, mod != null ? mod.ModHelper.Assets : Main.Instance.ModHelper.Assets);

            Main.BodyList.Add(body);
        }

        public void LoadConfigs(IModBehaviour mod)
        {
            Main.Instance.LoadConfigs(mod);
        }

        public GameObject GetPlanet(string name)
        {
            return Main.BodyList.FirstOrDefault(x => x.Config.Name == name).Object;
        }
    }
}
using NewHorizons.External.Modules;
using NewHorizons.Utility;
using UnityEngine;
namespace NewHorizons.Builder.Props
{
    public static class VolcanoBuilder
    {
        private static Color defaultStoneTint = new Color(0.07450981f, 0.07450981f, 0.07450981f);
        private static Color defaultLavaTint = new Color(4.594794f, 0.3419145f, 0f, 1f);
        private static readonly int Color1 = Shader.PropertyToID("_Color");
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        private static GameObject _meteorLauncherPrefab;

        internal static void InitPrefab()
        {
            if (_meteorLauncherPrefab == null) _meteorLauncherPrefab = SearchUtilities.Find("VolcanicMoon_Body/Sector_VM/Effects_VM/VolcanoPivot (2)/MeteorLauncher").InstantiateInactive().Rename("Prefab_VM_MeteorLauncher").DontDestroyOnLoad();
        }

        public static void Make(GameObject planetGO, Sector sector, PropModule.VolcanoInfo info)
        {
            InitPrefab();

            var launcherGO = _meteorLauncherPrefab.InstantiateInactive();
            launcherGO.transform.parent = sector.transform;
            launcherGO.transform.position = planetGO.transform.TransformPoint(info.position == null ? Vector3.zero : (Vector3)info.position);
            launcherGO.transform.rotation = Quaternion.FromToRotation(launcherGO.transform.TransformDirection(Vector3.up), ((Vector3)info.position).normalized).normalized;
            launcherGO.name = "MeteorLauncher";

            var meteorLauncher = launcherGO.GetComponent<MeteorLauncher>();
            meteorLauncher._dynamicMeteorPrefab = null;
            meteorLauncher._detectableFluid = null;
            meteorLauncher._detectableField = null;

            meteorLauncher._launchDirection = Vector3.up;

            meteorLauncher._dynamicProbability = 0f;

            meteorLauncher._minLaunchSpeed = info.minLaunchSpeed;
            meteorLauncher._maxLaunchSpeed = info.maxLaunchSpeed;
            meteorLauncher._minInterval = info.minInterval;
            meteorLauncher._maxInterval = info.maxInterval;

            launcherGO.SetActive(true);

            // Have to null check else it breaks on reload configs
            Delay.RunWhen(() => Main.IsSystemReady && meteorLauncher._meteorPool != null, () =>
            {
                foreach (var meteor in meteorLauncher._meteorPool)
                {
                    FixMeteor(meteor, info);
                }
            });
        }

        private static void FixMeteor(MeteorController meteor, PropModule.VolcanoInfo info)
        {
            meteor.transform.localScale = Vector3.one * info.scale;

            var mat = meteor.GetComponentInChildren<MeshRenderer>().material;
            mat.SetColor(Color1, info.stoneTint?.ToColor() ?? defaultStoneTint);
            mat.SetColor(EmissionColor, info.lavaTint?.ToColor() ?? defaultLavaTint);

            GameObject.Destroy(meteor.transform.Find("ConstantDetectors").gameObject);

            var detectors = meteor.transform.Find("DynamicDetector").gameObject;

            meteor._constantFluidDetector = null;
            meteor._constantForceDetector = null;

            var forceDetector = detectors.gameObject.AddComponent<DynamicForceDetector>();

            meteor._owColliders = meteor.gameObject.GetComponentsInChildren<OWCollider>();
        }
    }
}

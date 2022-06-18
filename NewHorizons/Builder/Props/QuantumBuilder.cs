using HarmonyLib;
using NewHorizons.External.Configs;
using NewHorizons.External.Modules;
using NewHorizons.Utility;
using OWML.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

using Logger = NewHorizons.Utility.Logger;

// BUGS THAT REQUIRE REWRITING MOBIUS CODE
// 1) MultiStateQuantumObjects don't check to see if the new state would be visible before choosing it
// 2) QuantumShuffleObjects don't respect rotation, they set rotation to 0 on collapse

// New features to support
// 1) multiState._prerequisiteObjects

namespace NewHorizons.Builder.Props
{
    public static class QuantumBuilder
    {
        
        public static void Make(GameObject go, Sector sector, PlanetConfig config, IModBehaviour mod, PropModule.QuantumGroupInfo quantumGroup, GameObject[] propsInGroup)
        {
            // if a prop doesn't have a visibiilty volume, create a box volume using the prop's mesh bounds (if there are multiple mesh filters, use the min/max bounds accross all meshes)
            switch(quantumGroup.type)
            {
                case PropModule.QuantumGroupType.Sockets: MakeSocketGroup (go, sector, config, mod, quantumGroup, propsInGroup); return;
                case PropModule.QuantumGroupType.States:  MakeStateGroup  (go, sector, config, mod, quantumGroup, propsInGroup); return;
                case PropModule.QuantumGroupType.Shuffle: MakeShuffleGroup(go, sector, config, mod, quantumGroup, propsInGroup); return;
                // TODO: for quantum socket group allow specifying an _emptySocketObject
            }
        }
        
        public static void MakeSocketGroup(GameObject go, Sector sector, PlanetConfig config, IModBehaviour mod, PropModule.QuantumGroupInfo quantumGroup, GameObject[] propsInGroup)
        {
            // note: for the visibility boxes on quantum sockets, if there's only one prop that's part of this group, clone its visibility volume
            // otherwise, create a box according to the max and min dimensions across all props in this group (ie the box should be able to fit inside of it the visibility volume on any prop in this group)
         
            // ??? what's with this above comment? I thought only the actual props that are usnig the sockets needed a visibility volume

            var groupRoot = new GameObject("Quantum Sockets - " + quantumGroup.id);
            groupRoot.transform.parent = sector.transform;
            groupRoot.transform.localPosition = Vector3.zero;
            groupRoot.transform.localEulerAngles = Vector3.zero;
            
            var sockets = new QuantumSocket[quantumGroup.sockets.Length];
            for (int i = 0; i < quantumGroup.sockets.Length; i++)
            {
                var socketInfo = quantumGroup.sockets[i];

                var socket = new GameObject("Socket " + i);
                socket.SetActive(false);
                socket.transform.parent = groupRoot.transform;
                socket.transform.localPosition = socketInfo.position;
                socket.transform.localEulerAngles = socketInfo.rotation;

                sockets[i] = socket.AddComponent<QuantumSocket>();
                sockets[i]._lightSources = new Light[0];
                socket.SetActive(true);
            }

            foreach(var prop in propsInGroup)
            {
                prop.SetActive(false);
                var quantumObject = prop.AddComponent<SocketedQuantumObject>();
                quantumObject._socketRoot = groupRoot;
                quantumObject._socketList = sockets.ToList();
                quantumObject._sockets = sockets;
                quantumObject._prebuilt = true;
                quantumObject._childSockets = new List<QuantumSocket>();
                // TODO: support _alignWithGravity?
                if (prop.GetComponentInChildren<VisibilityTracker>() == null) AddBoundsVisibility(prop);
                prop.SetActive(true);        
            }
        }

        public static void MakeStateGroup(GameObject go, Sector sector, PlanetConfig config, IModBehaviour mod, PropModule.QuantumGroupInfo quantumGroup, GameObject[] propsInGroup)
        {
            // on parent of the states, MultiStateQuantumObject
            
            var groupRoot = new GameObject("Quantum States - " + quantumGroup.id);
            groupRoot.transform.parent = sector.transform;
            groupRoot.transform.localPosition = Vector3.zero;

            var states = new List<QuantumState>();
            foreach(var prop in propsInGroup)
            {
                prop.transform.parent = groupRoot.transform;
                var state = prop.AddComponent<QuantumState>();
                state._probability = 1;
                states.Add(state);

                if (prop.GetComponentInChildren<ShapeVisibilityTracker>() != null) continue;

                AddBoundsVisibility(prop);
            }

            if (quantumGroup.hasEmptyState)
            {
                var template = propsInGroup[0];
                
                var empty = new GameObject("Empty State");
                empty.transform.parent = groupRoot.transform;
                var state = empty.AddComponent<QuantumState>();
                states.Add(state);

                var boxBounds = GetBoundsOfSelfAndChildMeshes(template);
                var boxShape = empty.AddComponent<BoxShape>();
                boxShape.center = boxBounds.center;
                boxShape.extents = boxBounds.size;
                empty.AddComponent<BoxShapeVisualizer>();
                
                empty.AddComponent<ShapeVisibilityTracker>();
            }

            groupRoot.SetActive(false);
            var multiState = groupRoot.AddComponent<MultiStateQuantumObject>();
            multiState._loop = quantumGroup.loop;
            multiState._sequential = quantumGroup.sequential;
            multiState._states = states.ToArray();
            multiState._prerequisiteObjects = new MultiStateQuantumObject[0]; // TODO: support this
            multiState._initialState = 0;
            groupRoot.SetActive(true);
        }

        public static void MakeShuffleGroup(GameObject go, Sector sector, PlanetConfig config, IModBehaviour mod, PropModule.QuantumGroupInfo quantumGroup, GameObject[] propsInGroup)
        {
            //var averagePosition = propsInGroup.Aggregate(Vector3.zero, (avg, prop) => avg + prop.transform.position) / propsInGroup.Count();
            GameObject shuffleParent = new GameObject("Quantum Shuffle - " + quantumGroup.id);
            shuffleParent.SetActive(false);
            shuffleParent.transform.parent = sector.transform;
            shuffleParent.transform.localPosition = Vector3.zero;
            propsInGroup.ToList().ForEach(p => p.transform.parent = shuffleParent.transform);    

            var shuffle = shuffleParent.AddComponent<QuantumShuffleObject>();
            shuffle._shuffledObjects = propsInGroup.Select(p => p.transform).ToArray();
            shuffle.Awake(); // this doesn't get called on its own for some reason
            
            AddBoundsVisibility(shuffleParent);
            shuffleParent.SetActive(true);
        }

        
        struct BoxShapeReciever
        {
            public MeshFilter f;
            public SkinnedMeshRenderer s;
            public GameObject g;
        }

        public static void AddBoundsVisibility(GameObject g)
        {
            var meshFilters = g.GetComponentsInChildren<MeshFilter>();
            var skinnedMeshRenderers = g.GetComponentsInChildren<SkinnedMeshRenderer>();
            
            var boxShapeRecievers = meshFilters
                .Select(f => new BoxShapeReciever() { f=f, g=f.gameObject })
                .Concat (
                    skinnedMeshRenderers.Select(s => new BoxShapeReciever() { s=s, g=s.gameObject })
                )
                .ToList();

            foreach(var boxshapeReciever in boxShapeRecievers)
            {
                var box = boxshapeReciever.g.AddComponent<BoxShape>();
                boxshapeReciever.g.AddComponent<ShapeVisibilityTracker>();
                boxshapeReciever.g.AddComponent<BoxShapeVisualizer>();

                var fixer = boxshapeReciever.g.AddComponent<BoxShapeFixer>();
                fixer.shape = box;
                fixer.meshFilter = boxshapeReciever.f;
                fixer.skinnedMeshRenderer = boxshapeReciever.s;
            }
        }

        public static Bounds GetBoundsOfSelfAndChildMeshes(GameObject g)
        {
            var meshFilters = g.GetComponentsInChildren<MeshFilter>();
            var corners = meshFilters.SelectMany(m => GetMeshCorners(m, g)).ToList();
            
            Bounds b = new Bounds(corners[0], Vector3.zero);
            corners.ForEach(corner => b.Encapsulate(corner));

            NewHorizons.Utility.Logger.Log("CORNERS:=-=-=-=-=-==-=-=-=-==-=-=-==-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=- ");
            NewHorizons.Utility.Logger.Log("CORNERS: "+ string.Join(", ",corners));
            NewHorizons.Utility.Logger.Log("BOUNDS: "+ b.center + "    " + b.size);

            return b;
        }

        public static Vector3[] GetMeshCorners(MeshFilter m, GameObject relativeTo = null)
        {
            var bounds = m.mesh.bounds;
                
            Logger.Log(m.gameObject.name + " " + bounds.min + "  " + bounds.max);

            var localCorners = new Vector3[]
            {
                 bounds.min,
                 bounds.max,
                 new Vector3(bounds.min.x, bounds.min.y, bounds.max.z),
                 new Vector3(bounds.min.x, bounds.max.y, bounds.min.z),
                 new Vector3(bounds.max.x, bounds.min.y, bounds.min.z),
                 new Vector3(bounds.min.x, bounds.max.y, bounds.max.z),
                 new Vector3(bounds.max.x, bounds.min.y, bounds.max.z),
                 new Vector3(bounds.max.x, bounds.max.y, bounds.min.z),
            };
            
            var globalCorners = localCorners.Select(localCorner => m.transform.TransformPoint(localCorner)).ToArray();
            
            if (relativeTo == null) return globalCorners;

            return globalCorners.Select(globalCorner => relativeTo.transform.InverseTransformPoint(globalCorner)).ToArray();
        }
    }

    public class BoxShapeFixer : MonoBehaviour
    {
        bool _fixed = false;
        public BoxShape shape;
        public MeshFilter meshFilter;
        public SkinnedMeshRenderer skinnedMeshRenderer;

        void Update()
        {
            if (meshFilter == null && skinnedMeshRenderer == null) { Logger.Log("Useless BoxShapeFixer, destroying"); GameObject.DestroyImmediate(this); }

            Mesh sharedMesh = null;
            if (meshFilter != null) sharedMesh = meshFilter.sharedMesh;
            if (skinnedMeshRenderer != null) sharedMesh = skinnedMeshRenderer.sharedMesh;

            if (sharedMesh == null) return;
            if (sharedMesh.bounds.size == Vector3.zero) return;
            
            shape.size = sharedMesh.bounds.size;
            shape.center = sharedMesh.bounds.center;

            GameObject.DestroyImmediate(this);
        }
    }
}

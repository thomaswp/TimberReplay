﻿using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Timberborn.BlockObjectTools;
using Timberborn.BlockSystem;
using Timberborn.Buildings;
using Timberborn.BuildingTools;
using Timberborn.Coordinates;
using Timberborn.DemolishingUI;
using Timberborn.EntitySystem;
using Timberborn.Forestry;
using Timberborn.PlantingUI;
using Timberborn.ScienceSystem;
using UnityEngine;

namespace TimberModTest.Events
{

    [Serializable]
    class BuildingPlacedEvent : ReplayEvent
    {
        public string prefabName;
        public Vector3Int coordinates;
        public Orientation orientation;

        //public BuildingPlacedEvent(float timeInFixedSecs, string prefab, Vector3Int coordinates, Orientation orientation) : base(timeInFixedSecs)
        //{
        //    this.prefab = prefab;
        //    this.coordinates = coordinates;
        //    this.orientation = orientation;
        //}

        public override void Replay(IReplayContext context)
        {
            var buildingPrefab = GetBuilding(context, prefabName);
            var blockObject = buildingPrefab.GetComponentFast<BlockObject>();
            var placer = context.GetSingleton<BlockObjectPlacerService>().GetMatchingPlacer(blockObject);
            placer.Place(blockObject, coordinates, orientation);
        }
    }


    [HarmonyPatch(typeof(BuildingPlacer), nameof(BuildingPlacer.Place))]
    class PlacePatcher
    {
        static bool Prefix(BlockObject prefab, Vector3Int coordinates, Orientation orientation)
        {
            string prefabName = ReplayEvent.GetBuildingName(prefab);
            Plugin.Log($"Placing {prefabName}, {coordinates}, {orientation}");

            //System.Diagnostics.StackTrace t = new System.Diagnostics.StackTrace();
            //Plugin.Log(t.ToString());

            ReplayService.RecordEvent(new BuildingPlacedEvent()
            {
                prefabName = prefabName,
                coordinates = coordinates,
                orientation = orientation,
            });

            return EventIO.ShouldPlayPatchedEvents;
        }
    }

    class BuildingsDeconstructedEvent : ReplayEvent
    {
        public List<string> entityIDs = new List<string>();

        public override void Replay(IReplayContext context)
        {
            var entityService = context.GetSingleton<EntityService>();
            foreach (string entityID in entityIDs)
            {
                var entity = GetEntityComponent(context, entityID);
                if (entity == null) continue;
                entityService.Delete(entity);
            }
        }
    }

    [HarmonyPatch(typeof(BlockObjectDeletionTool<Building>), nameof(BlockObjectDeletionTool<Building>.DeleteBlockObjects))]
    class BuildingDeconstructionPatcher
    {
        static bool Prefix(BlockObjectDeletionTool<Building> __instance)
        {
            // TODO: If this does work, it may affect other deletions too :(
            List<string> entityIDs = __instance._temporaryBlockObjects
                    .Select(ReplayEvent.GetEntityID)
                    .ToList();
            Plugin.Log($"Deconstructing: {string.Join(", ", entityIDs)}");

            ReplayService.RecordEvent(new BuildingsDeconstructedEvent()
            {
                entityIDs = entityIDs,
            });

            if (!EventIO.ShouldPlayPatchedEvents)
            {
                // If we cancel the event, clean up the tool
                __instance._temporaryBlockObjects.Clear();
            }

            return EventIO.ShouldPlayPatchedEvents;
        }
    }

    [Serializable]
    class PlantingAreaMarkedEvent : ReplayEvent
    {
        public List<Vector3Int> inputBlocks;
        public Ray ray;
        public string prefabName;

        public const string UNMARK = "Unmark";

        public override void Replay(IReplayContext context)
        {
            var plantingService = context.GetSingleton<PlantingSelectionService>();
            if (prefabName == UNMARK)
            {
                plantingService.UnmarkArea(inputBlocks, ray);
            }
            else
            {
                plantingService.MarkArea(inputBlocks, ray, prefabName);
            }
        }
    }

    [HarmonyPatch(typeof(PlantingSelectionService), nameof(PlantingSelectionService.MarkArea))]
    class PlantingAreaMarkedPatcher
    {
        static bool Prefix(IEnumerable<Vector3Int> inputBlocks, Ray ray, string prefabName)
        {
            Plugin.Log($"Planting {inputBlocks.Count()} of {prefabName}");

            ReplayService.RecordEvent(new PlantingAreaMarkedEvent()
            {
                prefabName = prefabName,
                ray = ray,
                inputBlocks = new List<Vector3Int>(inputBlocks)
            });

            return EventIO.ShouldPlayPatchedEvents;
        }
    }

    [HarmonyPatch(typeof(PlantingSelectionService), nameof(PlantingSelectionService.UnmarkArea))]
    class PlantingAreaUnmarkedPatcher
    {
        static bool Prefix(IEnumerable<Vector3Int> inputBlocks, Ray ray)
        {
            Plugin.Log($"Removing planting x{inputBlocks.Count()}");

            ReplayService.RecordEvent(new PlantingAreaMarkedEvent()
            {
                prefabName = PlantingAreaMarkedEvent.UNMARK,
                ray = ray,
                inputBlocks = new List<Vector3Int>(inputBlocks)
            });

            return EventIO.ShouldPlayPatchedEvents;
        }
    }

    [Serializable]
    class ClearResourcesMarkedEvent : ReplayEvent
    {
        public List<Vector3Int> blocks;
        public Ray ray;
        public bool markForDemolition;

        public override void Replay(IReplayContext context)
        {
            var demolitionService = context.GetSingleton<DemolishableSelectionService>();
            if (markForDemolition)
            {
                demolitionService.MarkDemolishablesInArea(blocks, ray);
            }
            else
            {
                demolitionService.UnmarkDemolishablesInArea(blocks, ray);
            }
        }
        public static bool DoPrefix(IEnumerable<Vector3Int> blocks, Ray ray, bool markForDemolition)
        {
            Plugin.Log($"Setting {blocks.Count()} as marked: {markForDemolition}");

            ReplayService.RecordEvent(new ClearResourcesMarkedEvent()
            {
                markForDemolition = markForDemolition,
                ray = ray,
                blocks = new List<Vector3Int>(blocks)
            });

            return EventIO.ShouldPlayPatchedEvents;
        }
    }

    [HarmonyPatch(typeof(DemolishableSelectionService), nameof(DemolishableSelectionService.MarkDemolishablesInArea))]
    class DemolishableSelectionServiceMarkPatcher
    {
        static bool Prefix(IEnumerable<Vector3Int> blocks, Ray ray)
        {
            return ClearResourcesMarkedEvent.DoPrefix(blocks, ray, true);
        }
    }

    [HarmonyPatch(typeof(DemolishableSelectionService), nameof(DemolishableSelectionService.UnmarkDemolishablesInArea))]
    class DemolishableSelectionServiceUnmarkPatcher
    {
        static bool Prefix(IEnumerable<Vector3Int> blocks, Ray ray)
        {
            return ClearResourcesMarkedEvent.DoPrefix(blocks, ray, false);
        }
    }

    [Serializable]
    class TreeCuttingAreaEvent : ReplayEvent
    {
        public List<Vector3Int> coordinates;
        public bool wasAdded;

        public override void Replay(IReplayContext context)
        {
            // TODO: Looks like this doesn't work until the receiver
            // has at least opened the tool tray. Need to test more.
            var treeService = context.GetSingleton<TreeCuttingArea>();
            if (wasAdded)
            {
                treeService.AddCoordinates(coordinates);
            }
            else
            {
                treeService.RemoveCoordinates(coordinates);
            }
        }
    }

    // TODO: These events seem to only replay successfully if the
    // tool is open...
    [HarmonyPatch(typeof(TreeCuttingArea), nameof(TreeCuttingArea.AddCoordinates))]
    class TreeCuttingAreaAddedPatcher
    {
        static bool Prefix(IEnumerable<Vector3Int> coordinates)
        {
            Plugin.Log($"Adding tree planting coordinate {coordinates.Count()}");

            ReplayService.RecordEvent(new TreeCuttingAreaEvent()
            {
                coordinates = new List<Vector3Int>(coordinates),
                wasAdded = true,
            });

            return EventIO.ShouldPlayPatchedEvents;
        }
    }

    [HarmonyPatch(typeof(TreeCuttingArea), nameof(TreeCuttingArea.RemoveCoordinates))]
    class TreeCuttingAreaRemovedPatcher
    {
        static bool Prefix(IEnumerable<Vector3Int> coordinates)
        {
            Plugin.Log($"Removing tree planting coordinate {coordinates.Count()}");

            ReplayService.RecordEvent(new TreeCuttingAreaEvent()
            {
                coordinates = new List<Vector3Int>(coordinates),
                wasAdded = false,
            });

            return EventIO.ShouldPlayPatchedEvents;
        }
    }

    [Serializable]
    class BuildingUnlockedEvent : ReplayEvent
    {
        public string buildingName;

        public override void Replay(IReplayContext context)
        {
            var building = GetBuilding(context, buildingName);
            if (building == null) return;
            context.GetSingleton<BuildingUnlockingService>().Unlock(building);
        }
    }

    [HarmonyPatch(typeof(BuildingUnlockingService), nameof(BuildingUnlockingService.Unlock))]
    class BuildingUnlockingServiceUnlockPatcher
    {
        static bool Prefix(Building building)
        {
            Plugin.Log($"Unlocking building: {building.name}");

            ReplayService.RecordEvent(new BuildingUnlockedEvent()
            {
                buildingName = building.name,
            });

            return EventIO.ShouldPlayPatchedEvents;
        }
    }
}

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class MapConfigurationValidator
{
    [MenuItem("DiceWitch/Map/Validate Selected Map Config")]
    public static void ValidateSelectedMapConfig()
    {
        BoardMapConfigSO config = Selection.activeObject as BoardMapConfigSO;
        if (config == null)
        {
            Debug.LogError("请选择一个 BoardMapConfigSO 后再执行地图配置检查。");
            return;
        }

        Validate(config);
    }

    private static void Validate(BoardMapConfigSO config)
    {
        int errorCount = 0;
        int warningCount = 0;
        HashSet<GameEnums.RoomType> usedRoomTypes = new HashSet<GameEnums.RoomType>();
        HashSet<GameEnums.BoardNodeType> usedNodeTypes = new HashSet<GameEnums.BoardNodeType>();

        if (config.presentationCatalog == null)
            LogError("BoardMapConfigSO 缺少 presentationCatalog。", ref errorCount, config);

        if (config.nodePassedBackgroundFolder == null || config.NodePassedBackgroundCount == 0)
            LogError("BoardMapConfigSO 缺少节点已抵达背景文件夹，或尚未刷新背景索引。", ref errorCount, config);

        if (config.regions == null || config.regions.Count == 0)
        {
            LogError("BoardMapConfigSO 没有配置任何章节 Region。", ref errorCount, config);
            return;
        }

        for (int regionIndex = 0; regionIndex < config.regions.Count; regionIndex++)
        {
            BoardRegionConfig region = config.regions[regionIndex];
            string regionName = string.IsNullOrEmpty(region.regionName) ? $"Region {regionIndex}" : region.regionName;

            if (region.regionPrefab == null)
            {
                LogError($"{regionName} 缺少 regionPrefab。", ref errorCount, config);
                continue;
            }

            MapRegionLayout regionLayout = region.regionPrefab.GetComponent<MapRegionLayout>();
            if (regionLayout == null)
            {
                LogError($"{regionName} 的 prefab 没有 MapRegionLayout。", ref errorCount, region.regionPrefab);
                continue;
            }

            if (regionLayout.orderedRooms == null || regionLayout.orderedRooms.Count == 0)
            {
                LogError($"{regionName} 没有配置任何房间。", ref errorCount, region.regionPrefab);
                continue;
            }

            UnityEngine.UI.Image rootBackground = region.regionPrefab.GetComponent<UnityEngine.UI.Image>();
            bool hasRootBackground = rootBackground != null && rootBackground.sprite != null;
            if (regionLayout.baseGridRoot == null && !hasRootBackground)
                LogError($"{regionName} 缺少地图底图：请配置根节点 Image，或指定 baseGridRoot。", ref errorCount, region.regionPrefab);

            if (regionLayout.passedGridRevealLayer == null)
                LogError($"{regionName} 缺少已走过格纹层 passedGridRevealLayer。", ref errorCount, region.regionPrefab);
            else if (!regionLayout.passedGridRevealLayer.IsConfigured)
                LogError($"{regionName} 的已走过格纹层缺少 Shader 或覆盖 Image 引用。", ref errorCount, regionLayout.passedGridRevealLayer);

            if (regionLayout.passedRouteRevealLayer == null)
                LogError($"{regionName} 缺少已走过路线层 passedRouteRevealLayer。", ref errorCount, region.regionPrefab);
            else if (!regionLayout.passedRouteRevealLayer.IsConfigured)
                LogError($"{regionName} 的已走过路线层缺少 Shader 或路线 Image 引用。", ref errorCount, regionLayout.passedRouteRevealLayer);

            ValidateRegionRooms(config, regionName, regionLayout, usedRoomTypes, usedNodeTypes, ref errorCount, ref warningCount);
        }

        if (config.presentationCatalog != null)
            ValidateCatalogCoverage(config.presentationCatalog, usedRoomTypes, usedNodeTypes, ref errorCount, ref warningCount);

        Debug.Log($"地图配置检查完成：{errorCount} 个错误，{warningCount} 个警告。", config);
    }

    private static void ValidateRegionRooms(
        BoardMapConfigSO config,
        string regionName,
        MapRegionLayout regionLayout,
        HashSet<GameEnums.RoomType> usedRoomTypes,
        HashSet<GameEnums.BoardNodeType> usedNodeTypes,
        ref int errorCount,
        ref int warningCount)
    {
        MapRoomLayout lastRoom = null;

        for (int roomIndex = 0; roomIndex < regionLayout.orderedRooms.Count; roomIndex++)
        {
            MapRoomLayout room = regionLayout.orderedRooms[roomIndex];
            if (room == null)
            {
                LogError($"{regionName} 的 orderedRooms[{roomIndex}] 为空。", ref errorCount, regionLayout);
                continue;
            }

            lastRoom = room;
            string roomLabel = $"{regionName}/{room.name}";

            if (room.roomData == null)
                LogError($"{roomLabel} 缺少 roomData。", ref errorCount, room);
            else
            {
                usedRoomTypes.Add(room.roomData.roomType);
                if (roomIndex == 0 && room.roomData.roomType != GameEnums.RoomType.Start)
                    LogWarning($"{regionName} 的第一个房间不是 Start。若这是章节起点，建议使用 StartRoomSO。", ref warningCount, room);
            }

            if (room.roomNodes == null || room.roomNodes.Count == 0)
            {
                LogError($"{roomLabel} 没有配置 roomNodes。", ref errorCount, room);
                continue;
            }

            ValidateRoomNodes(config, roomLabel, room, usedNodeTypes, ref errorCount);

            if (room.nextRooms != null)
            {
                foreach (MapRoomLayout nextRoom in room.nextRooms)
                {
                    if (nextRoom == null)
                        LogError($"{roomLabel} 的 nextRooms 中存在空引用。", ref errorCount, room);
                }
            }
        }

        if (lastRoom != null && (lastRoom.roomData == null || lastRoom.roomData.roomType != GameEnums.RoomType.Boss))
            LogWarning($"{regionName} 的最后一个房间不是 Boss。按当前设计，每个章节建议由 Boss 收尾。", ref warningCount, regionLayout);
    }

    private static void ValidateRoomNodes(
        BoardMapConfigSO config,
        string roomLabel,
        MapRoomLayout room,
        HashSet<GameEnums.BoardNodeType> usedNodeTypes,
        ref int errorCount)
    {
        for (int nodeIndex = 0; nodeIndex < room.roomNodes.Count; nodeIndex++)
        {
            MapNodeAnchor node = room.roomNodes[nodeIndex];
            if (node == null)
            {
                LogError($"{roomLabel} 的 roomNodes[{nodeIndex}] 为空。", ref errorCount, room);
                continue;
            }

            if (node.nodeType != GameEnums.BoardNodeType.空)
                usedNodeTypes.Add(node.nodeType);

            if (node.nodeType == GameEnums.BoardNodeType.锻造)
            {
                if (node.forgeBonusType == GameEnums.BoardNodeType.空 || node.forgeBonusType == GameEnums.BoardNodeType.锻造)
                    LogError($"{roomLabel}/{node.name} 是锻造节点，但 forgeBonusType 未配置为有效额外效果。", ref errorCount, node);
                else
                    usedNodeTypes.Add(node.forgeBonusType);
            }

            if (node.baseIconImage == null
                || node.frameImage == null
                || node.effectIconImage == null
                || node.valueText == null
                || node.passedBackgroundImage == null)
            {
                LogError($"{roomLabel}/{node.name} 的节点 UI 引用不完整（主图标、边框、效果图标、数值文本和已抵达背景均需配置）。", ref errorCount, node);
            }

            bool isStartNode = room.roomData != null && room.roomData.roomType == GameEnums.RoomType.Start;
            if (!isStartNode && string.IsNullOrWhiteSpace(node.passedBackgroundSpriteName))
                LogError($"{roomLabel}/{node.name} 未填写节点已抵达背景图片名。", ref errorCount, node);
            else if (!string.IsNullOrWhiteSpace(node.passedBackgroundSpriteName)
                && config.GetNodePassedBackground(node.passedBackgroundSpriteName) == null)
                LogError($"{roomLabel}/{node.name} 找不到节点背景 Sprite：{node.passedBackgroundSpriteName}。", ref errorCount, node);
        }
    }

    private static void ValidateCatalogCoverage(
        MapPresentationCatalogSO catalog,
        HashSet<GameEnums.RoomType> usedRoomTypes,
        HashSet<GameEnums.BoardNodeType> usedNodeTypes,
        ref int errorCount,
        ref int warningCount)
    {
        foreach (GameEnums.RoomType roomType in usedRoomTypes)
        {
            MapPresentationCatalogSO.RoomIconEntry roomEntry = catalog.FindRoomIconEntry(roomType);
            if (roomEntry == null)
            {
                LogError($"PresentationCatalog 缺少房间类型 {roomType} 的表现配置。", ref errorCount, catalog);
                continue;
            }

            if (roomEntry.incompleteSprite == null)
                LogError($"PresentationCatalog 房间类型 {roomType} 缺少未完成贴图 incompleteSprite。", ref errorCount, catalog);
            if (roomEntry.completedSprite == null)
                LogError($"PresentationCatalog 房间类型 {roomType} 缺少已完成贴图 completedSprite。", ref errorCount, catalog);
        }

        foreach (GameEnums.BoardNodeType nodeType in usedNodeTypes)
        {
            if (nodeType == GameEnums.BoardNodeType.空 || nodeType == GameEnums.BoardNodeType.锻造) continue;

            if (catalog.FindNodeEffectEntry(nodeType) == null)
                LogError($"PresentationCatalog 缺少节点效果 {nodeType} 的配置。", ref errorCount, catalog);
        }
    }

    private static void LogError(string message, ref int count, Object context)
    {
        count++;
        Debug.LogError(message, context);
    }

    private static void LogWarning(string message, ref int count, Object context)
    {
        count++;
        Debug.LogWarning(message, context);
    }
}

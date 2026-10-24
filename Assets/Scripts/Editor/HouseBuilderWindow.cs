#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace JRPG.EditorTools
{
    /// <summary>
    /// Menu: Tools > Lentera > House Builder
    /// Builds rectangular houses from modular straight, door, and optional window wall pieces.
    /// </summary>
    public class HouseBuilderWindow : EditorWindow
    {
        private enum WallPivot { Center, LeftEnd }
        private enum FloorPivot { Center, Corner }
        private enum Axis { X, Z }
        private enum TileRidge { Auto, X, Z }

        private GameObject wallStraight;
        private GameObject wallDoor;
        private GameObject wallWindow;
        private GameObject floorTile;

        private GameObject roofTile;
        private bool buildRoof = true;
        private Axis houseRidge = Axis.X;
        private TileRidge tileRidge = TileRidge.Auto;
        private string roofNote = "";

        private WallPivot wallPivot = WallPivot.Center;
        private FloorPivot floorPivot = FloorPivot.Center;

        private float cellSize = 4f;
        private float floorHeight = 3f;
        private float yawOffset = 0f;

        private int width = 3;
        private int depth = 3;
        private int floors = 1;
        private int doorIndex = 1;

        private bool buildFloor = true;
        private string houseName = "House";
        private string measureNote = "";

        [MenuItem("Tools/Lentera/House Builder")]
        private static void Open()
        {
            GetWindow<HouseBuilderWindow>("House Builder");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Pieces", EditorStyles.boldLabel);
            wallStraight = (GameObject)EditorGUILayout.ObjectField("Wall (straight)", wallStraight, typeof(GameObject), false);
            wallDoor = (GameObject)EditorGUILayout.ObjectField("Wall (door, optional)", wallDoor, typeof(GameObject), false);
            wallWindow = (GameObject)EditorGUILayout.ObjectField("Wall (window, optional)", wallWindow, typeof(GameObject), false);
            floorTile = (GameObject)EditorGUILayout.ObjectField("Floor tile (optional)", floorTile, typeof(GameObject), false);
            roofTile = (GameObject)EditorGUILayout.ObjectField("Roof tile (optional)", roofTile, typeof(GameObject), false);
            if (wallWindow != null)
            {
                EditorGUILayout.HelpBox(
                    "The window wall is used for every non-door wall segment on all four sides.",
                    MessageType.Info);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Measurements", EditorStyles.boldLabel);
            cellSize = EditorGUILayout.FloatField("Cell size (wall width)", cellSize);
            floorHeight = EditorGUILayout.FloatField("Floor height (wall height)", floorHeight);

            using (new EditorGUI.DisabledScope(wallStraight == null))
            {
                if (GUILayout.Button("Measure from straight wall"))
                {
                    MeasureWall();
                }
            }

            if (!string.IsNullOrEmpty(measureNote))
            {
                EditorGUILayout.HelpBox(measureNote, MessageType.Info);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Orientation", EditorStyles.boldLabel);
            wallPivot = (WallPivot)EditorGUILayout.EnumPopup("Wall pivot", wallPivot);
            floorPivot = (FloorPivot)EditorGUILayout.EnumPopup("Floor pivot", floorPivot);
            yawOffset = EditorGUILayout.FloatField("Yaw offset (0/90/180/270)", yawOffset);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("House", EditorStyles.boldLabel);
            houseName = EditorGUILayout.TextField("Name", houseName);
            width = Mathf.Max(1, EditorGUILayout.IntField("Width (cells, front/back)", width));
            depth = Mathf.Max(1, EditorGUILayout.IntField("Depth (cells, left/right)", depth));
            floors = Mathf.Max(1, EditorGUILayout.IntField("Floors", floors));
            doorIndex = Mathf.Clamp(EditorGUILayout.IntField("Door cell on front (0-based)", doorIndex), 0, width - 1);
            buildFloor = EditorGUILayout.Toggle("Build floor tiles", buildFloor);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Roof", EditorStyles.boldLabel);
            buildRoof = EditorGUILayout.Toggle("Build roof", buildRoof);
            houseRidge = (Axis)EditorGUILayout.EnumPopup("Ridge runs along (house)", houseRidge);
            tileRidge = (TileRidge)EditorGUILayout.EnumPopup("Ridge axis (roof model)", tileRidge);
            EditorGUILayout.HelpBox(
                "The roof tile is repeated across both house dimensions to cover the full footprint.",
                MessageType.None);

            using (new EditorGUI.DisabledScope(roofTile == null))
            {
                if (GUILayout.Button("Measure roof"))
                {
                    MeasureRoof();
                }

                if (GUILayout.Button("Fit house size to roof"))
                {
                    FitHouseToRoof();
                }
            }

            if (!string.IsNullOrEmpty(roofNote))
            {
                EditorGUILayout.HelpBox(roofNote, MessageType.Info);
            }

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(wallStraight == null))
            {
                if (GUILayout.Button("Build House"))
                {
                    Build(false);
                }

                if (GUILayout.Button("Build House + Save Prefab"))
                {
                    Build(true);
                }
            }

            if (wallStraight == null)
            {
                EditorGUILayout.HelpBox("Assign at least the straight wall piece.", MessageType.Warning);
            }
        }

        private void MeasureWall()
        {
            Bounds b;
            if (!TryGetBounds(wallStraight, out b))
            {
                measureNote = "Could not find any Renderer in that piece.";
                return;
            }

            bool widthAlongX = b.size.x >= b.size.z;
            cellSize = widthAlongX ? b.size.x : b.size.z;
            floorHeight = b.size.y;

            string pivotInfo;
            if (widthAlongX)
            {
                if (Mathf.Abs(b.center.x) < cellSize * 0.1f)
                {
                    wallPivot = WallPivot.Center;
                    pivotInfo = "Pivot looks centered.";
                }
                else if (Mathf.Abs(b.min.x) < cellSize * 0.1f)
                {
                    wallPivot = WallPivot.LeftEnd;
                    pivotInfo = "Pivot looks like the left end.";
                }
                else
                {
                    pivotInfo = "Pivot is unusual, check the result and adjust.";
                }
            }
            else
            {
                pivotInfo = "Wall width runs along Z, so try Yaw offset 90 or 270.";
            }

            measureNote = "Size " + b.size.x.ToString("0.##") + " x " + b.size.y.ToString("0.##") + " x "
                          + b.size.z.ToString("0.##") + ". " + pivotInfo;
        }

        private static bool TryGetBounds(GameObject prefab, out Bounds bounds)
        {
            bounds = new Bounds(Vector3.zero, Vector3.zero);
            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (inst == null)
            {
                return false;
            }

            // Keep the prefab's own root rotation (FBX models from Blender are often
            // baked with a -90 X rotation to stand upright). Only reset the position.
            inst.transform.position = Vector3.zero;
            Renderer[] renderers = inst.GetComponentsInChildren<Renderer>();
            bool found = false;
            foreach (Renderer r in renderers)
            {
                if (!found)
                {
                    bounds = r.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }

            DestroyImmediate(inst);
            return found;
        }

        private void Build(bool savePrefab)
        {
            GameObject root = new GameObject(houseName + "_" + width + "x" + depth + "x" + floors);
            Undo.RegisterCreatedObjectUndo(root, "Build House");

            float c = cellSize;

            for (int f = 0; f < floors; f++)
            {
                float y = f * floorHeight;

                if (buildFloor && floorTile != null)
                {
                    for (int x = 0; x < width; x++)
                    {
                        for (int z = 0; z < depth; z++)
                        {
                            Vector3 pos = floorPivot == FloorPivot.Center
                                ? new Vector3((x + 0.5f) * c, y, (z + 0.5f) * c)
                                : new Vector3(x * c, y, z * c);
                            Place(root.transform, floorTile, pos, Quaternion.identity);
                        }
                    }
                }

                // Front edge (z = depth): outward +Z, yaw 0
                for (int i = 0; i < width; i++)
                {
                    GameObject piece = GetWallPiece(f == 0 && i == doorIndex);
                    PlaceWall(root.transform, piece, new Vector3((i + 0.5f) * c, y, depth * c), 0f);
                }

                // Back edge (z = 0): outward -Z, yaw 180
                for (int i = 0; i < width; i++)
                {
                    PlaceWall(root.transform, GetWallPiece(false), new Vector3((i + 0.5f) * c, y, 0f), 180f);
                }

                // Right edge (x = width): outward +X, yaw 90
                for (int i = 0; i < depth; i++)
                {
                    PlaceWall(root.transform, GetWallPiece(false), new Vector3(width * c, y, (i + 0.5f) * c), 90f);
                }

                // Left edge (x = 0): outward -X, yaw 270
                for (int i = 0; i < depth; i++)
                {
                    PlaceWall(root.transform, GetWallPiece(false), new Vector3(0f, y, (i + 0.5f) * c), 270f);
                }
            }

            if (buildRoof && roofTile != null)
            {
                BuildRoof(root.transform, floors * floorHeight);
            }

            if (savePrefab)
            {
                string folder = "Assets/_Project/Prefabs";
                System.IO.Directory.CreateDirectory(Application.dataPath + "/_Project/Prefabs");
                AssetDatabase.Refresh();
                string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + root.name + ".prefab");
                PrefabUtility.SaveAsPrefabAssetAndConnect(root, path, InteractionMode.UserAction);
            }

            Selection.activeGameObject = root;
        }

        private GameObject GetWallPiece(bool isDoorSlot)
        {
            if (isDoorSlot && wallDoor != null)
            {
                return wallDoor;
            }

            return wallWindow != null ? wallWindow : wallStraight;
        }

        private bool TileRidgeIsX(Bounds tb)
        {
            if (tileRidge == TileRidge.X)
            {
                return true;
            }

            if (tileRidge == TileRidge.Z)
            {
                return false;
            }

            // Auto: a gable roof section is normally longer along its ridge than its span.
            return tb.size.x >= tb.size.z;
        }

        private void MeasureRoof()
        {
            Bounds tb;
            if (!TryGetBounds(roofTile, out tb))
            {
                roofNote = "Could not find any Renderer in the roof piece.";
                return;
            }

            bool ridgeX = TileRidgeIsX(tb);
            float ridgeLen = ridgeX ? tb.size.x : tb.size.z;
            float span = ridgeX ? tb.size.z : tb.size.x;

            roofNote = "Size " + tb.size.x.ToString("0.##") + " x " + tb.size.y.ToString("0.##") + " x "
                       + tb.size.z.ToString("0.##") + ". Ridge length " + ridgeLen.ToString("0.##")
                       + " (" + (ridgeLen / cellSize).ToString("0.#") + " cells), span "
                       + span.ToString("0.##") + " (" + (span / cellSize).ToString("0.#")
                       + " cells). The house should be about that many cells across the ridge.";
        }

        private void FitHouseToRoof()
        {
            Bounds tb;
            if (!TryGetBounds(roofTile, out tb))
            {
                roofNote = "Could not find any Renderer in the roof piece.";
                return;
            }

            bool ridgeX = TileRidgeIsX(tb);
            float tileLen = ridgeX ? tb.size.x : tb.size.z;
            float span = ridgeX ? tb.size.z : tb.size.x;

            int spanCells = Mathf.Max(1, Mathf.RoundToInt(span / cellSize));
            float currentRidgeLen = (houseRidge == Axis.X ? width : depth) * cellSize;
            int tiles = Mathf.Max(1, Mathf.RoundToInt(currentRidgeLen / tileLen));
            int ridgeCells = Mathf.Max(1, Mathf.RoundToInt(tiles * tileLen / cellSize));

            if (houseRidge == Axis.X)
            {
                width = ridgeCells;
                depth = spanCells;
            }
            else
            {
                depth = ridgeCells;
                width = spanCells;
            }

            doorIndex = Mathf.Clamp(doorIndex, 0, width - 1);
            roofNote = "House set to " + width + " x " + depth + " cells to match the roof.";
        }

        private void BuildRoof(Transform parent, float wallTopY)
        {
            Bounds tb;
            if (!TryGetBounds(roofTile, out tb))
            {
                return;
            }

            bool houseX = houseRidge == Axis.X;
            bool tileX = TileRidgeIsX(tb);
            float yaw = (tileX == houseX) ? 0f : 90f;
            Quaternion tileRotation = Quaternion.Euler(0f, yaw, 0f);
            Vector3 rotatedSize = Quaternion.Euler(0f, yaw, 0f) * new Vector3(tb.size.x, 0f, tb.size.z);
            float tileWidth = Mathf.Abs(rotatedSize.x);
            float tileDepth = Mathf.Abs(rotatedSize.z);
            float houseWidth = width * cellSize;
            float houseDepth = depth * cellSize;
            int columns = Mathf.Max(1, Mathf.CeilToInt(houseWidth / Mathf.Max(tileWidth, 0.01f)));
            int rows = Mathf.Max(1, Mathf.CeilToInt(houseDepth / Mathf.Max(tileDepth, 0.01f)));

            for (int x = 0; x < columns; x++)
            {
                for (int z = 0; z < rows; z++)
                {
                    Vector3 targetCenter = new Vector3(
                        (x + 0.5f) * houseWidth / columns,
                        wallTopY,
                        (z + 0.5f) * houseDepth / rows);

                    GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(roofTile, parent);
                    Quaternion baseRot = go.transform.rotation;
                    go.transform.SetPositionAndRotation(Vector3.zero, tileRotation * baseRot);

                    Bounds b;
                    if (TryGetInstanceBounds(go, out b))
                    {
                        Vector3 delta = new Vector3(
                            targetCenter.x - b.center.x,
                            wallTopY - b.min.y,
                            targetCenter.z - b.center.z);
                        go.transform.position += delta;
                    }

                    Undo.RegisterCreatedObjectUndo(go, "Build House");
                }
            }

            roofNote = "Roof coverage: " + columns + " x " + rows + " tiles.";
        }

        private static bool TryGetInstanceBounds(GameObject inst, out Bounds bounds)
        {
            bounds = new Bounds(inst.transform.position, Vector3.zero);
            bool found = false;
            foreach (Renderer r in inst.GetComponentsInChildren<Renderer>())
            {
                if (!found)
                {
                    bounds = r.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }

            return found;
        }

        private void PlaceWall(Transform parent, GameObject prefab, Vector3 center, float yaw)
        {
            Quaternion rot = Quaternion.Euler(0f, yaw + yawOffset, 0f);
            Vector3 pos = center;

            if (wallPivot == WallPivot.LeftEnd)
            {
                pos -= rot * Vector3.right * (cellSize * 0.5f);
            }

            Place(parent, prefab, pos, rot);
        }

        private static void Place(Transform parent, GameObject prefab, Vector3 pos, Quaternion rot)
        {
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);

            // Compose our rotation with the prefab's built-in root rotation instead of
            // replacing it, otherwise Z-up FBX pieces end up lying flat on the ground.
            Quaternion baseRot = go.transform.rotation;
            go.transform.SetPositionAndRotation(pos, rot * baseRot);
            Undo.RegisterCreatedObjectUndo(go, "Build House");
        }
    }
}
#endif
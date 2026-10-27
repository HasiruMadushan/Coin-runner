using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

public static class CoinRunnerBuilder
{
    const string ScenePath = "Assets/Scenes/SampleScene.unity";
    const int GroundLayer = 6;

    static readonly string[] Folders = { "Scenes", "Scripts", "Prefabs", "Tiles", "Palettes", "Sprites" };
    static readonly string[] Tags = { "Coin", "Hazard", "Goal" };
    static readonly string[] KeepRoots = { "Main Camera", "Global Light 2D" };

    static readonly Vector3[] CoinPositions =
    {
        new Vector3(3, 0), new Vector3(4, 0),
        new Vector3(7.5f, -2),
        new Vector3(13, 2), new Vector3(14, 2),
        new Vector3(18, -3),
    };
    static readonly Vector3[] SpikePositions = { new Vector3(11, -3), new Vector3(16.5f, -3) };

    [MenuItem("Coin Runner/Build Level")]
    public static void BuildLevel()
    {
        foreach (string folder in Folders)
        {
            if (!AssetDatabase.IsValidFolder("Assets/" + folder)) AssetDatabase.CreateFolder("Assets", folder);
        }
        SetActiveInputHandlingBoth();
        SetupTagsAndLayers();

        Scene scene = File.Exists(ScenePath)
            ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
            : EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (System.Array.IndexOf(KeepRoots, root.name) < 0) Object.DestroyImmediate(root);
        }

        Sprite square = MakeSprite("Assets/Tiles/GroundSprite.png", 4, 4, 4, FilterMode.Point, (x, y) => true);
        Sprite capsule = MakeSprite("Assets/Sprites/Capsule.png", 128, 256, 128, FilterMode.Bilinear, InCapsule);
        Sprite circle = MakeSprite("Assets/Sprites/Circle.png", 128, 128, 128, FilterMode.Bilinear,
            (x, y) => new Vector2(x + 0.5f - 64, y + 0.5f - 64).magnitude <= 64);
        Sprite triangle = MakeSprite("Assets/Sprites/Triangle.png", 128, 128, 128, FilterMode.Bilinear,
            (x, y) => Mathf.Abs(x + 0.5f - 64) <= 64 * (1 - (y + 0.5f) / 128f));
        Sprite whiteSquare = MakeSprite("Assets/Sprites/Square.png", 4, 4, 4, FilterMode.Point, (x, y) => true);

        Camera cam = SetupCamera();

        Tile groundTile = LoadOrCreate("Assets/Tiles/GroundSprite.asset", () => ScriptableObject.CreateInstance<Tile>());
        groundTile.sprite = square;
        EditorUtility.SetDirty(groundTile);
        CreatePalette(groundTile);

        var grid = new GameObject("Grid", typeof(Grid));
        var ground = new GameObject("Ground", typeof(Tilemap), typeof(TilemapRenderer), typeof(TilemapCollider2D));
        ground.transform.SetParent(grid.transform, false);
        ground.layer = GroundLayer;
        Tilemap tilemap = ground.GetComponent<Tilemap>();
        tilemap.color = new Color(0.55f, 0.38f, 0.2f);
        Paint(tilemap, groundTile, -10, 6, -5, -4);
        Paint(tilemap, groundTile, 9, 22, -5, -4);
        Paint(tilemap, groundTile, 2, 5, -1, -1);
        Paint(tilemap, groundTile, 12, 15, 1, 1);
        Paint(tilemap, groundTile, -10, -10, -3, 2);

        PhysicsMaterial2D noFriction = LoadOrCreate("Assets/Tiles/NoFriction.physicsMaterial2D", () => new PhysicsMaterial2D());
        noFriction.friction = 0f;
        noFriction.bounciness = 0f;
        EditorUtility.SetDirty(noFriction);

        GameObject player = NewSprite("Player", capsule, new Color(0.2f, 0.75f, 0.3f), new Vector3(-6, -2, 0), 2);
        var capsuleCollider = player.AddComponent<CapsuleCollider2D>();
        capsuleCollider.direction = CapsuleDirection2D.Vertical;
        capsuleCollider.size = new Vector2(1f, 2f);
        capsuleCollider.sharedMaterial = noFriction;
        var rb = player.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        var groundCheck = new GameObject("GroundCheck");
        groundCheck.transform.SetParent(player.transform, false);
        groundCheck.transform.localPosition = new Vector3(0, -1, 0);

        GameObject eye = NewSprite("Eye", circle, new Color(0.1f, 0.1f, 0.1f), Vector3.zero, 3);
        eye.transform.SetParent(player.transform, false);
        eye.transform.localPosition = new Vector3(0.25f, 0.5f, 0);
        eye.transform.localScale = new Vector3(0.25f, 0.25f, 1);

        var movement = player.AddComponent<PlayerMovement>();
        movement.groundCheck = groundCheck.transform;
        movement.groundLayer = 1 << GroundLayer;
        movement.eye = eye.transform;
        player.AddComponent<PlayerInteractions>();

        var follow = cam.GetComponent<CameraFollow>();
        if (follow == null) follow = cam.gameObject.AddComponent<CameraFollow>();
        follow.target = player.transform;
        cam.transform.position = player.transform.position + follow.offset;

        GameObject coinTemplate = NewSprite("Coin", circle, new Color(1f, 0.85f, 0.1f), Vector3.zero, 1);
        coinTemplate.transform.localScale = new Vector3(0.5f, 0.5f, 1);
        var coinCollider = coinTemplate.AddComponent<CircleCollider2D>();
        coinCollider.radius = 0.5f;
        coinCollider.isTrigger = true;
        coinTemplate.tag = "Coin";
        GameObject coinPrefab = SavePrefab(coinTemplate, "Assets/Prefabs/Coin.prefab");
        foreach (Vector3 pos in CoinPositions) Place(coinPrefab, scene, pos);

        GameObject spikeTemplate = NewSprite("Spike", triangle, new Color(0.9f, 0.15f, 0.15f), Vector3.zero, 1);
        var spikeCollider = spikeTemplate.AddComponent<PolygonCollider2D>();
        spikeCollider.pathCount = 1;
        spikeCollider.SetPath(0, new[] { new Vector2(-0.5f, -0.5f), new Vector2(0.5f, -0.5f), new Vector2(0f, 0.5f) });
        spikeCollider.isTrigger = false;
        spikeTemplate.tag = "Hazard";
        GameObject spikePrefab = SavePrefab(spikeTemplate, "Assets/Prefabs/Spike.prefab");
        foreach (Vector3 pos in SpikePositions) Place(spikePrefab, scene, pos);

        GameObject goal = NewSprite("Goal", whiteSquare, Color.white, new Vector3(21, -2, 0), 1);
        goal.transform.localScale = new Vector3(0.3f, 2f, 1f);
        goal.AddComponent<BoxCollider2D>().isTrigger = true;
        goal.tag = "Goal";

        BuildUI(out TextMeshProUGUI coinText, out GameObject winPanel, out Button restartButton);
        var gm = new GameObject("GameManager").AddComponent<GameManager>();
        gm.coinText = coinText;
        gm.winPanel = winPanel;
        UnityEventTools.AddPersistentListener(restartButton.onClick, new UnityAction(gm.RestartGame));

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuild();
        AssetDatabase.SaveAssets();
        Debug.Log("CoinRunnerBuilder: level built and saved to " + ScenePath);
    }

    static void SetActiveInputHandlingBoth()
    {
        Object settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0];
        var so = new SerializedObject(settings);
        SerializedProperty prop = so.FindProperty("activeInputHandler");
        if (prop != null && prop.intValue != 2)
        {
            prop.intValue = 2;
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.LogWarning("CoinRunnerBuilder: Active Input Handling set to Both - restart the Editor.");
        }
    }

    static void SetupTagsAndLayers()
    {
        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty tags = tagManager.FindProperty("tags");
        foreach (string tag in Tags)
        {
            bool found = false;
            for (int i = 0; i < tags.arraySize; i++)
            {
                if (tags.GetArrayElementAtIndex(i).stringValue == tag) found = true;
            }
            if (!found)
            {
                tags.InsertArrayElementAtIndex(tags.arraySize);
                tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
            }
        }
        tagManager.FindProperty("layers").GetArrayElementAtIndex(GroundLayer).stringValue = "Ground";
        tagManager.ApplyModifiedPropertiesWithoutUndo();
    }

    static void AddSceneToBuild()
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        int index = scenes.FindIndex(s => s.path == ScenePath);
        if (index >= 0) scenes[index].enabled = true;
        else scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    static bool InCapsule(int x, int y)
    {
        Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
        float r = 64f;
        if (p.y >= r && p.y <= 256 - r) return Mathf.Abs(p.x - r) <= r;
        Vector2 centre = new Vector2(r, p.y < r ? r : 256 - r);
        return (p - centre).magnitude <= r;
    }

    static Sprite MakeSprite(string path, int width, int height, int pixelsPerUnit, FilterMode filter,
        System.Func<int, int, bool> inside)
    {
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        var pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                pixels[y * width + x] = inside(x, y) ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
        tex.SetPixels32(pixels);
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = pixelsPerUnit;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = filter;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static T LoadOrCreate<T>(string path, System.Func<T> create) where T : Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        asset = create();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    static void CreatePalette(Tile tile)
    {
        const string palettePath = "Assets/Palettes/LevelPalette.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(palettePath) == null)
        {
            GridPaletteUtility.CreateNewPalette("Assets/Palettes", "LevelPalette", GridLayout.CellLayout.Rectangle,
                GridPalette.CellSizing.Automatic, Vector3.one, GridLayout.CellSwizzle.XYZ);
        }
        GameObject root = PrefabUtility.LoadPrefabContents(palettePath);
        Tilemap paletteMap = root.GetComponentInChildren<Tilemap>();
        if (paletteMap != null) paletteMap.SetTile(Vector3Int.zero, tile);
        PrefabUtility.SaveAsPrefabAsset(root, palettePath);
        PrefabUtility.UnloadPrefabContents(root);
    }

    static Camera SetupCamera()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            go.tag = "MainCamera";
            cam = go.GetComponent<Camera>();
        }
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.62f, 0.84f, 0.98f);
        return cam;
    }

    static void Paint(Tilemap tilemap, TileBase tile, int xMin, int xMax, int yMin, int yMax)
    {
        for (int x = xMin; x <= xMax; x++)
            for (int y = yMin; y <= yMax; y++)
                tilemap.SetTile(new Vector3Int(x, y, 0), tile);
    }

    static GameObject NewSprite(string name, Sprite sprite, Color color, Vector3 position, int sortingOrder)
    {
        var go = new GameObject(name);
        go.transform.position = position;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = sortingOrder;
        return go;
    }

    static GameObject SavePrefab(GameObject template, string path)
    {
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(template, path);
        Object.DestroyImmediate(template);
        return prefab;
    }

    static void Place(GameObject prefab, Scene scene, Vector3 position)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        go.transform.position = position;
    }

    static void BuildUI(out TextMeshProUGUI coinText, out GameObject winPanel, out Button restartButton)
    {
        var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.layer = LayerMask.NameToLayer("UI");
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        coinText = NewText("CoinText", canvasGo.transform, "Coins: 0 / 6", 50, new Color(0.15f, 0.15f, 0.2f),
            TextAlignmentOptions.TopLeft);
        SetRect(coinText.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(30, -20), new Vector2(500, 80));

        winPanel = new GameObject("WinPanel", typeof(RectTransform), typeof(Image));
        winPanel.transform.SetParent(canvasGo.transform, false);
        var panelRect = winPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        var panelImage = winPanel.GetComponent<Image>();
        panelImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        panelImage.type = Image.Type.Sliced;
        panelImage.color = new Color(0.08f, 0.08f, 0.12f, 200f / 255f);

        var title = NewText("LevelCompleteText", winPanel.transform, "LEVEL COMPLETE!", 110, Color.white,
            TextAlignmentOptions.Center);
        SetRect(title.rectTransform, Center, Center, Center, new Vector2(0, 120), new Vector2(1400, 200));

        var buttonGo = new GameObject("RestartButton", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonGo.transform.SetParent(winPanel.transform, false);
        SetRect(buttonGo.GetComponent<RectTransform>(), Center, Center, Center, new Vector2(0, -100), new Vector2(400, 100));
        var buttonImage = buttonGo.GetComponent<Image>();
        buttonImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        buttonImage.type = Image.Type.Sliced;
        restartButton = buttonGo.GetComponent<Button>();
        restartButton.targetGraphic = buttonImage;

        var label = NewText("Text (TMP)", buttonGo.transform, "Play Again", 50, new Color(0.15f, 0.15f, 0.2f),
            TextAlignmentOptions.Center);
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
    }

    static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

    static TextMeshProUGUI NewText(string name, Transform parent, string text, float size, Color color,
        TextAlignmentOptions alignment)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null) tmp.font = TMP_Settings.defaultFontAsset;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = alignment;
        return tmp;
    }

    static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}

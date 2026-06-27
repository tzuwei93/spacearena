using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Unity.Profiling;
#endif
using UnityEngine.UI;

public sealed class SpaceArenaBootstrap : MonoBehaviour
{
    private const float RoundLength = 30f;
    private const float StartSafeRadius = 7.6f;
    private const float EndSafeRadius = 3.2f;
    private const int FinalBossWave = 10;
    private const int ArenaRingSegments = 96;
    private const float RingRadiusUpdateThreshold = 0.015f;
    private const float DuelSpawnX = 3.65f;
    private const float MinFighterDistance = 1.45f;
    private const float CombatLaneY = 0f;
    private const float FirePoseHold = 0.72f;
    private const float AnimationFps = 4f;
    private const float SkillPoseHold = 0.78f;
    private const float JumpDuration = 0.95f;
    private const float JumpHeight = 1.15f;
    private const float JumpCooldown = 0.45f;
    private const float EnemyJumpChancePerSecond = 0.18f;
    private const float LaneWallPadding = 0.75f;
    private const float BeamDuration = 0.12f;

    private static SpaceArenaBootstrap instance;
    private static readonly Dictionary<string, Sprite> SpriteCache = new Dictionary<string, Sprite>();
    private static readonly Dictionary<string, Sprite[]> AnimationCache = new Dictionary<string, Sprite[]>();
    private static readonly AlienDefinition[] PlayerAliens =
    {
        new AlienDefinition("green", "Green Striker", "Balanced starter"),
        new AlienDefinition("blue", "Blue Volt", "Fast tempo"),
        new AlienDefinition("red", "Red Raider", "Aggressive duelist"),
        new AlienDefinition("armor", "Armor Guard", "Heavy defense"),
        new AlienDefinition("gray", "Gray Comet", "Mobile fighter"),
        new AlienDefinition("darkgray", "Darkgray Shade", "Steady shooter"),
        new AlienDefinition("predator", "Predator Mask", "Long-range focus")
    };
    private static readonly Vector3[] RingUnitCircle = new Vector3[ArenaRingSegments];
    private static bool ringUnitCircleInitialized;
    private static Sprite sharedCircleSprite;
    private static Material sharedArenaLineMaterial;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private static readonly ProfilerMarker FighterTickMarker = new ProfilerMarker("SpaceArena.FighterTicks");
    private static readonly ProfilerMarker ProjectileTickMarker = new ProfilerMarker("SpaceArena.ProjectileTicks");
    private static readonly ProfilerMarker ArenaRingMarker = new ProfilerMarker("SpaceArena.ArenaRingUpdate");
    private static readonly ProfilerMarker StatusMarker = new ProfilerMarker("SpaceArena.StatusUpdate");
#endif

    private readonly List<Fighter> fighters = new List<Fighter>();
    private readonly List<Projectile> projectiles = new List<Projectile>();
    private readonly List<Beam> beams = new List<Beam>();
    private readonly Stack<Fighter> fighterPool = new Stack<Fighter>();
    private readonly Stack<Projectile> projectilePool = new Stack<Projectile>();
    private readonly Stack<Beam> beamPool = new Stack<Beam>();
    private readonly List<LineRenderer> arenaRings = new List<LineRenderer>();
    private readonly List<Button> alienButtons = new List<Button>();
    private readonly List<Button> loadoutButtons = new List<Button>();
    private readonly List<Button> attackModeButtons = new List<Button>();

    private Camera mainCamera;
    private Canvas canvas;
    private Text statusText;
    private Text resourceText;
    private Text feedText;
    private Text deckAlienNameText;
    private Text deckAlienRoleText;
    private Image deckAlienPreview;
    private Image statusBanner;
    private Button jumpButton;
    private Button turnButton;
    private GameObject deckPanel;
    private GameObject augmentPanel;
    private GameObject arenaRoot;

    private int selectedBuild;
    private int selectedAlienIndex;
    private AttackMode selectedAttackMode = AttackMode.Projectile;
    private AttackMode enemyAttackMode = AttackMode.Projectile;
    private int wave = 1;
    private int scrap;
    private int rerolls = 1;
    private int playerAugments;
    private float roundTimer;
    private float safeRadius;
    private float deckPreviewTimer;
    private int deckPreviewFrame;
    private bool matchRunning;
    private bool choosingAugment;
    private bool matchFinished;
    private System.Random rng;
    private EnemyWaveProfile currentWaveProfile;
    private string cachedStatusValue = string.Empty;
    private string cachedResourceValue = string.Empty;
    private int cachedStatusSeconds = int.MinValue;
    private int cachedStatusRadiusTenths = int.MinValue;
    private int cachedStatusWave = int.MinValue;
    private int cachedPlayerHp = int.MinValue;
    private int cachedEnemyHp = int.MinValue;
    private string cachedStatusProfileTitle = string.Empty;
    private float renderedSafeRadius = -1f;

    private TeamStats playerStats;
    private TeamStats enemyStats;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RuntimeBoot()
    {
        if (FindAnyObjectByType<SpaceArenaBootstrap>() != null)
        {
            return;
        }

        GameObject boot = new GameObject("SpaceArenaBootstrap");
        DontDestroyOnLoad(boot);
        boot.AddComponent<SpaceArenaBootstrap>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        rng = new System.Random(DateTime.UtcNow.Millisecond);
        Application.targetFrameRate = 60;
        BuildCamera();
        BuildArena();
        BuildUi();
        ShowDeckBuilder();
    }

    private void Update()
    {
        if (deckPanel != null && !matchRunning)
        {
            HandleDeckInput();
            UpdateDeckPreview(Time.deltaTime);
        }

        if (!matchRunning || choosingAugment || matchFinished)
        {
            return;
        }

        roundTimer += Time.deltaTime;
        safeRadius = Mathf.Lerp(StartSafeRadius, EndSafeRadius, Mathf.Clamp01(roundTimer / RoundLength));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        using (ArenaRingMarker.Auto())
        {
            UpdateArenaRings();
        }
#else
        UpdateArenaRings();
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        using (FighterTickMarker.Auto())
        {
#endif
        for (int i = fighters.Count - 1; i >= 0; i--)
        {
            Fighter fighter = fighters[i];
            if (fighter == null || !fighter.IsAlive)
            {
                RecycleFighter(fighter);
                fighters.RemoveAt(i);
                continue;
            }

            fighter.Tick(Time.deltaTime, fighters, safeRadius);
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        }
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        using (ProjectileTickMarker.Auto())
        {
#endif
        for (int i = projectiles.Count - 1; i >= 0; i--)
        {
            Projectile projectile = projectiles[i];
            if (projectile == null || !projectile.Tick(Time.deltaTime, fighters))
            {
                RecycleProjectile(projectile);
                projectiles.RemoveAt(i);
            }
        }
        for (int i = beams.Count - 1; i >= 0; i--)
        {
            Beam beam = beams[i];
            if (beam == null || !beam.Tick(Time.deltaTime))
            {
                RecycleBeam(beam);
                beams.RemoveAt(i);
            }
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        }
#endif

        if (!HasLivingTeam(0))
        {
            FinishMatch(false);
            return;
        }

        if (!HasLivingTeam(1))
        {
            HandleWaveWin("Wave cleared.");
            return;
        }

        if (roundTimer >= RoundLength)
        {
            ResolveSuddenDecision();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        using (StatusMarker.Auto())
        {
            UpdateStatus();
        }
#else
        UpdateStatus();
#endif
    }

    internal Projectile SpawnProjectile(Vector3 position, Vector3 direction, int team, float damage, float speed, float radius, Color color)
    {
        Projectile projectile = projectilePool.Count > 0 ? projectilePool.Pop() : CreateProjectile();
        GameObject go = projectile.gameObject;
        go.name = "Plasma Bolt";
        go.SetActive(true);
        go.transform.position = position;
        go.transform.localScale = Vector3.one * radius;

        SpriteRenderer renderer = projectile.Renderer;
        renderer.sprite = GetCircleSprite();
        renderer.color = color;
        renderer.sortingOrder = 6;
        projectile.Init(team, direction, damage, speed, StartSafeRadius - LaneWallPadding);
        projectiles.Add(projectile);
        return projectile;
    }

    internal void SpawnEyeLaser(Vector3 position, int facingSign, int team, float damage, Color color, List<Fighter> activeFighters)
    {
        Beam beam = beamPool.Count > 0 ? beamPool.Pop() : CreateBeam();
        beam.gameObject.SetActive(true);

        float wallX = facingSign > 0 ? StartSafeRadius - LaneWallPadding : -StartSafeRadius + LaneWallPadding;
        Vector3 end = new Vector3(wallX, position.y, position.z);
        beam.Init(position + new Vector3(facingSign * 0.28f, 0.24f, 0f), end, color, BeamDuration);
        beams.Add(beam);

        Fighter hitTarget = null;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < activeFighters.Count; i++)
        {
            Fighter fighter = activeFighters[i];
            if (fighter == null || !fighter.IsAlive || fighter.Team == team)
            {
                continue;
            }

            float offset = fighter.transform.position.x - position.x;
            if (Mathf.Sign(offset) != facingSign)
            {
                continue;
            }

            float verticalDelta = Mathf.Abs(fighter.transform.position.y - position.y);
            float distance = Mathf.Abs(offset);
            if (verticalDelta <= 0.85f && distance < bestDistance)
            {
                bestDistance = distance;
                hitTarget = fighter;
            }
        }

        if (hitTarget != null)
        {
            hitTarget.TakeDamage(damage, null);
        }
    }

    private void BuildCamera()
    {
        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        mainCamera = cameraObject.AddComponent<Camera>();
        mainCamera.orthographic = true;
        mainCamera.orthographicSize = 6.25f;
        mainCamera.clearFlags = CameraClearFlags.SolidColor;
        mainCamera.backgroundColor = new Color(0.015f, 0.02f, 0.05f);
        cameraObject.transform.position = new Vector3(0f, 0f, -10f);
    }

    private void BuildArena()
    {
        arenaRoot = new GameObject("Arena");
        safeRadius = StartSafeRadius;
        if (!ringUnitCircleInitialized)
        {
            for (int i = 0; i < RingUnitCircle.Length; i++)
            {
                float t = i / (float)RingUnitCircle.Length * Mathf.PI * 2f;
                RingUnitCircle[i] = new Vector3(Mathf.Cos(t), Mathf.Sin(t), 0f);
            }
            ringUnitCircleInitialized = true;
        }
        AddStarfield();
        AddArenaRing("Outer Rim", StartSafeRadius, new Color(0.25f, 0.9f, 1f, 0.42f), 0.08f);
        AddArenaRing("Death Zone", safeRadius, new Color(1f, 0.2f, 0.32f, 0.86f), 0.12f);
        UpdateArenaRings();
    }

    private void BuildUi()
    {
        EnsureEventSystem();

        GameObject canvasObject = new GameObject("HUD");
        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject bannerObject = AddPanel("Combat Banner", canvas.transform, new Vector2(0.5f, 1f), new Vector2(760f, 66f));
        statusBanner = bannerObject.GetComponent<Image>();
        statusBanner.color = new Color(0.02f, 0.035f, 0.08f, 0.86f);
        bannerObject.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -10f);
        statusText = AddText("Status", bannerObject.transform, Vector2.zero, Vector2.one, Vector2.zero, 18, TextAnchor.MiddleCenter);
        statusText.GetComponent<RectTransform>().sizeDelta = new Vector2(-24f, -8f);
        resourceText = AddText("Resources", canvas.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -18f), 16, TextAnchor.UpperLeft);
        feedText = AddText("Feed", canvas.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 22f), 17, TextAnchor.LowerCenter);
        turnButton = AddButton("Turn", canvas.transform, new Vector2(1f, 0f), new Vector2(106f, 46f), new Vector2(-150f, 24f), RequestPlayerTurn);
        turnButton.gameObject.SetActive(false);
        jumpButton = AddButton("Jump", canvas.transform, new Vector2(1f, 0f), new Vector2(116f, 46f), new Vector2(-24f, 24f), RequestPlayerJump);
        jumpButton.gameObject.SetActive(false);
    }

    private static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<StandaloneInputModule>();
    }

    private void ShowDeckBuilder()
    {
        matchRunning = false;
        matchFinished = false;
        choosingAugment = false;
        currentWaveProfile = null;
        SetJumpButtonVisible(false);
        ClearCombat();
        alienButtons.Clear();
        loadoutButtons.Clear();
        attackModeButtons.Clear();
        if (deckPanel != null)
        {
            Destroy(deckPanel);
        }

        deckPanel = AddPanel("Deck Builder", canvas.transform, new Vector2(0.5f, 0.5f), new Vector2(820f, 500f));
        AddText("Title", deckPanel.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), 28, TextAnchor.UpperCenter, "SPACE ARENA");
        AddText("Subtitle", deckPanel.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -68f), 15, TextAnchor.UpperCenter, "Build your duelist, then enter the arena.");

        AddText("CombatStyleTitle", deckPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-250f, 126f), 16, TextAnchor.MiddleLeft, "Combat Style");
        AddLoadoutButton("Plasma Swarm", "Fast fire", 0, 80f);
        AddLoadoutButton("Titan Lancers", "Armor break", 1, 30f);
        AddLoadoutButton("Comet Riders", "Dash burst", 2, -20f);
        AddText("AttackModeTitle", deckPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-250f, -72f), 16, TextAnchor.MiddleLeft, "Attack Mode");
        AddAttackModeButton("Bullets", "Wall range", AttackMode.Projectile, -102f);
        AddAttackModeButton("Eye Laser", "Instant beam", AttackMode.EyeLaser, -152f);

        AddText("AlienTitle", deckPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(190f, 126f), 16, TextAnchor.MiddleCenter, "Alien");
        CreateAlienPreview();
        AddButton("<", deckPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(42f, 42f), new Vector2(26f, 28f), () => ChangeSelectedAlien(-1));
        AddButton(">", deckPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(42f, 42f), new Vector2(354f, 28f), () => ChangeSelectedAlien(1));
        AddButton("Start Match", deckPanel.transform, new Vector2(0.5f, 0f), new Vector2(190f, 42f), new Vector2(248f, 34f), StartMatch);
        selectedBuild = 0;
        selectedAlienIndex = Mathf.Clamp(selectedAlienIndex, 0, PlayerAliens.Length - 1);
        RefreshLoadoutSelection();
        RefreshAttackModeSelection();
        RefreshAlienSelection();
        statusText.text = string.Empty;
        resourceText.text = string.Empty;
        feedText.text = "Crazy Arena-inspired auto-battle prototype";
    }

    private void AddLoadoutButton(string title, string body, int buildIndex, float y)
    {
        Button button = AddButton(title + "\n" + body, deckPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(260f, 42f), new Vector2(-250f, y), () =>
        {
            selectedBuild = buildIndex;
            RefreshLoadoutSelection();
            feedText.text = "Selected: " + title;
        });
        loadoutButtons.Add(button);
    }

    private void AddAttackModeButton(string title, string body, AttackMode mode, float y)
    {
        Button button = AddButton(title + "\n" + body, deckPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(260f, 42f), new Vector2(-250f, y), () =>
        {
            selectedAttackMode = mode;
            RefreshAttackModeSelection();
            feedText.text = "Attack mode: " + GetAttackModeLabel(mode);
        });
        attackModeButtons.Add(button);
    }

    private void AddAlienSelectorButtons()
    {
        alienButtons.Clear();
        for (int i = 0; i < PlayerAliens.Length; i++)
        {
            int index = i;
            AlienDefinition alien = PlayerAliens[i];
            float x = -300f + (i % 4) * 200f;
            float y = i < 4 ? -122f : -176f;
            Button button = AddButton(alien.Name + "\n" + alien.Role, deckPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(178f, 46f), new Vector2(x, y), () =>
            {
                selectedAlienIndex = index;
                RefreshAlienSelection();
            });
            alienButtons.Add(button);
        }
    }

    private void CreateAlienPreview()
    {
        GameObject previewObject = new GameObject("Alien Preview");
        previewObject.transform.SetParent(deckPanel.transform, false);
        deckAlienPreview = previewObject.AddComponent<Image>();
        deckAlienPreview.preserveAspect = true;
        deckAlienPreview.color = Color.white;
        RectTransform previewRect = deckAlienPreview.GetComponent<RectTransform>();
        previewRect.anchorMin = new Vector2(0.5f, 0.5f);
        previewRect.anchorMax = new Vector2(0.5f, 0.5f);
        previewRect.pivot = new Vector2(0.5f, 0.5f);
        previewRect.sizeDelta = new Vector2(230f, 230f);
        previewRect.anchoredPosition = new Vector2(190f, 24f);

        deckAlienNameText = AddText("Alien Name", deckPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(190f, -116f), 21, TextAnchor.MiddleCenter);
        deckAlienNameText.GetComponent<RectTransform>().sizeDelta = new Vector2(300f, 34f);
        deckAlienRoleText = AddText("Alien Role", deckPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(190f, -150f), 15, TextAnchor.MiddleCenter);
        deckAlienRoleText.GetComponent<RectTransform>().sizeDelta = new Vector2(300f, 34f);
    }

    private void RefreshLoadoutSelection()
    {
        for (int i = 0; i < loadoutButtons.Count; i++)
        {
            Image image = loadoutButtons[i].GetComponent<Image>();
            if (image != null)
            {
                image.color = i == selectedBuild ? new Color(0.08f, 0.5f, 0.64f, 0.98f) : new Color(0.055f, 0.11f, 0.18f, 0.96f);
            }
        }
    }

    private void RefreshAttackModeSelection()
    {
        for (int i = 0; i < attackModeButtons.Count; i++)
        {
            Image image = attackModeButtons[i].GetComponent<Image>();
            if (image != null)
            {
                AttackMode mode = i == 0 ? AttackMode.Projectile : AttackMode.EyeLaser;
                image.color = mode == selectedAttackMode ? new Color(0.08f, 0.5f, 0.64f, 0.98f) : new Color(0.055f, 0.11f, 0.18f, 0.96f);
            }
        }
    }

    private static string GetAttackModeLabel(AttackMode mode)
    {
        return mode == AttackMode.EyeLaser ? "Eye Laser" : "Bullets";
    }

    private void RefreshAlienSelection()
    {
        for (int i = 0; i < alienButtons.Count; i++)
        {
            Image image = alienButtons[i].GetComponent<Image>();
            if (image != null)
            {
                image.color = i == selectedAlienIndex ? new Color(0.05f, 0.58f, 0.78f, 0.98f) : new Color(0.1f, 0.28f, 0.42f, 0.96f);
            }
        }

        AlienDefinition alien = PlayerAliens[Mathf.Clamp(selectedAlienIndex, 0, PlayerAliens.Length - 1)];
        deckPreviewTimer = 0f;
        deckPreviewFrame = 0;
        if (deckAlienNameText != null)
        {
            deckAlienNameText.text = alien.Name;
        }
        if (deckAlienRoleText != null)
        {
            deckAlienRoleText.text = alien.Role;
        }
        UpdateDeckPreview(0f);
        feedText.text = "Selected alien: " + alien.Name;
    }

    private void ChangeSelectedAlien(int direction)
    {
        selectedAlienIndex = (selectedAlienIndex + direction + PlayerAliens.Length) % PlayerAliens.Length;
        RefreshAlienSelection();
    }

    private void HandleDeckInput()
    {
        float scroll = Input.mouseScrollDelta.y;
        if (scroll > 0.05f)
        {
            ChangeSelectedAlien(-1);
        }
        else if (scroll < -0.05f)
        {
            ChangeSelectedAlien(1);
        }
    }

    private void UpdateDeckPreview(float dt)
    {
        if (deckAlienPreview == null || PlayerAliens.Length == 0)
        {
            return;
        }

        AlienDefinition alien = PlayerAliens[Mathf.Clamp(selectedAlienIndex, 0, PlayerAliens.Length - 1)];
        Sprite[] frames = LoadAlienAnimation(alien.Skin, FighterPose.Fire);
        if (frames.Length == 0)
        {
            return;
        }

        deckPreviewTimer += dt;
        float frameDuration = 1f / Mathf.Max(1f, AnimationFps);
        while (deckPreviewTimer >= frameDuration)
        {
            deckPreviewTimer -= frameDuration;
            deckPreviewFrame = (deckPreviewFrame + 1) % frames.Length;
        }

        deckAlienPreview.sprite = frames[Mathf.Clamp(deckPreviewFrame, 0, frames.Length - 1)];
    }

    private void StartMatch()
    {
        if (deckPanel != null)
        {
            Destroy(deckPanel);
        }

        wave = 1;
        scrap = 0;
        rerolls = 1;
        playerAugments = 0;
        playerStats = TeamStats.ForBuild(selectedBuild);
        enemyStats = TeamStats.EnemyBaseline();
        BeginWave();
    }

    private void BeginWave()
    {
        ClearCombat();
        roundTimer = 0f;
        safeRadius = StartSafeRadius;
        renderedSafeRadius = -1f;
        choosingAugment = false;
        matchRunning = true;
        matchFinished = false;
        currentWaveProfile = BuildEnemyWaveProfile();
        enemyAttackMode = rng.Next(2) == 0 ? AttackMode.Projectile : AttackMode.EyeLaser;

        SpawnDuelist(0, playerStats, -DuelSpawnX, GetPlayerDuelistSkin(), selectedAttackMode);
        SpawnDuelist(1, currentWaveProfile.TeamStats, DuelSpawnX, GetEnemyDuelistSkin(), enemyAttackMode, GetEnemyDuelistName());
        SetJumpButtonVisible(true);
        feedText.text = currentWaveProfile.IntroText + " Enemy uses " + GetAttackModeLabel(enemyAttackMode) + ".";
        UpdateStatus();
    }

    private void SpawnDuelist(int team, TeamStats stats, float x, string skin, AttackMode attackMode, string displayName = null)
    {
        TeamStats fighterStats = stats;
        if (team == 1 && currentWaveProfile != null && currentWaveProfile.IsBossWave)
        {
            fighterStats = stats.AsBoss(currentWaveProfile.BossName);
        }

        Fighter fighter = CreateFighter(team, skin, new Vector3(x, 0f, 0f), fighterStats, 0, attackMode, displayName);
        fighters.Add(fighter);
    }

    private string GetPlayerDuelistSkin()
    {
        return PlayerAliens[Mathf.Clamp(selectedAlienIndex, 0, PlayerAliens.Length - 1)].Skin;
    }

    private string GetEnemyDuelistSkin()
    {
        if (currentWaveProfile == null || currentWaveProfile.Skins == null || currentWaveProfile.Skins.Length == 0)
        {
            return "red";
        }

        int index = currentWaveProfile.IsBossWave && currentWaveProfile.Skins.Length > 1 ? 1 : 0;
        return currentWaveProfile.Skins[Mathf.Clamp(index, 0, currentWaveProfile.Skins.Length - 1)];
    }

    private string GetEnemyDuelistName()
    {
        return currentWaveProfile != null && currentWaveProfile.IsBossWave ? currentWaveProfile.BossName : null;
    }

    private Fighter CreateFighter(int team, string skin, Vector3 position, TeamStats stats, int slot, AttackMode attackMode, string displayName = null)
    {
        Fighter fighter;
        SpriteRenderer renderer;
        GameObject go;
        if (fighterPool.Count > 0)
        {
            fighter = fighterPool.Pop();
            go = fighter.gameObject;
            renderer = fighter.Renderer;
        }
        else
        {
            go = new GameObject("Fighter");
            renderer = go.AddComponent<SpriteRenderer>();
            fighter = go.AddComponent<Fighter>();
            fighter.BindRenderer(renderer);
        }

        go.name = displayName ?? ((team == 0 ? "Player " : "Enemy ") + skin);
        go.SetActive(true);
        go.transform.position = position;
        renderer.sortingOrder = 5;
        go.transform.localScale = Vector3.one * stats.VisualScale;
        renderer.color = stats.SpriteTint;

        fighter.Init(this, team, skin, stats, slot, attackMode);
        return fighter;
    }

    private void BeginAugmentChoice()
    {
        choosingAugment = true;
        matchRunning = false;
        currentWaveProfile = null;
        SetJumpButtonVisible(false);
        ClearCombat();

        if (augmentPanel != null)
        {
            Destroy(augmentPanel);
        }

        augmentPanel = AddPanel("Augments", canvas.transform, new Vector2(0.5f, 0.5f), new Vector2(720f, 360f));
        AddText("AugmentTitle", augmentPanel.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -26f), 24, TextAnchor.UpperCenter, "Choose a mutation");

        List<AugmentOption> choices = RollAugments();
        for (int i = 0; i < choices.Count; i++)
        {
            AugmentOption option = choices[i];
            float x = -230f + i * 230f;
            AddButton(option.Title + "\n" + option.Description, augmentPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(205f, 146f), new Vector2(x, 14f), () =>
            {
                option.Apply(playerStats);
                playerAugments++;
                enemyStats.RandomEnemyGrowth(rng);
                Destroy(augmentPanel);
                BeginWave();
            });
        }

        Button rerollButton = AddButton("Reroll (" + rerolls + ")", augmentPanel.transform, new Vector2(0.5f, 0f), new Vector2(160f, 42f), new Vector2(-90f, 32f), () =>
        {
            if (rerolls <= 0 && scrap < 10)
            {
                feedText.text = "Need 10 scrap for another reroll.";
                return;
            }
            if (rerolls > 0)
            {
                rerolls--;
            }
            else
            {
                scrap -= 10;
            }
            Destroy(augmentPanel);
            BeginAugmentChoice();
        });
        rerollButton.interactable = rerolls > 0 || scrap >= 10;
        AddButton("Hold Formation", augmentPanel.transform, new Vector2(0.5f, 0f), new Vector2(180f, 42f), new Vector2(110f, 32f), () =>
        {
            playerStats.MaxHealth += 8f;
            playerStats.HealOnWave += 2f;
            enemyStats.RandomEnemyGrowth(rng);
            Destroy(augmentPanel);
            BeginWave();
        });
        UpdateStatus();
    }

    private List<AugmentOption> RollAugments()
    {
        List<AugmentOption> pool = new List<AugmentOption>
        {
            new AugmentOption("Twin Comets", "Projectile count +1\nDamage -10%", s => { s.ProjectileCount += 1; s.Damage *= 0.9f; }),
            new AugmentOption("Overclock Core", "Fire rate +26%\nHeat damage +8%", s => { s.FireRate *= 1.26f; s.Damage *= 1.08f; }),
            new AugmentOption("Ion Lances", "Range +20%\nProjectile speed +15%", s => { s.Range *= 1.2f; s.ProjectileSpeed *= 1.15f; }),
            new AugmentOption("Aegis Hull", "Max health +28\nArmor +2", s => { s.MaxHealth += 28f; s.Armor += 2f; }),
            new AugmentOption("Rider Spurs", "Move speed +18%\nCharge lasts longer", s => { s.MoveSpeed *= 1.18f; s.ChargeDuration += 0.7f; }),
            new AugmentOption("Vampire Battery", "Heal after each hit\nLower max health", s => { s.LifeSteal += 0.18f; s.MaxHealth -= 8f; }),
            new AugmentOption("Singularity Rounds", "Damage +32%\nFire rate -12%", s => { s.Damage *= 1.32f; s.FireRate *= 0.88f; }),
            new AugmentOption("Emergency Clones", "Add squad endurance", s => { s.MaxHealth += 16f; s.HealOnWave += 9f; })
        };

        List<AugmentOption> result = new List<AugmentOption>();
        while (result.Count < 3 && pool.Count > 0)
        {
            int index = rng.Next(pool.Count);
            result.Add(pool[index]);
            pool.RemoveAt(index);
        }
        return result;
    }

    private void ResolveSuddenDecision()
    {
        float playerHealth = TeamHealth(0);
        float enemyHealth = TeamHealth(1);
        if (playerHealth >= enemyHealth)
        {
            HandleWaveWin("Judges favor your squad.");
        }
        else
        {
            FinishMatch(false);
        }
    }

    private void HandleWaveWin(string prefix)
    {
        scrap += currentWaveProfile != null ? currentWaveProfile.ScrapReward : 12 + wave * 4;
        if (currentWaveProfile != null && currentWaveProfile.IsBossWave && wave >= FinalBossWave)
        {
            FinishMatch(true);
            return;
        }

        wave++;
        feedText.text = prefix + " Choose a mutation.";
        BeginAugmentChoice();
    }

    private void FinishMatch(bool victory)
    {
        matchRunning = false;
        matchFinished = true;
        currentWaveProfile = null;
        SetJumpButtonVisible(false);
        ClearCombat();
        feedText.text = victory ? "Arena conquered." : "Squad wiped. The arena keeps the scrap.";

        GameObject panel = AddPanel("Results", canvas.transform, new Vector2(0.5f, 0.5f), new Vector2(520f, 250f));
        AddText("ResultTitle", panel.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -32f), 25, TextAnchor.UpperCenter, victory ? "VICTORY" : "DEFEAT");
        int clearedWaves = victory ? wave : Mathf.Max(0, wave - 1);
        AddText("ResultBody", panel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), 17, TextAnchor.MiddleCenter, "Waves cleared: " + clearedWaves + "\nMutations drafted: " + playerAugments + "\nScrap banked: " + scrap);
        AddButton("Back to Deck", panel.transform, new Vector2(0.5f, 0f), new Vector2(180f, 42f), new Vector2(0f, 32f), () =>
        {
            Destroy(panel);
            ShowDeckBuilder();
        });
        UpdateStatus();
    }

    private bool HasLivingTeam(int team)
    {
        for (int i = 0; i < fighters.Count; i++)
        {
            if (fighters[i] != null && fighters[i].IsAlive && fighters[i].Team == team)
            {
                return true;
            }
        }
        return false;
    }

    private float TeamHealth(int team)
    {
        float total = 0f;
        for (int i = 0; i < fighters.Count; i++)
        {
            if (fighters[i] != null && fighters[i].IsAlive && fighters[i].Team == team)
            {
                total += fighters[i].Health;
            }
        }
        return total;
    }

    private float TeamMaxHealth(int team)
    {
        float total = 0f;
        for (int i = 0; i < fighters.Count; i++)
        {
            if (fighters[i] != null && fighters[i].IsAlive && fighters[i].Team == team)
            {
                total += fighters[i].MaxHealth;
            }
        }
        return total;
    }

    private void ClearCombat()
    {
        for (int i = 0; i < fighters.Count; i++)
        {
            if (fighters[i] != null)
            {
                RecycleFighter(fighters[i]);
            }
        }
        fighters.Clear();

        for (int i = 0; i < projectiles.Count; i++)
        {
            if (projectiles[i] != null)
            {
                RecycleProjectile(projectiles[i]);
            }
        }
        projectiles.Clear();

        for (int i = 0; i < beams.Count; i++)
        {
            if (beams[i] != null)
            {
                RecycleBeam(beams[i]);
            }
        }
        beams.Clear();
    }

    private void SetJumpButtonVisible(bool visible)
    {
        if (jumpButton != null)
        {
            jumpButton.gameObject.SetActive(visible);
            jumpButton.interactable = visible;
        }
        if (turnButton != null)
        {
            turnButton.gameObject.SetActive(visible);
            turnButton.interactable = visible;
        }
    }

    private void RequestPlayerTurn()
    {
        Fighter player = FindLivingPlayerFighter();
        if (player == null)
        {
            return;
        }

        player.ChangeDirection();
        feedText.text = "Turn!";
    }

    private void RequestPlayerJump()
    {
        Fighter player = FindLivingPlayerFighter();
        if (player == null)
        {
            return;
        }

        if (player.RequestJump(FindLivingEnemyFighter()))
        {
            feedText.text = "Jump!";
        }
    }

    private Fighter FindLivingEnemyFighter()
    {
        for (int i = 0; i < fighters.Count; i++)
        {
            Fighter fighter = fighters[i];
            if (fighter != null && fighter.IsAlive && fighter.Team == 1)
            {
                return fighter;
            }
        }
        return null;
    }

    private Fighter FindLivingPlayerFighter()
    {
        for (int i = 0; i < fighters.Count; i++)
        {
            Fighter fighter = fighters[i];
            if (fighter != null && fighter.IsAlive && fighter.Team == 0)
            {
                return fighter;
            }
        }
        return null;
    }

    private void RecycleFighter(Fighter fighter)
    {
        if (fighter == null || fighter.IsPooled)
        {
            return;
        }

        fighter.MarkPooled();
        fighterPool.Push(fighter);
    }

    private void UpdateStatus()
    {
        string profileTitle = currentWaveProfile != null ? currentWaveProfile.Title : string.Empty;
        int secondsRemaining = matchRunning ? Mathf.CeilToInt(Mathf.Max(0f, RoundLength - roundTimer)) : -1;
        int radiusTenths = matchRunning ? Mathf.RoundToInt(safeRadius * 10f) : -1;
        int playerHp = matchRunning ? Mathf.CeilToInt(TeamHealth(0)) : -1;
        int enemyHp = matchRunning ? Mathf.CeilToInt(TeamHealth(1)) : -1;
        int playerMaxHp = matchRunning ? Mathf.CeilToInt(TeamMaxHealth(0)) : -1;
        int enemyMaxHp = matchRunning ? Mathf.CeilToInt(TeamMaxHealth(1)) : -1;
        if (wave != cachedStatusWave || secondsRemaining != cachedStatusSeconds || radiusTenths != cachedStatusRadiusTenths || playerHp != cachedPlayerHp || enemyHp != cachedEnemyHp || profileTitle != cachedStatusProfileTitle)
        {
            string profileLine = profileTitle.Length > 0 ? string.Format("\n{0}", profileTitle) : string.Empty;
            string nextStatus = matchRunning
                ? string.Format("WAVE {0}   YOU {1}/{2} HP   {3}s   ENEMY {4}/{5} HP   RING {6:0.0}{7}", wave, playerHp, playerMaxHp, secondsRemaining, enemyHp, enemyMaxHp, radiusTenths * 0.1f, profileLine)
                : string.Format("Wave {0}", wave);
            cachedStatusWave = wave;
            cachedStatusSeconds = secondsRemaining;
            cachedStatusRadiusTenths = radiusTenths;
            cachedPlayerHp = playerHp;
            cachedEnemyHp = enemyHp;
            cachedStatusProfileTitle = profileTitle;
            cachedStatusValue = nextStatus;
            statusText.text = nextStatus;
        }

        string enemyLine = currentWaveProfile != null ? string.Format("\nEnemy {0}", currentWaveProfile.ShortLabel) : string.Empty;
        string nextResource = string.Format("Scrap {0}\nRerolls {1}\nMutations {2}{3}", scrap, rerolls, playerAugments, enemyLine);
        if (nextResource != cachedResourceValue)
        {
            cachedResourceValue = nextResource;
            resourceText.text = nextResource;
        }
    }

    private EnemyWaveProfile BuildEnemyWaveProfile()
    {
        TeamStats stats = enemyStats.Scaled(1f + wave * 0.09f);
        int faction = (wave - 1) % 4;

        switch (faction)
        {
            case 0:
                stats.MoveSpeed *= 1.16f;
                stats.FireRate *= 1.08f;
                return BuildProfile("Raiders", "Fast flankers with aggressive tempo.", stats, new[] { "red", "darkgray", "predator" }, "Raiders closing fast");
            case 1:
                stats.MaxHealth += 22f;
                stats.Armor += 2f;
                return BuildProfile("Bulwark Line", "Heavy frontliners that soak plasma well.", stats, new[] { "armor", "red", "armor" }, "Bulwark shields locking in");
            case 2:
                stats.Range *= 1.22f;
                stats.ProjectileSpeed *= 1.18f;
                return BuildProfile("Star Snipers", "Long-range pressure from disciplined shooters.", stats, new[] { "predator", "red", "darkgray" }, "Sniper cells lining up shots");
            default:
                stats.LifeSteal += 0.1f;
                stats.Damage *= 1.12f;
                return BuildProfile("Leech Brood", "Sustain-heavy brood that heals through trades.", stats, new[] { "green", "predator", "red" }, "Leech brood feeding on the arena");
        }
    }

    private EnemyWaveProfile BuildProfile(string title, string description, TeamStats stats, string[] skins, string intro)
    {
        bool isBossWave = wave % 5 == 0;
        if (!isBossWave)
        {
            return new EnemyWaveProfile(title, title, description, "Wave " + wave + " launched. " + intro + ".", skins, stats, false, null, 12 + wave * 4);
        }

        string bossName = wave >= FinalBossWave ? "Orbital Warden" : "Arena Champion";
        stats.MaxHealth += 34f;
        stats.Damage *= 1.12f;
        stats.ProjectileCount = Mathf.Clamp(stats.ProjectileCount + 1, 1, 4);
        string bossTitle = title + " Boss Wave";
        string bossDescription = description + " A command unit anchors the formation.";
        string introText = wave >= FinalBossWave
            ? "Final boss wave. " + bossName + " enters with the " + title.ToLowerInvariant() + "."
            : "Boss wave. " + bossName + " enters with the " + title.ToLowerInvariant() + ".";
        return new EnemyWaveProfile(bossTitle, title + " Boss", bossDescription, introText, skins, stats, true, bossName, 24 + wave * 6);
    }

    private Sprite LoadAlienSprite(string skin, FighterPose pose)
    {
        Sprite[] frames = LoadAlienAnimation(skin, pose);
        return frames.Length > 0 ? frames[0] : GetCircleSprite();
    }

    private Sprite[] LoadAlienAnimation(string skin, FighterPose pose)
    {
        string cacheKey = skin + ":" + pose;
        if (AnimationCache.TryGetValue(cacheKey, out Sprite[] cachedFrames))
        {
            return cachedFrames;
        }

        string folder = "Aliens/" + skin;
        string poseToken = GetPoseToken(pose);
        Sprite[] sprites = Resources.LoadAll<Sprite>(folder);
        List<Sprite> frames = new List<Sprite>();
        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] != null && sprites[i].name.IndexOf(poseToken, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                frames.Add(sprites[i]);
            }
        }

        if (frames.Count == 0)
        {
            Texture2D[] textures = Resources.LoadAll<Texture2D>(folder);
            for (int i = 0; i < textures.Length; i++)
            {
                Texture2D texture = textures[i];
                if (texture == null || texture.name.IndexOf(poseToken, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                string spriteKey = folder + "/" + texture.name;
                if (!SpriteCache.TryGetValue(spriteKey, out Sprite sprite))
                {
                    texture.filterMode = FilterMode.Bilinear;
                    sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.2f), 120f);
                    sprite.name = texture.name;
                    SpriteCache[spriteKey] = sprite;
                }
                frames.Add(sprite);
            }
        }

        frames.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        if (frames.Count == 0 && pose != FighterPose.Idle)
        {
            Sprite[] idleFrames = LoadAlienAnimation(skin, FighterPose.Idle);
            AnimationCache[cacheKey] = idleFrames;
            return idleFrames;
        }

        if (frames.Count == 0)
        {
            frames.Add(GetCircleSprite());
        }

        Sprite[] result = frames.ToArray();
        AnimationCache[cacheKey] = result;
        return result;
    }

    private static string GetPoseToken(FighterPose pose)
    {
        switch (pose)
        {
            case FighterPose.Run:
                return "_run_";
            case FighterPose.Fire:
            case FighterPose.Idle:
                return "_fire_";
            case FighterPose.Punch:
                return "_attack_";
            case FighterPose.Jump:
                return "_jump_";
            case FighterPose.Dead:
                return "_dead_";
            default:
                return "_fire_";
        }
    }

    private Sprite LoadAlienSpriteByName(string skin, string frameName)
    {
        string path = "Aliens/" + skin + "/" + frameName;

        if (SpriteCache.TryGetValue(path, out Sprite cached))
        {
            return cached;
        }

        Sprite sprite = Resources.Load<Sprite>(path);
        if (sprite != null)
        {
            SpriteCache[path] = sprite;
            return sprite;
        }

        Texture2D texture = Resources.Load<Texture2D>(path);
        if (texture == null)
        {
            return GetCircleSprite();
        }

        texture.filterMode = FilterMode.Bilinear;
        sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.2f), 120f);
        SpriteCache[path] = sprite;
        return sprite;
    }

    private static Sprite GetCircleSprite()
    {
        if (sharedCircleSprite != null)
        {
            return sharedCircleSprite;
        }

        int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center) / (size * 0.5f);
                float alpha = Mathf.Clamp01(1f - Mathf.Pow(distance, 2.8f));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        texture.Apply();
        sharedCircleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 64f);
        return sharedCircleSprite;
    }

    private void AddStarfield()
    {
        for (int i = 0; i < 90; i++)
        {
            GameObject star = new GameObject("Star");
            star.transform.SetParent(arenaRoot.transform);
            star.transform.position = new Vector3(UnityEngine.Random.Range(-9.5f, 9.5f), UnityEngine.Random.Range(-5.8f, 5.8f), 1f);
            SpriteRenderer renderer = star.AddComponent<SpriteRenderer>();
            float glow = UnityEngine.Random.Range(0.35f, 0.9f);
            renderer.color = new Color(glow, glow, glow + 0.1f, UnityEngine.Random.Range(0.35f, 0.85f));
            renderer.sprite = GetCircleSprite();
            renderer.sortingOrder = 0;
            star.transform.localScale = Vector3.one * UnityEngine.Random.Range(0.018f, 0.05f);
        }
    }

    private void AddArenaRing(string name, float radius, Color color, float width)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(arenaRoot.transform);
        LineRenderer line = go.AddComponent<LineRenderer>();
        line.loop = true;
        line.useWorldSpace = false;
        line.positionCount = ArenaRingSegments;
        line.startWidth = width;
        line.endWidth = width;
        line.material = GetArenaLineMaterial();
        line.startColor = color;
        line.endColor = color;
        arenaRings.Add(line);
        SetRing(line, radius);
    }

    private Projectile CreateProjectile()
    {
        GameObject go = new GameObject("Plasma Bolt");
        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = GetCircleSprite();
        Projectile projectile = go.AddComponent<Projectile>();
        projectile.BindRenderer(renderer);
        go.SetActive(false);
        return projectile;
    }

    private void RecycleProjectile(Projectile projectile)
    {
        if (projectile == null)
        {
            return;
        }
        if (projectile.IsPooled)
        {
            return;
        }

        projectile.MarkPooled();
        projectilePool.Push(projectile);
    }

    private Beam CreateBeam()
    {
        GameObject go = new GameObject("Eye Laser");
        LineRenderer line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.startWidth = 0.08f;
        line.endWidth = 0.02f;
        line.material = GetArenaLineMaterial();
        line.sortingOrder = 7;
        Beam beam = go.AddComponent<Beam>();
        beam.BindLine(line);
        go.SetActive(false);
        return beam;
    }

    private void RecycleBeam(Beam beam)
    {
        if (beam == null || beam.IsPooled)
        {
            return;
        }

        beam.MarkPooled();
        beamPool.Push(beam);
    }

    private static Material GetArenaLineMaterial()
    {
        if (sharedArenaLineMaterial == null)
        {
            sharedArenaLineMaterial = new Material(Shader.Find("Sprites/Default"));
        }

        return sharedArenaLineMaterial;
    }

    private void UpdateArenaRings()
    {
        if (arenaRings.Count > 1 && Mathf.Abs(safeRadius - renderedSafeRadius) >= RingRadiusUpdateThreshold)
        {
            SetRing(arenaRings[1], safeRadius);
            renderedSafeRadius = safeRadius;
        }
    }

    private static void SetRing(LineRenderer line, float radius)
    {
        for (int i = 0; i < line.positionCount; i++)
        {
            line.SetPosition(i, RingUnitCircle[i] * radius);
        }
    }

    private GameObject AddPanel(string name, Transform parent, Vector2 anchor, Vector2 size)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent, false);
        Image image = panel.AddComponent<Image>();
        image.color = new Color(0.03f, 0.045f, 0.09f, 0.92f);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.sizeDelta = size;
        rect.anchoredPosition = Vector2.zero;
        return panel;
    }

    private Text AddText(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, int size, TextAnchor alignment, string value = "")
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Text text = go.AddComponent<Text>();
        text.font = GetBuiltinFont();
        text.fontSize = size;
        text.alignment = alignment;
        text.color = new Color(0.88f, 0.96f, 1f);
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.text = value;

        RectTransform rect = text.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2((anchorMin.x + anchorMax.x) * 0.5f, (anchorMin.y + anchorMax.y) * 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(680f, 92f);
        return text;
    }

    private Button AddButton(string label, Transform parent, Vector2 anchor, Vector2 size, Vector2 position, UnityEngine.Events.UnityAction action)
    {
        GameObject go = new GameObject(label.Split('\n')[0]);
        go.transform.SetParent(parent, false);
        Image image = go.AddComponent<Image>();
        image.color = new Color(0.055f, 0.11f, 0.18f, 0.96f);
        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = image.color;
        colors.highlightedColor = new Color(0.09f, 0.24f, 0.32f, 0.98f);
        colors.pressedColor = new Color(0.04f, 0.42f, 0.55f, 1f);
        colors.selectedColor = new Color(0.08f, 0.5f, 0.64f, 0.98f);
        colors.disabledColor = new Color(0.03f, 0.04f, 0.06f, 0.55f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        button.onClick.AddListener(action);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        Text text = AddText("Label", go.transform, Vector2.zero, Vector2.one, Vector2.zero, size.y > 90f ? 15 : size.x < 60f ? 22 : 15, TextAnchor.MiddleCenter, label);
        text.GetComponent<RectTransform>().sizeDelta = new Vector2(-16f, -10f);
        text.color = Color.white;
        return button;
    }

    private static Font GetBuiltinFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    private enum FighterPose
    {
        Idle,
        Run,
        Fire,
        Punch,
        Jump,
        Dead
    }

    internal enum AttackMode
    {
        Projectile,
        EyeLaser
    }

    private sealed class AlienDefinition
    {
        public readonly string Skin;
        public readonly string Name;
        public readonly string Role;

        public AlienDefinition(string skin, string name, string role)
        {
            Skin = skin;
            Name = name;
            Role = role;
        }
    }

    private sealed class AugmentOption
    {
        public readonly string Title;
        public readonly string Description;
        public readonly Action<TeamStats> Apply;

        public AugmentOption(string title, string description, Action<TeamStats> apply)
        {
            Title = title;
            Description = description;
            Apply = apply;
        }
    }

    private sealed class EnemyWaveProfile
    {
        public readonly string Title;
        public readonly string ShortLabel;
        public readonly string Description;
        public readonly string IntroText;
        public readonly string[] Skins;
        public readonly TeamStats TeamStats;
        public readonly bool IsBossWave;
        public readonly string BossName;
        public readonly int ScrapReward;

        public EnemyWaveProfile(string title, string shortLabel, string description, string introText, string[] skins, TeamStats teamStats, bool isBossWave, string bossName, int scrapReward)
        {
            Title = title;
            ShortLabel = shortLabel;
            Description = description;
            IntroText = introText;
            Skins = skins;
            TeamStats = teamStats;
            IsBossWave = isBossWave;
            BossName = bossName;
            ScrapReward = scrapReward;
        }
    }

    internal sealed class TeamStats
    {
        public float MaxHealth = 120f;
        public float Armor = 1f;
        public float Damage = 6f;
        public float FireRate = 0.72f;
        public float Range = 4.7f;
        public float MoveSpeed = 2.6f;
        public float ProjectileSpeed = 7.5f;
        public float ChargeDuration = 2.1f;
        public float LifeSteal;
        public float HealOnWave;
        public int ProjectileCount = 1;
        public float VisualScale = 0.54f;
        public Color SpriteTint = Color.white;

        public static TeamStats ForBuild(int build)
        {
            TeamStats stats = new TeamStats();
            if (build == 0)
            {
                stats.FireRate = 0.78f;
                stats.ProjectileCount = 2;
                stats.Damage = 4.2f;
                stats.MaxHealth = 116f;
            }
            else if (build == 1)
            {
                stats.MaxHealth = 148f;
                stats.Armor = 4f;
                stats.Damage = 6.8f;
                stats.MoveSpeed = 2.05f;
            }
            else
            {
                stats.MoveSpeed = 3.25f;
                stats.ChargeDuration = 3.2f;
                stats.Damage = 5.8f;
            }
            return stats;
        }

        public static TeamStats EnemyBaseline()
        {
            return new TeamStats { MaxHealth = 125f, Damage = 5.4f, FireRate = 0.68f, MoveSpeed = 2.45f, Range = 4.2f };
        }

        public TeamStats AsBoss(string bossName)
        {
            return new TeamStats
            {
                MaxHealth = MaxHealth * 1.8f,
                Armor = Armor + 3f,
                Damage = Damage * 1.18f,
                FireRate = FireRate * 1.08f,
                Range = Range * 1.08f,
                MoveSpeed = MoveSpeed * 0.94f,
                ProjectileSpeed = ProjectileSpeed * 1.1f,
                ChargeDuration = ChargeDuration + 0.6f,
                LifeSteal = LifeSteal,
                HealOnWave = HealOnWave,
                ProjectileCount = Mathf.Clamp(ProjectileCount + 1, 1, 5),
                VisualScale = 0.72f,
                SpriteTint = bossName == "Orbital Warden" ? new Color(1f, 0.84f, 0.45f) : new Color(1f, 0.74f, 0.74f)
            };
        }

        public TeamStats Scaled(float scale)
        {
            return new TeamStats
            {
                MaxHealth = MaxHealth * scale,
                Armor = Armor + (scale - 1f) * 3f,
                Damage = Damage * (0.82f + scale * 0.08f),
                FireRate = FireRate,
                Range = Range,
                MoveSpeed = MoveSpeed,
                ProjectileSpeed = ProjectileSpeed,
                ChargeDuration = ChargeDuration,
                LifeSteal = LifeSteal,
                HealOnWave = HealOnWave,
                ProjectileCount = ProjectileCount,
                VisualScale = VisualScale,
                SpriteTint = SpriteTint
            };
        }

        public void RandomEnemyGrowth(System.Random random)
        {
            int roll = random.Next(4);
            if (roll == 0) MaxHealth += 14f;
            if (roll == 1) Damage *= 1.1f;
            if (roll == 2) FireRate *= 1.08f;
            if (roll == 3) MoveSpeed *= 1.08f;
        }
    }

    internal sealed class Fighter : MonoBehaviour
    {
        private SpaceArenaBootstrap arena;
        private SpriteRenderer spriteRenderer;
        private TeamStats stats;
        private string skin;
        private Color baseTint;
        private Vector3 baseScale;
        private AttackMode attackMode;
        private float fireCooldown;
        private float localTime;
        private float hitFlash;
        private float poseLockTimer;
        private float maxHealth;
        private float animationTimer;
        private float skillCooldown;
        private float jumpTimer;
        private float jumpCooldown;
        private float jumpStartX;
        private float jumpTargetX;
        private int slot;
        private int animationFrame;
        private int facingSign;
        private FighterPose currentPose;
        private SpriteRenderer markerRenderer;
        private SpriteRenderer hpBackRenderer;
        private SpriteRenderer hpFillRenderer;
        private TextMesh labelMesh;

        public int Team { get; private set; }
        public float Health { get; private set; }
        public float MaxHealth => maxHealth;
        public bool IsPooled { get; private set; }
        public SpriteRenderer Renderer => spriteRenderer;
        public bool IsAlive => Health > 0f;

        public void BindRenderer(SpriteRenderer renderer)
        {
            spriteRenderer = renderer;
        }

        public void Init(SpaceArenaBootstrap owner, int team, string alienSkin, TeamStats teamStats, int squadSlot, AttackMode selectedMode)
        {
            arena = owner;
            Team = team;
            skin = alienSkin;
            stats = teamStats;
            attackMode = selectedMode;
            baseTint = team == 0 ? Color.Lerp(stats.SpriteTint, new Color(0.2f, 0.95f, 1f), 0.22f) : stats.SpriteTint;
            maxHealth = Mathf.Max(20f, stats.MaxHealth + stats.HealOnWave);
            Health = maxHealth;
            slot = squadSlot;
            localTime = 0f;
            hitFlash = 0f;
            poseLockTimer = 0f;
            animationTimer = 0f;
            animationFrame = 0;
            fireCooldown = UnityEngine.Random.Range(0.1f, 0.7f);
            skillCooldown = UnityEngine.Random.Range(2.6f, 4.2f);
            jumpTimer = 0f;
            jumpCooldown = 0f;
            jumpStartX = transform.position.x;
            jumpTargetX = transform.position.x;
            facingSign = Team == 0 ? 1 : -1;
            IsPooled = false;
            baseScale = Vector3.one * stats.VisualScale;
            transform.localScale = baseScale;
            spriteRenderer.color = baseTint;
            EnsureCombatReadouts();
            UpdateCombatReadouts();
            currentPose = (FighterPose)(-1);
            SetPose(FighterPose.Idle);
        }

        public void Tick(float dt, List<Fighter> allFighters, float safeRadius)
        {
            localTime += dt;
            fireCooldown -= dt;
            skillCooldown -= dt;
            jumpCooldown -= dt;
            hitFlash -= dt;
            poseLockTimer -= dt;
            UpdateJump(dt);
            bool airborne = jumpTimer > 0f;

            Fighter target = FindTarget(allFighters);
            if (target == null)
            {
                AnimateBody(false);
                AdvanceAnimation(dt);
                UpdateCombatReadouts();
                return;
            }

            float deltaX = target.transform.position.x - transform.position.x;
            float distance = Mathf.Abs(deltaX);
            Vector3 desired = new Vector3(Mathf.Sign(Mathf.Approximately(deltaX, 0f) ? (Team == 0 ? 1f : -1f) : deltaX), 0f, 0f);
            bool charging = localTime <= stats.ChargeDuration && distance > MinFighterDistance * 1.6f;
            float speed = stats.MoveSpeed * (charging ? 1.85f : 1f);

            if (!airborne)
            {
                MoveOnLane(speed, dt, safeRadius, target);
            }

            float radius = transform.position.magnitude;
            if (!airborne && radius > safeRadius)
            {
                transform.position = new Vector3(Mathf.MoveTowards(transform.position.x, 0f, speed * 1.15f * dt), CombatLaneY, transform.position.z);
                TakeDamage((6f + (radius - safeRadius) * 8f) * dt, null);
            }

            ApplyFacing();
            spriteRenderer.color = hitFlash > 0f ? new Color(1f, 0.45f, 0.45f) : baseTint;
            if (poseLockTimer <= 0f)
            {
                SetPose(distance > stats.Range * 0.65f || charging ? FighterPose.Run : FighterPose.Idle);
            }
            AdvanceAnimation(dt);
            AnimateBody(charging || distance > stats.Range * 0.65f);
            UpdateCombatReadouts();

            if (Team == 1 && jumpTimer <= 0f && jumpCooldown <= 0f && distance > MinFighterDistance * 0.95f && distance < stats.Range * 0.95f && UnityEngine.Random.value < EnemyJumpChancePerSecond * dt)
            {
                RequestJump(target);
            }
            else if (!airborne && IsTargetAhead(target) && distance <= stats.Range && fireCooldown <= 0f)
            {
                FireAt(target, allFighters);
            }
        }

        public void TakeDamage(float amount, Fighter attacker)
        {
            float incoming = jumpTimer > 0f ? amount * 0.45f : amount;
            float mitigated = Mathf.Max(1f, incoming - stats.Armor);
            Health -= mitigated;
            hitFlash = 0.12f;
            if (attacker != null && attacker.stats.LifeSteal > 0f)
            {
                attacker.Health = Mathf.Min(attacker.stats.MaxHealth, attacker.Health + mitigated * attacker.stats.LifeSteal);
            }
            if (Health <= 0f)
            {
                Health = 0f;
                SetPose(FighterPose.Dead);
                UpdateCombatReadouts();
                gameObject.SetActive(false);
            }
        }

        public void MarkPooled()
        {
            Health = 0f;
            IsPooled = true;
            gameObject.SetActive(false);
        }

        public bool RequestJump(Fighter target)
        {
            if (jumpTimer > 0f || jumpCooldown > 0f || !IsAlive)
            {
                return false;
            }

            float direction = facingSign;
            bool targetAhead = target != null && Mathf.Sign(target.transform.position.x - transform.position.x) == facingSign;
            jumpStartX = transform.position.x;
            float targetX = targetAhead ? target.transform.position.x + direction * (MinFighterDistance + 0.55f) : transform.position.x + direction * 2.45f;
            jumpTargetX = Mathf.Clamp(targetX, -StartSafeRadius + 0.8f, StartSafeRadius - 0.8f);
            jumpTimer = JumpDuration;
            jumpCooldown = JumpCooldown;
            poseLockTimer = JumpDuration;
            ApplyFacing();
            SetPose(FighterPose.Jump);
            return true;
        }

        public void ChangeDirection()
        {
            facingSign *= -1;
            ApplyFacing();
        }

        private Fighter FindTarget(List<Fighter> allFighters)
        {
            Fighter best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < allFighters.Count; i++)
            {
                Fighter other = allFighters[i];
                if (other == null || !other.IsAlive || other.Team == Team)
                {
                    continue;
                }
                float distance = Vector3.SqrMagnitude(other.transform.position - transform.position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = other;
                }
            }
            return best;
        }

        private void FireAt(Fighter target, List<Fighter> allFighters)
        {
            fireCooldown = 1f / Mathf.Max(0.25f, stats.FireRate);
            poseLockTimer = FirePoseHold;
            SetPose(FighterPose.Punch);

            Vector3 direction = new Vector3(facingSign, 0f, 0f);
            Color color = Team == 0 ? new Color(0.25f, 0.95f, 1f, 0.95f) : new Color(1f, 0.25f, 0.38f, 0.95f);
            if (attackMode == AttackMode.EyeLaser)
            {
                arena.SpawnEyeLaser(transform.position, facingSign, Team, stats.Damage * 0.82f, color, allFighters);
                return;
            }

            int count = Mathf.Clamp(stats.ProjectileCount, 1, 5);
            for (int i = 0; i < count; i++)
            {
                float angle = count == 1 ? 0f : Mathf.Lerp(-12f, 12f, i / (float)(count - 1));
                Vector3 shotDirection = Quaternion.Euler(0f, 0f, angle) * direction;
                arena.SpawnProjectile(transform.position + shotDirection * 0.45f, shotDirection, Team, stats.Damage, stats.ProjectileSpeed, 0.22f, color);
            }
        }

        private void UseSkill(Fighter target, Vector3 desired, float distance)
        {
            skillCooldown = Mathf.Lerp(6.2f, 8.4f, UnityEngine.Random.value);
            poseLockTimer = SkillPoseHold;
            SetPose(FighterPose.Punch);

            if (skin == "armor")
            {
                Health = Mathf.Min(maxHealth, Health + 8f);
                if (distance <= stats.Range * 0.8f)
                {
                    target.TakeDamage(stats.Damage * 0.45f, this);
                }
                return;
            }

            if (skin == "red" || skin == "blue")
            {
                float rushDistance = Mathf.Max(0f, distance - MinFighterDistance * 1.35f);
                float wallLimit = StartSafeRadius - LaneWallPadding;
                float rushX = transform.position.x + facingSign * Mathf.Min(1.15f, rushDistance);
                rushX = Mathf.Clamp(ApplyBodyBlock(rushX, wallLimit, target), -wallLimit, wallLimit);
                transform.position = new Vector3(rushX, transform.position.y, transform.position.z);
                target.TakeDamage(stats.Damage * (skin == "red" ? 0.85f : 0.7f), this);
                return;
            }

            if (skin == "green" || skin == "darkgray")
            {
                target.TakeDamage(stats.Damage * 0.55f, this);
                Health = Mathf.Min(maxHealth, Health + stats.Damage * 0.16f);
                return;
            }

            Color skillColor = Team == 0 ? new Color(0.55f, 1f, 0.95f, 0.95f) : new Color(1f, 0.55f, 0.2f, 0.95f);
            Vector3 skillDirection = new Vector3(facingSign, 0f, 0f);
            arena.SpawnProjectile(transform.position + skillDirection * 0.45f, skillDirection, Team, stats.Damage * 1.05f, stats.ProjectileSpeed * 1.25f, 0.32f, skillColor);
        }

        private void SetPose(FighterPose pose)
        {
            if (currentPose == pose)
            {
                return;
            }

            currentPose = pose;
            animationFrame = 0;
            animationTimer = 0f;
            Sprite[] frames = arena.LoadAlienAnimation(skin, pose);
            spriteRenderer.sprite = frames.Length > 0 ? frames[0] : arena.LoadAlienSprite(skin, pose);
        }

        private void AdvanceAnimation(float dt)
        {
            Sprite[] frames = arena.LoadAlienAnimation(skin, currentPose);
            if (frames.Length <= 1)
            {
                return;
            }

            animationTimer += dt;
            float frameDuration = 1f / AnimationFps;
            while (animationTimer >= frameDuration)
            {
                animationTimer -= frameDuration;
                animationFrame++;
                if (currentPose == FighterPose.Fire || currentPose == FighterPose.Punch || currentPose == FighterPose.Jump)
                {
                    animationFrame = Mathf.Min(animationFrame, frames.Length - 1);
                }
                else
                {
                    animationFrame %= frames.Length;
                }
                spriteRenderer.sprite = frames[animationFrame];
            }
        }

        private void MoveOnLane(float speed, float dt, float safeRadius, Fighter target)
        {
            float wallLimit = Mathf.Max(EndSafeRadius - LaneWallPadding, safeRadius - LaneWallPadding);
            float nextX = transform.position.x + facingSign * speed * dt;
            if (nextX > wallLimit)
            {
                nextX = wallLimit;
                facingSign = -1;
            }
            else if (nextX < -wallLimit)
            {
                nextX = -wallLimit;
                facingSign = 1;
            }

            nextX = ApplyBodyBlock(nextX, wallLimit, target);
            transform.position = new Vector3(nextX, transform.position.y, transform.position.z);
            ApplyFacing();
        }

        private float ApplyBodyBlock(float nextX, float wallLimit, Fighter target)
        {
            if (target == null || !target.IsAlive || target.jumpTimer > 0f)
            {
                return nextX;
            }

            float targetOffset = target.transform.position.x - transform.position.x;
            if (Mathf.Sign(targetOffset) != facingSign)
            {
                return nextX;
            }

            float blockGap = MinFighterDistance * 0.96f;
            if (facingSign > 0)
            {
                float maxX = target.transform.position.x - blockGap;
                if (nextX > maxX)
                {
                    return Mathf.Clamp(Mathf.Min(transform.position.x, maxX), -wallLimit, wallLimit);
                }
            }
            else
            {
                float minX = target.transform.position.x + blockGap;
                if (nextX < minX)
                {
                    return Mathf.Clamp(Mathf.Max(transform.position.x, minX), -wallLimit, wallLimit);
                }
            }

            return nextX;
        }

        private bool IsTargetAhead(Fighter target)
        {
            if (target == null)
            {
                return false;
            }

            float targetOffset = target.transform.position.x - transform.position.x;
            return Mathf.Sign(targetOffset) == facingSign;
        }

        private void ApplyFacing()
        {
            spriteRenderer.flipX = facingSign < 0;
        }

        private void UpdateJump(float dt)
        {
            if (jumpTimer > 0f)
            {
                jumpTimer = Mathf.Max(0f, jumpTimer - dt);
                float progress = 1f - jumpTimer / JumpDuration;
                float lift = Mathf.Sin(progress * Mathf.PI) * JumpHeight;
                float x = Mathf.SmoothStep(jumpStartX, jumpTargetX, progress);
                transform.position = new Vector3(x, CombatLaneY + lift, transform.position.z);
                return;
            }

            transform.position = new Vector3(transform.position.x, Mathf.MoveTowards(transform.position.y, CombatLaneY, stats.MoveSpeed * 1.4f * dt), transform.position.z);
        }

        private void AnimateBody(bool moving)
        {
            float bob = Mathf.Sin(localTime * (moving ? 12f : 5.5f)) * (moving ? 0.055f : 0.025f);
            float squash = currentPose == FighterPose.Fire || currentPose == FighterPose.Punch ? 0.08f : currentPose == FighterPose.Jump ? -0.04f : hitFlash > 0f ? -0.06f : 0f;
            transform.localScale = new Vector3(baseScale.x * (1f + squash), baseScale.y * (1f - squash + bob), baseScale.z);
        }

        private void EnsureCombatReadouts()
        {
            if (markerRenderer == null)
            {
                markerRenderer = CreateChildSprite("Team Marker", new Vector3(0f, -0.82f, 0.12f), Team == 0 ? new Vector3(1.45f, 0.28f, 1f) : new Vector3(1.05f, 0.18f, 1f), 2);
                hpBackRenderer = CreateChildSprite("HP Back", new Vector3(0f, 1.1f, 0.08f), new Vector3(1.2f, 0.12f, 1f), 8);
                hpFillRenderer = CreateChildSprite("HP Fill", new Vector3(0f, 1.1f, 0.07f), new Vector3(1.14f, 0.07f, 1f), 9);

                GameObject labelObject = new GameObject("Team Label");
                labelObject.transform.SetParent(transform, false);
                labelObject.transform.localPosition = new Vector3(0f, 1.36f, 0f);
                labelObject.transform.localScale = Vector3.one * 0.08f;
                labelMesh = labelObject.AddComponent<TextMesh>();
                labelMesh.anchor = TextAnchor.MiddleCenter;
                labelMesh.alignment = TextAlignment.Center;
                labelMesh.fontSize = 42;
                labelMesh.characterSize = 1f;
                labelMesh.GetComponent<MeshRenderer>().sortingOrder = 10;
            }

            Color teamColor = Team == 0 ? new Color(0.05f, 0.95f, 1f, 0.86f) : new Color(1f, 0.2f, 0.32f, 0.62f);
            markerRenderer.color = teamColor;
            hpBackRenderer.color = new Color(0f, 0f, 0f, 0.72f);
            hpFillRenderer.color = Team == 0 ? new Color(0.1f, 0.95f, 1f, 0.95f) : new Color(1f, 0.25f, 0.32f, 0.95f);
            labelMesh.text = Team == 0 ? "YOU" : "ENEMY";
            labelMesh.color = Team == 0 ? new Color(0.72f, 1f, 1f, 1f) : new Color(1f, 0.72f, 0.72f, 0.95f);
        }

        private SpriteRenderer CreateChildSprite(string childName, Vector3 localPosition, Vector3 localScale, int sortingOrder)
        {
            GameObject child = new GameObject(childName);
            child.transform.SetParent(transform, false);
            child.transform.localPosition = localPosition;
            child.transform.localScale = localScale;
            SpriteRenderer childRenderer = child.AddComponent<SpriteRenderer>();
            childRenderer.sprite = GetCircleSprite();
            childRenderer.sortingOrder = sortingOrder;
            return childRenderer;
        }

        private void UpdateCombatReadouts()
        {
            if (hpFillRenderer == null)
            {
                return;
            }

            float healthRatio = maxHealth > 0f ? Mathf.Clamp01(Health / maxHealth) : 0f;
            hpFillRenderer.transform.localScale = new Vector3(1.14f * healthRatio, 0.07f, 1f);
            hpFillRenderer.transform.localPosition = new Vector3(-0.57f * (1f - healthRatio), 1.1f, 0.07f);
        }
    }

    internal sealed class Projectile : MonoBehaviour
    {
        private int team;
        private Vector3 velocity;
        private float damage;
        private float wallLimit;

        public SpriteRenderer Renderer { get; private set; }
        public bool IsPooled { get; private set; }

        public void BindRenderer(SpriteRenderer spriteRenderer)
        {
            Renderer = spriteRenderer;
        }

        public void Init(int ownerTeam, Vector3 direction, float hitDamage, float speed, float laneWallLimit)
        {
            team = ownerTeam;
            velocity = direction.normalized * speed;
            damage = hitDamage;
            wallLimit = laneWallLimit;
            IsPooled = false;
        }

        public void MarkPooled()
        {
            IsPooled = true;
            gameObject.SetActive(false);
        }

        public bool Tick(float dt, List<Fighter> fighters)
        {
            transform.position += velocity * dt;
            if (Mathf.Abs(transform.position.x) >= wallLimit || Mathf.Abs(transform.position.y) >= StartSafeRadius)
            {
                return false;
            }

            for (int i = 0; i < fighters.Count; i++)
            {
                Fighter fighter = fighters[i];
                if (fighter == null || !fighter.IsAlive || fighter.Team == team)
                {
                    continue;
                }
                if (Vector3.SqrMagnitude(fighter.transform.position - transform.position) <= 0.34f)
                {
                    Fighter attacker = FindAttacker(fighters);
                    fighter.TakeDamage(damage, attacker);
                    return false;
                }
            }
            return true;
        }

        private Fighter FindAttacker(List<Fighter> fighters)
        {
            Fighter nearest = null;
            float best = float.MaxValue;
            for (int i = 0; i < fighters.Count; i++)
            {
                Fighter fighter = fighters[i];
                if (fighter == null || !fighter.IsAlive || fighter.Team != team)
                {
                    continue;
                }
                float distance = Vector3.SqrMagnitude(fighter.transform.position - transform.position);
                if (distance < best)
                {
                    best = distance;
                    nearest = fighter;
                }
            }
            return nearest;
        }
    }

    internal sealed class Beam : MonoBehaviour
    {
        private LineRenderer line;
        private float ttl;

        public bool IsPooled { get; private set; }

        public void BindLine(LineRenderer beamLine)
        {
            line = beamLine;
        }

        public void Init(Vector3 start, Vector3 end, Color color, float lifetime)
        {
            ttl = lifetime;
            IsPooled = false;
            line.startColor = color;
            line.endColor = new Color(color.r, color.g, color.b, 0.08f);
            line.SetPosition(0, start);
            line.SetPosition(1, end);
        }

        public void MarkPooled()
        {
            IsPooled = true;
            gameObject.SetActive(false);
        }

        public bool Tick(float dt)
        {
            ttl -= dt;
            return ttl > 0f;
        }
    }
}

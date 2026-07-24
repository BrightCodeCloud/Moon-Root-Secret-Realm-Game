using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Moonroot;

public partial class Main : Node2D
{
    private const float Width = 640f;
    private const float Height = 360f;
    private const float MusicBaseVolumeDb = -12f;
    private const float MusicSilentVolumeDb = -60f;
    private const float MusicCrossfadeSeconds = 0.8f;
    private static readonly Rect2 Arena = new(38, 48, 564, 280);

    private enum RunState { Title, Playing, Upgrade, Paused, GameOver, Victory }
    private enum EnemyKind { Sprout, Radish, Beetle, Boss }
    private enum PickupKind { MoonDew, SeedPod, Heart }

    private sealed class Enemy
    {
        public EnemyKind Kind;
        public Vector2 Position;
        public Vector2 Velocity;
        public float Health;
        public float MaxHealth;
        public float AttackTimer;
        public float StateTimer;
        public float Flash;
        public float Phase;
        public Vector2 LockedDirection;
        public bool Charging;
    }

    private sealed class Projectile
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Damage;
        public float Life;
        public float Radius;
        public int Pierce;
        public bool Friendly;
        public Color Color;
    }

    private sealed class Plant
    {
        public Vector2 Position;
        public float Growth;
        public float Age;
        public float ShootTimer;
        public float Pulse;
        public bool Mature;
    }

    private sealed class Pickup
    {
        public PickupKind Kind;
        public Vector2 Position;
        public Vector2 Velocity;
        public float Life;
    }

    private sealed class Particle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Life;
        public float MaxLife;
        public float Size;
        public Color Color;
    }

    private sealed class WetPatch
    {
        public Vector2 Position;
        public float Radius;
        public float Life;
    }

    private readonly record struct Upgrade(string Id, string Name, string Description, string Tag);

    private readonly List<Enemy> _enemies = [];
    private readonly List<Projectile> _projectiles = [];
    private readonly List<Plant> _plants = [];
    private readonly List<Pickup> _pickups = [];
    private readonly List<Particle> _particles = [];
    private readonly List<WetPatch> _wetPatches = [];
    private readonly List<(Vector2 Position, int Kind)> _decor = [];
    private readonly List<Upgrade> _upgradeChoices = [];
    private readonly List<AudioStreamPlayer> _audioPlayers = [];
    private readonly List<AudioStreamPlayer> _musicPlayers = [];
    private readonly Dictionary<string, AudioStreamWav> _sounds = [];
    private readonly Dictionary<string, (AudioStream Stream, float GainDb)> _musicTracks = [];
    private readonly RandomNumberGenerator _rng = new();

    private RunState _state = RunState.Title;
    private Texture2D? _titleTexture;
    private Font? _font;

    private Vector2 _playerPosition = new(320, 278);
    private Vector2 _playerVelocity;
    private Vector2 _aimDirection = Vector2.Up;
    private Vector2 _rollDirection = Vector2.Up;
    private float _playerHealth = 6;
    private float _playerMaxHealth = 6;
    private float _invulnerability;
    private float _shotCooldown;
    private float _rollCooldown;
    private float _rollTime;
    private float _dewCooldown;
    private int _seedPods = 3;
    private int _maxSeedPods = 3;
    private int _moonDew;

    private float _damageMultiplier = 1f;
    private float _fireRateMultiplier = 1f;
    private float _moveSpeedMultiplier = 1f;
    private float _plantRateMultiplier = 1f;
    private float _growthMultiplier = 1f;
    private float _harvestMultiplier = 1f;
    private float _dewCooldownMultiplier = 1f;
    private int _bulletPierce;

    private int _room = 1;
    private ulong _runSeed;
    private bool _roomClear;
    private float _clearTimer;
    private float _roomIntroTimer;
    private float _screenShake;
    private float _hurtVignette;
    private float _time;
    private float _toastTimer;
    private string _toast = "";
    private int _hoveredCard = -1;
    private bool _usingControllerAim;
    private int _audioCursor;
    private int _activeMusicIndex = -1;
    private int _fadingMusicIndex = -1;
    private float _musicFadeElapsed;
    private float _musicFadeInTargetDb = MusicBaseVolumeDb;
    private float _musicFadeOutStartDb = MusicBaseVolumeDb;
    private string _currentMusic = "";
    private bool _smokeTest;
    private float _smokeTestTimer;
    private float _smokeShotTimer;

    private static readonly Color Deep = Color.FromHtml("#0e1628");
    private static readonly Color DeepBlue = Color.FromHtml("#1d2d44");
    private static readonly Color MoonBlue = Color.FromHtml("#2f5572");
    private static readonly Color Moss = Color.FromHtml("#496b3b");
    private static readonly Color SproutGreen = Color.FromHtml("#88b84b");
    private static readonly Color Soil = Color.FromHtml("#6b4632");
    private static readonly Color Wood = Color.FromHtml("#9a6139");
    private static readonly Color Honey = Color.FromHtml("#e6a84a");
    private static readonly Color Pumpkin = Color.FromHtml("#d96832");
    private static readonly Color HealthRed = Color.FromHtml("#c84c4c");
    private static readonly Color MoonCyan = Color.FromHtml("#3bc6c4");
    private static readonly Color Parchment = Color.FromHtml("#f3deb3");

    private readonly Upgrade[] _upgradePool =
    [
        new("damage", "饱满豆荚", "芽弹与植物伤害提高 25%", "攻击"),
        new("firerate", "晨风细枝", "主工具攻击速度提高 22%", "攻速"),
        new("speed", "不沾泥的靴子", "移动速度提高 14%", "移动"),
        new("heart", "心莓果酱", "生命上限增加 2，并恢复 2 点", "生存"),
        new("seeds", "双层种袋", "种荚上限增加 1，并立刻补满", "种植"),
        new("plant", "雨后豆荚", "成熟植物攻击速度提高 30%", "植物"),
        new("growth", "蜗牛时钟", "植物成长速度提高 25%", "生长"),
        new("harvest", "三齿小耙", "收割爆发范围与伤害提高 30%", "收割"),
        new("dew", "旧铜喷头", "晨露圈冷却缩短 25%", "湿润"),
        new("pierce", "金色稻壳", "芽弹额外穿透 1 个敌人", "穿透")
    ];

    public override void _Ready()
    {
        _titleTexture = GD.Load<Texture2D>("res://assets/moonroot-title.png");
        _font = ThemeDB.FallbackFont;
        InitializeAudio();
        DisplayServer.WindowSetTitle("月根秘境 · Moonroot Hollow");
        Input.MouseMode = Input.MouseModeEnum.Visible;
        SetProcess(true);
        _smokeTest = OS.GetCmdlineUserArgs().Contains("--smoke-test");
        if (_smokeTest)
        {
            StartRun();
            _playerMaxHealth = 1000;
            _playerHealth = 1000;
            TryPlant(_playerPosition + new Vector2(38, -46));
            UseDewRing();
        }
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        float dt = Math.Min((float)delta, 0.033f);
        _time += dt;
        UpdateMusic(dt);

        if (_toastTimer > 0) _toastTimer -= dt;
        if (_screenShake > 0) _screenShake = Math.Max(0, _screenShake - dt * 18f);
        if (_hurtVignette > 0) _hurtVignette = Math.Max(0, _hurtVignette - dt * 2.4f);

        if (_state == RunState.Playing)
            UpdateGame(dt);

        if (_smokeTest)
            UpdateSmokeTest(dt);

        if (_state == RunState.Upgrade)
            UpdateUpgradeHover();

        QueueRedraw();
    }

    private void UpdateSmokeTest(float dt)
    {
        _smokeTestTimer += dt;
        _smokeShotTimer -= dt;
        _invulnerability = 1f;

        if (_state == RunState.Playing && _enemies.Count > 0 && _smokeShotTimer <= 0)
        {
            Enemy target = _enemies.OrderBy(e => e.Position.DistanceSquaredTo(_playerPosition)).First();
            _aimDirection = _playerPosition.DirectionTo(target.Position);
            ShootPlayerProjectile();
            _smokeShotTimer = 0.12f;
        }

        if (_roomClear && _clearTimer > 0.65f && _room < 5)
        {
            OpenUpgradeChoice();
            ChooseUpgrade(0);
        }

        if (_smokeTestTimer >= 12f)
        {
            GD.Print($"SMOKE_TEST_OK room={_room} state={_state} enemies={_enemies.Count} plants={_plants.Count} dew={_moonDew}");
            GetTree().Quit(0);
            _smokeTest = false;
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseMotion)
            _usingControllerAim = false;

        if (@event is InputEventMouseButton mouse && mouse.Pressed)
        {
            if (mouse.ButtonIndex == MouseButton.Left)
                HandlePrimaryClick(mouse.Position);
            else if (mouse.ButtonIndex == MouseButton.Right && _state == RunState.Playing)
                TryPlant(GetLocalMousePosition());
        }

        if (@event is InputEventKey key && key.Pressed && !key.Echo)
        {
            if (key.Keycode is Key.Enter or Key.KpEnter)
            {
                if (_state == RunState.Title || _state is RunState.GameOver or RunState.Victory)
                    StartRun();
            }
            else if (key.Keycode == Key.Escape)
            {
                TogglePause();
            }
            else if (key.Keycode == Key.Space && _state == RunState.Playing)
            {
                TryRoll();
            }
            else if (key.Keycode == Key.E && _state == RunState.Playing)
            {
                TryInteract();
            }
            else if (key.Keycode == Key.Q && _state == RunState.Playing)
            {
                UseDewRing();
            }
            else if (_state == RunState.Upgrade)
            {
                if (key.Keycode == Key.Key1) ChooseUpgrade(0);
                if (key.Keycode == Key.Key2) ChooseUpgrade(1);
                if (key.Keycode == Key.Key3) ChooseUpgrade(2);
            }
        }

        if (@event is InputEventJoypadButton joy && joy.Pressed)
        {
            _usingControllerAim = true;
            if (joy.ButtonIndex == JoyButton.Start) TogglePause();
            if (_state == RunState.Title && joy.ButtonIndex == JoyButton.A) StartRun();
            else if (_state == RunState.Playing)
            {
                if (joy.ButtonIndex == JoyButton.A) TryRoll();
                if (joy.ButtonIndex == JoyButton.X) TryInteract();
                if (joy.ButtonIndex == JoyButton.Y) UseDewRing();
                if (joy.ButtonIndex == JoyButton.LeftShoulder) TryPlant(_playerPosition + _aimDirection * 44f);
            }
            else if (_state == RunState.Upgrade)
            {
                if (joy.ButtonIndex == JoyButton.DpadLeft) _hoveredCard = Math.Max(0, _hoveredCard - 1);
                if (joy.ButtonIndex == JoyButton.DpadRight) _hoveredCard = Math.Min(2, Math.Max(0, _hoveredCard) + 1);
                if (joy.ButtonIndex == JoyButton.A) ChooseUpgrade(Math.Max(0, _hoveredCard));
            }
            else if (_state is RunState.GameOver or RunState.Victory && joy.ButtonIndex == JoyButton.A)
            {
                StartRun();
            }
        }
    }

    private void HandlePrimaryClick(Vector2 mousePosition)
    {
        if (_state == RunState.Title)
        {
            if (new Rect2(236, 248, 168, 38).HasPoint(mousePosition))
                StartRun();
            return;
        }

        if (_state == RunState.Upgrade)
        {
            for (int i = 0; i < 3; i++)
            {
                if (CardRect(i).HasPoint(mousePosition))
                {
                    ChooseUpgrade(i);
                    return;
                }
            }
        }

        if (_state == RunState.Paused && new Rect2(246, 231, 148, 32).HasPoint(mousePosition))
            TogglePause();
        else if (_state is RunState.GameOver or RunState.Victory && new Rect2(235, 255, 170, 34).HasPoint(mousePosition))
            StartRun();
    }

    private void StartRun()
    {
        _runSeed = (ulong)Time.GetTicksMsec();
        _rng.Seed = _runSeed;
        _state = RunState.Playing;
        _room = 1;
        _playerHealth = 6;
        _playerMaxHealth = 6;
        _maxSeedPods = 3;
        _seedPods = 3;
        _moonDew = 0;
        _damageMultiplier = 1;
        _fireRateMultiplier = 1;
        _moveSpeedMultiplier = 1;
        _plantRateMultiplier = 1;
        _growthMultiplier = 1;
        _harvestMultiplier = 1;
        _dewCooldownMultiplier = 1;
        _bulletPierce = 0;
        _dewCooldown = 0;
        _rollCooldown = 0;
        Input.MouseMode = Input.MouseModeEnum.Hidden;
        StartRoom();
        ShowToast("月根在呼吸……");
    }

    private void StartRoom()
    {
        _enemies.Clear();
        _projectiles.Clear();
        _plants.Clear();
        _pickups.Clear();
        _particles.Clear();
        _wetPatches.Clear();
        _decor.Clear();
        _roomClear = false;
        _clearTimer = 0;
        _roomIntroTimer = 1.35f;
        _playerPosition = new Vector2(320, 286);
        _playerVelocity = Vector2.Zero;
        _invulnerability = 0.8f;

        _rng.Seed = _runSeed + (ulong)(_room * 7919);
        GenerateDecor();

        if (_room == 1)
        {
            SpawnGroup(EnemyKind.Sprout, 4);
            SpawnGroup(EnemyKind.Radish, 1);
        }
        else if (_room == 2)
        {
            SpawnGroup(EnemyKind.Sprout, 4);
            SpawnGroup(EnemyKind.Radish, 2);
        }
        else if (_room == 3)
        {
            SpawnGroup(EnemyKind.Sprout, 2);
            SpawnGroup(EnemyKind.Radish, 2);
            SpawnGroup(EnemyKind.Beetle, 2);
        }
        else if (_room == 4)
        {
            SpawnGroup(EnemyKind.Radish, 3);
            SpawnGroup(EnemyKind.Beetle, 3);
            SpawnGroup(EnemyKind.Sprout, 2);
        }
        else
        {
            SpawnEnemy(EnemyKind.Boss, new Vector2(320, 120));
        }

        PlayMusic(_room >= 5 ? "boss" : "spring");
    }

    private void GenerateDecor()
    {
        for (int i = 0; i < 28; i++)
        {
            float x = _rng.RandfRange(Arena.Position.X + 8, Arena.End.X - 8);
            float y = _rng.RandfRange(Arena.Position.Y + 8, Arena.End.Y - 8);
            if (new Vector2(x, y).DistanceTo(_playerPosition) < 55) continue;
            _decor.Add((new Vector2(Mathf.Round(x), Mathf.Round(y)), _rng.RandiRange(0, 4)));
        }
    }

    private void SpawnGroup(EnemyKind kind, int count)
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 position;
            int guard = 0;
            do
            {
                position = new Vector2(_rng.RandfRange(76, 564), _rng.RandfRange(78, 226));
                guard++;
            } while (position.DistanceTo(_playerPosition) < 120 && guard < 20);
            SpawnEnemy(kind, position);
        }
    }

    private void SpawnEnemy(EnemyKind kind, Vector2 position)
    {
        float health = kind switch
        {
            EnemyKind.Sprout => 22,
            EnemyKind.Radish => 30,
            EnemyKind.Beetle => 46,
            EnemyKind.Boss => 920,
            _ => 20
        };

        _enemies.Add(new Enemy
        {
            Kind = kind,
            Position = position,
            Health = health,
            MaxHealth = health,
            AttackTimer = _rng.RandfRange(0.5f, 1.5f),
            StateTimer = _rng.RandfRange(0.2f, 1f),
            Phase = _rng.RandfRange(0, Mathf.Tau)
        });
    }

    private void UpdateGame(float dt)
    {
        if (_roomIntroTimer > 0) _roomIntroTimer -= dt;
        if (_invulnerability > 0) _invulnerability -= dt;
        if (_shotCooldown > 0) _shotCooldown -= dt;
        if (_rollCooldown > 0) _rollCooldown -= dt;
        if (_dewCooldown > 0) _dewCooldown -= dt;

        UpdateAim();
        UpdatePlayer(dt);
        UpdateWetPatches(dt);
        UpdatePlants(dt);
        UpdateEnemies(dt);
        UpdateProjectiles(dt);
        UpdatePickups(dt);
        UpdateParticles(dt);

        if (!_roomClear && _enemies.Count == 0)
        {
            _roomClear = true;
            _clearTimer = 0;
            _seedPods = Math.Min(_maxSeedPods, _seedPods + 1);
            Burst(new Vector2(320, 72), Honey, 18, 80);
            PlaySound("clear");
            ShowToast(_room == 5 ? "月光平静下来了" : "根门已经苏醒");
        }

        if (_roomClear) _clearTimer += dt;
    }

    private void UpdateAim()
    {
        Vector2 stick = new(Input.GetJoyAxis(0, JoyAxis.RightX), Input.GetJoyAxis(0, JoyAxis.RightY));
        if (stick.Length() > 0.35f)
        {
            _aimDirection = stick.Normalized();
            _usingControllerAim = true;
        }
        else if (!_usingControllerAim)
        {
            Vector2 direction = GetLocalMousePosition() - _playerPosition;
            if (direction.LengthSquared() > 4) _aimDirection = direction.Normalized();
        }
    }

    private void UpdatePlayer(float dt)
    {
        Vector2 movement = GetMovementInput();

        if (_rollTime > 0)
        {
            _rollTime -= dt;
            _playerVelocity = _rollDirection * 245f;
        }
        else
        {
            _playerVelocity = movement * 104f * _moveSpeedMultiplier;
        }

        _playerPosition += _playerVelocity * dt;
        _playerPosition.X = Mathf.Clamp(_playerPosition.X, Arena.Position.X + 12, Arena.End.X - 12);
        _playerPosition.Y = Mathf.Clamp(_playerPosition.Y, Arena.Position.Y + 16, Arena.End.Y - 10);

        bool firing = Input.IsMouseButtonPressed(MouseButton.Left) || Input.IsJoyButtonPressed(0, JoyButton.RightShoulder);
        if (firing && _shotCooldown <= 0 && _roomIntroTimer <= 0)
            ShootPlayerProjectile();
    }

    private Vector2 GetMovementInput()
    {
        Vector2 movement = Vector2.Zero;
        if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left)) movement.X -= 1;
        if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right)) movement.X += 1;
        if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up)) movement.Y -= 1;
        if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down)) movement.Y += 1;

        Vector2 stick = new(Input.GetJoyAxis(0, JoyAxis.LeftX), Input.GetJoyAxis(0, JoyAxis.LeftY));
        if (stick.Length() > 0.2f) movement = stick;
        return movement.Length() > 1 ? movement.Normalized() : movement;
    }

    private void ShootPlayerProjectile()
    {
        _shotCooldown = 0.26f / _fireRateMultiplier;
        Vector2 perpendicular = new(-_aimDirection.Y, _aimDirection.X);
        float wobble = Mathf.Sin(_time * 19f) * 0.018f;
        Vector2 direction = (_aimDirection + perpendicular * wobble).Normalized();
        _projectiles.Add(new Projectile
        {
            Position = _playerPosition + direction * 13,
            Velocity = direction * 240f,
            Damage = 10f * _damageMultiplier,
            Life = 1.15f,
            Radius = 3,
            Pierce = _bulletPierce,
            Friendly = true,
            Color = MoonCyan
        });
        for (int i = 0; i < 2; i++)
            AddParticle(_playerPosition + direction * 11, -direction * _rng.RandfRange(12, 28) + perpendicular * _rng.RandfRange(-10, 10), 0.18f, 2, SproutGreen);
        PlaySound("shoot");
    }

    private void TryRoll()
    {
        if (_rollCooldown > 0 || _rollTime > 0) return;
        Vector2 movement = GetMovementInput();
        _rollDirection = movement.LengthSquared() > 0.1f ? movement.Normalized() : _aimDirection;
        _rollTime = 0.18f;
        _rollCooldown = 1.05f;
        _invulnerability = Math.Max(_invulnerability, 0.24f);
        Burst(_playerPosition, MoonCyan, 7, 44);
    }

    private void TryPlant(Vector2 target)
    {
        if (_seedPods <= 0)
        {
            ShowToast("种袋空了");
            return;
        }

        target.X = Mathf.Clamp(target.X, Arena.Position.X + 14, Arena.End.X - 14);
        target.Y = Mathf.Clamp(target.Y, Arena.Position.Y + 18, Arena.End.Y - 12);
        if (target.DistanceTo(_playerPosition) > 112)
            target = _playerPosition + _playerPosition.DirectionTo(target) * 112;

        target = new Vector2(Mathf.Round(target.X / 8) * 8, Mathf.Round(target.Y / 8) * 8);
        if (_plants.Any(p => p.Position.DistanceTo(target) < 18))
        {
            ShowToast("这里已经长着东西");
            return;
        }

        if (_plants.Count >= 4 + (_maxSeedPods - 3))
        {
            Plant oldest = _plants.OrderByDescending(p => p.Age).First();
            HarvestPlant(oldest, false);
        }

        _seedPods--;
        _plants.Add(new Plant { Position = target, Growth = 0, Age = 0, ShootTimer = 0.4f, Pulse = 0 });
        Burst(target, Soil.Lightened(0.22f), 8, 35);
        PlaySound("plant");
        ShowToast(IsWet(target) ? "湿润土壤：生长加速" : "播下豌豆种");
    }

    private void UseDewRing()
    {
        if (_dewCooldown > 0)
        {
            ShowToast($"晨露圈还需 {_dewCooldown:0.0} 秒");
            return;
        }

        _dewCooldown = 11f * _dewCooldownMultiplier;
        _wetPatches.Add(new WetPatch { Position = _playerPosition, Radius = 54, Life = 12 });
        foreach (Plant plant in _plants)
        {
            if (plant.Position.DistanceTo(_playerPosition) <= 58)
                plant.Growth += 0.7f;
        }
        foreach (Enemy enemy in _enemies)
        {
            if (enemy.Position.DistanceTo(_playerPosition) <= 58 && enemy.Kind != EnemyKind.Boss)
                enemy.Velocity += _playerPosition.DirectionTo(enemy.Position) * 150f;
        }
        Burst(_playerPosition, MoonCyan, 20, 95);
        PlaySound("dew");
        ShowToast("晨露浸润了土地");
    }

    private void TryInteract()
    {
        Plant? nearest = _plants.Where(p => p.Mature && p.Position.DistanceTo(_playerPosition) < 42)
            .OrderBy(p => p.Position.DistanceTo(_playerPosition)).FirstOrDefault();
        if (nearest != null)
        {
            HarvestPlant(nearest, true);
            return;
        }

        if (_roomClear && _clearTimer > 0.5f && _playerPosition.DistanceTo(new Vector2(320, 69)) < 42)
        {
            if (_room >= 5)
            {
                _state = RunState.Victory;
                Input.MouseMode = Input.MouseModeEnum.Visible;
                PlayMusic("menu");
            }
            else
            {
                OpenUpgradeChoice();
            }
        }
    }

    private void UpdateWetPatches(float dt)
    {
        for (int i = _wetPatches.Count - 1; i >= 0; i--)
        {
            _wetPatches[i].Life -= dt;
            if (_wetPatches[i].Life <= 0) _wetPatches.RemoveAt(i);
        }
    }

    private bool IsWet(Vector2 position) => _wetPatches.Any(w => w.Position.DistanceTo(position) <= w.Radius);

    private void UpdatePlants(float dt)
    {
        foreach (Plant plant in _plants.ToArray())
        {
            plant.Age += dt;
            plant.Pulse += dt;
            if (!plant.Mature)
            {
                float wetMultiplier = IsWet(plant.Position) ? 1.65f : 1f;
                plant.Growth += dt * _growthMultiplier * wetMultiplier;
                if (plant.Growth >= 3.2f)
                {
                    plant.Mature = true;
                    plant.ShootTimer = 0.05f;
                    Burst(plant.Position, SproutGreen, 11, 48);
                }
            }
            else
            {
                plant.ShootTimer -= dt;
                if (plant.ShootTimer <= 0 && _enemies.Count > 0)
                {
                    Enemy? target = _enemies.OrderBy(e => e.Position.DistanceSquaredTo(plant.Position)).FirstOrDefault();
                    if (target != null && target.Position.DistanceTo(plant.Position) < 175)
                    {
                        Vector2 direction = plant.Position.DirectionTo(target.Position);
                        _projectiles.Add(new Projectile
                        {
                            Position = plant.Position + direction * 8,
                            Velocity = direction * 190,
                            Damage = 7 * _damageMultiplier,
                            Life = 1.2f,
                            Radius = 3,
                            Pierce = 0,
                            Friendly = true,
                            Color = SproutGreen
                        });
                        plant.ShootTimer = 0.78f / _plantRateMultiplier;
                    }
                    else plant.ShootTimer = 0.2f;
                }
            }
        }
    }

    private void HarvestPlant(Plant plant, bool reward)
    {
        float radius = 46f * _harvestMultiplier;
        float damage = 28f * _damageMultiplier * _harvestMultiplier;
        foreach (Enemy enemy in _enemies.ToArray())
        {
            if (enemy.Position.DistanceTo(plant.Position) <= radius)
                DamageEnemy(enemy, damage, plant.Position.DirectionTo(enemy.Position) * 70);
        }
        Burst(plant.Position, Honey, 20, 110);
        PlaySound("harvest");
        _screenShake = Math.Max(_screenShake, 3.5f);
        _plants.Remove(plant);
        if (reward && _rng.Randf() < 0.38f)
            SpawnPickup(PickupKind.SeedPod, plant.Position);
    }

    private void UpdateEnemies(float dt)
    {
        foreach (Enemy enemy in _enemies.ToArray())
        {
            enemy.Flash = Math.Max(0, enemy.Flash - dt * 8);
            enemy.AttackTimer -= dt;
            enemy.StateTimer -= dt;
            enemy.Phase += dt;

            switch (enemy.Kind)
            {
                case EnemyKind.Sprout:
                    UpdateSprout(enemy, dt);
                    break;
                case EnemyKind.Radish:
                    UpdateRadish(enemy, dt);
                    break;
                case EnemyKind.Beetle:
                    UpdateBeetle(enemy, dt);
                    break;
                case EnemyKind.Boss:
                    UpdateBoss(enemy, dt);
                    break;
            }

            enemy.Position += enemy.Velocity * dt;
            enemy.Velocity = enemy.Velocity.MoveToward(Vector2.Zero, dt * 170);
            enemy.Position.X = Mathf.Clamp(enemy.Position.X, Arena.Position.X + 12, Arena.End.X - 12);
            enemy.Position.Y = Mathf.Clamp(enemy.Position.Y, Arena.Position.Y + 15, Arena.End.Y - 10);

            float hitRadius = enemy.Kind == EnemyKind.Boss ? 26 : 11;
            if (_roomIntroTimer <= 0 && enemy.Position.DistanceTo(_playerPosition) < hitRadius + 8)
                HurtPlayer(enemy.Position, enemy.Kind == EnemyKind.Boss && enemy.Charging ? 2 : 1);
        }
    }

    private void UpdateSprout(Enemy enemy, float dt)
    {
        Vector2 direction = enemy.Position.DirectionTo(_playerPosition);
        float hop = 0.72f + Mathf.Max(0, Mathf.Sin(enemy.Phase * 5f)) * 0.45f;
        enemy.Velocity += direction * 55f * hop * dt * 5f;
        if (enemy.AttackTimer <= 0)
        {
            enemy.AttackTimer = 1.6f;
            enemy.Velocity += direction * 68f;
        }
    }

    private void UpdateRadish(Enemy enemy, float dt)
    {
        Vector2 toPlayer = enemy.Position.DirectionTo(_playerPosition);
        float distance = enemy.Position.DistanceTo(_playerPosition);
        Vector2 tangent = new(-toPlayer.Y, toPlayer.X);
        if (distance < 105) enemy.Velocity -= toPlayer * 35f * dt * 5;
        else if (distance > 165) enemy.Velocity += toPlayer * 28f * dt * 5;
        enemy.Velocity += tangent * Mathf.Sin(enemy.Phase * 1.7f) * 11f * dt;

        if (enemy.AttackTimer <= 0 && _roomIntroTimer <= 0)
        {
            enemy.AttackTimer = 2.05f;
            for (int i = -1; i <= 1; i++)
            {
                Vector2 direction = toPlayer.Rotated(i * 0.16f);
                ShootEnemyProjectile(enemy.Position + direction * 10, direction, 95, 4);
            }
        }
    }

    private void UpdateBeetle(Enemy enemy, float dt)
    {
        if (enemy.Charging)
        {
            enemy.Velocity = enemy.LockedDirection * 175f;
            if (enemy.StateTimer <= 0)
            {
                enemy.Charging = false;
                enemy.StateTimer = 1.25f;
                enemy.Velocity *= 0.2f;
            }
        }
        else if (enemy.StateTimer <= 0 && _roomIntroTimer <= 0)
        {
            enemy.Charging = true;
            enemy.LockedDirection = enemy.Position.DirectionTo(_playerPosition);
            enemy.StateTimer = 0.64f;
            Burst(enemy.Position, Pumpkin, 5, 28);
        }
        else
        {
            enemy.Velocity += enemy.Position.DirectionTo(_playerPosition) * 20f * dt;
        }
    }

    private void UpdateBoss(Enemy enemy, float dt)
    {
        float healthRatio = enemy.Health / enemy.MaxHealth;
        float speed = healthRatio < 0.35f ? 24 : 17;
        Vector2 target = new Vector2(320, 150) + new Vector2(Mathf.Cos(enemy.Phase * 0.55f), Mathf.Sin(enemy.Phase * 0.8f)) * new Vector2(150, 62);
        enemy.Velocity += enemy.Position.DirectionTo(target) * speed * dt * 3f;

        if (enemy.AttackTimer <= 0 && _roomIntroTimer <= 0)
        {
            int count = healthRatio < 0.35f ? 14 : healthRatio < 0.68f ? 11 : 8;
            float bulletSpeed = healthRatio < 0.35f ? 92 : 78;
            float offset = enemy.Phase * 0.4f;
            for (int i = 0; i < count; i++)
            {
                Vector2 direction = Vector2.Right.Rotated(offset + Mathf.Tau * i / count);
                ShootEnemyProjectile(enemy.Position + direction * 24, direction, bulletSpeed, 5);
            }
            if (healthRatio < 0.68f)
            {
                Vector2 aimed = enemy.Position.DirectionTo(_playerPosition);
                for (int i = -1; i <= 1; i++)
                    ShootEnemyProjectile(enemy.Position + aimed * 18, aimed.Rotated(i * 0.14f), 125, 5);
            }
            enemy.AttackTimer = healthRatio < 0.35f ? 1.05f : 1.42f;
            _screenShake = Math.Max(_screenShake, 2.2f);
        }
    }

    private void ShootEnemyProjectile(Vector2 position, Vector2 direction, float speed, float radius)
    {
        _projectiles.Add(new Projectile
        {
            Position = position,
            Velocity = direction * speed,
            Damage = 1,
            Life = 4.8f,
            Radius = radius,
            Friendly = false,
            Color = Pumpkin
        });
    }

    private void UpdateProjectiles(float dt)
    {
        for (int i = _projectiles.Count - 1; i >= 0; i--)
        {
            Projectile projectile = _projectiles[i];
            projectile.Position += projectile.Velocity * dt;
            projectile.Life -= dt;

            if (projectile.Life <= 0 || !Arena.Grow(8).HasPoint(projectile.Position))
            {
                _projectiles.RemoveAt(i);
                continue;
            }

            if (projectile.Friendly)
            {
                bool removed = false;
                foreach (Enemy enemy in _enemies.ToArray())
                {
                    float radius = enemy.Kind == EnemyKind.Boss ? 25 : 10;
                    if (projectile.Position.DistanceTo(enemy.Position) <= projectile.Radius + radius)
                    {
                        DamageEnemy(enemy, projectile.Damage, projectile.Velocity.Normalized() * 28);
                        Burst(projectile.Position, projectile.Color, 4, 40);
                        if (projectile.Pierce > 0)
                        {
                            projectile.Pierce--;
                            projectile.Damage *= 0.82f;
                        }
                        else
                        {
                            _projectiles.RemoveAt(i);
                            removed = true;
                        }
                        break;
                    }
                }
                if (removed) continue;
            }
            else if (projectile.Position.DistanceTo(_playerPosition) <= projectile.Radius + 7)
            {
                HurtPlayer(projectile.Position, 1);
                _projectiles.RemoveAt(i);
            }
        }
    }

    private void DamageEnemy(Enemy enemy, float damage, Vector2 knockback)
    {
        if (!_enemies.Contains(enemy)) return;
        enemy.Health -= damage;
        enemy.Flash = 1;
        if (enemy.Kind != EnemyKind.Boss) enemy.Velocity += knockback;
        _screenShake = Math.Max(_screenShake, enemy.Kind == EnemyKind.Boss ? 1.4f : 0.7f);

        if (enemy.Health <= 0)
        {
            _enemies.Remove(enemy);
            int particleCount = enemy.Kind == EnemyKind.Boss ? 42 : 12;
            Burst(enemy.Position, enemy.Kind == EnemyKind.Boss ? Pumpkin : SproutGreen, particleCount, enemy.Kind == EnemyKind.Boss ? 140 : 72);
            int dewCount = enemy.Kind == EnemyKind.Boss ? 12 : _rng.RandiRange(1, 2);
            for (int i = 0; i < dewCount; i++) SpawnPickup(PickupKind.MoonDew, enemy.Position);
            if (enemy.Kind != EnemyKind.Boss && _rng.Randf() < 0.12f) SpawnPickup(PickupKind.SeedPod, enemy.Position);
            if (_playerHealth <= 3 && _rng.Randf() < 0.08f) SpawnPickup(PickupKind.Heart, enemy.Position);
        }
    }

    private void HurtPlayer(Vector2 source, float damage)
    {
        if (_invulnerability > 0 || _rollTime > 0 || _state != RunState.Playing) return;
        _playerHealth -= damage;
        _invulnerability = 0.82f;
        _playerVelocity += source.DirectionTo(_playerPosition) * 110;
        _screenShake = 6;
        _hurtVignette = 1;
        Burst(_playerPosition, HealthRed, 12, 85);
        PlaySound("hurt");
        ShowToast(damage > 1 ? "重击！" : "小心！");
        if (_playerHealth <= 0)
        {
            _playerHealth = 0;
            _state = RunState.GameOver;
            Input.MouseMode = Input.MouseModeEnum.Visible;
            PlayMusic("menu");
        }
    }

    private void SpawnPickup(PickupKind kind, Vector2 position)
    {
        _pickups.Add(new Pickup
        {
            Kind = kind,
            Position = position,
            Velocity = new Vector2(_rng.RandfRange(-38, 38), _rng.RandfRange(-55, -20)),
            Life = 14
        });
    }

    private void UpdatePickups(float dt)
    {
        for (int i = _pickups.Count - 1; i >= 0; i--)
        {
            Pickup pickup = _pickups[i];
            pickup.Life -= dt;
            pickup.Velocity = pickup.Velocity.MoveToward(Vector2.Zero, dt * 80);
            if (pickup.Position.DistanceTo(_playerPosition) < 68)
                pickup.Velocity += pickup.Position.DirectionTo(_playerPosition) * 260 * dt;
            pickup.Position += pickup.Velocity * dt;

            if (pickup.Position.DistanceTo(_playerPosition) < 13)
            {
                switch (pickup.Kind)
                {
                    case PickupKind.MoonDew:
                        _moonDew++;
                        break;
                    case PickupKind.SeedPod:
                        _seedPods = Math.Min(_maxSeedPods, _seedPods + 1);
                        ShowToast("获得种荚");
                        break;
                    case PickupKind.Heart:
                        _playerHealth = Math.Min(_playerMaxHealth, _playerHealth + 2);
                        ShowToast("心莓恢复了生命");
                        break;
                }
                Burst(pickup.Position, pickup.Kind == PickupKind.Heart ? HealthRed : Honey, 6, 48);
                PlaySound("pickup");
                _pickups.RemoveAt(i);
            }
            else if (pickup.Life <= 0)
            {
                _pickups.RemoveAt(i);
            }
        }
    }

    private void UpdateParticles(float dt)
    {
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            Particle particle = _particles[i];
            particle.Life -= dt;
            particle.Position += particle.Velocity * dt;
            particle.Velocity *= Mathf.Pow(0.04f, dt);
            if (particle.Life <= 0) _particles.RemoveAt(i);
        }
    }

    private void AddParticle(Vector2 position, Vector2 velocity, float life, float size, Color color)
    {
        _particles.Add(new Particle { Position = position, Velocity = velocity, Life = life, MaxLife = life, Size = size, Color = color });
    }

    private void Burst(Vector2 position, Color color, int count, float speed)
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 velocity = Vector2.Right.Rotated(_rng.RandfRange(0, Mathf.Tau)) * _rng.RandfRange(speed * 0.35f, speed);
            AddParticle(position, velocity, _rng.RandfRange(0.2f, 0.55f), _rng.RandfRange(1.5f, 3.5f), color);
        }
    }

    private void OpenUpgradeChoice()
    {
        _state = RunState.Upgrade;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        _upgradeChoices.Clear();
        foreach (Upgrade choice in _upgradePool.OrderBy(_ => _rng.Randf()).Take(3))
            _upgradeChoices.Add(choice);
        _hoveredCard = 0;
    }

    private void UpdateUpgradeHover()
    {
        Vector2 mouse = GetLocalMousePosition();
        for (int i = 0; i < 3; i++)
            if (CardRect(i).HasPoint(mouse)) _hoveredCard = i;
    }

    private Rect2 CardRect(int index) => new(56 + index * 181, 105, 166, 142);

    private void ChooseUpgrade(int index)
    {
        if (_state != RunState.Upgrade || index < 0 || index >= _upgradeChoices.Count) return;
        Upgrade choice = _upgradeChoices[index];
        switch (choice.Id)
        {
            case "damage": _damageMultiplier *= 1.25f; break;
            case "firerate": _fireRateMultiplier *= 1.22f; break;
            case "speed": _moveSpeedMultiplier *= 1.14f; break;
            case "heart": _playerMaxHealth += 2; _playerHealth = Math.Min(_playerMaxHealth, _playerHealth + 2); break;
            case "seeds": _maxSeedPods++; _seedPods = _maxSeedPods; break;
            case "plant": _plantRateMultiplier *= 1.3f; break;
            case "growth": _growthMultiplier *= 1.25f; break;
            case "harvest": _harvestMultiplier *= 1.3f; break;
            case "dew": _dewCooldownMultiplier *= 0.75f; break;
            case "pierce": _bulletPierce++; break;
        }
        _room++;
        PlaySound("upgrade");
        _state = RunState.Playing;
        Input.MouseMode = Input.MouseModeEnum.Hidden;
        StartRoom();
        ShowToast($"获得：{choice.Name}");
    }

    private void TogglePause()
    {
        if (_state == RunState.Playing)
        {
            _state = RunState.Paused;
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }
        else if (_state == RunState.Paused)
        {
            _state = RunState.Playing;
            Input.MouseMode = Input.MouseModeEnum.Hidden;
        }
    }

    private void ShowToast(string message)
    {
        _toast = message;
        _toastTimer = 1.8f;
    }

    private void InitializeAudio()
    {
        EnsureAudioBus("Music");
        EnsureAudioBus("SFX");

        for (int i = 0; i < 8; i++)
        {
            AudioStreamPlayer player = new() { Bus = "SFX", VolumeDb = -9f };
            AddChild(player);
            _audioPlayers.Add(player);
        }

        for (int i = 0; i < 2; i++)
        {
            AudioStreamPlayer player = new() { Bus = "Music", VolumeDb = MusicSilentVolumeDb };
            AddChild(player);
            _musicPlayers.Add(player);
        }

        _sounds["shoot"] = CreateTone(510, 0.045f, 0.22f, 1.8f);
        _sounds["plant"] = CreateTone(235, 0.11f, 0.28f, 1.7f);
        _sounds["dew"] = CreateTone(340, 0.18f, 0.25f, 2.05f);
        _sounds["harvest"] = CreateTone(620, 0.12f, 0.30f, 1.42f);
        _sounds["pickup"] = CreateTone(790, 0.07f, 0.24f, 1.3f);
        _sounds["hurt"] = CreateTone(105, 0.16f, 0.35f, 0.7f);
        _sounds["clear"] = CreateTone(420, 0.28f, 0.26f, 2f);
        _sounds["upgrade"] = CreateTone(540, 0.24f, 0.28f, 1.52f);

        LoadMusic("menu", "res://assets/audio/music/menu.ogg", 0f);
        LoadMusic("spring", "res://assets/audio/music/spring.ogg", 1f);
        LoadMusic("boss", "res://assets/audio/music/boss.ogg", -7.5f);
        PlayMusic("menu");
    }

    private static void EnsureAudioBus(StringName busName)
    {
        if (AudioServer.GetBusIndex(busName) >= 0) return;
        AudioServer.AddBus();
        AudioServer.SetBusName(AudioServer.BusCount - 1, busName);
    }

    private void LoadMusic(string id, string path, float gainDb)
    {
        AudioStream? stream = GD.Load<AudioStream>(path);
        if (stream == null)
        {
            GD.PushWarning($"Music stream could not be loaded: {path}");
            return;
        }

        if (stream is AudioStreamOggVorbis ogg)
            ogg.Loop = true;

        _musicTracks[id] = (stream, gainDb);
    }

    private void PlayMusic(string id)
    {
        if (!_musicTracks.TryGetValue(id, out (AudioStream Stream, float GainDb) track) || _musicPlayers.Count == 0)
            return;

        if (_currentMusic == id && _activeMusicIndex >= 0 && _musicPlayers[_activeMusicIndex].Playing)
            return;

        int nextIndex = _activeMusicIndex < 0 ? 0 : (_activeMusicIndex + 1) % _musicPlayers.Count;
        AudioStreamPlayer next = _musicPlayers[nextIndex];
        next.Stop();
        next.Stream = track.Stream;
        next.VolumeDb = MusicSilentVolumeDb;
        next.Play();

        _fadingMusicIndex = _activeMusicIndex;
        _musicFadeOutStartDb = _fadingMusicIndex >= 0
            ? _musicPlayers[_fadingMusicIndex].VolumeDb
            : MusicSilentVolumeDb;
        _activeMusicIndex = nextIndex;
        _musicFadeInTargetDb = MusicBaseVolumeDb + track.GainDb;
        _musicFadeElapsed = 0f;
        _currentMusic = id;
    }

    private void UpdateMusic(float dt)
    {
        if (_activeMusicIndex < 0 || _musicPlayers.Count == 0) return;

        _musicFadeElapsed += dt;
        float t = Mathf.Clamp(_musicFadeElapsed / MusicCrossfadeSeconds, 0f, 1f);
        float eased = t * t * (3f - 2f * t);
        _musicPlayers[_activeMusicIndex].VolumeDb =
            Mathf.Lerp(MusicSilentVolumeDb, _musicFadeInTargetDb, eased);

        if (_fadingMusicIndex >= 0)
        {
            AudioStreamPlayer fading = _musicPlayers[_fadingMusicIndex];
            fading.VolumeDb = Mathf.Lerp(_musicFadeOutStartDb, MusicSilentVolumeDb, eased);
            if (t >= 1f)
            {
                fading.Stop();
                _fadingMusicIndex = -1;
            }
        }
    }

    private static AudioStreamWav CreateTone(float frequency, float duration, float volume, float endPitch)
    {
        const int sampleRate = 22050;
        int sampleCount = Math.Max(1, (int)(sampleRate * duration));
        byte[] data = new byte[sampleCount * 2];
        double phase = 0;
        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleCount;
            float envelope = Mathf.Sin(Mathf.Pi * t) * (1f - t * 0.35f);
            float pitch = Mathf.Lerp(1f, endPitch, t);
            phase += Mathf.Tau * frequency * pitch / sampleRate;
            float wave = (float)(Math.Sin(phase) * 0.72 + Math.Sin(phase * 2) * 0.18);
            short sample = (short)Mathf.Clamp(wave * envelope * volume * short.MaxValue, short.MinValue, short.MaxValue);
            data[i * 2] = (byte)(sample & 0xff);
            data[i * 2 + 1] = (byte)((sample >> 8) & 0xff);
        }

        return new AudioStreamWav
        {
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = sampleRate,
            Stereo = false,
            Data = data
        };
    }

    private void PlaySound(string id)
    {
        if (!_sounds.TryGetValue(id, out AudioStreamWav? sound) || _audioPlayers.Count == 0) return;
        AudioStreamPlayer player = _audioPlayers[_audioCursor++ % _audioPlayers.Count];
        player.Stop();
        player.Stream = sound;
        player.Play();
    }

    public override void _Draw()
    {
        if (_state == RunState.Title)
        {
            DrawTitle();
            return;
        }

        DrawGameWorld();
        DrawHud();

        if (_state == RunState.Upgrade) DrawUpgradeScreen();
        if (_state == RunState.Paused) DrawPauseScreen();
        if (_state == RunState.GameOver) DrawEndScreen(false);
        if (_state == RunState.Victory) DrawEndScreen(true);
    }

    private void DrawTitle()
    {
        DrawRect(new Rect2(0, 0, Width, Height), Deep);
        if (_titleTexture != null)
            DrawTextureRect(_titleTexture, new Rect2(0, 0, Width, Height), false, new Color(1, 1, 1, 0.78f));
        DrawRect(new Rect2(0, 0, Width, Height), new Color(Deep, 0.28f));
        DrawRect(new Rect2(0, 0, Width, 58), new Color(Deep, 0.72f));
        DrawRect(new Rect2(0, 303, Width, 57), new Color(Deep, 0.82f));

        Panel(new Rect2(154, 68, 332, 104), new Color(Deep, 0.88f), Honey);
        Text("月 根 秘 境", new Vector2(170, 117), 31, Parchment, 300, HorizontalAlignment.Center);
        Text("MOONROOT HOLLOW", new Vector2(170, 143), 10, MoonCyan, 300, HorizontalAlignment.Center);
        Text("在月光侵蚀的地下农庄，种下你的战斗方式", new Vector2(160, 165), 12, Parchment.Darkened(0.08f), 320, HorizontalAlignment.Center);

        Rect2 start = new(236, 248, 168, 38);
        bool hover = start.HasPoint(GetLocalMousePosition());
        Panel(start, hover ? Wood.Lightened(0.16f) : Wood.Darkened(0.05f), hover ? Parchment : Honey);
        Text("开始下潜", new Vector2(start.Position.X, start.Position.Y + 25), 16, Parchment, start.Size.X, HorizontalAlignment.Center);
        Text("Enter / 点击", new Vector2(0, 299), 10, new Color(Parchment, 0.8f), Width, HorizontalAlignment.Center);
        Text("WASD 移动 · 鼠标瞄准 · 左键射击 · 右键播种", new Vector2(0, 334), 11, new Color(Parchment, 0.82f), Width, HorizontalAlignment.Center);
    }

    private void DrawGameWorld()
    {
        Vector2 shake = Vector2.Zero;
        if (_screenShake > 0.1f)
            shake = new Vector2(_rng.RandfRange(-_screenShake, _screenShake), _rng.RandfRange(-_screenShake, _screenShake)).Round();

        DrawRect(new Rect2(0, 0, Width, Height), Deep);
        DrawRect(new Rect2(Arena.Position + shake, Arena.Size), Soil.Darkened(0.28f));

        for (int y = 48; y < 328; y += 16)
        {
            for (int x = 38; x < 602; x += 16)
            {
                bool alternate = ((x / 16 + y / 16 + _room) & 1) == 0;
                Color tile = alternate ? Soil.Darkened(0.18f) : Soil.Darkened(0.23f);
                DrawRect(new Rect2(new Vector2(x, y) + shake, new Vector2(16, 16)), tile);
                DrawLine(new Vector2(x, y + 15) + shake, new Vector2(x + 15, y + 15) + shake, new Color(Deep, 0.13f), 1);
            }
        }

        DrawWalls(shake);
        DrawDecor(shake);

        foreach (WetPatch wet in _wetPatches)
        {
            float alpha = Mathf.Clamp(wet.Life / 2, 0.1f, 0.34f);
            DrawCircle(wet.Position + shake, wet.Radius, new Color(MoonCyan, alpha));
            DrawArc(wet.Position + shake, wet.Radius - 2, 0, Mathf.Tau, 32, new Color(MoonBlue, 0.8f), 1.5f);
        }

        if (_roomClear && _clearTimer > 0.25f)
            DrawPortal(new Vector2(320, 69) + shake);

        foreach (Plant plant in _plants) DrawPlant(plant, shake);
        foreach (Pickup pickup in _pickups) DrawPickup(pickup, shake);
        foreach (Enemy enemy in _enemies) DrawEnemy(enemy, shake);
        foreach (Projectile projectile in _projectiles) DrawProjectile(projectile, shake);
        DrawPlayer(shake);
        foreach (Particle particle in _particles) DrawParticle(particle, shake);

        if (_roomIntroTimer > 0)
        {
            float alpha = Mathf.Clamp(_roomIntroTimer * 2, 0, 1);
            string name = _room == 5 ? "根窟深处 · 灯笼南瓜王" : $"苔灯地窖 · 房间 {_room}";
            Text(name, new Vector2(0, 185), 17, new Color(Parchment, alpha), Width, HorizontalAlignment.Center);
        }

        if (_hurtVignette > 0)
        {
            Color hurt = new(HealthRed, _hurtVignette * 0.32f);
            DrawRect(new Rect2(0, 0, Width, 9), hurt);
            DrawRect(new Rect2(0, Height - 9, Width, 9), hurt);
            DrawRect(new Rect2(0, 0, 9, Height), hurt);
            DrawRect(new Rect2(Width - 9, 0, 9, Height), hurt);
        }
    }

    private void DrawWalls(Vector2 offset)
    {
        Color wall = DeepBlue.Lightened(0.04f);
        Color top = MoonBlue.Darkened(0.25f);
        DrawRect(new Rect2(offset, new Vector2(Width, 48)), Deep);
        DrawRect(new Rect2(new Vector2(0, 328) + offset, new Vector2(Width, 32)), Deep);
        DrawRect(new Rect2(new Vector2(0, 48) + offset, new Vector2(38, 280)), Deep);
        DrawRect(new Rect2(new Vector2(602, 48) + offset, new Vector2(38, 280)), Deep);

        for (int x = 30; x < 614; x += 24)
        {
            DrawRect(new Rect2(new Vector2(x, 36 + (x / 24 % 2) * 3) + offset, new Vector2(22, 14)), wall);
            DrawLine(new Vector2(x + 2, 39) + offset, new Vector2(x + 19, 39) + offset, top, 2);
        }
        for (int x = 30; x < 614; x += 22)
            DrawRect(new Rect2(new Vector2(x, 326) + offset, new Vector2(20, 12)), wall);

        DrawLine(Arena.Position + offset, new Vector2(Arena.End.X, Arena.Position.Y) + offset, Wood.Darkened(0.25f), 3);
        DrawLine(new Vector2(Arena.Position.X, Arena.End.Y) + offset, Arena.End + offset, Wood.Darkened(0.35f), 3);
    }

    private void DrawDecor(Vector2 offset)
    {
        foreach ((Vector2 pos, int kind) in _decor)
        {
            Vector2 p = pos + offset;
            switch (kind)
            {
                case 0:
                    DrawRect(new Rect2(p, new Vector2(3, 2)), Soil.Lightened(0.14f));
                    break;
                case 1:
                    DrawCircle(p, 1.5f, MoonBlue.Darkened(0.2f));
                    break;
                case 2:
                    DrawLine(p, p + new Vector2(0, -4), Moss, 1);
                    DrawRect(new Rect2(p + new Vector2(-2, -4), new Vector2(2, 2)), SproutGreen.Darkened(0.18f));
                    break;
                case 3:
                    DrawRect(new Rect2(p, new Vector2(4, 3)), Wood.Darkened(0.28f));
                    break;
                case 4:
                    DrawCircle(p, 2, new Color(MoonCyan, 0.22f));
                    break;
            }
        }
    }

    private void DrawPortal(Vector2 position)
    {
        float pulse = 1 + Mathf.Sin(_time * 4) * 0.12f;
        DrawCircle(position, 20 * pulse, new Color(MoonCyan, 0.12f));
        DrawArc(position, 13 * pulse, 0, Mathf.Tau, 20, MoonCyan, 2);
        DrawArc(position, 8 / pulse, 0, Mathf.Tau, 16, Honey, 2);
        DrawRect(new Rect2(position + new Vector2(-2, -2), new Vector2(4, 4)), Parchment);
        if (_playerPosition.DistanceTo(new Vector2(320, 69)) < 44)
            Text(_room == 5 ? "E  返回温室" : "E  穿过根门", new Vector2(position.X - 58, position.Y + 31), 11, Parchment, 116, HorizontalAlignment.Center);
    }

    private void DrawPlayer(Vector2 offset)
    {
        Vector2 p = (_playerPosition + offset).Round();
        float blink = _invulnerability > 0 ? (Mathf.Sin(_time * 35) > 0 ? 0.38f : 1f) : 1f;
        Color body = new(MoonCyan, blink);
        Color hair = new(Pumpkin, blink);
        Color skin = new(Parchment.Darkened(0.12f), blink);
        Color outline = new(Deep, blink);

        DrawRect(new Rect2(p + new Vector2(-7, 8), new Vector2(5, 4)), new Color(outline, 0.45f));
        DrawRect(new Rect2(p + new Vector2(2, 8), new Vector2(5, 4)), new Color(outline, 0.45f));
        DrawRect(new Rect2(p + new Vector2(-6, 1), new Vector2(12, 10)), outline);
        DrawRect(new Rect2(p + new Vector2(-5, 1), new Vector2(10, 8)), body.Darkened(0.2f));
        DrawRect(new Rect2(p + new Vector2(-7, -8), new Vector2(14, 11)), outline);
        DrawRect(new Rect2(p + new Vector2(-5, -7), new Vector2(10, 8)), skin);
        DrawRect(new Rect2(p + new Vector2(-7, -9), new Vector2(13, 5)), hair);
        DrawRect(new Rect2(p + new Vector2(-8, -7), new Vector2(4, 6)), hair.Darkened(0.18f));
        DrawRect(new Rect2(p + new Vector2(-6, 0), new Vector2(12, 3)), body);
        Vector2 staffEnd = p + _aimDirection * 17;
        DrawLine(p + _aimDirection * 4, staffEnd, Wood.Lightened(0.12f), 2);
        DrawCircle(staffEnd, 3, SproutGreen);
        DrawRect(new Rect2(staffEnd + new Vector2(-1, -5), new Vector2(2, 4)), SproutGreen.Lightened(0.22f));
    }

    private void DrawEnemy(Enemy enemy, Vector2 offset)
    {
        Vector2 p = (enemy.Position + offset).Round();
        Color flash = enemy.Flash > 0 ? Parchment : Color.FromHtml("#00000000");
        float bob = Mathf.Sin(enemy.Phase * 4) * 1.5f;

        if (enemy.Kind == EnemyKind.Sprout)
        {
            DrawRect(new Rect2(p + new Vector2(-8, 7), new Vector2(16, 3)), new Color(Deep, 0.5f));
            DrawCircle(p + new Vector2(0, bob), 9, enemy.Flash > 0 ? flash : Moss.Lightened(0.05f));
            DrawRect(new Rect2(p + new Vector2(-5, -9 + bob), new Vector2(4, 7)), SproutGreen);
            DrawRect(new Rect2(p + new Vector2(1, -10 + bob), new Vector2(5, 5)), SproutGreen.Lightened(0.14f));
            DrawRect(new Rect2(p + new Vector2(-4, -1 + bob), new Vector2(2, 2)), Deep);
            DrawRect(new Rect2(p + new Vector2(3, -1 + bob), new Vector2(2, 2)), Deep);
        }
        else if (enemy.Kind == EnemyKind.Radish)
        {
            DrawRect(new Rect2(p + new Vector2(-8, 8), new Vector2(16, 3)), new Color(Deep, 0.5f));
            DrawCircle(p + new Vector2(0, bob), 9, enemy.Flash > 0 ? flash : HealthRed.Lightened(0.13f));
            for (int i = -1; i <= 1; i++)
                DrawLine(p + new Vector2(i * 3, -7 + bob), p + new Vector2(i * 5, -14 + bob), SproutGreen, 2);
            DrawRect(new Rect2(p + new Vector2(-4, -2 + bob), new Vector2(2, 2)), Deep);
            DrawRect(new Rect2(p + new Vector2(3, -2 + bob), new Vector2(2, 2)), Deep);
        }
        else if (enemy.Kind == EnemyKind.Beetle)
        {
            Color shell = enemy.Flash > 0 ? flash : (enemy.Charging ? Pumpkin.Lightened(0.2f) : MoonBlue);
            DrawRect(new Rect2(p + new Vector2(-11, 7), new Vector2(22, 4)), new Color(Deep, 0.5f));
            DrawCircle(p, 11, Deep);
            DrawRect(new Rect2(p + new Vector2(-9, -7), new Vector2(18, 14)), shell);
            DrawLine(p + new Vector2(0, -7), p + new Vector2(0, 7), DeepBlue, 2);
            DrawRect(new Rect2(p + new Vector2(-7, -5), new Vector2(3, 3)), Honey.Darkened(0.12f));
        }
        else
        {
            Color boss = enemy.Flash > 0 ? flash : Pumpkin;
            float squish = 1 + Mathf.Sin(enemy.Phase * 3) * 0.04f;
            DrawCircle(p + new Vector2(0, 18), 28, new Color(Deep, 0.55f));
            DrawCircle(p, 29 * squish, Deep);
            DrawCircle(p, 26 * squish, boss);
            DrawRect(new Rect2(p + new Vector2(-4, -34), new Vector2(8, 13)), Moss);
            DrawRect(new Rect2(p + new Vector2(-16, -4), new Vector2(8, 10)), Deep);
            DrawRect(new Rect2(p + new Vector2(8, -4), new Vector2(8, 10)), Deep);
            DrawRect(new Rect2(p + new Vector2(-10, 13), new Vector2(20, 4)), Pumpkin.Darkened(0.35f));
            DrawRect(new Rect2(p + new Vector2(-13, -1), new Vector2(3, 4)), Honey);
            DrawRect(new Rect2(p + new Vector2(10, -1), new Vector2(3, 4)), Honey);
        }
    }

    private void DrawPlant(Plant plant, Vector2 offset)
    {
        Vector2 p = (plant.Position + offset).Round();
        DrawRect(new Rect2(p + new Vector2(-7, 5), new Vector2(14, 4)), new Color(Deep, 0.42f));
        if (!plant.Mature)
        {
            int stage = Mathf.Clamp((int)(plant.Growth / 0.82f), 0, 3);
            float height = 3 + stage * 3;
            DrawLine(p + new Vector2(0, 5), p + new Vector2(0, 5 - height), SproutGreen.Darkened(0.15f), 2);
            if (stage >= 1) DrawRect(new Rect2(p + new Vector2(-4, 1 - height / 2), new Vector2(4, 3)), SproutGreen);
            if (stage >= 2) DrawRect(new Rect2(p + new Vector2(1, -2 - height / 2), new Vector2(5, 3)), SproutGreen.Lightened(0.1f));
            DrawCircle(p + new Vector2(0, 5 - height), 2 + stage * 0.6f, MoonCyan);
        }
        else
        {
            float pulse = 1 + Mathf.Sin(plant.Pulse * 7) * 0.12f;
            DrawLine(p + new Vector2(0, 6), p + new Vector2(0, -10), SproutGreen.Darkened(0.18f), 3);
            DrawCircle(p + new Vector2(-5, -5), 5 * pulse, SproutGreen);
            DrawCircle(p + new Vector2(5, -7), 5 * pulse, SproutGreen.Lightened(0.12f));
            DrawCircle(p + new Vector2(0, -12), 4 * pulse, MoonCyan);
            if (_playerPosition.DistanceTo(plant.Position) < 42)
                Text("E 收割", new Vector2(p.X - 28, p.Y + 24), 9, Honey, 56, HorizontalAlignment.Center);
        }
    }

    private void DrawProjectile(Projectile projectile, Vector2 offset)
    {
        Vector2 p = (projectile.Position + offset).Round();
        if (projectile.Friendly)
        {
            DrawCircle(p, projectile.Radius + 2, new Color(projectile.Color, 0.18f));
            DrawCircle(p, projectile.Radius, projectile.Color);
            DrawRect(new Rect2(p + new Vector2(-1, -1), new Vector2(2, 2)), Parchment);
        }
        else
        {
            DrawCircle(p, projectile.Radius + 2, new Color(Pumpkin, 0.18f));
            DrawRect(new Rect2(p + new Vector2(-projectile.Radius, -projectile.Radius), new Vector2(projectile.Radius * 2, projectile.Radius * 2)), Pumpkin);
            DrawRect(new Rect2(p + new Vector2(-1, -1), new Vector2(2, 2)), Honey);
        }
    }

    private void DrawPickup(Pickup pickup, Vector2 offset)
    {
        Vector2 p = (pickup.Position + offset + new Vector2(0, Mathf.Sin(_time * 5 + pickup.Position.X) * 2)).Round();
        Color color = pickup.Kind switch
        {
            PickupKind.MoonDew => Honey,
            PickupKind.SeedPod => SproutGreen,
            PickupKind.Heart => HealthRed,
            _ => Parchment
        };
        DrawCircle(p, 6, new Color(color, 0.18f));
        if (pickup.Kind == PickupKind.Heart)
        {
            DrawCircle(p + new Vector2(-2, -1), 3, color);
            DrawCircle(p + new Vector2(2, -1), 3, color);
            DrawRect(new Rect2(p + new Vector2(-3, 0), new Vector2(6, 4)), color);
        }
        else
        {
            DrawRect(new Rect2(p + new Vector2(-3, -3), new Vector2(6, 6)), color);
            DrawRect(new Rect2(p + new Vector2(-1, -5), new Vector2(2, 2)), Parchment);
        }
    }

    private void DrawParticle(Particle particle, Vector2 offset)
    {
        float alpha = Mathf.Clamp(particle.Life / particle.MaxLife, 0, 1);
        float size = Math.Max(1, particle.Size * alpha);
        DrawRect(new Rect2((particle.Position + offset).Round(), new Vector2(size, size)), new Color(particle.Color, alpha));
    }

    private void DrawHud()
    {
        Panel(new Rect2(12, 10, 175, 31), new Color(Deep, 0.88f), Wood.Darkened(0.1f));
        for (int i = 0; i < Mathf.CeilToInt(_playerMaxHealth / 2); i++)
        {
            float amount = Mathf.Clamp(_playerHealth - i * 2, 0, 2);
            DrawHeart(new Vector2(25 + i * 22, 24), amount);
        }

        Panel(new Rect2(254, 9, 132, 32), new Color(Deep, 0.88f), Wood.Darkened(0.1f));
        Text(_room == 5 ? "灯笼王庭" : $"苔灯地窖  {_room}/5", new Vector2(254, 30), 12, Parchment, 132, HorizontalAlignment.Center);

        Panel(new Rect2(459, 10, 169, 31), new Color(Deep, 0.88f), Wood.Darkened(0.1f));
        Text($"◆ {_moonDew:00}", new Vector2(472, 31), 13, Honey);
        Text($"种荚 {_seedPods}/{_maxSeedPods}", new Vector2(535, 31), 12, SproutGreen);

        Enemy? boss = _enemies.FirstOrDefault(e => e.Kind == EnemyKind.Boss);
        if (boss != null)
        {
            Panel(new Rect2(173, 47, 294, 20), new Color(Deep, 0.88f), Pumpkin.Darkened(0.18f));
            DrawRect(new Rect2(181, 55, 278 * Mathf.Clamp(boss.Health / boss.MaxHealth, 0, 1), 5), Pumpkin);
            Text("灯笼南瓜王", new Vector2(173, 63), 9, Parchment, 294, HorizontalAlignment.Center);
        }

        Panel(new Rect2(12, 330, 241, 23), new Color(Deep, 0.84f), Wood.Darkened(0.18f));
        Text("左键 芽弹   右键 播种   E 收割", new Vector2(21, 347), 10, Parchment);
        Panel(new Rect2(438, 330, 190, 23), new Color(Deep, 0.84f), Wood.Darkened(0.18f));
        string dew = _dewCooldown <= 0 ? "Q 晨露圈：就绪" : $"Q 晨露圈：{_dewCooldown:0.0}s";
        string roll = _rollCooldown <= 0 ? "翻滚：就绪" : $"翻滚：{_rollCooldown:0.0}s";
        Text(dew, new Vector2(447, 347), 9, _dewCooldown <= 0 ? MoonCyan : Parchment.Darkened(0.35f));
        Text(roll, new Vector2(546, 347), 9, _rollCooldown <= 0 ? SproutGreen : Parchment.Darkened(0.35f));

        if (_toastTimer > 0)
        {
            float alpha = Mathf.Clamp(_toastTimer * 2, 0, 1);
            DrawRect(new Rect2(218, 292, 204, 25), new Color(Deep, 0.78f * alpha));
            Text(_toast, new Vector2(218, 309), 11, new Color(Parchment, alpha), 204, HorizontalAlignment.Center);
        }

        if (_state == RunState.Playing && !_usingControllerAim)
            DrawCrosshair(GetLocalMousePosition());
    }

    private void DrawHeart(Vector2 center, float amount)
    {
        Color empty = DeepBlue.Lightened(0.08f);
        Color fill = HealthRed;
        DrawCircle(center + new Vector2(-4, -2), 5, amount > 0 ? fill : empty);
        DrawCircle(center + new Vector2(4, -2), 5, amount > 1 ? fill : empty);
        DrawRect(new Rect2(center + new Vector2(-7, -1), new Vector2(14, 6)), amount > 0 ? fill : empty);
        DrawRect(new Rect2(center + new Vector2(-4, 5), new Vector2(8, 3)), amount > 0 ? fill.Darkened(0.12f) : empty);
    }

    private void DrawCrosshair(Vector2 p)
    {
        Color color = new(Parchment, 0.75f);
        DrawLine(p + new Vector2(-7, 0), p + new Vector2(-3, 0), color, 1);
        DrawLine(p + new Vector2(3, 0), p + new Vector2(7, 0), color, 1);
        DrawLine(p + new Vector2(0, -7), p + new Vector2(0, -3), color, 1);
        DrawLine(p + new Vector2(0, 3), p + new Vector2(0, 7), color, 1);
    }

    private void DrawUpgradeScreen()
    {
        DrawRect(new Rect2(0, 0, Width, Height), new Color(Deep, 0.88f));
        Text("选择一份月根祝福", new Vector2(0, 68), 24, Parchment, Width, HorizontalAlignment.Center);
        Text("按 1 / 2 / 3，或点击卡片", new Vector2(0, 91), 11, MoonCyan, Width, HorizontalAlignment.Center);

        for (int i = 0; i < _upgradeChoices.Count; i++)
        {
            Upgrade choice = _upgradeChoices[i];
            Rect2 rect = CardRect(i);
            bool selected = i == _hoveredCard;
            if (selected) rect.Position += new Vector2(0, -5);
            Panel(rect, selected ? Soil.Lightened(0.05f) : Soil.Darkened(0.17f), selected ? Honey : Wood);
            DrawCircle(new Vector2(rect.GetCenter().X, rect.Position.Y + 34), 18, new Color(MoonCyan, selected ? 0.25f : 0.13f));
            DrawUpgradeIcon(choice.Id, new Vector2(rect.GetCenter().X, rect.Position.Y + 34));
            Text($"{i + 1}", new Vector2(rect.Position.X + 9, rect.Position.Y + 18), 11, Honey);
            Text(choice.Name, new Vector2(rect.Position.X + 8, rect.Position.Y + 68), 15, Parchment, rect.Size.X - 16, HorizontalAlignment.Center);
            Text(choice.Description, new Vector2(rect.Position.X + 13, rect.Position.Y + 94), 10, Parchment.Darkened(0.12f), rect.Size.X - 26, HorizontalAlignment.Center);
            Text(choice.Tag, new Vector2(rect.Position.X + 36, rect.End.Y - 12), 9, MoonCyan, rect.Size.X - 72, HorizontalAlignment.Center);
        }
    }

    private void DrawUpgradeIcon(string id, Vector2 center)
    {
        Color color = id is "heart" ? HealthRed : id is "damage" or "firerate" or "pierce" ? Honey : SproutGreen;
        if (id == "heart")
        {
            DrawCircle(center + new Vector2(-4, -2), 5, color);
            DrawCircle(center + new Vector2(4, -2), 5, color);
            DrawRect(new Rect2(center + new Vector2(-7, 0), new Vector2(14, 7)), color);
        }
        else if (id is "growth" or "plant" or "seeds")
        {
            DrawLine(center + new Vector2(0, 9), center + new Vector2(0, -7), Moss, 3);
            DrawCircle(center + new Vector2(-5, -4), 5, color);
            DrawCircle(center + new Vector2(5, -7), 5, color.Lightened(0.15f));
        }
        else
        {
            DrawCircle(center, 8, color);
            DrawRect(new Rect2(center + new Vector2(-2, -12), new Vector2(4, 24)), Parchment);
            DrawRect(new Rect2(center + new Vector2(-12, -2), new Vector2(24, 4)), Parchment);
        }
    }

    private void DrawPauseScreen()
    {
        DrawRect(new Rect2(0, 0, Width, Height), new Color(Deep, 0.82f));
        Panel(new Rect2(170, 80, 300, 203), new Color(DeepBlue, 0.96f), Honey);
        Text("旅程暂停", new Vector2(170, 119), 25, Parchment, 300, HorizontalAlignment.Center);
        Text($"房间 {_room}/5   月露 {_moonDew}   种荚 {_seedPods}/{_maxSeedPods}", new Vector2(170, 151), 12, MoonCyan, 300, HorizontalAlignment.Center);
        Text($"伤害 ×{_damageMultiplier:0.00}   攻速 ×{_fireRateMultiplier:0.00}", new Vector2(170, 178), 11, Parchment, 300, HorizontalAlignment.Center);
        Text($"生长 ×{_growthMultiplier:0.00}   收割 ×{_harvestMultiplier:0.00}", new Vector2(170, 199), 11, Parchment, 300, HorizontalAlignment.Center);
        Rect2 resume = new(246, 231, 148, 32);
        Panel(resume, resume.HasPoint(GetLocalMousePosition()) ? Wood.Lightened(0.16f) : Wood, Parchment);
        Text("继续旅程", new Vector2(resume.Position.X, resume.Position.Y + 22), 14, Parchment, resume.Size.X, HorizontalAlignment.Center);
        Text("Esc 继续", new Vector2(0, 305), 10, new Color(Parchment, 0.72f), Width, HorizontalAlignment.Center);
    }

    private void DrawEndScreen(bool victory)
    {
        DrawRect(new Rect2(0, 0, Width, Height), new Color(Deep, 0.86f));
        Panel(new Rect2(140, 63, 360, 242), new Color(DeepBlue, 0.96f), victory ? Honey : HealthRed.Darkened(0.18f));
        Text(victory ? "月光重归宁静" : "这次种子没有发芽", new Vector2(140, 112), 24, victory ? Honey : Parchment, 360, HorizontalAlignment.Center);
        Text(victory ? "灯笼南瓜王恢复成了一颗安静的种子。" : "守圃人被送回了温室，根窟仍在等待。", new Vector2(155, 151), 12, Parchment, 330, HorizontalAlignment.Center);
        Text($"抵达房间  {_room}/5", new Vector2(140, 190), 14, MoonCyan, 360, HorizontalAlignment.Center);
        Text($"收集月露  {_moonDew}", new Vector2(140, 216), 13, Honey, 360, HorizontalAlignment.Center);
        Rect2 restart = new(235, 255, 170, 34);
        Panel(restart, restart.HasPoint(GetLocalMousePosition()) ? Wood.Lightened(0.16f) : Wood, Parchment);
        Text("再次下潜", new Vector2(restart.Position.X, restart.Position.Y + 23), 14, Parchment, restart.Size.X, HorizontalAlignment.Center);
    }

    private void Panel(Rect2 rect, Color fill, Color border)
    {
        DrawRect(rect, new Color(Deep, 0.9f));
        DrawRect(rect.Grow(-2), border);
        DrawRect(rect.Grow(-4), fill);
        DrawRect(new Rect2(rect.Position + new Vector2(5, 5), new Vector2(3, 3)), Honey.Darkened(0.18f));
        DrawRect(new Rect2(new Vector2(rect.End.X - 8, rect.Position.Y + 5), new Vector2(3, 3)), Honey.Darkened(0.18f));
    }

    private void Text(string value, Vector2 position, int size, Color color, float width = -1, HorizontalAlignment alignment = HorizontalAlignment.Left)
    {
        if (_font == null) return;
        DrawString(_font, position + Vector2.One, value, alignment, width, size, new Color(Deep, color.A * 0.9f));
        DrawString(_font, position, value, alignment, width, size, color);
    }
}

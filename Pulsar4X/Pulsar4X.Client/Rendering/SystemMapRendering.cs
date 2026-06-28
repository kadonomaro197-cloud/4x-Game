using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Concurrent;
using System.Drawing;
using Pulsar4X.Orbital;
using Pulsar4X.Engine;
using Pulsar4X.Engine.Sensors;
using Pulsar4X.Messaging;
using Pulsar4X.JumpPoints;
using Pulsar4X.Names;
using Pulsar4X.Orbits;
using Pulsar4X.Ships;
using Pulsar4X.Weapons;
using Pulsar4X.Galaxy;
using Pulsar4X.Movement;
using Pulsar4X.Sensors;
using Pulsar4X.Combat;
using SDL3;

namespace Pulsar4X.Client.Rendering
{
    internal class SystemMapRendering : UpdateWindowState
    {
        GlobalUIState _state;
        SystemSensorContacts? _sensorMgr;
        ConcurrentQueue<Message>? _sensorChanges;
        SystemState? _sysState;
        Camera _camera;
        SDL3Window _window;
        SystemLabelDistributor _distributor;

        internal Dictionary<string, IDrawData> UIWidgets = new ();

        ConcurrentDictionary<int, Icon> _testIcons = new ();
        ConcurrentDictionary<int, Icon> _orbitRings = new ();
        ConcurrentDictionary<int, Icon> _moveIcons = new ();
        ConcurrentDictionary<int, Icon> _entityIcons = new ();
        ConcurrentDictionary<int, Icon> _bodyIcons = new ();

        // Ship ids HIDDEN this frame because their fleet is drawn as ONE icon (its flagship). Recomputed every frame
        // from FleetTools (engine, CI-tested), so collapse/expand tracks fleet membership live. A ship's icon, orbit
        // ring, move trail, and label are all skipped when it's in here — so a fleet shows a single marker, not a
        // scattered cluster, until it's broken up. Empty when no multi-ship fleet exists, so non-fleet play is unchanged.
        HashSet<int> _collapsedFleetMembers = new ();

        // Fog-of-war sensor-contact blips (detected foreign units), keyed by the real entity's id. Rebuilt from the
        // live contact list each frame on the render thread, so a plain Dictionary is fine. See UpdateContactBlips.
        readonly Dictionary<int, SensorContactIcon> _contactIcons = new ();

        HashSet<EntityLabel> _allLabels = new ();
        HashSet<EntityLabel> _visibleLabels = new ();

        // Per-item render-fault isolation: if ONE map item's Draw throws (commonly a NaN coordinate from a
        // mid-warp / detached position hitting Convert.ToInt32 -> OverflowException), skip just that item and
        // log it ONCE — so one bad entity can't blank the rest of the map. Names the item so game_log.txt says
        // WHICH one faulted (a precise gauge, vs the coarse whole-map SafeRender in PulsarMainWindow).
        readonly HashSet<string> _loggedDrawErrors = new ();

        // Throttle for camera pan/zoom logging. Pan/zoom fire many times per drag — without a throttle the
        // session log would drown in CAMERA lines. Only emit one every ~400 ms (Environment.TickCount is ms).
        int _lastCamLogTick = 0;

        // Per-body-type minimum camera zoom for the label to render. Lower-tier
        // bodies (moons, ships, asteroids, comets) only show labels once you've
        // zoomed in enough that they aren't just visual clutter. Stars, planets,
        // dwarf planets and colonies are always shown (subject to view prefs).
        static readonly Dictionary<UserOrbitSettings.OrbitBodyType, float> _minZoomForLabel = new ()
        {
            { UserOrbitSettings.OrbitBodyType.Star,         0f },
            { UserOrbitSettings.OrbitBodyType.Planet,       0f },
            { UserOrbitSettings.OrbitBodyType.DwarfPlanet,  0f },
            { UserOrbitSettings.OrbitBodyType.Colony,       0f },
            { UserOrbitSettings.OrbitBodyType.Moon,        1e4f },
            { UserOrbitSettings.OrbitBodyType.Ship,        2e4f },
            { UserOrbitSettings.OrbitBodyType.Asteroid,    5e4f },
            { UserOrbitSettings.OrbitBodyType.Comet,       2e4f },
            { UserOrbitSettings.OrbitBodyType.Unknown,      0f },
        };

        ConcurrentDictionary<int, InteractableState[]> _interactable = new ();
        IOrderedEnumerable<IGrouping<byte, InteractableState>> _interactableGrouped;

        internal List<IDrawData> SelectedEntityExtras = new List<IDrawData>();
        internal Vector2 GalacticMapPosition = new Vector2();
        //internal SystemMap_DrawableVM SysMap;
        Entity? _faction;

        bool _updateLabels = false;

        internal SystemMapRendering(SDL3Window window, GlobalUIState state)
        {
            _state = state;

            _distributor = EntityLabelDistributor.Group;

            _camera = _state.Camera;
            _window = window;

            // Initialize ship icon texture
            ShipIcon.InitializeTexture(window.Renderer);

            //UIWidgets.Add(new CursorCrosshair(new Vector4())); //used for debugging the cursor world position.
            foreach (var item in TestDrawIconData.GetTestIcons())
            {
                _testIcons.TryAdd(-1, item);
            }

            //_state.OnStarSystemChanged += RespondToSystemChange;
            //_state.OnFactionChanged += RespondToSystemChange;

            var mainWin = (PulsarMainWindow)window;
            mainWin.MouseButtonDownOccured += (object sender, SDL.Event e) => {
                if (mainWin.PlatformBackend.WantsMouseCapture())
                    return;

                foreach (var i in _interactableGrouped)
                {
                    var key = i.Key;

                    foreach (var j in i)
                    {
                        if (j.IsDisabled)
                            continue;

                        var item = j.Item;

                        var c = item.Contains(new (e.Motion.X, e.Motion.Y));

                        if (c)
                        {
                            j.IsPressed = true;
                            if (item.OnPointerDown(e))
                                return;
                        }
                    }
                }
            };
            mainWin.MouseButtonUpOccured += (object sender, SDL.Event e) => {
                if (mainWin.PlatformBackend.WantsMouseCapture())
                    return;

                foreach (var i in _interactableGrouped)
                {
                    var key = i.Key;

                    foreach (var j in i)
                    {
                        if (j.IsDisabled)
                            continue;

                        var item = j.Item;

                        var c = item.Contains(new (e.Motion.X, e.Motion.Y));

                        if (c)
                        {
                            j.IsPressed = false;
                            if (item.OnPointerUp(e))
                                return;
                        }
                    }
                }
            };
            mainWin.MouseMoveOccured += (object sender, SDL.Event e) => {
                foreach (var i in _interactableGrouped)
                {
                    var key = i.Key;

                    foreach (var j in i)
                    {
                        if (j.IsDisabled)
                            continue;

                        var item = j.Item;

                        if (mainWin.PlatformBackend.WantsMouseCapture())
                        {
                            if (j.IsHovered)
                            {
                                j.IsHovered = false;
                                if (item.OnPointerExit(e))
                                    return;
                            }
                            continue;
                        }

                        var c = item.Contains(new (e.Motion.X, e.Motion.Y));

                        if (j.IsHovered)
                        {
                            if (c)
                            {
                                if (item.OnPointerMove(e))
                                    return;
                            }
                            else
                            {
                                j.IsHovered = false;
                                if (item.OnPointerExit(e))
                                    return;
                            }
                        }
                        else if (c)
                        {
                            j.IsHovered = true;
                            if (item.OnPointerEnter(e))
                                return;
                        }
                    }
                }
            };

            _camera.PanOccured +=
                (object sender, Orbital.Vector3 pos) => {
                    _updateLabels = true;
                    if (Environment.TickCount - _lastCamLogTick > 400)
                    { _lastCamLogTick = Environment.TickCount; SessionLog.Camera("pan -> " + (pos.X / 1e9).ToString("0.#") + "," + (pos.Y / 1e9).ToString("0.#") + " Gm"); }
                };

            _camera.ZoomOccured +=
                (object sender, float zoom) => {
                    _updateLabels = true;
                    if (Environment.TickCount - _lastCamLogTick > 400)
                    { _lastCamLogTick = Environment.TickCount; SessionLog.Camera("zoom -> " + zoom.ToString("0.####")); }
                };

            SystemViewPreferences.GetInstance().ViewUpdateOccured +=
                (object sender, SystemViewPreferences.View view) => _updateLabels = true;

            // should be empty
            _interactableGrouped = _interactable
                .Values
                .SelectMany(x => x)
                .GroupBy(x => x.Item.Priority)
                .OrderByDescending(x => x.Key);
        }

        internal void Initialize(StarSystem starSys)
        {
            if(_state.Faction == null)
                throw new NullReferenceException();

            _faction = _state.Faction;

            if (_state.StarSystemStates.ContainsKey(starSys.ID))
            {
                _sysState = _state.StarSystemStates[starSys.ID];
            }
            else
            {
                _sysState = new SystemState(starSys, _faction.Id);
                _state.StarSystemStates[_sysState.StarSystem.ID] = _sysState;
            }

            _sensorMgr = starSys.GetSensorContacts(_faction.Id);
            _sensorChanges = _sensorMgr.Changes.Subscribe();
            _sysState.OnEntityAdded += OnSystemStateEntityAdded;
            _sysState.OnEntityUpdated += OnSystemStateEntityUpdated;
            _sysState.OnEntityRemoved += OnSystemStateEntityRemoved;

            foreach (var entityItem in _sysState.EntityStatesWithPosition.Values)
            {
                AddIconable(entityItem);
            }

            _updateLabels = true; // update labels on first frame
        }

        public void UpdateSystemState(SystemState systemState)
        {
            _testIcons.Clear();
            _entityIcons.Clear();
            _orbitRings.Clear();
            _moveIcons.Clear();
            _allLabels.Clear();
            _bodyIcons.Clear();
            _interactable.Clear();
            ClearContactBlips();

            _sysState = systemState;
            _state.StarSystemStates[_sysState.StarSystem.ID] = _sysState;

            if(_state.Faction == null)
                throw new NullReferenceException();

            _faction = _state.Faction;
            _sensorMgr = systemState.StarSystem.GetSensorContacts(_faction.Id);
            _sensorChanges = _sensorMgr.Changes.Subscribe();

            foreach (var entityItem in _sysState.EntityStatesWithPosition.Values)
            {
                AddIconable(entityItem);
            }
        }

        void AddEntityIcon(Entity entity, Icon icon)
        {
            var l = new EntityLabelExtCombo(entity);
            l.Padding = 3;
            l.Faction = _state.Faction?.Id ?? Game.NeutralFactionId;
            l.AttachState(_state);

            _interactable.TryAdd(
                    entity.Id,
                    new[] { new InteractableState(l) });
            _entityIcons.TryAdd(entity.Id, icon);
            _allLabels.Add(l);
        }

        void AddIconable(EntityState entityState)
        {
            // FOG OF WAR (only when CombatEngagement.RequireDetectionToEngage is on): a foreign-faction MOBILE unit
            // (ship / projectile / beam) is NEVER drawn as a full icon — you see it only as a sensor-contact BLIP
            // (UpdateContactBlips) and only once your sensors hold it; undetected, it's invisible. Bodies (stars/
            // planets/moons/jump points), your OWN units, and neutrals are unaffected; with fog off, all draw as
            // before. Guarding at the TOP also hides the unit's orbit/move trail, so the trail can't betray it.
            if (CombatEngagement.RequireDetectionToEngage)
            {
                int viewedFaction = _faction?.Id ?? Game.NeutralFactionId;
                int ownerId = entityState.Entity.FactionOwnerID;
                if (ownerId != viewedFaction && ownerId != Game.NeutralFactionId
                    && (entityState.TryGetDataBlob<ShipInfoDB>(out _)
                        || entityState.TryGetDataBlob<ProjectileInfoDB>(out _)
                        || entityState.TryGetDataBlob<BeamInfoDB>(out _)))
                {
                    return;
                }
            }

            entityState.TryGetDataBlob<PositionDB>(out var positionDB);
            entityState.TryGetDataBlob<MassVolumeDB>(out var massVolumeDB);

            if (entityState.TryGetDataBlob<OrbitDB>(out var orbitDB))
            {
                if (!orbitDB.IsStationary)
                {
                    OrbitIconBase orbit;
                    if (orbitDB.Eccentricity < 1)
                    {
                        orbit = new OrbitEllipseIcon(entityState, _state.UserOrbitSettingsMtx);
                        _orbitRings.TryAdd(entityState.Id, orbit);
                    }
                    else
                    {
                        orbit = new OrbitHyperbolicIcon2(entityState, _state.UserOrbitSettingsMtx);
                        _orbitRings.TryAdd(entityState.Id, orbit);
                    }
                }
            }

            if (entityState.TryGetDataBlob<NewtonMoveDB>(out var newtonMoveDB))
            {
                _orbitRings.TryAdd(entityState.Id, new NewtonMoveIcon(entityState, newtonMoveDB, _state.UserOrbitSettingsMtx));
            }

            if (entityState.TryGetDataBlob<NewtonSimpleMoveDB>(out var newtonSimpleMoveDB))
            {
                _orbitRings.TryAdd(entityState.Id, new NewtonSimpleIcon(entityState, newtonSimpleMoveDB, _state.UserOrbitSettingsMtx));
            }

            if (entityState.TryGetDataBlob<WarpMovingDB>(out var warpMovingDB) && positionDB != null)
            {
                _orbitRings.TryAdd(entityState.Id, new WarpMovingIcon(warpMovingDB, positionDB));
            }


            if (entityState.TryGetDataBlob<StarInfoDB>(out var starInfoDB)
                && massVolumeDB != null
                && positionDB != null)
            {
                AddEntityIcon(
                        entityState.Entity,
                        new StarIcon(starInfoDB, positionDB, massVolumeDB));
            }

            if (entityState.TryGetDataBlob<SystemBodyInfoDB>(out var systemBodyInfoDB)
                && massVolumeDB != null
                && positionDB != null)
            {
                var i = new SysBodyIcon(entityState, systemBodyInfoDB, positionDB, massVolumeDB);
                i.AttachState(_state);

                var l = new EntityLabelExtCombo(entityState.Entity);
                l.Padding = 3;
                l.Faction = _state.Faction?.Id ?? Game.NeutralFactionId;
                l.AttachState(_state);

                _interactable.TryAdd(
                        entityState.Entity.Id,
                        new[] { new InteractableState(i), new InteractableState(l) });
                _bodyIcons.TryAdd(entityState.Id, i);
                _allLabels.Add(l);
            }

            if (entityState.TryGetDataBlob<ShipInfoDB>(out var shipInfoDB) && positionDB != null)
            {
                AddEntityIcon(
                        entityState.Entity,
                        new ShipIcon(entityState, shipInfoDB, positionDB));
            }

            if (entityState.TryGetDataBlob<ProjectileInfoDB>(out var projectileInfoDB) && positionDB != null)
            {
                AddEntityIcon(
                        entityState.Entity,
                        new ProjectileIcon(entityState, positionDB));
            }

            if (entityState.TryGetDataBlob<BeamInfoDB>(out var beamInfoDB) && positionDB != null)
            {
                AddEntityIcon(
                        entityState.Entity,
                        new BeamIcon(beamInfoDB, positionDB));
            }

            if(entityState.TryGetDataBlob<JPSurveyableDB>(out var jPSurveyableDB) && positionDB != null)
            {
                AddEntityIcon(
                        entityState.Entity,
                        new PointOfInterestIcon(positionDB));
            }
        }

        void RemoveIconable(int entityGuid)
        {
            _testIcons.TryRemove(entityGuid, out var testIcon);
            _entityIcons.TryRemove(entityGuid, out var entityIcon);
            _orbitRings.TryRemove(entityGuid, out var orbitIcon);
            _moveIcons.TryRemove(entityGuid, out var moveIcon);
            _interactable.TryRemove(entityGuid, out _);
            _bodyIcons.TryRemove(entityGuid, out _);
            _allLabels.RemoveWhere(x => x.Entity.Id == entityGuid);
        }

        // Drop the icon/label/interactable for any entity that has gone invalid (destroyed). A ship killed in
        // combat flips its entity's IsValid to false IMMEDIATELY (TagEntityForRemoval), but RemoveIconable only
        // runs when the engine's EntityRemoved MESSAGE is later processed — which lags, and never arrives while
        // the game is paused after a single step. In that gap the dead ship's AbsolutePosition collapses to the
        // origin, so its leftover icon + label pile up ON THE SUN and stay on screen (clickable too — that was
        // the crash, now guarded in GlobalUIState). Running this every frame makes the ghost vanish the instant
        // the ship dies, matching the screen to reality during a battle. Bodies (stars/planets) never go invalid.
        void PruneDeadEntities()
        {
            List<int> dead = null;
            foreach (var lbl in _allLabels)
            {
                if (lbl.Entity != null && !lbl.Entity.IsValid)
                    (dead ??= new List<int>()).Add(lbl.Entity.Id);
            }
            if (dead == null) return;
            foreach (var id in dead)
            {
                RemoveIconable(id);
                SessionLog.State("pruned ghost icon/label for dead entity #" + id);
            }
            _updateLabels = true; // rebuild the visible-label + interactable caches without the dead ones
        }

        public void UpdateUserOrbitSettings()
        {
            foreach (var item in _orbitRings.Values)
            {
                if(item is IUpdateUserSettings foo)
                {
                    foo.UpdateUserSettings();
                }
            }
        }

        void HandleChanges(EntityState entityState)
        {

            foreach (var message in entityState.Changes)
            {
                if(message.EntityId == null) continue;

                if (message.MessageType == MessageTypes.DBAdded)
                {
                    if (message.DataBlob is OrbitDB)
                    {
                        OrbitDB orbitDB = (OrbitDB)message.DataBlob;
                        if (orbitDB.Parent == null)
                            continue;


                        if (!orbitDB.IsStationary)
                        {
                            if (_sysState != null && _sysState.EntityStatesWithPosition.ContainsKey(message.EntityId.Value))
                            {
                                entityState = _sysState.EntityStatesWithPosition[message.EntityId.Value];
                            }
                            else if(_sysState != null && message.FactionId != null && _sysState.StarSystem.TryGetEntityById(message.EntityId.Value, out var retrievedEntity))
                            {
                                entityState = new EntityState(retrievedEntity, message.EntityId.Value, message.FactionId.Value);
                            }

                            OrbitIconBase orbit;
                            if (orbitDB.Eccentricity < 1)
                            {
                               orbit = new OrbitEllipseIcon(entityState, _state.UserOrbitSettingsMtx);
                            }
                            else
                            {
                                orbit = new OrbitHyperbolicIcon2(entityState, _state.UserOrbitSettingsMtx);
                            }
                            _orbitRings[message.EntityId.Value] = orbit;

                        }
                    }
                    if (message.DataBlob is WarpMovingDB
                        && _sysState != null
                        && _sysState.StarSystem.TryGetEntityById(message.EntityId.Value, out var entity)
                        && entity.TryGetDataBlob<PositionDB>(out var positionDB))
                    {
                        var widget = new WarpMovingIcon((WarpMovingDB)message.DataBlob, positionDB);
                        widget.OnPhysicsUpdate();
                        //Matrix matrix = new Matrix();
                        //matrix.Scale(_camera.ZoomLevel);
                        //widget.OnFrameUpdate(matrix, _camera);
                        _moveIcons[message.EntityId.Value] = widget;
                        //_moveIcons.Add(changeData.Entity.ID, widget);
                    }

                    if (message.DataBlob is NewtonMoveDB)
                    {

                        Icon orb = new NewtonMoveIcon(entityState, (NewtonMoveDB)message.DataBlob, _state.UserOrbitSettingsMtx);
                        _orbitRings.AddOrUpdate(message.EntityId.Value, orb, ((guid, data) => data = orb));
                    }
                    //if (changeData.Datablob is NameDB)
                    //TextIconList[changeData.Entity.ID] = new TextIcon(changeData.Entity, _camera);

                    //_entityIcons[changeData.Entity.ID] = new EntityIcon(changeData.Entity, _camera);
                }
                if (message.MessageType == MessageTypes.DBRemoved)
                {
                    if (message.DataBlob is OrbitDB)
                    {

                        _orbitRings.TryRemove(message.EntityId.Value, out var foo);
                    }
                    if (message.DataBlob is WarpMovingDB)
                    {
                        _moveIcons.TryRemove(message.EntityId.Value, out var foo);
                    }

                    if (message.DataBlob is NewtonMoveDB)
                    {
                        _orbitRings.TryRemove(message.EntityId.Value, out var foo);
                    }
                }
            }
        }

        private void OnSystemStateEntityAdded(SystemState systemState, Entity entity)
        {
            if(systemState.EntityStatesWithPosition.ContainsKey(entity.Id))
                AddIconable(systemState.EntityStatesWithPosition[entity.Id]);
        }

        private void OnSystemStateEntityUpdated(SystemState systemState, int entityId, Message message)
        {
            // Refreseh the icons for the updated entity
            if(systemState.EntityStatesWithPosition.ContainsKey(entityId))
            {
                RemoveIconable(entityId);
                AddIconable(systemState.EntityStatesWithPosition[entityId]);
            }
        }

        private void OnSystemStateEntityRemoved(SystemState systemState, int entityId)
        {
            RemoveIconable(entityId);
        }

        internal void Update()
        {
            if(_sysState == null) return;

            PruneDeadEntities();

            // Which of the viewed faction's ships are non-representative fleet members (drawn as one fleet icon).
            // Engine-side + CI-tested; recomputed each frame so it tracks fleet membership live. Defensive: a throw
            // here must not blank the map, so fall back to "hide nothing" (every ship draws individually).
            try
            {
                _collapsedFleetMembers = Pulsar4X.Fleets.FleetTools.CollapsedFleetMemberShipIds(
                    _sysState.StarSystem, _faction?.Id ?? Game.NeutralFactionId);
            }
            catch
            {
                _collapsedFleetMembers = new HashSet<int>();
            }

            UpdateAllRangeRings();
            UpdateHazardRegions();

            foreach (var item in _sysState.EntityStatesWithPosition.Values)
            {
                if (item.Changes.Count > 0)
                {
                    HandleChanges(item);
                }
            }

            var matrix = _camera.GetZoomMatrix();
            foreach (var (_, item) in UIWidgets)
                item.OnFrameUpdate(matrix, _camera);

            foreach (var (_, item) in _orbitRings)
                item.OnFrameUpdate(matrix, _camera);

            foreach (var (_, item) in _moveIcons)
                item.OnFrameUpdate(matrix, _camera);

            foreach (var (_, item) in _entityIcons)
                item.OnFrameUpdate(matrix, _camera);

            foreach (var (_, item) in _bodyIcons)
                item.OnFrameUpdate(matrix, _camera);

            foreach (var item in SelectedEntityExtras)
                item.OnFrameUpdate(matrix, _camera);

            foreach (var item in _allLabels)
                item.OnFrameUpdate(matrix, _camera);

            UpdateContactBlips(matrix);

            if (_updateLabels)
            {
                _updateLabels = false;

                var prefs = SystemViewPreferences.GetInstance();

                foreach (var item in _interactable.Values)
                {
                    foreach (var i in item)
                        i.IsDisabled = true;
                }

                var zoom = _camera.ZoomLevel;
                var lbl = _allLabels
                    .Where(x => {
                        var t = Utils.EntityBodyType(x.Entity);
                        return prefs.ShouldDisplay("map", t)
                            && zoom >= _minZoomForLabel[t];
                    });

                _visibleLabels.Clear();
                foreach (var i in _distributor(lbl))
                {
                    foreach (var j in _interactable[i.Entity.Id])
                        j.IsDisabled = false;
                    _visibleLabels.Add(i);
                }

                _interactableGrouped = _interactable
                    .Values
                    .SelectMany(x => x)
                    .GroupBy(x => x.Item.Priority)
                    .OrderByDescending(x => x.Key);
            }
        }

        internal void Draw()
        {
            DrawIcons(UIWidgets.Values);
            DrawIconsExceptCollapsed(_orbitRings);   // a collapsed member's orbit ring is hidden too...
            DrawIconsExceptCollapsed(_moveIcons);    // ...and its move/warp trail...
            DrawIconsExceptCollapsed(_entityIcons);  // ...and its ship icon — so the fleet is ONE marker.
            DrawIcons(_contactIcons.Values);
            DrawIcons(_bodyIcons.Values);
            DrawIcons(SelectedEntityExtras);

            foreach (var i in _visibleLabels)
            {
                if (i.Entity != null && _collapsedFleetMembers.Contains(i.Entity.Id)) continue; // hide member labels
                SafeDraw(i.GetType().Name, () => i.Draw(_window.Renderer, _camera));
            }
        }

        void DrawIcons(IEnumerable<IDrawData> icons)
        {
            foreach (var item in icons)
                SafeDraw(item.GetType().Name, () => item.Draw(_window.Renderer, _camera));
        }

        // Draw an entity-id-keyed icon set, skipping any ship collapsed into its fleet's single icon this frame.
        // Only ship ids ever land in _collapsedFleetMembers, so bodies/stars/contacts keyed here are never skipped.
        void DrawIconsExceptCollapsed(ConcurrentDictionary<int, Icon> icons)
        {
            foreach (var kv in icons)
            {
                if (_collapsedFleetMembers.Contains(kv.Key)) continue;
                SafeDraw(kv.Value.GetType().Name, () => kv.Value.Draw(_window.Renderer, _camera));
            }
        }

        // ── All-ranges always-on ("all units and places have their ranges on display") ──────────────────────────
        // Draws reach rings for EVERY own unit + place, regardless of selection (GlobalUIState.ShowAllRangeRings,
        // default on). "Units" = every own ship drawn as its OWN icon — lone ships + each fleet's representative
        // (we reuse _collapsedFleetMembers, so rings land on exactly the ships that show as icons, one ring-set per
        // fleet marker). "Places" = every own colony (one detection ring — e.g. Earth's system-wide megasensor).
        // A ring's centre is the entity's LIVE PositionDB, so it TRACKS the ship as it moves — no per-frame rebuild
        // needed. We rebuild only when the SET of units/places or their loudness (EMCON) changes, via a cheap
        // fingerprint. SimpleCircle culls off-screen segments (the zoom-stutter fix), so a huge colony ring is cheap
        // when out of view. Keys are "allrange_*", distinct from the Combat tab's "rangering_*" so the two coexist.
        private readonly List<string> _allRangeKeys = new();
        private string _allRangeFingerprint = "";

        void UpdateAllRangeRings()
        {
            if (_sysState == null || _faction == null) return;

            // Toggled off → drop any rings we own and reset, so flipping it back rebuilds clean.
            if (!_state.ShowAllRangeRings)
            {
                if (_allRangeKeys.Count > 0)
                {
                    foreach (var k in _allRangeKeys) UIWidgets.Remove(k);
                    _allRangeKeys.Clear();
                    _allRangeFingerprint = "";
                }
                return;
            }

            int facId = _faction.Id;
            List<Entity> ownShips, ownColonies; Entity refTarget = null;
            try
            {
                var allShips = _sysState.StarSystem.GetAllEntitiesWithDataBlob<ShipInfoDB>();
                ownShips = allShips
                    .Where(e => e.FactionOwnerID == facId && e.IsValid
                                && !_collapsedFleetMembers.Contains(e.Id) && e.HasDataBlob<PositionDB>())
                    .ToList();
                ownColonies = _sysState.StarSystem.GetAllEntitiesWithDataBlob<Pulsar4X.Colonies.ColonyInfoDB>()
                    .Where(e => e.FactionOwnerID == facId && e.IsValid && e.HasDataBlob<PositionDB>())
                    .ToList();
                // Reference target for a PLACE's detection ring: "how far does this colony detect a ship like THIS?"
                // Prefer a real foreign ship (the thing you actually want to spot — gives the honest bubble that
                // reaches the inner planets), else one of your own ships (a ship-like target). Sized by the detector
                // vs the target's signature, NOT the colony's own signature — which is why the old SensorReachRange
                // ring came out tiny (it measured "detect a thing as loud as a colony", not "as loud as a ship").
                refTarget = allShips.FirstOrDefault(e => e.FactionOwnerID != facId && e.IsValid && e.HasDataBlob<SensorProfileDB>())
                            ?? ownShips.FirstOrDefault(e => e.HasDataBlob<SensorProfileDB>());
            }
            catch { return; }   // a bad engine read must never blank the map

            // Fingerprint = which units/places + their loudness + the place-ring reference. Positions are deliberately
            // NOT in it (the rings track live), so this only changes when a unit/colony/enemy appears or leaves, or
            // EMCON flips a ship's detectability — exactly when the rings need rebuilding.
            double loud = 0;
            foreach (var s in ownShips) loud += SensorTools.CurrentActivityMultiplier(s);
            string fp = string.Join(",", ownShips.Select(s => s.Id).OrderBy(i => i))
                      + "|" + string.Join(",", ownColonies.Select(c => c.Id).OrderBy(i => i))
                      + "|" + (refTarget?.Id ?? -1) + "|" + Math.Round(loud, 2);
            if (fp == _allRangeFingerprint) return;
            _allRangeFingerprint = fp;

            foreach (var k in _allRangeKeys) UIWidgets.Remove(k);
            _allRangeKeys.Clear();

            void Ring(string key, PositionDB center, double range_m, byte r, byte g, byte b, byte a)
            {
                if (range_m <= 0 || center == null) return;
                UIWidgets[key] = new SimpleCircle(center, Pulsar4X.Orbital.Distance.MToAU(range_m),
                    new SDL3.SDL.Color { R = r, G = g, B = b, A = a });
                _allRangeKeys.Add(key);
            }

            foreach (var ship in ownShips)
            {
                var pos = ship.GetDataBlob<PositionDB>();
                Ring("allrange_" + ship.Id + "_beam",   pos, WeaponUtils.GetMaxBeamRange_m(ship),   225, 90, 70, 70);  // red: how far it can SHOOT
                Ring("allrange_" + ship.Id + "_sensor", pos, SensorTools.SensorReachRange_m(ship),   80, 210, 110, 55); // green: how far it can SEE
                Ring("allrange_" + ship.Id + "_detect", pos, SensorTools.DetectabilityRange_m(ship), 240, 160, 40, 60); // amber: how far it can BE SEEN
            }

            foreach (var colony in ownColonies)
            {
                // A "place" shows how far it detects an actual SHIP (Earth's megasensor = the inner-system early-
                // warning bubble that spots fleets at Mercury/Mars). Falls back to the self-referential reach only if
                // there's no ship anywhere to measure against.
                double placeReach = refTarget != null
                    ? SensorTools.DetectionRangeAgainst(colony, refTarget)
                    : SensorTools.SensorReachRange_m(colony);
                Ring("allrange_colony_" + colony.Id + "_sensor", colony.GetDataBlob<PositionDB>(), placeReach, 80, 210, 110, 45);
            }

            SessionLog.Action($"[range-ring] all-ranges rebuilt: {ownShips.Count} unit(s) + {ownColonies.Count} place(s) = {_allRangeKeys.Count} ring(s)");
        }

        // Draws each space hazard (gas cloud / solar flare) as a coloured circle marking the area it covers — the
        // visual half of "a region of space that affects ships inside it". Rebuilt each frame (a system holds only
        // a handful, and a flare's radius GROWS as it erupts, so we re-read its current size). Reuses the proven,
        // zoom-safe SimpleCircle (same vehicle as the range rings), keyed "hazard_*" so it coexists with them. A
        // bad engine read must never blank the map, so the whole thing is wrapped defensively.
        private readonly List<string> _hazardKeys = new();

        void UpdateHazardRegions()
        {
            if (_sysState == null) return;

            foreach (var k in _hazardKeys) UIWidgets.Remove(k);
            _hazardKeys.Clear();

            List<Entity> hazards;
            try
            {
                hazards = _sysState.StarSystem.GetAllEntitiesWithDataBlob<Pulsar4X.Hazards.SpaceHazardDB>();
            }
            catch { return; }

            foreach (var haz in hazards)
            {
                if (haz == null || !haz.IsValid || !haz.HasDataBlob<PositionDB>()) continue;
                var hazDb = haz.GetDataBlob<Pulsar4X.Hazards.SpaceHazardDB>();
                if (hazDb.Radius_m <= 0) continue;

                SDL3.SDL.Color colour;
                switch (hazDb.HazardType)
                {
                    case Pulsar4X.Hazards.SpaceHazardType.SolarFlare:
                        colour = new SDL3.SDL.Color { R = 255, G = 140, B = 40, A = 160 }; break;  // bright orange
                    case Pulsar4X.Hazards.SpaceHazardType.StarCorona:
                        colour = new SDL3.SDL.Color { R = 230, G = 80, B = 30, A = 70 }; break;    // faint red-orange
                    default:
                        colour = new SDL3.SDL.Color { R = 120, G = 200, B = 120, A = 90 }; break;  // gas cloud: soft green
                }

                string key = "hazard_" + haz.Id;
                UIWidgets[key] = new SimpleCircle(haz.GetDataBlob<PositionDB>(),
                    Pulsar4X.Orbital.Distance.MToAU(hazDb.Radius_m), colour);
                _hazardKeys.Add(key);
            }
        }

        // Rebuild the fog-of-war contact blips from the viewed faction's CURRENT sensor contacts. Runs every frame
        // (cheap — contacts are few). Only when fog is ON (CombatEngagement.RequireDetectionToEngage): with fog off
        // you see the real ships, so a blip would just double them. A contact for one of YOUR ships or a neutral is
        // skipped (yours draw normally; neutrals aren't fogged). Add the new, refresh the held, drop the gone — so
        // the blip set mirrors GetAllContacts: a target that leaves range drops off the list (blip removed); a
        // destroyed one lingers as a faded "memory" ghost (the grave rung) until the engine ages the contact out.
        void UpdateContactBlips(Matrix matrix)
        {
            if (_sensorMgr == null || !CombatEngagement.RequireDetectionToEngage)
            {
                if (_contactIcons.Count > 0) _contactIcons.Clear(); // textures freed on GC via the icon finalizer
                return;
            }

            int viewedFaction = _faction?.Id ?? Game.NeutralFactionId;
            var live = new HashSet<int>();
            foreach (var contact in _sensorMgr.GetAllContacts())
            {
                if (contact?.Position == null) continue;
                int ownerId = contact.ActualEntity != null ? contact.ActualEntity.FactionOwnerID : Game.NeutralFactionId;
                if (ownerId == viewedFaction || ownerId == Game.NeutralFactionId) continue; // own ships / neutrals aren't fogged
                live.Add(contact.ActualEntityId);
                if (!_contactIcons.TryGetValue(contact.ActualEntityId, out var icon))
                {
                    icon = new SensorContactIcon(contact, hostile: true); // v1: any rival contact reads hostile/unknown (IFF is later)
                    _contactIcons[contact.ActualEntityId] = icon;
                }
                icon.OnFrameUpdate(matrix, _camera);
            }

            if (_contactIcons.Count > live.Count)
            {
                List<int> gone = null;
                foreach (var id in _contactIcons.Keys)
                    if (!live.Contains(id)) (gone ??= new List<int>()).Add(id);
                if (gone != null)
                    foreach (var id in gone) _contactIcons.Remove(id);
            }
        }

        void ClearContactBlips() => _contactIcons.Clear();

        // Draw one map item, isolating a fault: if it throws (commonly a NaN coordinate from a mid-warp or
        // detached position hitting Convert.ToInt32), skip just that item and log it ONCE so the rest of the
        // map still renders and game_log.txt names which item faulted. Without this, one bad icon aborts the
        // whole map draw under the coarse SafeRender, leaving a half-drawn frame ("stuck lines, ships gone").
        void SafeDraw(string label, Action draw)
        {
            try
            {
                draw();
            }
            catch (Exception e)
            {
                string sig = label + "|" + e.GetType().Name + "|" + e.Message;
                if (_loggedDrawErrors.Add(sig))
                {
                    Console.WriteLine("[RenderError] map item '" + label + "' threw and was skipped (logged once): " + e);
                    Console.Out.Flush();
                }
            }
        }

        public override bool GetActive()
        {
            return true;
        }

        public override void OnGameTickChange(DateTime newDate)
        {

        }

        public override void OnSystemTickChange(DateTime newDate)
        {
            _state.PrimarySystemDateTime = newDate;

            foreach (var icon in UIWidgets.Values)
            {
                icon.OnPhysicsUpdate();
            }
            foreach (var icon in _orbitRings.Values)
            {
                icon.OnPhysicsUpdate();
            }
            foreach (var icon in _entityIcons.Values)
            {
                icon.OnPhysicsUpdate();
            }
            foreach (var icon in _moveIcons.Values.ToArray())
            {
                icon.OnPhysicsUpdate();
            }
            foreach(var icon in SelectedEntityExtras)
            {
                icon.OnPhysicsUpdate();
            }
        }
    }
}
